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
    /// <summary>Warrior 的组合根：装配、喂视线、转发弹反、按出生快照复位；弹反范围取 ReboundCollider</summary>
    public sealed class WarriorController : ManagedEnemyController
    {
        [Tooltip("敌人参数资产，运行时只读不改。")]
        [SerializeField]
        private WarriorConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private WarriorMotor moveMotor;

        [Tooltip("这只敌人的固有形态：勾上 = 黑形态，扑击方向与弹反能力都跟它走，与当前世界极性无关。\n世界每换一次极性它会翻到另一态。")]
        [SerializeField]
        private bool blackForm;

        [Tooltip("子弹预制体：白形态射击与弹反回射都从这里出。留空则既不射击也弹不了。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("出膛点相对身体的局部偏移（单位，x 按开火方向镜像，y 向上为正）。")]
        [SerializeField]
        private Vector2 bulletMuzzle = new(0.5f, 0.3f);

        [Tooltip("蓄力吸入粒子的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color chargeInColor = Color.red;

        [Tooltip("扑击触发距离（单位，两个中心之间）：进了这个圈且冷却好了才起手。\nScene 视图里以此半径为圆画出来。")]
        [Min(0f)]
        [SerializeField]
        private float pounceRange = 6f;

        [Tooltip("巡逻范围相对敌人的偏移（局部坐标）：编辑器里跟着敌人显示，运行时以进入游戏那一刻的站位固化成世界坐标。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻范围尺寸（单位）：宽度决定左右折返点，高度只用来画框。\n两个分量都留 0（默认）= 不启用巡逻框，按整平台走。")]
        [SerializeField]
        private Vector2 patrolSize;

        [Tooltip("弹反成功时 ReboundCollider 红光亮起的时长（秒）：0 表示不闪。")]
        [Min(0f)]
        [SerializeField]
        private float reboundFlashTime = 0.18f;

        private const string ReboundChildName = "ReboundCollider";
        private const int BulletPoolSize = 4;

        private readonly EnemySight _sight = new(false);

        private WarriorLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private ReboundFlash _reboundFlash;
        private bool _tintBlack;

        private EnemyArea _patrolArea;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);
            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(WarriorController), _sight);

            CacheReboundZone();
        }

        protected override EnemySpawnState CaptureSpawn() => new(
            EnemyKind.Warrior,
            transform.position,
            moveMotor.FaceSign,
            blackForm,
            config.maxHealth);

        protected override bool BodyAlive => _logic != null && _logic.IsAlive;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        protected override void Assemble()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);

            Require.Assigned(moveMotor, nameof(WarriorController), nameof(moveMotor));
            Require.Assigned(config, nameof(WarriorController), nameof(config));

            moveMotor.BindConfig(config);

            _hurtFlash = new(Root.Effects, transform, config.flashTime);
            _logic = new WarriorLogic(moveMotor, config, _hurtFlash, true, pounceRange, bulletMuzzle, blackForm);

            HitBoxBinder.Bind(transform, _logic, _logic);

            AttachBullets();

            _tintBlack = _logic.Black;
            _tint.Apply(_tintBlack);
        }

        protected override void OnTick(float deltaTime)
        {
            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, _sight.Sense(), SamplePatrolArea());

            _bullets?.Tick(deltaTime);

            TickTint();
            _reboundFlash?.Tick(deltaTime);
            _hurtFlash.SetCharging(_logic.ChargingShot, _logic.ChargingTime, _logic.Muzzle(Vector2.zero, moveMotor.FaceSign), chargeInColor);
        }

        /// <summary>弹反入口：只认玩家阵营的飞行子弹；形态、冷却与当前阶段都归逻辑层判</summary>
        public void OnReboundEnter(Collider2D other)
        {
            if (!Attached || IsDead || other == null || _bullets == null) return;

            Bullet bullet = other.GetComponentInParent<Bullet>();
            if (bullet == null || !bullet.PlayerOwned) return;

            float now = Time.fixedTime;
            if (!_logic.CanRebound(now)) return;

            Damage damage = bullet.ProduceDamage();

            // 反射出去的那发归敌方，数值沿用原弹：只把方向反过来
            if (!bullet.TryReflect(_bullets, damage.Amount, damage.KnockbackSpeed, 0f)) return;

            _logic.MarkRebounded(now);
            Root.Audio?.EnqueueSfx(AudioId.WeaponClashEnemy);

            // 弹成了才亮：冷却挡掉的那次不该有反馈
            _reboundFlash?.Play();
        }

        protected override void OnRecycle()
        {
            _bullets?.ClearAll();
            _hurtFlash?.StopCharging();
        }

        protected override void ResetBody()
        {
            _logic?.Dispose();
            _logic = new WarriorLogic(moveMotor, config, _hurtFlash, true, pounceRange, bulletMuzzle, SpawnState.InitialBlack);
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
        }

        protected override void OnDispose()
        {
            _logic?.Dispose();
            _reboundFlash?.Dispose();

            if (_bulletField != null) Destroy(_bulletField.gameObject);
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
                Debug.LogWarning($"[WarriorController] 找不到 {ReboundChildName} 子物体：这只战士不会弹反。");
                return;
            }

            Collider2D trigger = zone.GetComponent<Collider2D>();
            if (trigger == null || !trigger.isTrigger)
            {
                Debug.LogWarning($"[WarriorController] {ReboundChildName} 上没有触发器碰撞体（Is Trigger 未勾选）：弹反收不到子弹。");
                return;
            }

            WarriorRebound forwarder = zone.GetComponent<WarriorRebound>();
            if (forwarder == null) forwarder = zone.gameObject.AddComponent<WarriorRebound>();

            forwarder.Init(this);

            _reboundFlash = new ReboundFlash(trigger, reboundFlashTime);
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
            if (Application.isPlaying && Root != null) EnemyGizmos.DrawArea(_patrolArea, EnemyGizmos.Patrol);
            else EnemyGizmos.DrawZone(transform, patrolOffset, patrolSize, EnemyGizmos.Patrol);

            EnemyGizmos.DrawSight(transform, EnemyColliderSetup.DetectChildName);
            EnemyGizmos.DrawSight(transform, ReboundChildName);

            // 扑击范围：以自身为圆心，与运行时那套距离判定同一份数值
            Gizmos.color = EnemyGizmos.Chase;
            Gizmos.DrawWireSphere(transform.position, pounceRange);

            if (bulletPrefab != null) EnemyGizmos.DrawMuzzle(transform, bulletMuzzle);
        }
#endif
    }
}
