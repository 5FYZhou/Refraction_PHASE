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
    /// <summary>飞行敌人 E_Second 的组合根：装配、喂玩家位置、按黑白取色、按出生快照复位</summary>
    public sealed class E_SecondController : ManagedEnemyController, IRenderTicked
    {
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

        [Tooltip("蓄力吸入粒子的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color chargeInColor = Color.red;

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

        [Tooltip("转 z 轴的那个视觉根：留空按名字找 View 子物体。\n黑冲刺转到前摇结束时锁定的角（±90° 内，冲刺起手才开始转），白攻击全程实时追玩家，其余时候转回它的初始角。")]
        [SerializeField]
        private Transform viewRoot;

        private const float DirEps = 1e-6f;
        private const float MaxViewAngle = 90f;
        private const string ViewChildName = "View";

        private readonly EnemySight _sight = new(false);

        private E_SecondLogic _logic;
        private BulletLauncher _bullets;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private bool _tintBlack;

        private EnemyArea _patrolArea;
        private EnemyArea _chaseArea;

        private float _viewAngle;
        private float _baseViewAngle;
        private float _lockedViewAngle;
        private bool _dashAimed;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);
            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(E_SecondController), _sight);
        }

        protected override EnemySpawnState CaptureSpawn() => new(
            EnemyKind.E_Second,
            transform.position,
            moveMotor.FaceSign,
            blackForm,
            config.maxHealth);

        protected override bool BodyAlive => _logic != null && _logic.IsAlive;

        /// <summary>被 Boss 召唤时用：Start 之前定形态</summary>
        public override void PrepareSpawn(bool black) => blackForm = black;

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

        /// <summary>渲染帧转 z 轴：目标角随阶段变，逼近速率看 dashTurnSpeed；放物理帧里会被 50Hz 卡成跳格</summary>
        public void RenderTick(float deltaTime)
        {
            if (!Attached || IsDead || viewRoot == null) return;

            float target = ResolveViewAngle();

            if (config.dashTurnSpeed <= 0f) _viewAngle = target;
            else
            {
                float step = config.dashTurnSpeed * deltaTime;
                _viewAngle += Mathf.Clamp(Mathf.DeltaAngle(_viewAngle, target), -step, step);
            }

            viewRoot.localRotation = Quaternion.Euler(0f, 0f, _viewAngle);
        }

        /// <summary>黑冲刺只转向前摇锁死的那个角、白攻击全程追玩家、其余回初始角</summary>
        private float ResolveViewAngle()
        {
            if (_logic.DashAiming) return _lockedViewAngle;
            if (_logic.AimAtTarget) return ClampViewAngle(AimAngle());

            return _baseViewAngle;
        }

        /// <summary>前摇结束那一帧锁角：之后玩家再换位也不再追，锁的是起冲那一刻的朝向</summary>
        private void TickAimLock()
        {
            bool dashing = _logic.DashAiming;

            if (dashing && !_dashAimed) _lockedViewAngle = ClampViewAngle(AimAngle());

            _dashAimed = dashing;
        }

        private float ClampViewAngle(float angle) =>
            _baseViewAngle + Mathf.Clamp(Mathf.DeltaAngle(_baseViewAngle, angle), -MaxViewAngle, MaxViewAngle);

        /// <summary>View 的初始角当场记下：后摇要转回的是它，不是硬编码的 0</summary>
        private void CacheView()
        {
            if (viewRoot == null) viewRoot = transform.Find(ViewChildName);
            if (viewRoot == null) return;

            _baseViewAngle = Mathf.DeltaAngle(0f, viewRoot.localEulerAngles.z);
            _viewAngle = _baseViewAngle;
            _lockedViewAngle = _baseViewAngle;
        }

        private float AimAngle()
        {
            Vector2 dir = _logic.AimPoint - (Vector2)transform.position;

            if (dir.sqrMagnitude <= DirEps) return _baseViewAngle;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // View 被根节点的 scale.x 翻号镜像着：镜像时本地角要按 180−角 反算，否则朝左时往下扑会朝上仰
            return viewRoot.lossyScale.x < 0f ? 180f - angle : angle;
        }

        protected override void Assemble()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);
            _chaseArea = EnemyArea.Snapshot(transform.position, chaseOffset, chaseSize);

            Require.Assigned(moveMotor, nameof(E_SecondController), nameof(moveMotor));
            Require.Assigned(config, nameof(E_SecondController), nameof(config));

            moveMotor.BindConfig(config);

            _hurtFlash = new(Root.Effects, transform, config.flashTime);
            _logic = new E_SecondLogic(moveMotor, config, _hurtFlash, _patrolArea, _chaseArea, blackForm, true, bulletMuzzle);

            HitBoxBinder.Bind(transform, _logic, _logic);

            _tintBlack = _logic.Black;
            _tint.Apply(_tintBlack);

            CacheView();
        }

        protected override void OnTick(float deltaTime)
        {
            EnsureLauncher();

            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, _sight.Sense());

            TickAimLock();

            TickTint();
            _hurtFlash.SetCharging(_logic.ChargingShot, _logic.ChargingTime, _logic.Muzzle(Vector2.zero, moveMotor.FaceSign), chargeInColor);
        }

        protected override void OnRecycle()
        {
            _hurtFlash?.StopCharging();
        }

        protected override void ResetBody()
        {
            _logic?.Dispose();
            _logic = new E_SecondLogic(moveMotor, config, _hurtFlash, _patrolArea, _chaseArea, SpawnState.InitialBlack, true, bulletMuzzle);
            _logic.SetHealth(SpawnState.InitialHealth);

            HitBoxBinder.Bind(transform, _logic, _logic);

            if (_bullets != null)
            {
                _bullets.Clear();
                _logic.BindLauncher(_bullets);
            }

            _sight.Reset();

            moveMotor.FaceTowards(new Vector2(SpawnState.FaceSign, 0f));
            moveMotor.SpawnAt(SpawnState.Position, Vector2.zero);

            _tintBlack = _logic.Black;
            _tint.Apply(_tintBlack);

            _viewAngle = _baseViewAngle;
            _lockedViewAngle = _baseViewAngle;
            _dashAimed = false;

            if (viewRoot != null) viewRoot.localRotation = Quaternion.Euler(0f, 0f, _baseViewAngle);
        }

        protected override void OnDispose()
        {
            _logic?.Dispose();
        }

        private void TickTint()
        {
            bool black = _logic.Black;

            if (black == _tintBlack) return;

            _tintBlack = black;
            _tint.Apply(black);
        }

        /// <summary>发射口按需领取：敌人 Start 早于 CombatRoot.Attach，拿到之前开火自然哑掉</summary>
        private void EnsureLauncher()
        {
            if (_bullets != null || bulletPrefab == null) return;

            _bullets = CombatRoot.Acquire(bulletPrefab, transform, BulletFaction.Enemy);

            if (_bullets != null) _logic?.BindLauncher(_bullets);
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
            if (Application.isPlaying && Root != null)
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
