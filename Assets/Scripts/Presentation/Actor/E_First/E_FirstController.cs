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
    public sealed class E_FirstController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IDetectSink
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

        private GameRoot _root;
        private bool _attached;
        private E_FirstLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;

        private PlayerController _player;
        private Transform _playerTransform;

        private SpriteRenderer[] _viewRenderers;
        private EnemyVisual _visual = (EnemyVisual)(-1);
        private bool _visualBlack;

        private bool _inSight;

        private bool _hasPatrolBounds;
        private float _patrolMinX;
        private float _patrolMaxX;
        private float _patrolCenterY;

        private void Awake()
        {
            CacheView();
            DetectZoneBinder.Bind(transform, DetectChildName, nameof(E_FirstController), this);
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

            Require.Assigned(moveMotor, nameof(E_FirstController), nameof(moveMotor));
            Require.Assigned(config, nameof(E_FirstController), nameof(config));

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

            _logic.FixedTick(context, Sense(), SamplePatrolArea());

            _bullets?.Tick(deltaTime);

            TickVisual();

            if (!_logic.IsAlive) Destroy(gameObject);
        }

        /// <summary>位置一直带着：出视线之后后摇还要往玩家那边爬</summary>
        private EnemyTargetInfo Sense()
        {
            if (_player == null) return default;

            return new EnemyTargetInfo(true, _inSight, _playerTransform.position);
        }

        public void OnDetectEnter(Collider2D other)
        {
            if (other == null) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            _player = player;
            _playerTransform = player.transform;
            _inSight = true;
        }

        /// <summary>出门只关视线，玩家引用留着：后摇朝玩家爬还要取位置</summary>
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

        private EnemyArea SamplePatrolArea()
            => _hasPatrolBounds ? EnemyArea.SpanX(_patrolMinX, _patrolMaxX) : default;

#if UNITY_EDITOR
        private void Reset()
        {
            patrolOffset = Vector2.zero;
            patrolSize = new Vector2(10f, 3f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 1f, 0.45f, 0.9f);

            // 跑起来画逻辑真正在用的那份快照：框钉在世界上，敌人自己怎么爬都不动
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
