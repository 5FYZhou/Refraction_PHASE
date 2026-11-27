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
    /// <summary>Boss mmfly 的组合根：装配、喂玩家位置、按技能阶段转 View、弹反转发、召唤小怪</summary>
    public sealed class MmflyController : ManagedEnemyController, IRenderTicked
    {
        [Tooltip("Boss 参数资产，运行时只读不改。")]
        [SerializeField]
        private MmflyConfig config;

        [Tooltip("驱动刚体的执行器：飞行怪那套，无重力、只探墙。")]
        [SerializeField]
        private SpliterMotor moveMotor;

        [Tooltip("这只 Boss 的固有形态：勾上 = 黑形态，俯冲 / 弹幕召唤与弹反能力都跟它走，与当前世界极性无关。\n世界每换一次极性它会翻到另一态，技能池跟着换。")]
        [SerializeField]
        private bool blackForm;

        [Tooltip("子弹预制体：白形态弹幕从这里出。留空则既不开火也弹不了。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("出膛点相对身体的局部偏移（单位，x 按开火方向镜像）。")]
        [SerializeField]
        private Vector2 bulletMuzzle = Vector2.zero;

        [Tooltip("蓄力吸入粒子的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color chargeInColor = Color.red;

        [Header("范围")]
        [Tooltip("理想距离上限（单位，两个中心之间）：超过就逼近，进到以内就停；观察结束也用它判要不要逼近。\nScene 视图里以此半径为圆画出来。")]
        [Min(0f)]
        [SerializeField]
        private float idealDistance = 6f;

        [Tooltip("俯冲触发距离（单位）：玩家进这个圈且冷却好了才进技能池。")]
        [Min(0f)]
        [SerializeField]
        private float diveRange = 7f;

        [Tooltip("弹幕触发距离（单位）：同上，只看白形态。")]
        [Min(0f)]
        [SerializeField]
        private float barrageRange = 8f;

        [Tooltip("召唤触发距离（单位）：同上，只看白形态。")]
        [Min(0f)]
        [SerializeField]
        private float summonRange = 8f;

        [Tooltip("召唤半径（单位）：小怪在以自身为圆心的这个圈里随机落点，落点撞墙就重摇。")]
        [Min(0f)]
        [SerializeField]
        private float summonRadius = 4f;

        [Tooltip("转 z 轴的视觉根：留空按名字找 View。\n俯冲与弹幕前摇实时转向玩家，锁定后停在该角，出技能转回初始角。")]
        [SerializeField]
        private Transform viewRoot;

        [Tooltip("转 z 轴的角速度（度/秒）：0 表示瞬间对齐。")]
        [Min(0f)]
        [SerializeField]
        private float viewTurnSpeed = 540f;

        [Tooltip("弹反成功时 ReboundCollider 红光亮起的时长（秒）：0 表示不闪。")]
        [Min(0f)]
        [SerializeField]
        private float reboundFlashTime = 0.18f;

        [Header("召唤")]
        [Tooltip("召唤用的飞行小怪预制体（E_Second）。与毛毛虫都留空 = 召唤技能永远不进技能池。")]
        [SerializeField]
        private GameObject minionFlyPrefab;

        [Tooltip("召唤用的爬行小怪预制体（E_First）。")]
        [SerializeField]
        private GameObject minionCrawlerPrefab;

        [Tooltip("小怪落点要避开的层：召唤半径内被这些层占住的位置会重摇。")]
        [SerializeField]
        private LayerMask minionBlockMask;

        [Tooltip("小怪落点的净空半径（单位）：按小怪身体半径留，太小会卡进墙里。")]
        [Min(0f)]
        [SerializeField]
        private float minionClearance = 0.5f;

        private const string ReboundChildName = "ReboundCollider";
        private const string ViewChildName = "View";
        private const float DirEps = 1e-6f;
        private const float MaxViewAngle = 90f;
        private const float SummonShakeAmp = 0.08f;
        private const float SummonShakeRate = 48f;
        private const float SummonFlashInterval = 0.09f;

        private readonly EnemySight _sight = new(false);

        private MmflyLogic _logic;
        private MmflyMinionField _minions;
        private BulletLauncher _bullets;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private ReboundFlash _reboundFlash;
        private bool _tintBlack;

        private Vector3 _viewBasePos;
        private float _viewAngle;
        private float _baseViewAngle;
        private float _lockedViewAngle;
        private bool _aimLocked;

        private bool _warning;
        private bool _flashOn;
        private float _flashTimer;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);
            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(MmflyController), _sight);

            CacheReboundZone();
        }

        protected override EnemySpawnState CaptureSpawn() => new(
            EnemyKind.Mmfly,
            transform.position,
            moveMotor.FaceSign,
            blackForm,
            config.maxHealth);

        protected override bool BodyAlive => _logic != null && _logic.IsAlive;

        /// <summary>渲染帧转 z 轴并做召唤预警：放物理帧里会被 50Hz 卡成跳格</summary>
        public void RenderTick(float deltaTime)
        {
            if (!Attached || IsDead) return;

            TickSummonWarning(deltaTime);

            if (viewRoot == null) return;

            float target = ResolveViewAngle();

            if (viewTurnSpeed <= 0f) _viewAngle = target;
            else
            {
                float step = viewTurnSpeed * deltaTime;
                _viewAngle += Mathf.Clamp(Mathf.DeltaAngle(_viewAngle, target), -step, step);
            }

            viewRoot.localRotation = Quaternion.Euler(0f, 0f, _viewAngle);
        }

        /// <summary>弹反入口：只认玩家阵营的飞行子弹；形态、冷却与当前行为都归逻辑层判，触发后不打断它</summary>
        public void OnReboundEnter(Collider2D other)
        {
            if (!Attached || IsDead || other == null) return;

            Bullet bullet = other.GetComponentInParent<Bullet>();
            if (bullet == null || bullet.Faction != BulletFaction.Player) return;

            float now = Time.fixedTime;
            if (!_logic.CanRebound(now)) return;

            Damage damage = bullet.ProduceDamage();
            BulletRebind rebind = new(BulletFaction.Enemy, damage.Amount, damage.KnockbackSpeed);

            if (!bullet.TryReflect(in rebind, transform, null)) return;

            _logic.MarkRebounded(now);
            Root.Audio?.EnqueueSfx(AudioId.WeaponClashEnemy);

            _reboundFlash?.Play();
        }

        protected override void Assemble()
        {
            Require.Assigned(moveMotor, nameof(MmflyController), nameof(moveMotor));
            Require.Assigned(config, nameof(MmflyController), nameof(config));

            moveMotor.BindConfig(config);

            _hurtFlash = new(Root.Effects, transform, config.flashTime);
            _logic = new MmflyLogic(moveMotor, config, _hurtFlash, blackForm, true, RangeOf(), bulletMuzzle);

            _minions = new MmflyMinionField(
                transform,
                minionFlyPrefab,
                minionCrawlerPrefab,
                minionBlockMask,
                minionClearance,
                summonRadius);

            _logic.BindMinions(_minions);

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
            _reboundFlash?.Tick(deltaTime);
            _hurtFlash.SetCharging(
                _logic.ChargingShot,
                _logic.ChargingTime,
                _logic.Muzzle(Vector2.zero, moveMotor.FaceSign),
                chargeInColor);
        }

        protected override void OnRecycle()
        {
            _hurtFlash?.StopCharging();
            _minions?.Clear();
        }

        protected override void ResetBody()
        {
            _logic?.Dispose();
            _minions?.Clear();

            _logic = new MmflyLogic(moveMotor, config, _hurtFlash, SpawnState.InitialBlack, true, RangeOf(), bulletMuzzle);
            _logic.SetHealth(SpawnState.InitialHealth);

            _minions = new MmflyMinionField(
                transform,
                minionFlyPrefab,
                minionCrawlerPrefab,
                minionBlockMask,
                minionClearance,
                summonRadius);

            _logic.BindMinions(_minions);

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

            ResetView();

            _warning = false;
            _flashOn = false;
            _flashTimer = 0f;
        }

        protected override void OnDispose()
        {
            _logic?.Dispose();
            _minions?.Clear();
            _reboundFlash?.Dispose();
        }

        /// <summary>Scene 里画的那几个圈与逻辑判定共用同一份值，改一处两边一起变</summary>
        private MmflyRange RangeOf() => new(diveRange, barrageRange, summonRange, summonRadius, idealDistance);

        private void ResetView()
        {
            _viewAngle = _baseViewAngle;
            _lockedViewAngle = _baseViewAngle;
            _aimLocked = false;

            if (viewRoot == null) return;

            viewRoot.localRotation = Quaternion.Euler(0f, 0f, _baseViewAngle);
            viewRoot.localPosition = _viewBasePos;
        }

        /// <summary>召唤预警：身体来回抖 + 颜色按固定频率在黑白之间闪，进了攻击段就停</summary>
        private void TickSummonWarning(float deltaTime)
        {
            bool warn = _logic.SummonWarning;

            if (!warn)
            {
                if (!_warning) return;

                _warning = false;

                if (viewRoot != null) viewRoot.localPosition = _viewBasePos;

                _tint.Apply(_tintBlack);

                return;
            }

            _warning = true;
            _flashTimer -= deltaTime;

            if (_flashTimer <= 0f)
            {
                _flashTimer = SummonFlashInterval;
                _flashOn = !_flashOn;

                _tint.Apply(_tintBlack ^ _flashOn);
            }

            if (viewRoot == null) return;

            float t = Time.time * SummonShakeRate;

            viewRoot.localPosition = _viewBasePos + new Vector3(
                Mathf.Sin(t) * SummonShakeAmp,
                Mathf.Cos(t * 0.83f) * SummonShakeAmp,
                0f);
        }

        private float ResolveViewAngle()
        {
            if (_logic.AimAtTarget) return ClampViewAngle(AimAngle());
            if (_logic.AimLocked) return _lockedViewAngle;

            return _baseViewAngle;
        }

        /// <summary>锁定那一帧把角度定格：与逻辑层锁方向读的是同一份玩家位置，两边不会打架</summary>
        private void TickAimLock()
        {
            bool locked = _logic.AimLocked;

            if (locked && !_aimLocked) _lockedViewAngle = ClampViewAngle(AimAngle());

            _aimLocked = locked;
        }

        private float ClampViewAngle(float angle) =>
            _baseViewAngle + Mathf.Clamp(Mathf.DeltaAngle(_baseViewAngle, angle), -MaxViewAngle, MaxViewAngle);

        private float AimAngle()
        {
            Vector2 dir = _logic.AimPoint - (Vector2)transform.position;

            if (dir.sqrMagnitude <= DirEps) return _baseViewAngle;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // View 被根节点的 scale.x 翻号镜像着：镜像时本地角要按 180−角 反算，否则朝左时会指着反侧
            return viewRoot.lossyScale.x < 0f ? 180f - angle : angle;
        }

        /// <summary>View 的初始角与初始位置当场记下：锁定、回位与抖动复位都以它为基准</summary>
        private void CacheView()
        {
            if (viewRoot == null) viewRoot = transform.Find(ViewChildName);
            if (viewRoot == null) return;

            _baseViewAngle = Mathf.DeltaAngle(0f, viewRoot.localEulerAngles.z);
            _viewBasePos = viewRoot.localPosition;
            _viewAngle = _baseViewAngle;
            _lockedViewAngle = _baseViewAngle;
        }

        private void TickTint()
        {
            bool black = _logic.Black;

            if (black == _tintBlack) return;

            _tintBlack = black;
            _tint.Apply(black);
        }

        /// <summary>弹反范围是子物体上的触发器：就地补一个转发器，不去赌复合刚体的消息路由</summary>
        private void CacheReboundZone()
        {
            Transform zone = transform.Find(ReboundChildName);
            if (zone == null)
            {
                Debug.LogWarning($"[MmflyController] 找不到 {ReboundChildName} 子物体：这个 Boss 不会弹反。");
                return;
            }

            Collider2D trigger = zone.GetComponent<Collider2D>();
            if (trigger == null || !trigger.isTrigger)
            {
                Debug.LogWarning($"[MmflyController] {ReboundChildName} 上没有触发器碰撞体（Is Trigger 未勾选）：弹反收不到子弹。");
                return;
            }

            MmflyRebound forwarder = zone.GetComponent<MmflyRebound>();
            if (forwarder == null) forwarder = zone.gameObject.AddComponent<MmflyRebound>();

            forwarder.Init(this);

            _reboundFlash = new ReboundFlash(trigger, reboundFlashTime);
        }

        /// <summary>发射口按需领取：Boss 的 Start 早于 CombatRoot.Attach，拿到之前开火自然哑掉</summary>
        private void EnsureLauncher()
        {
            if (_bullets != null || bulletPrefab == null) return;

            _bullets = CombatRoot.Acquire(bulletPrefab, transform, BulletFaction.Enemy);

            if (_bullets != null) _logic?.BindLauncher(_bullets);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            bulletMuzzle = Vector2.zero;
            minionClearance = 0.5f;
        }

        private void OnDrawGizmosSelected()
        {
            EnemyGizmos.DrawSight(transform, EnemyColliderSetup.DetectChildName);
            EnemyGizmos.DrawSight(transform, ReboundChildName);

            Gizmos.color = EnemyGizmos.Chase;
            Gizmos.DrawWireSphere(transform.position, idealDistance);

            Gizmos.color = EnemyGizmos.Patrol;
            Gizmos.DrawWireSphere(transform.position, diveRange);

            Gizmos.color = EnemyGizmos.Sight;
            Gizmos.DrawWireSphere(transform.position, barrageRange);

            Gizmos.color = EnemyGizmos.Chase;
            Gizmos.DrawWireSphere(transform.position, summonRange);

            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, summonRadius);

            if (bulletPrefab != null) EnemyGizmos.DrawMuzzle(transform, bulletMuzzle);
        }
#endif
    }
}
