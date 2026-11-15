using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Logic.Level;
using Refraction.Presentation.Combat;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>敌人组合根：只做装配与转发</summary>
    public sealed class EnemyController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IDetectSink
    {
        public int Order => SceneOrder.World;

        [Tooltip("敌人参数资产。巡逻、索敌与接触伤害的数值都在这里，运行时只读不改。")]
        [SerializeField]
        private EnemyConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private EnemyMotor moveMotor;

        [Tooltip("是否启用 AI。关掉即站桩靶子：巡逻/追踪都不推进、原地不动，受击表现、命中反馈与子弹回收照常，但伤害值恒为 0（打不死）。")]
        [SerializeField]
        private bool OpenAI = true;

        [Tooltip("巡逻范围相对敌人的偏移（局部坐标）：编辑器里跟着敌人一起显示，方便对着摆。\n运行时以进入游戏那一刻的站位为准固化成世界坐标，之后敌人自己怎么走框都不动。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻范围尺寸（单位）：宽度决定左右折返点，高度只用来画框。\n两个分量都留 0（默认）= 不启用巡逻框，按整平台走。")]
        [SerializeField]
        private Vector2 patrolSize;

        [Tooltip("敌人子弹的预制体：看见玩家时从这里出一发。留空 = 这只敌人不开火，只巡逻与追击。")]
        [SerializeField]
        private GameObject bulletPrefab;

        private const string DetectChildName = "DetectCollider";
        private const int EnemyBulletPoolSize = 4;

        private GameRoot _root;
        private bool _attached;
        private EnemyLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;

        private PlayerController _target;
        private Transform _targetTransform;

        private bool _hasPatrolBounds;
        private float _patrolMinX;
        private float _patrolMaxX;
        private float _patrolCenterY;

        private void Awake()
        {
            CacheDetectZone();
        }

        private void Start()
        {
            SnapshotPatrolBounds();

            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            _logic?.Dispose();

            if (_bulletField != null) Destroy(_bulletField.gameObject);

            if (_root != null) _root.UnregisterSceneRoot(this);
        }
        
        public void Attach()
        {
            if (_attached) return;

            Require.Assigned(moveMotor, nameof(EnemyController), nameof(moveMotor));
            Require.Assigned(config, nameof(EnemyController), nameof(config));

            _root ??= GameRoot.Instance;

            WorldPolarity world = _root != null ? _root.World : WorldPolarity.White;

            _logic = new EnemyLogic(moveMotor, config, OpenAI, world);

            var hurtBox = transform.GetComponentInChildren<HurtBox>();
            if (hurtBox != null) hurtBox.SetIAttackable(_logic);
            var attackBox = transform.GetComponentInChildren<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(_logic);

            AttachBullets();

            _attached = true;
        }

        public bool AIEnabled => OpenAI;

        public bool HasTarget => _target != null;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        public void FixedTick(float deltaTime)
        {
            if (!_attached) return;

            // 世界信息由 EnemyMotor 探测，本层只组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, Sense(), SamplePatrolArea());

            _bullets?.Tick(deltaTime);

            // 血被打空：这具身体没有死亡演出，直接消失（销毁排在帧末，本帧后续逻辑不受影响）
            if (!_logic.IsAlive) Destroy(gameObject);
        }

        private void AttachBullets()
        {
            if (bulletPrefab == null) return;

            GameObject host = new(nameof(BulletField));

            _bulletField = host.AddComponent<BulletField>();
            _bulletField.Init(bulletPrefab, transform, null, EnemyBulletPoolSize, 1);

            _bullets = new BulletLogic(_bulletField, false);
            _logic.BindLauncher(_bullets);
        }

        /// <summary>目标被销毁（换场景、死亡）后引用会变成伪 null，这里一律收成"看不见"，逻辑层不必再判空</summary>
        private EnemyTargetInfo Sense()
        {
            if (_target == null) return default;

            return new EnemyTargetInfo(true, true, _targetTransform.position);
        }

        /// <summary>探测触发器转发过来的进入：玩家的碰撞体可能挂在子物体上，往父级找（与 Door 同一套判法）</summary>
        public void OnDetectEnter(Collider2D other)
        {
            if (other == null) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _target = player;
            _targetTransform = player.transform;
        }

        public void OnDetectExit(Collider2D other)
        {
            if (_target == null || other == null) return;
            if (!other.transform.IsChildOf(_target.transform)) return;

            _target = null;
            _targetTransform = null;
        }

        private void CacheDetectZone()
        {
            DetectZoneBinder.Bind(transform, DetectChildName, nameof(EnemyController), this);
        }

        /// <summary>只快照一次：每帧拿局部偏移重算，等于框跟着敌人跑</summary>
        private void SnapshotPatrolBounds()
        {
            _hasPatrolBounds = patrolSize.x > 0.01f;
            if (!_hasPatrolBounds) return;

            float centerX = transform.position.x + patrolOffset.x;
            float half = patrolSize.x * 0.5f;

            _patrolCenterY = transform.position.y + patrolOffset.y;
            _patrolMinX = centerX - half;
            _patrolMaxX = centerX + half;
        }

        private EnemyPatrolArea SamplePatrolArea()
            => _hasPatrolBounds ? new EnemyPatrolArea(true, _patrolMinX, _patrolMaxX) : default;

#if UNITY_EDITOR
        private void Reset()
        {
            patrolOffset = Vector2.zero;
            patrolSize = new Vector2(18f, 3f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 1f, 0.45f, 0.9f);

            // 跑起来画逻辑用的那份快照：框钉在世界上，敌人自己怎么走都不动
            if (Application.isPlaying && _root != null)
            {
                if (!_hasPatrolBounds) return;

                Gizmos.DrawWireCube(
                    new Vector3((_patrolMinX + _patrolMaxX) * 0.5f, _patrolCenterY, 0f),
                    new Vector2(_patrolMaxX - _patrolMinX, patrolSize.y));

                return;
            }

            if (patrolSize.x <= 0f || patrolSize.y <= 0f) return;

            Gizmos.DrawWireCube(transform.position + (Vector3)patrolOffset, patrolSize);
        }
#endif
    }
}
