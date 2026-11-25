using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Presentation.Actor;
using Refraction.Presentation.Effects;
using UnityEngine;

namespace Refraction.Presentation.Combat
{
    /// <summary>子弹外壳：姿势落到刚体与碰撞体；外观与母池不变，阵营与伤害只活在运行时</summary>
    public sealed class Bullet : MonoBehaviour, IAttacker, IBulletView, IHitLanded, IHitBodyLanded
    {
        private const string BoundTag = "Bound";
        private const int DefaultLayer = 0;
        private const int GroundLayer = 6;
        private const int PlayerLayer = 7;
        private const int EnemyLayer = 8;

        // 穿墙不跟阵营走：只看这颗子弹是谁射出来的，弹反只换敌我两位
        private static readonly int PlayerBulletMask = (1 << DefaultLayer) | (1 << PlayerLayer);

        private static readonly int EnemyBulletMask = (1 << DefaultLayer) | (1 << EnemyLayer);

        [SerializeField] private Color particleColor;

        private BulletPool _pool;
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
        private Vector2 _velocity;
        private int _id = -1;
        private int _segFrom = -1;
        private int _segTo = -1;
        private Collider2D _hurtOther;
        private HurtBox _hurtBox;
        private Collider2D _hitCollider;
        private bool _bound;
        private bool _throughWall;
        private bool _hit;
        private bool _trailPending;
        private int _trailFrame;
        private TrailRenderer[] _trails;

        public Vector2 Position => _rb != null ? _rb.position : (Vector2)transform.position;

        public Vector2 ReflectDirection => _velocity.sqrMagnitude > 1e-6f ? -_velocity.normalized : Vector2.zero;

        public BulletFaction Faction =>
            _damage.Source == DamageSource.PlayerBullet ? BulletFaction.Player : BulletFaction.Enemy;

        /// <summary>从池里取出时调一次：写入飞行与攻击归属</summary>
        public void Initialize(BulletPool pool, BulletField field, BulletLogic system, in BulletRequest request,
            Transform owner, IHitLanded hitSink)
        {
            _pool = pool;
            _home = field;
            _system = system;
            _id = request.Id;
            _owner = owner;
            _hitSink = hitSink;
            _damage = request.Damage;
            _throughWall = request.Shooter.Faction == BulletFaction.Enemy;
            _hit = false;
            _bound = false;
            _state = BulletState.Flying;

            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (_colliders == null) _colliders = GetComponents<Collider2D>();

            Vector2 velocity = request.Direction * request.Speed;

            _velocity = velocity;
            _rb.position = request.Origin;
            _rb.velocity = velocity;

            if (request.Direction.sqrMagnitude > 1e-8f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(request.Direction.y, request.Direction.x) * Mathf.Rad2Deg);
            }

            ResetTrail();

            ApplyHitMask();

            if (_attackBox == null) _attackBox = GetComponent<AttackBox>();

            if (_attackBox != null)
            {
                _attackBox.SetAttack(this);
                _attackBox.SetOwnerRoot(owner);
            }

            _home?.Register(this);
        }

        // 预制体上的排除层不作数，命中面按当前阵营现设
        private void ApplyHitMask()
        {
            if (_colliders == null) return;

            int mask = Faction == BulletFaction.Player ? PlayerBulletMask : EnemyBulletMask;

            mask |= 1 << gameObject.layer;

            if (_throughWall) mask |= 1 << GroundLayer;

            for (int i = 0; i < _colliders.Length; i++) _colliders[i].excludeLayers = mask;
        }

        private int HitLayer => Faction == BulletFaction.Player ? EnemyLayer : PlayerLayer;

        private HurtBox HurtBoxOf(Collider2D other)
        {
            if (other == _hurtOther) return _hurtBox;

            _hurtOther = other;
            _hurtBox = other.GetComponent<HurtBox>();

            return _hurtBox;
        }

