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
    /// <summary>毛毛虫 E_First 的组合根：装配、喂视线、按展示态与黑白给 View 换色</summary>
    public sealed class E_FirstController : MonoBehaviour, ISceneRoot, IPhysicsTicked
    {
        public int Order => SceneOrder.World;

        [Tooltip("敌人参数资产，运行时只读不改。")]
        [SerializeField]
        private E_FirstConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private E_FirstMotor moveMotor;

        [Tooltip("白色攻击的子弹预制体：留空则不开火，只走动作。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("巡逻范围相对敌人的偏移（局部坐标）：编辑器里跟着敌人显示，运行时以进入游戏那一刻的站位固化成世界坐标。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻范围尺寸（单位）：宽度决定左右折返点，高度只用来画框。\n两个分量都留 0（默认）= 不启用巡逻框，按整平台走。")]
        [SerializeField]
        private Vector2 patrolSize;

        private const string ViewChildName = "View";
        private const string DetectChildName = "DetectCollider";
        private const int BulletPoolSize = 4;

        private readonly EnemySight _sight = new(false);

        private GameRoot _root;
        private bool _attached;
        private E_FirstLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;

        private SpriteRenderer[] _viewRenderers;
        private EnemyVisual _visual = (EnemyVisual)(-1);
        private bool _visualBlack;

        private EnemyArea _patrolArea;

        private void Awake()
        {
            CacheView();
            DetectZoneBinder.Bind(transform, DetectChildName, nameof(E_FirstController), _sight);
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

            Require.Assigned(moveMotor, nameof(E_FirstController), nameof(moveMotor));
            Require.Assigned(config, nameof(E_FirstController), nameof(config));

            moveMotor.BindConfig(config);

            _root ??= GameRoot.Instance;

            WorldPolarity world = _root != null ? _root.World : WorldPolarity.White;

            _logic = new E_FirstLogic(moveMotor, config, true, world);

            var hurtBox = transform.GetComponentInChildren<HurtBox>();
            if (hurtBox != null) hurtBox.SetIAttackable(_logic);
            var attackBox = transform.GetComponentInChildren<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(_logic);

            AttachBullets();

            _visual = _logic.Visual;
            _visualBlack = _logic.Black;
            ApplyColor(EnemyVisualColors.Of(_visual, _visualBlack));

            _attached = true;
        }

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        public void FixedTick(float deltaTime)
        {
            if (!_attached) return;

            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, _sight.Sense(), SamplePatrolArea());

            _bullets?.Tick(deltaTime);

            TickVisual();

            if (!_logic.IsAlive) Destroy(gameObject);
        }

        private void TickVisual()
        {
            EnemyVisual visual = _logic.Visual;
            bool black = _logic.Black;

            if (visual == _visual && black == _visualBlack) return;

            _visual = visual;
            _visualBlack = black;
            ApplyColor(EnemyVisualColors.Of(visual, black));
        }

        private void ApplyColor(Color color)
        {
            for (int i = 0; i < _viewRenderers.Length; i++) _viewRenderers[i].color = color;
        }

        private void CacheView()
        {
            Transform view = transform.Find(ViewChildName);

            _viewRenderers = (view != null ? view : transform).GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void AttachBullets()
        {
            if (bulletPrefab == null) return;

            GameObject host = new(nameof(BulletField));

            _bulletField = host.AddComponent<BulletField>();
            _bulletField.Init(bulletPrefab, transform, null, BulletPoolSize, 1);

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
            patrolSize = new Vector2(10f, 3f);
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
