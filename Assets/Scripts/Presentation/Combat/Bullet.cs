using System.Collections.Generic;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>白色子弹：直线飞行，进残影后按路径走位，再从残影朝向出射</summary>
    /// <remarks>伤害自己结算：击退方向要用飞行方向，且要排掉开火者自己</remarks>
    public sealed class Bullet : MonoBehaviour, IAttack, ISceneRoot, IPhysicsTicked
    {
        private enum State
        {
            Flying,
            Transporting
        }

        public int Order => SceneOrder.World;

        private const string BoundTag = "Bound";
        private const int GroundLayer = 6;
        private const float ArriveEps = 1e-4f;

        private readonly List<TransferNode> _plan = new();

        private Damage _damage;
        private Transform _owner;
        private GameRoot _root;
        private Rigidbody2D _rb;
        private Collider2D[] _colliders;
        private AfterimageLogic _logic;
        private State _state;
        private float _speed;
        private float _transferSpeed;
        private Vector2 _enterDir = Vector2.right;
        private int _step;
        private int _node;
        private int _exitIndex = -1;

        /// <summary>生成后由组合根调用一次；预制体没刚体时补一个</summary>
        public void Init(in BulletSpawn spawn, Transform owner)
        {
            _owner = owner;
            _damage = new Damage(spawn.Damage, DamageSource.Bullet, spawn.Direction, spawn.KnockbackSpeed);
            _speed = spawn.Speed;
            _transferSpeed = spawn.TransferSpeed;

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.velocity = spawn.Direction * spawn.Speed;

            _colliders = GetComponents<Collider2D>();

            // 默认朝右（+X），直接按飞行方向旋转
            float angle = Mathf.Atan2(spawn.Direction.y, spawn.Direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.position = new Vector3(spawn.Origin.x, spawn.Origin.y, transform.position.z);

            // 预制体上的攻击盒会重复结算，本弹自己算
            AttackBox attackBox = GetComponent<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(this);

            _root = GameRoot.Instance;
            if (_root != null) _root.RegisterSceneRoot(this);
        }

        public void Attach() { }

        // 传输只做位移，路径走完或目标失效就地出射
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

            if ((next - target.Pos).sqrMagnitude <= ArriveEps) _node = _step++;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;

            // 出生点在开火者体内，先排自己人
            if (_owner != null && (other.transform == _owner || other.transform.IsChildOf(_owner))) return;

            if (other.CompareTag(BoundTag))
            {
                Destroy(gameObject);
                return;
            }

            AfterimageAnchor anchor = other.GetComponentInParent<AfterimageAnchor>();
            if (anchor != null)
            {
                TryEnterTransport(anchor);
                return;
            }

            // 打在墙地上一并消散
            if (other.gameObject.layer == GroundLayer)
            {
                Destroy(gameObject);
                return;
            }

            // 命中即消散，一颗只结算一次
            Destroy(gameObject);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null) return;

            if (other.CompareTag(BoundTag))
            {
                Destroy(gameObject);
                return;
            }

            AfterimageAnchor anchor = other.GetComponentInParent<AfterimageAnchor>();
            if (anchor != null && anchor.Logic == _logic && anchor.Index == _exitIndex) _exitIndex = -1;
        }

        // 进入残影：当场规划整条路径并锁住，每段出发前再验活
        private void TryEnterTransport(AfterimageAnchor anchor)
        {
            if (_state == State.Transporting) return;
            if (anchor.Logic == null) return;
            if (_owner != anchor.PlayerRoot) return;

            // 刚出射的这一枚不要原地重进
            if (anchor.Logic == _logic && anchor.Index == _exitIndex) return;

            if (anchor.Logic.BuildTransfer(anchor.Index, _plan) == 0) return;

            _logic = anchor.Logic;
            _state = State.Transporting;
            _node = 0;
            _step = 1;

            if (_rb.velocity.sqrMagnitude > 1e-6f) _enterDir = _rb.velocity.normalized;

            _rb.velocity = Vector2.zero;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.position = _plan[0].Pos;

            transform.position = new Vector3(_plan[0].Pos.x, _plan[0].Pos.y, transform.position.z);

            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = false;
        }

        // 出射：水平按残影朝向，垂直沿用进入方向
        private void LeaveTransport()
        {
            TransferNode node = _plan[_node];

            Vector2 dir = new(node.Facing < 0 ? -1f : 1f, _enterDir.y);
            if (dir.sqrMagnitude < 1e-6f) dir = new(node.Facing < 0 ? -1f : 1f, 0f);

            dir.Normalize();

            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.velocity = dir * _speed;

            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = true;

            _state = State.Flying;
            _exitIndex = node.Index;
            _step = 0;
            _node = 0;

            _plan.Clear();
        }

        private void OnDestroy()
        {
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        public Damage ProduceDamage()
        {
            return _damage;
        }
    }
}
