using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic;
using Refraction.Logic.Actor;
using Refraction.Logic.Combat;
using Refraction.Presentation.Combat;
using Refraction.Presentation.Effects;
using UnityEngine;

namespace Refraction.Presentation.Actor
{
    /// <summary>毛毛虫 E_First 的组合根：装配、喂视线、按黑白给 View 取色、按出生快照复位</summary>
    public sealed class E_FirstController : MonoBehaviour, IManagedEnemy
    {
        [Tooltip("敌人参数资产，运行时只读不改。")]
        [SerializeField]
        private E_FirstConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private E_FirstMotor moveMotor;

        [Tooltip("这只敌人的固有形态：勾上 = 黑形态，黑 / 白攻击与实体换色都跟它走，与当前世界极性无关。\n世界每换一次极性它会翻到另一态。")]
        [SerializeField]
        private bool blackForm;

        [Tooltip("白色攻击的子弹预制体：留空则不开火，只走动作。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("出膛点相对身体的局部偏移（单位，x 按开火方向镜像，y 向上为正）：只在接了子弹预制体时有用，Editor 里会画出这个点。")]
        [SerializeField]
        private Vector2 bulletMuzzle = new(0.6f, 0.2f);

        [Tooltip("蓄力吸入粒子的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color chargeInColor = Color.red;

        [Tooltip("巡逻范围相对敌人的偏移（局部坐标）：编辑器里跟着敌人显示，运行时以进入游戏那一刻的站位固化成世界坐标。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻范围尺寸（单位）：宽度决定左右折返点，高度只用来画框。\n两个分量都留 0（默认）= 不启用巡逻框，按整平台走。")]
        [SerializeField]
        private Vector2 patrolSize;

        private const int BulletPoolSize = 4;

        private readonly EnemySight _sight = new(false);

        private GameRoot _root;
        private bool _attached;
        private bool _dead;
        private E_FirstLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;
        private EnemySpawnState _spawn;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private bool _tintBlack;

        private EnemyArea _patrolArea;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);
            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(E_FirstController), _sight);
        }

        private void Start()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);

            _root = GameRoot.Instance;

            Attach();

            _spawn = new EnemySpawnState(
                EnemyKind.E_First,
                transform.position,
                moveMotor.FaceSign,
                blackForm,
                config.maxHealth);

            EnemyMgr.Join(this);
        }

        private void OnDestroy()
        {
            _logic?.Dispose();

            if (_bulletField != null) Destroy(_bulletField.gameObject);

            EnemyMgr.Leave(this);
        }

        public EnemySpawnState SpawnState => _spawn;

        public bool IsDead => _dead;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        private void Attach()
        {
            if (_attached) return;

            Require.Assigned(moveMotor, nameof(E_FirstController), nameof(moveMotor));
            Require.Assigned(config, nameof(E_FirstController), nameof(config));

            moveMotor.BindConfig(config);

            _hurtFlash = new(_root.Effects, transform, config.flashTime);
            _logic = new E_FirstLogic(moveMotor, config, _hurtFlash, true, bulletMuzzle, blackForm);

            HitBoxBinder.Bind(transform, _logic, _logic);

            AttachBullets();

            _tintBlack = _logic.Black;
            _tint.Apply(_tintBlack);

            _attached = true;
        }

        public void ManagedTick(float deltaTime)
        {
            if (!_attached || _dead) return;

            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, _sight.Sense(), SamplePatrolArea());

            _bullets?.Tick(deltaTime);

            TickTint();
            _hurtFlash.SetCharging(_logic.ChargingShot, _logic.ChargingTime, _logic.Muzzle(Vector2.zero, moveMotor.FaceSign), chargeInColor);

            if (!_logic.IsAlive) _dead = true;
        }

        public void Recycle()
        {
            _bullets?.ClearAll();
            gameObject.SetActive(false);
        }

        public void ResetToSpawn()
        {
            if (!_attached) return;

            gameObject.SetActive(false);
            _dead = false;

            _logic?.Dispose();
            _logic = new E_FirstLogic(moveMotor, config, _hurtFlash, true, bulletMuzzle, SpawnState.InitialBlack);
            _logic.SetHealth(SpawnState.InitialHealth);

            HitBoxBinder.Bind(transform, _logic, _logic);

            if (_bullets != null)
            {
                _bullets.ClearAll();
                _logic.BindLauncher(_bullets);
            }

            _sight.Reset();

            moveMotor.FaceTowards(new Vector2(SpawnState.FaceSign, 0f));
            moveMotor.SpawnAt(SpawnState.Position, Vector2.zero);

            _tintBlack = _logic.Black;
            _tint.Apply(_tintBlack);

            // 重新入世：探测器对当前重叠再发一次 Enter，复位清掉的视线才补得回来
            gameObject.SetActive(true);
        }

        private void TickTint()
        {
            bool black = _logic.Black;

            if (black == _tintBlack) return;

            _tintBlack = black;
            _tint.Apply(black);
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

            EnemyGizmos.DrawSight(transform, EnemyColliderSetup.DetectChildName);

            if (bulletPrefab != null) EnemyGizmos.DrawMuzzle(transform, bulletMuzzle);
        }
#endif
    }
}
