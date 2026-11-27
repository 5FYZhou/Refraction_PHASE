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
    /// <summary>Fortress 的组合根：装配、采样、执行、复位，外加弹幕开火与弹反转发</summary>
    public sealed class FortressController : ManagedEnemyController
    {
        [Tooltip("敌人参数资产，运行时只读不改。")]
        [SerializeField]
        private FortressConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private EnemyMotor moveMotor;

        [Tooltip("这只敌人的固有形态：勾上 = 黑形态，弹幕与弹反能力都跟它走，与当前世界极性无关。\n世界每换一次极性它会翻到另一态。")]
        [SerializeField]
        private bool blackForm;

        [Tooltip("是否启用 AI。关掉即站桩靶子：巡逻/追踪都不推进、原地不动，受击表现与命中反馈照常，但伤害值恒为 0（打不死）。")]
        [SerializeField]
        private bool OpenAI = true;

        [Tooltip("巡逻范围相对敌人的偏移（局部坐标）：编辑器里跟着敌人显示，运行时以进入游戏那一刻的站位固化成世界坐标。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻范围尺寸（单位）：宽度决定左右折返点，高度只用来画框。\n两个分量都留 0 = 不启用巡逻框，按整平台走。")]
        [SerializeField]
        private Vector2 patrolSize;

        [Header("范围")]
        [Tooltip("追击停止距离（单位，两个中心之间）：追进这个圈就刹住，别把玩家一路顶走。\nScene 视图里以此半径为圆画出来。")]
        [Min(0f)]
        [SerializeField]
        private float stopDistance = 1.8f;

        [Tooltip("触发弹幕的最长距离（单位，两个中心之间）：进这个圈且冷却好了就起手。\nScene 视图里以此半径为圆画出来。")]
        [Min(0f)]
        [SerializeField]
        private float barrageRange = 6f;

        [Tooltip("敌人子弹的预制体：弹幕从这里出。留空 = 这只敌人不开火。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("出膛点相对敌人的局部偏移（单位，x 按开火方向镜像，y 向上为正）。\n要大于「身体半径 + 子弹半宽」，否则出膛那一帧子弹压在自己的受击盒里。")]
        [SerializeField]
        private Vector2 bulletMuzzle = new(1.2f, 0.4f);

        [Tooltip("蓄力吸入粒子的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color chargeInColor = Color.red;

        [Tooltip("弹反成功时 ReboundCollider 红光亮起的时长（秒）：0 表示不闪。")]
        [Min(0f)]
        [SerializeField]
        private float reboundFlashTime = 0.18f;

        private const string ReboundChildName = "ReboundCollider";

        private readonly EnemySight _sight = new(false);

        private FortressLogic _logic;
        private BulletLauncher _bullets;

        private EnemyArea _patrolArea;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private ReboundFlash _reboundFlash;
        private bool _tintBlack;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);
            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(FortressController), _sight);

            CacheReboundZone();
        }

        protected override EnemySpawnState CaptureSpawn() => new(
            EnemyKind.Fortress,
            transform.position,
            moveMotor.FaceSign,
            blackForm,
            config.maxHealth);

        protected override bool BodyAlive => _logic != null && _logic.IsAlive;

        public bool AIEnabled => OpenAI;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        protected override void Assemble()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);

            Require.Assigned(moveMotor, nameof(FortressController), nameof(moveMotor));
            Require.Assigned(config, nameof(FortressController), nameof(config));

            moveMotor.BindConfig(config);

            _hurtFlash = new(Root.Effects, transform, config.flashTime);
            _logic = new FortressLogic(moveMotor, config, _hurtFlash, OpenAI, stopDistance, barrageRange, bulletMuzzle, blackForm);

            HitBoxBinder.Bind(transform, _logic, _logic);

            _tintBlack = _logic.Black;
            _tint.Apply(_tintBlack);
        }

        protected override void OnTick(float deltaTime)
        {
            EnsureLauncher();

            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, _sight.Sense(), SamplePatrolArea());

            TickTint();

            _reboundFlash?.Tick(deltaTime);
            _hurtFlash.SetCharging(
                _logic.ChargingShot,
                _logic.ChargingTime,
                _logic.Muzzle(Vector2.zero, moveMotor.FaceSign),
                chargeInColor);
        }

        /// <summary>弹反入口：只认玩家阵营的飞行子弹；形态与冷却归逻辑层判，触发后不打断当前行为</summary>
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

        protected override void OnRecycle()
        {
            _hurtFlash?.StopCharging();
        }

        protected override void ResetBody()
        {
            _logic?.Dispose();
            _logic = new FortressLogic(
                moveMotor,
                config,
                _hurtFlash,
                OpenAI,
                stopDistance,
                barrageRange,
                bulletMuzzle,
                SpawnState.InitialBlack);

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
        }

        protected override void OnDispose()
        {
            _logic?.Dispose();
            _reboundFlash?.Dispose();
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
                Debug.LogWarning($"[FortressController] 找不到 {ReboundChildName} 子物体：这只敌人不会弹反。");
                return;
            }

            Collider2D trigger = zone.GetComponent<Collider2D>();
            if (trigger == null || !trigger.isTrigger)
            {
                Debug.LogWarning($"[FortressController] {ReboundChildName} 上没有触发器碰撞体（Is Trigger 未勾选）：弹反收不到子弹。");
                return;
            }

            FortressRebound forwarder = zone.GetComponent<FortressRebound>();
            if (forwarder == null) forwarder = zone.gameObject.AddComponent<FortressRebound>();

            forwarder.Init(this);

            _reboundFlash = new ReboundFlash(trigger, reboundFlashTime);
        }

        /// <summary>发射口按需领取：敌人 Start 早于 CombatRoot.Attach，拿到之前开火自然哑掉</summary>
        private void EnsureLauncher()
        {
            if (_bullets != null || bulletPrefab == null) return;

            _bullets = CombatRoot.Acquire(bulletPrefab, transform, BulletFaction.Enemy);

            if (_bullets != null) _logic?.BindLauncher(_bullets);
        }

        /// <summary>逻辑只认左右边界：快照里的高度留给编辑器画框，纵向不参与判定</summary>
        private EnemyArea SamplePatrolArea() => _patrolArea.HasBounds
            ? EnemyArea.SpanX(_patrolArea.MinX, _patrolArea.MaxX)
            : default;

#if UNITY_EDITOR
        private void Reset()
        {
            patrolOffset = Vector2.zero;
            patrolSize = new Vector2(12f, 3f);
            stopDistance = 1.8f;
            barrageRange = 6f;
        }

        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && Root != null) EnemyGizmos.DrawArea(_patrolArea, EnemyGizmos.Patrol);
            else EnemyGizmos.DrawZone(transform, patrolOffset, patrolSize, EnemyGizmos.Patrol);

            EnemyGizmos.DrawSight(transform, EnemyColliderSetup.DetectChildName);
            EnemyGizmos.DrawSight(transform, ReboundChildName);

            Gizmos.color = EnemyGizmos.Chase;
            Gizmos.DrawWireSphere(transform.position, stopDistance);

            Gizmos.color = EnemyGizmos.Sight;
            Gizmos.DrawWireSphere(transform.position, barrageRange);

            if (bulletPrefab != null) EnemyGizmos.DrawMuzzle(transform, bulletMuzzle);
        }
#endif
    }
}
