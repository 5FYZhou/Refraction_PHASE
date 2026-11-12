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

        private BulletLogic _system;
        private AfterimageField _field;
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

        public Vector2 Position => _rb != null ? _rb.position : (Vector2)transform.position;

        /// <summary>由 BulletField 造出来时调一次：抓住刚体、碰撞体、伤害与命中回调，之后等逻辑层下发姿势</summary>
        public void Bind(BulletLogic system, int id, Transform owner, IHitLanded hitSink)
        {
            _system = system;
            _id = id;
            _owner = owner;
            _hitSink = hitSink;
            _damage = system.DamageOf(id);

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _colliders = GetComponents<Collider2D>();

            AttackBox attackBox = GetComponent<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(this);
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

            Destroy(gameObject);
        }

        // 撞到谁先在这里分流，真正的判定（验活、路径、是否接管）全部交回逻辑层
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || _system == null) return;

            // Bound 是地图边界，穿过去飞，飞出才销毁
            if (other.CompareTag(BoundTag)) return;

            if (_owner != null && (other.transform == _owner || other.transform.IsChildOf(_owner))) return;

            AfterimageAnchor anchor = other.GetComponentInParent<AfterimageAnchor>();

            if (anchor != null)
            {
                if (anchor.PlayerRoot != _owner) return;

                _field = anchor.Field;
                _system.TryEnterTransport(_id, anchor.Logic, anchor.Index);
                return;
            }

            _system.Kill(_id);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null || _system == null) return;

            // 传输时关碰撞体也会发退出回调，别当成飞出地图
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

            if (_field != null && _segFrom >= 0) _field.EndTraverse(_segFrom, _segTo);

            _segFrom = from;
            _segTo = to;

            if (_field != null && from >= 0) _field.BeginTraverse(from, to);
        }

        public Damage ProduceDamage() => _damage;

        public void OnHitLanded() => _hitSink?.OnHitLanded();
    }
}
