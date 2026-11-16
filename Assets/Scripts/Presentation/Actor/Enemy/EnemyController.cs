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
    public sealed class EnemyController : MonoBehaviour, ISceneRoot, IPhysicsTicked
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

        private readonly EnemySight _sight = new(true);

        private GameRoot _root;
        private bool _attached;
        private EnemyLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;

        private EnemyArea _patrolArea;

        private void Awake()
        {
            DetectZoneBinder.Bind(transform, DetectChildName, nameof(EnemyController), _sight);
        }

        private void Start()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);

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

            moveMotor.BindConfig(config);

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

        public bool HasTarget => _sight.HasTarget;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        public void FixedTick(float deltaTime)
        {
            if (!_attached) return;

            // 世界信息由 EnemyMotor 探测，本层只组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, _sight.Sense(), SamplePatrolArea());

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

        /// <summary>逻辑只认左右边界：快照里的高度留给编辑器画框，纵向不参与判定</summary>
        private EnemyArea SamplePatrolArea() => _patrolArea.HasBounds
            ? EnemyArea.SpanX(_patrolArea.MinX, _patrolArea.MaxX)
            : default;

#if UNITY_EDITOR
        private void Reset()
        {
            patrolOffset = Vector2.zero;
            patrolSize = new Vector2(18f, 3f);
        }

        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && _root != null) EnemyGizmos.DrawArea(_patrolArea, EnemyGizmos.Patrol);
            else EnemyGizmos.DrawZone(transform, patrolOffset, patrolSize, EnemyGizmos.Patrol);

            EnemyGizmos.DrawSight(transform, DetectChildName);
        }
#endif
    }
}
