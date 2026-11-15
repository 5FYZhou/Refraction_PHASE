using System.Text;
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
    /// <summary>飞行敌人 E_Second 的组合根：装配、喂玩家位置、按展示态与黑白给 View 换色</summary>
    public sealed class E_SecondController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IDetectSink
    {
        public int Order => SceneOrder.World;

        [Tooltip("敌人参数资产，运行时只读不改。")]
        [SerializeField]
        private E_SecondConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private E_SecondMotor moveMotor;

        [Tooltip("白色攻击的子弹预制体：留空则白极性不开火。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("巡逻框相对敌人的偏移（局部坐标）：编辑器里跟着敌人显示，运行时以进入游戏那一刻的站位固化成世界坐标。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻框尺寸（单位）：只有宽度参与判定。\n两个分量都留 0 = 不启用巡逻框，只沿朝向飞。")]
        [SerializeField]
        private Vector2 patrolSize = new(12f, 4f);

        [Tooltip("追击框相对敌人的偏移（局部坐标）：和巡逻框一样在编辑器里对着摆，运行时快照成世界坐标。")]
        [SerializeField]
        private Vector2 chaseOffset;

        [Tooltip("追击框尺寸（单位）：玩家 x 或 y 出框即脱战。\n宽度留 0 = 无界，玩家在场就一直追；高度留 0 = 只判横向。通常要比巡逻框大，追出去一段再回头。")]
        [SerializeField]
        private Vector2 chaseSize = new(20f, 6f);

        private const string ViewChildName = "View";
        private const string DetectChildName = "DetectCollider";
        private const int BulletPoolSize = 4;

        private GameRoot _root;
        private bool _attached;
        private E_SecondLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;

        private PlayerController _player;
        private Transform _playerTransform;
        private Collider2D _playerBody;

        private SpriteRenderer[] _viewRenderers;
        private EnemyVisual _visual = (EnemyVisual)(-1);
        private bool _visualBlack;

        private EnemyArea _patrolArea;
        private EnemyArea _chaseArea;

        private bool _inSight;

        private void Awake()
        {
            CacheView();
            DetectZoneBinder.Bind(transform, DetectChildName, nameof(E_SecondController), this);
        }

        private void Start()
        {
            _patrolArea = SnapshotArea(patrolOffset, patrolSize);
            _chaseArea = SnapshotArea(chaseOffset, chaseSize);

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

            Require.Assigned(moveMotor, nameof(E_SecondController), nameof(moveMotor));
            Require.Assigned(config, nameof(E_SecondController), nameof(config));

            _root ??= GameRoot.Instance;

            WorldPolarity world = _root != null ? _root.World : WorldPolarity.White;

            _logic = new E_SecondLogic(moveMotor, config, _patrolArea, _chaseArea, world == WorldPolarity.Black);

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

        /// <summary>装配完成前数值全是默认值，调试面板按这个开关决定列不列</summary>
        public bool Attached => _attached;

        /// <summary>只读给调试面板看状态与两个框，面板不反向写任何东西</summary>
        public E_SecondLogic Logic => _logic;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        /// <summary>调试面板右侧栏：一行极性/状态，一行视线与领地及两个框的中心</summary>
        public void AppendDebug(StringBuilder builder)
        {
            if (_logic == null) return;

            builder.Append("\n").Append(name)
                   .Append(" ").Append(_logic.Black ? "黑" : "白")
                   .Append(" ").Append(_logic.CurrentTag.ToString())
                   .Append("\n视").Append(_logic.Visible ? "有" : "无")
                   .Append(" 领").Append(_logic.Alert ? "有" : "无")
                   .Append(" 巡");

            AppendCenter(builder, _logic.PatrolArea);

            builder.Append(" 追");

            AppendCenter(builder, _logic.ChaseArea);
        }

        private static void AppendCenter(StringBuilder builder, in EnemyArea area)
        {
            if (!area.HasBounds)
            {
                builder.Append(" 无");
                return;
            }

            builder.Append(area.Center.x.ToString("F2")).Append(",").Append(area.Center.y.ToString("F2"));
        }

        public void FixedTick(float deltaTime)
        {
            if (!_attached) return;

            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, Sense());

            _bullets?.Tick(deltaTime);

            TickVisual();

            if (!_logic.IsAlive) Destroy(gameObject);
        }

        /// <summary>位置一直带着：脱战判定还要用；Visible 只由触发器进出决定</summary>
        private EnemyTargetInfo Sense()
        {
            if (_player == null) return default;

            Vector2 pos = _playerTransform.position;

            if (_playerBody == null) return new EnemyTargetInfo(true, _inSight, pos, pos, Vector2.zero);

            Bounds body = _playerBody.bounds;

            return new EnemyTargetInfo(true, _inSight, pos, body.center, body.extents);
        }

        public void OnDetectEnter(Collider2D other)
        {
            if (other == null) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _player = player;
            _playerTransform = player.transform;
            _playerBody = PlayerBody();
            _inSight = true;
        }

        /// <summary>撞人判定要的是受击盒：玩家枢轴在脚下，拿枢轴算距离会把贴身判成没撞上</summary>
        private Collider2D PlayerBody()
        {
            HurtBox hurtBox = _player.GetComponentInChildren<HurtBox>();
            if (hurtBox == null) return null;

            return hurtBox.GetComponent<Collider2D>();
        }

        /// <summary>出门只关视线，玩家引用留着：追击框的脱战判定要靠它继续取位置</summary>
        public void OnDetectExit(Collider2D other)
        {
            if (_player == null || other == null) return;
            if (!other.transform.IsChildOf(_player.transform)) return;

            _inSight = false;
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

        /// <summary>只快照一次：每帧拿局部偏移重算，等于框跟着敌人跑</summary>
        private EnemyArea SnapshotArea(Vector2 offset, Vector2 size)
        {
            if (size.x <= 0.01f) return default;

            Vector2 center = (Vector2)transform.position + offset;

            return new EnemyArea(true, center, size * 0.5f);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            patrolOffset = Vector2.zero;
            patrolSize = new Vector2(12f, 4f);
            chaseOffset = Vector2.zero;
            chaseSize = new Vector2(20f, 6f);
        }

        private void OnDrawGizmosSelected()
        {
            Color patrol = new(1f, 0.75f, 0.2f, 0.9f);
            Color chase = new(1f, 0.4f, 0.4f, 0.9f);

            // 跑起来画逻辑真正在用的那份快照：框钉在世界上，只有 Recenter 会成对平移巡逻框与追击框
            if (Application.isPlaying && _root != null)
            {
                DrawArea(_logic != null ? _logic.PatrolArea : _patrolArea, patrol);
                DrawArea(_logic != null ? _logic.ChaseArea : _chaseArea, chase);
                return;
            }

            DrawBox(patrolOffset, patrolSize, patrol);
            DrawBox(chaseOffset, chaseSize, chase);
        }

        private static void DrawArea(in EnemyArea area, Color color)
        {
            if (!area.HasBounds) return;

            Gizmos.color = color;
            Gizmos.DrawWireCube(
                new Vector3(area.Center.x, area.Center.y, 0f),
                new Vector2(area.HalfSize.x * 2f, Mathf.Max(area.HalfSize.y * 2f, 0.2f)));
        }

        private void DrawBox(Vector2 offset, Vector2 size, Color color)
        {
            if (size.x <= 0f) return;

            Gizmos.color = color;
            Gizmos.DrawWireCube(transform.position + (Vector3)offset, new Vector2(size.x, Mathf.Max(size.y, 0.2f)));
        }
#endif
    }
}
