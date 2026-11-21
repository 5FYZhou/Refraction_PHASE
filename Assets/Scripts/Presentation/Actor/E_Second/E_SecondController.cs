using System.Text;
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
    /// <summary>飞行敌人 E_Second 的组合根：装配、喂玩家位置、按展示态与黑白给 View 换色</summary>
    public sealed class E_SecondController : MonoBehaviour, ISceneRoot, IPhysicsTicked
    {
        public int Order => SceneOrder.World;

        [Tooltip("敌人参数资产，运行时只读不改。")]
        [SerializeField]
        private E_SecondConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private E_SecondMotor moveMotor;

        [Tooltip("这只敌人的固有形态：勾上 = 黑形态，黑 / 白攻击与实体换色都跟它走，与当前世界极性无关。\n世界每换一次极性它会翻到另一态。")]
        [SerializeField]
        private bool blackForm;

        [Tooltip("白色攻击的子弹预制体：留空则白极性不开火。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("出膛点相对身体的局部偏移（单位，x 按开火方向镜像）：只在接了子弹预制体时有用，Editor 里会画出这个点。\n(0,0) = 从身体中心出膛。")]
        [SerializeField]
        private Vector2 bulletMuzzle = Vector2.zero;

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

        private const int BulletPoolSize = 4;

        private readonly EnemySight _sight = new(false);

        private GameRoot _root;
        private bool _attached;
        private E_SecondLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private EnemyVisual _visual = (EnemyVisual)(-1);
        private bool _visualBlack;

        private EnemyArea _patrolArea;
        private EnemyArea _chaseArea;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);
            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(E_SecondController), _sight);
        }

        private void Start()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);
            _chaseArea = EnemyArea.Snapshot(transform.position, chaseOffset, chaseSize);

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

            moveMotor.BindConfig(config);

            _hurtFlash = new(_root.Effects, transform, config.flashTime);
            _logic = new E_SecondLogic(moveMotor, config, _hurtFlash, _patrolArea, _chaseArea, blackForm, true, bulletMuzzle);

            var hurtBox = transform.GetComponentInChildren<HurtBox>();
            if (hurtBox != null) hurtBox.SetIAttackable(_logic);
            var attackBox = transform.GetComponentInChildren<AttackBox>();
            if (attackBox != null) attackBox.SetAttack(_logic);

            AttachBullets();

            _visual = _logic.Visual;
            _visualBlack = _logic.Black;
            _tint.Apply(_visual, _visualBlack);

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

            _logic.FixedTick(context, _sight.Sense());

            _bullets?.Tick(deltaTime);

            TickVisual();
            _hurtFlash.SetCharging(_logic.ChargingShot, _logic.ChargingTime);

            if (!_logic.IsAlive) Destroy(gameObject);
        }

        private void TickVisual()
        {
            EnemyVisual visual = _logic.Visual;
            bool black = _logic.Black;

            if (visual == _visual && black == _visualBlack) return;

            _visual = visual;
            _visualBlack = black;
            _tint.Apply(visual, black);
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
            if (Application.isPlaying && _root != null)
            {
                EnemyGizmos.DrawArea(_logic != null ? _logic.PatrolArea : _patrolArea, EnemyGizmos.Patrol);
                EnemyGizmos.DrawArea(_logic != null ? _logic.ChaseArea : _chaseArea, EnemyGizmos.Chase);
            }
            else
            {
                EnemyGizmos.DrawZone(transform, patrolOffset, patrolSize, EnemyGizmos.Patrol);
                EnemyGizmos.DrawZone(transform, chaseOffset, chaseSize, EnemyGizmos.Chase);
            }

            EnemyGizmos.DrawSight(transform, EnemyColliderSetup.DetectChildName);

            if (bulletPrefab != null) EnemyGizmos.DrawMuzzle(transform, bulletMuzzle);
        }
#endif
    }
}