        public void Apply(in BulletPose pose)
        {
            if (_rb == null) return;

            bool transporting = pose.State == BulletState.Transporting;

            _velocity = transporting ? Vector2.zero : pose.Dir * pose.Speed;

            if (!_bound || _state != pose.State)
            {
                if (!_bound)
                {
                    _rb.position = pose.Pos;
                    _rb.velocity = _velocity;
                    Physics2D.SyncTransforms();
                }

                _bound = true;
                _state = pose.State;

                for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = !transporting;

                _rb.bodyType = transporting ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;

                if (!transporting) _rb.velocity = _velocity;

                // 换状态必然伴随瞬移：先停发清点，隔一帧再重开
                HoldTrail();
            }
            else if (_trailPending && !transporting && Time.frameCount > _trailFrame)
            {
                ArmTrail();
            }

            if (transporting)
            {
                _rb.position = pose.Pos;
                Physics2D.SyncTransforms();
            }

            if (pose.Dir.sqrMagnitude > 1e-8f)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pose.Dir.y, pose.Dir.x) * Mathf.Rad2Deg);
            }

            SyncSegment(pose.SegFrom, pose.SegTo);
        }

        public void Kill()
        {
            SyncSegment(-1, -1);
            ResetTrail();

            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
        }

        /// <summary>弹反：换阵营、伤害、发射者与弹道；命中面换敌我两位，穿墙不变</summary>
        public bool TryReflect(in BulletRebind rebind, Transform owner, IHitLanded hitSink)
        {
            if (_system == null || _state != BulletState.Flying) return false;

            Vector2 dir = ReflectDirection;

            if (dir.sqrMagnitude < 1e-6f) return false;

            float speed = _velocity.magnitude;

            _damage = Damage.FromBullet(rebind.Faction, rebind.Damage, dir, rebind.Knockback);
            _owner = owner;
            _hitSink = hitSink;
            _hit = false;
            _hurtOther = null;
            _hurtBox = null;
            _hitCollider = null;
            _velocity = dir * speed;

            if (_rb != null) _rb.velocity = _velocity;

            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            ApplyHitMask();

            if (_attackBox != null) _attackBox.SetOwnerRoot(owner);

            _system.Redirect(_id, dir, speed, rebind.TransferSpeed);

            return true;
        }

        public void ResetTrail() => HoldTrail();

        private void Awake()
        {
            _trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        private void OnEnable() => HoldTrail();

        private void HoldTrail()
        {
            _trailPending = true;
            _trailFrame = Time.frameCount;

            if (_trails == null) _trails = GetComponentsInChildren<TrailRenderer>(true);

            for (int i = 0; i < _trails.Length; i++)
            {
                TrailRenderer trail = _trails[i];

                trail.emitting = false;
                trail.Clear();
                trail.enabled = false;
            }
        }

        private void ArmTrail()
        {
            _trailPending = false;

            for (int i = 0; i < _trails.Length; i++)
            {
                TrailRenderer trail = _trails[i];

                trail.enabled = true;
                trail.emitting = false;
                trail.Clear();
                trail.emitting = true;
            }
        }

        /// <summary>回池复位：别把上一发的伤害与连线热度带到下一次</summary>
        public void Reset()
        {
            ResetTrail();

            _home?.Unregister(this);

            _pool = null;
            _home = null;
            _system = null;
            _afterimage = null;
            _owner = null;
            _hitSink = null;
            _id = -1;
            _hurtOther = null;
            _hurtBox = null;
            _hitCollider = null;
            _bound = false;
            _state = BulletState.Flying;
            _velocity = Vector2.zero;
            _segFrom = -1;
            _segTo = -1;

            // 摘攻击者：同帧未派发的受击盒回调别再结算
            if (_attackBox != null)
            {
                _attackBox.SetAttack(null);
                _attackBox.SetOwnerRoot(null);
            }

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

        private void PlayHitSpark(Collider2D other)
        {
            EffectModule effects = GameRoot.Instance != null ? GameRoot.Instance.Effects : null;

            if (effects == null) return;

            effects.PlayHitSpark(Position, SparkDirection(other), particleColor);
        }

        // 取外法线，不跟弹道走
        private Vector2 SparkDirection(Collider2D other)
        {
            if (other != null && other.enabled)
            {
                Vector2 normal = Position - other.ClosestPoint(Position);

                if (normal.sqrMagnitude > 1e-6f) return normal.normalized;
            }

            return _velocity.sqrMagnitude > 1e-6f ? -_velocity.normalized : Vector2.up;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || _system == null) return;

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

            // 敌弹打中就收，无敌窗挡掉也收
            HurtBox hurt = HurtBoxOf(other);

            if (Faction == BulletFaction.Enemy && hurt != null && hurt.Hurtable != null)
            {
                _hitCollider = other;
                hurt.Hurtable.TakeDamage(_damage);
                OnHitLanded();
                return;
            }

            if (_attackBox != null && other.gameObject.layer == HitLayer)
            {
                _hitCollider = other;
                return;
            }

            PlayHitSpark(other);
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

            _home?.Unregister(this);
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

        public void OnHitLanded(Collider2D other)
        {
            _hitCollider = other;
            OnHitLanded();
        }

        public void OnHitLanded()
        {
            if (_hit) return;

            _hit = true;

            _hitSink?.OnHitLanded();

            if (Faction == BulletFaction.Player) GameRoot.Instance?.Audio?.EnqueueSfx(AudioId.WhiteAttackHit);

            PlayHitSpark(_hitCollider);
            _system?.Kill(_id);
        }
    }
}
