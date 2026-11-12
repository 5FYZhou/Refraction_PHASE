using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>白色子弹的外壳：把逻辑层给的姿势落到刚体与碰撞体上，并把撞到谁报回 BulletLogic</summary>
    /// <remarks>没有驱动器也不做决策：开火、路径、验活、出射全在逻辑层，这里只负责触发器和表现</remarks>
    public sealed class Bullet : MonoBehaviour, IAttacker, IBulletView, IHitLanded
    {
        private const string BoundTag = "Bound";
        private const int EnemyLayer = 8;

        private BulletField _home;
        private BulletLogic _system;
        private AfterimageField _afterimage;
        private AttackBox _attackBox;
        private Transform _owner;
        private IHitLanded _hitSink;
        private Rigidbody2D _rb;
        private Collider2D[] _colliders;
        private Damage _damage;
        private BulletState _state;
        private int _id = -1;
        private int _segFrom = -1;
        private int _segTo = -1;
        private bool _bound;
        private bool _hit;

        public Vector2 Position => _rb != null ? _rb.position : (Vector2)transform.position;

        /// <summary>造出来时调一次：抓刚体、碰撞体、伤害与命中回调</summary>
        public void Bind(BulletField home, BulletLogic system, int id, Transform owner, IHitLanded hitSink)
        {
            _home = home;
            _system = system;
            _id = id;
            _owner = owner;
            _hitSink = hitSink;
            _hit = false;
            _damage = system.DamageOf(id);

            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (_colliders == null) _colliders = GetComponents<Collider2D>();

            if (_attackBox == null) _attackBox = GetComponent<AttackBox>();

            if (_attackBox != null) _attackBox.SetAttack(this);
        }

        public void Apply(in BulletPose pose)
        {
            if (_rb == null) return;

            if (!_bound || _state != pose.State)
            {
                if (!_bound) _rb.position = pose.Pos;

                _bound = true;
                _state = pose.State;

                bool transporting = pose.State == BulletState.Transporting;

                for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = !transporting;

                _rb.bodyType = transporting ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
                _rb.velocity = transporting ? Vector2.zero : pose.Dir * pose.Speed;
            }

            if (pose.State == BulletState.Transporting) _rb.position = pose.Pos;

            if (pose.Dir.sqrMagnitude > 1e-8f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pose.Dir.y, pose.Dir.x) * Mathf.Rad2Deg);
            }

            SyncSegment(pose.SegFrom, pose.SegTo);
        }

        public void Kill()
        {
            SyncSegment(-1, -1);

            if (_home != null) _home.Recycle(this);
            else Destroy(gameObject);
        }

        /// <summary>回池复位：别把上一发的伤害与连线热度带到下一次</summary>
        public void Reset()
        {
            _system = null;
            _afterimage = null;
            _owner = null;
            _hitSink = null;
            _id = -1;
            _bound = false;
            _state = BulletState.Flying;
            _segFrom = -1;
            _segTo = -1;

            // 摘攻击者：同帧未派发的受击盒回调别再结算
            if (_attackBox != null) _attackBox.SetAttack(null);

            if (_rb != null)
            {
                _rb.velocity = Vector2.zero;
                _rb.bodyType = RigidbodyType2D.Dynamic;
            }

            if (_colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = true;
            }
        }

        // 分流：真正的判定全在逻辑层
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || _system == null) return;

            // Bound 是地图边界，飞出才销毁
            if (other.CompareTag(BoundTag)) return;

            if (_owner != null && (other.transform == _owner || other.transform.IsChildOf(_owner))) return;

            AfterimageAnchor anchor = other.GetComponentInParent<AfterimageAnchor>();

            if (anchor != null)
            {
                if (anchor.PlayerRoot != _owner) return;

                _afterimage = anchor.Field;
                _system.TryEnterTransport(_id, anchor.Logic, anchor.Index);
                return;
            }

            // 敌人由 AttackBox 结算，先 Kill 会吞掉这一枪
            if (_attackBox != null && other.gameObject.layer == EnemyLayer) return;

            _system.Kill(_id);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null || _system == null) return;

            // 关碰撞体也发退出回调，别当成飞出地图
            if (_state != BulletState.Flying) return;

            if (other.CompareTag(BoundTag)) _system.Kill(_id);
        }

        private void OnDestroy()
        {
            SyncSegment(-1, -1);

            _system?.Forget(_id);
        }

        private void SyncSegment(int from, int to)
        {
            if (from == _segFrom && to == _segTo) return;

            if (_afterimage != null && _segFrom >= 0) _afterimage.EndTraverse(_segFrom, _segTo);

            _segFrom = from;
            _segTo = to;

            if (_afterimage != null && from >= 0) _afterimage.BeginTraverse(from, to);
        }

        public Damage ProduceDamage() => _damage;

        public void OnHitLanded()
        {
            if (_hit) return;

            _hit = true;

            _hitSink?.OnHitLanded();

            _system?.Kill(_id);
        }
    }
}
