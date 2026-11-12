using System.Collections.Generic;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>白子弹：直线飞行，进残影后按路径走位再出射</summary>
    /// <remarks>伤害自己结算：击退方向用飞行方向，排掉开火者</remarks>
    public sealed class Bullet : MonoBehaviour, IAttacker, ISceneRoot, IPhysicsTicked, IHitLanded
    {
        private enum State
        {
            Flying,
            Transporting
        }

        public int Order => SceneOrder.World;

        private const string BoundTag = "Bound";
        private const int GroundLayer = 6;
        private const int PlayerLayer = 7;
        private const float ArriveEps = 1e-4f;

        private readonly List<TransferNode> _plan = new();

        private Damage _damage;
        private Transform _owner;
        private IHitLanded _hitSink;
        private GameRoot _root;
        private Rigidbody2D _rb;
        private Collider2D[] _colliders;
        private AfterimageLogic _logic;
        private AfterimageField _field;
        private State _state;
        private float _speed;
        private float _transferSpeed;
        private Vector2 _enterDir = Vector2.right;
        private int _step;
        private int _node;
        private bool _transported;
        private int _hotFrom = -1;
        private int _hotTo = -1;

        /// <summary>组合根调用一次；没刚体时补一个</summary>
        public void Init(in BulletSpawn spawn, Transform owner, IHitLanded hitSink)
        {
            _owner = owner;
            _hitSink = hitSink;
            _damage = new Damage(spawn.Damage, DamageSource.Bullet, spawn.Direction, spawn.KnockbackSpeed);
            _speed = spawn.Speed;
            _transferSpeed = spawn.TransferSpeed;
            _transported = false;

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.velocity = spawn.Direction * spawn.Speed;

            _colliders = GetComponents<Collider2D>();

            // 默认朝右（+X），按飞行方向旋转
            float angle = Mathf.Atan2(spawn.Direction.y, spawn.Direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.position = new Vector3(spawn.Origin.x, spawn.Origin.y, transform.position.z);

            // 预制体自带攻击盒会重复结算，本弹自己算
            AttackBox attackBox = GetComponent<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(this);

            _root = GameRoot.Instance;
            if (_root != null) _root.RegisterSceneRoot(this);
        }

        public void Attach() { }

        // 路径走完或目标失效就地出射
        public void FixedTick(float deltaTime)
        {
            if (_state != State.Transporting || _rb == null) return;

            if (_step >= _plan.Count)
            {
                LeaveTransport();
                return;
            }

            TransferNode target = _plan[_step];

            if (!_logic.IsAlive(target.Index))
            {
                LeaveTransport();
                return;
            }

            Vector2 pos = _rb.position;
            float stride = _transferSpeed > 0f ? _transferSpeed * deltaTime : float.PositiveInfinity;
            Vector2 next = Vector2.MoveTowards(pos, target.Pos, stride);

            Vector2 heading = next - pos;
            if (heading.sqrMagnitude > 1e-8f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
            }

            _rb.position = next;

            if ((next - target.Pos).sqrMagnitude > ArriveEps) return;

            EndSegment();
            _node = _step++;

            if (_step < _plan.Count) BeginSegment(_plan[_node], _plan[_step]);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;

            // Bound 是地图边界，穿过去飞，飞出才销毁
            if (other.CompareTag(BoundTag)) return;

            // 排开火者
            if (_owner != null && (other.transform == _owner || other.transform.IsChildOf(_owner))) return;

            AfterimageAnchor anchor = other.GetComponentInParent<AfterimageAnchor>();
            if (anchor != null)
            {
                TryEnterTransport(anchor);
                return;
            }

            if (other.gameObject.layer == GroundLayer)
            {
                Destroy(gameObject);
                return;
            }

            // 命中即消散，只结算一次
            Destroy(gameObject);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            // 传输时禁用碰撞体也会发退出回调，别当成飞出地图
            if (_state != State.Flying) return;

            if (other != null && other.CompareTag(BoundTag)) Destroy(gameObject);
        }

        // 进入残影：规划整条路径并锁住，出发前再验活
        private void TryEnterTransport(AfterimageAnchor anchor)
        {
            if (_state == State.Transporting) return;
            if (anchor.Logic == null) return;
            if (_owner != anchor.PlayerRoot) return;

            // 走完一次链就作废，否则无限循环
            if (_transported) return;

            if (anchor.Logic.BuildTransfer(anchor.Index, _plan) < 2) return;

            _logic = anchor.Logic;
            _field = anchor.Field;
            _state = State.Transporting;
            _node = 0;
            _step = 1;

            if (_rb.velocity.sqrMagnitude > 1e-6f) _enterDir = _rb.velocity.normalized;

            _rb.velocity = Vector2.zero;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.position = _plan[0].Pos;

            transform.position = new Vector3(_plan[0].Pos.x, _plan[0].Pos.y, transform.position.z);

            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = false;

            BeginSegment(_plan[0], _plan[1]);
        }

        private void BeginSegment(in TransferNode from, in TransferNode to)
        {
            _hotFrom = from.Index;
            _hotTo = to.Index;

            if (_field != null) _field.BeginTraverse(_hotFrom, _hotTo);
        }

        private void EndSegment()
        {
            if (_hotFrom < 0) return;

            if (_field != null) _field.EndTraverse(_hotFrom, _hotTo);

            _hotFrom = -1;
            _hotTo = -1;
        }

        // 出射：水平按残影朝向，垂直沿用进入方向
        private void LeaveTransport()
        {
            EndSegment();

            TransferNode node = _plan[_node];

            Vector2 dir = new(node.Facing < 0 ? -1f : 1f, _enterDir.y);
            if (dir.sqrMagnitude < 1e-6f) dir = new(node.Facing < 0 ? -1f : 1f, 0f);

            dir.Normalize();

            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.velocity = dir * _speed;

            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = true;

            _state = State.Flying;
            _transported = true;
            _step = 0;
            _node = 0;

            _plan.Clear();
        }

        private void OnDestroy()
        {
            EndSegment();

            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public Damage ProduceDamage()
        {
            return _damage;
        }

        public void OnHitLanded()
        {
            if (_hitSink != null) _hitSink.OnHitLanded();
        }
    }
}
