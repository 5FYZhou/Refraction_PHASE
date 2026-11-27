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
    /// <summary>Mantis 的组合根：装配、喂玩家位置与巡逻框、按攻击请求结算前方范围伤害、转发弹反</summary>
    public sealed class MantisController : ManagedEnemyController
    {
        [Tooltip("敌人参数资产，运行时只读不改。")]
        [SerializeField]
        private MantisConfig config;

        [Tooltip("驱动刚体的执行器：地面敌人那套，探地贴地 + 探墙 + 探悬崖。")]
        [SerializeField]
        private WarriorMotor moveMotor;

        [Tooltip("这只敌人的固有形态：勾上 = 黑形态，两套攻击与弹反能力都跟它走，与当前世界极性无关。\n世界每换一次极性它会翻到另一态。")]
        [SerializeField]
        private bool blackForm;

        [Tooltip("白形态的子弹预制体：留空则白形态只走位移不开枪。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("出膛点相对身体的局部偏移（单位，x 按开火方向镜像）。")]
        [SerializeField]
        private Vector2 bulletMuzzle = Vector2.zero;

        [Tooltip("蓄力吸入粒子的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color chargeInColor = Color.red;

        [Tooltip("巡逻框相对敌人的偏移（局部坐标）：编辑器里跟着敌人显示，运行时以进入游戏那一刻的站位固化成世界坐标。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻框尺寸（单位）：只有宽度参与判定。\n两个分量都留 0 = 不启用巡逻框，按整平台巡逻。")]
        [SerializeField]
        private Vector2 patrolSize = new(10f, 3f);

        [Header("范围")]
        [Tooltip("追击停止距离（单位，两个中心之间）：追进这个圈就刹住并立刻起手。\nScene 视图里以此半径为圆画出来。")]
        [Min(0f)]
        [SerializeField]
        private float stopDistance = 1.8f;

        [Tooltip("攻击范围（单位）：身体正前方这么长的一段矩形，攻击段在这里结算范围伤害。\nScene 视图里按面朝方向画出这个框。")]
        [Min(0f)]
        [SerializeField]
        private float attackRange = 2.2f;

        [Tooltip("攻击范围的高度（单位）")]
        [Min(0f)]
        [SerializeField]
        private float attackHeight = 1.6f;

        [Tooltip("攻击范围中心相对枢轴的高度（单位）：框的竖直中心，按身体的腰线调。")]
        [SerializeField]
        private float attackCenterY = 0.6f;

        [Tooltip("弹反成功时 ReboundCollider 红光亮起的时长（秒）：0 表示不闪。")]
        [Min(0f)]
        [SerializeField]
        private float reboundFlashTime = 0.18f;

        private const string ReboundChildName = "ReboundCollider";

        private readonly EnemySight _sight = new(false);
        private readonly Collider2D[] _hits = new Collider2D[8];

        private MantisLogic _logic;
        private BulletLauncher _bullets;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private ReboundFlash _reboundFlash;
        private bool _tintBlack;

        private EnemyArea _patrolArea;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);
            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(MantisController), _sight);

            CacheReboundZone();
        }

        protected override EnemySpawnState CaptureSpawn() => new(
            EnemyKind.Mantis,
            transform.position,
            moveMotor.FaceSign,
            blackForm,
            config.maxHealth);

        protected override bool BodyAlive => _logic != null && _logic.IsAlive;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        protected override void Assemble()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);

            Require.Assigned(moveMotor, nameof(MantisController), nameof(moveMotor));
            Require.Assigned(config, nameof(MantisController), nameof(config));

            moveMotor.BindConfig(config);

            _hurtFlash = new(Root.Effects, transform, config.flashTime);
            _logic = new MantisLogic(moveMotor, config, _hurtFlash, true, stopDistance, bulletMuzzle, blackForm);

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

            if (_logic.ConsumeHit()) ApplyAreaDamage();

            TickTint();
            _reboundFlash?.Tick(deltaTime);
            _hurtFlash.SetCharging(
                _logic.ChargingShot,
                _logic.ChargingTime,
                _logic.Muzzle(Vector2.zero, moveMotor.FaceSign),
                chargeInColor);
        }

        /// <summary>弹反入口：只认玩家阵营的飞行子弹；形态与冷却归逻辑层判，触发后不打断当前攻击</summary>
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
            _logic = new MantisLogic(moveMotor, config, _hurtFlash, true, stopDistance, bulletMuzzle, SpawnState.InitialBlack);
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

        /// <summary>前方范围伤害：按面朝方向摆一个矩形，框内每个受击目标一帧只吃一次</summary>
        private void ApplyAreaDamage()
        {
            if (_logic == null) return;

            int face = moveMotor.FaceSign;

            Vector2 center = (Vector2)transform.position + new Vector2(face * attackRange * 0.5f, attackCenterY);
            Vector2 size = new Vector2(attackRange, attackHeight);

            int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, _hits);

            Damage damage = _logic.ProduceAreaDamage();

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];

                if (hit == null) continue;
                if (hit.transform.IsChildOf(transform)) continue;

                HurtBox box = hit.GetComponent<HurtBox>();

                if (box == null || box.Hurtable == null) continue;
                if (AlreadyHit(i, box.Hurtable)) continue;

                box.Hurtable.TakeDamage(in damage);
            }
        }

        /// <summary>同一个身体可能挂多个受击盒：同一帧里只让第一个生效</summary>
        private bool AlreadyHit(int index, IHurtable target)
        {
            for (int i = 0; i < index; i++)
            {
                Collider2D other = _hits[i];

                if (other == null) continue;

                HurtBox box = other.GetComponent<HurtBox>();

                if (box != null && box.Hurtable == target) return true;
            }

            return false;
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
                Debug.LogWarning($"[MantisController] 找不到 {ReboundChildName} 子物体：这只怪不会弹反。");
                return;
            }

            Collider2D trigger = zone.GetComponent<Collider2D>();
            if (trigger == null || !trigger.isTrigger)
            {
                Debug.LogWarning($"[MantisController] {ReboundChildName} 上没有触发器碰撞体（Is Trigger 未勾选）：弹反收不到子弹。");
                return;
            }

            MantisRebound forwarder = zone.GetComponent<MantisRebound>();
            if (forwarder == null) forwarder = zone.gameObject.AddComponent<MantisRebound>();

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
            patrolSize = new Vector2(10f, 3f);
            stopDistance = 1.8f;
            attackRange = 2.2f;
            attackHeight = 1.6f;
            attackCenterY = 0.6f;
        }

        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && Root != null) EnemyGizmos.DrawArea(_patrolArea, EnemyGizmos.Patrol);
            else EnemyGizmos.DrawZone(transform, patrolOffset, patrolSize, EnemyGizmos.Patrol);

            EnemyGizmos.DrawSight(transform, EnemyColliderSetup.DetectChildName);
            EnemyGizmos.DrawSight(transform, ReboundChildName);

            Gizmos.color = EnemyGizmos.Chase;
            Gizmos.DrawWireSphere(transform.position, stopDistance);

            int face = moveMotor != null ? moveMotor.FaceSign : 1;
            Vector3 center = transform.position + new Vector3(face * attackRange * 0.5f, attackCenterY, 0f);

            Gizmos.color = EnemyGizmos.Muzzle;
            Gizmos.DrawWireCube(center, new Vector3(attackRange, attackHeight, 0.01f));

            if (bulletPrefab != null) EnemyGizmos.DrawMuzzle(transform, bulletMuzzle);
        }
#endif
    }
}
