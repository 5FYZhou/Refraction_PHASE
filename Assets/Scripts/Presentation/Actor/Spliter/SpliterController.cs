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
    /// <summary>Spliter 的组合根：装配、喂玩家位置、转发弹反、按阶段转 View、按出生快照复位</summary>
    public sealed class SpliterController : ManagedEnemyController, IRenderTicked
    {
        [Tooltip("敌人参数资产，运行时只读不改。")]
        [SerializeField]
        private SpliterConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private SpliterMotor moveMotor;

        [Tooltip("这只敌人的固有形态：勾上 = 黑形态，冲撞 / 弹幕与弹反能力都跟它走，与当前世界极性无关。\n世界每换一次极性它会翻到另一态。")]
        [SerializeField]
        private bool blackForm;

        [Tooltip("子弹预制体：白形态弹幕与弹反的平行回射都从这里出。留空则既不开火也弹不了。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("出膛点相对身体的局部偏移（单位，x 按开火方向镜像）：只在接了子弹预制体时有用。")]
        [SerializeField]
        private Vector2 bulletMuzzle = Vector2.zero;

        [Tooltip("蓄力吸入粒子的颜色：直接覆盖粒子自带颜色。")]
        [SerializeField]
        private Color chargeInColor = Color.red;

        [Tooltip("巡逻框相对敌人的偏移（局部坐标）：编辑器里跟着敌人显示，运行时以进入游戏那一刻的站位固化成世界坐标。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻框尺寸（单位）：只有宽度参与判定。\n两个分量都留 0 = 不启用巡逻框，只朝墙与悬崖飞。")]
        [SerializeField]
        private Vector2 patrolSize = new(12f, 4f);

        [Tooltip("追击框相对敌人的偏移（局部坐标）：和巡逻框一样在编辑器里对着摆，运行时快照成世界坐标。")]
        [SerializeField]
        private Vector2 chaseOffset;

        [Tooltip("追击框尺寸（单位）：玩家 x 或 y 出框即脱战。\n宽度留 0 = 无界，玩家在场就一直追；高度留 0 = 只判横向。通常要比巡逻框大。")]
        [SerializeField]
        private Vector2 chaseSize = new(20f, 6f);

        [Tooltip("转 z 轴的那个视觉根：留空按名字找 View 子物体。\n白攻击前摇实时转向玩家，前摇结束锁在该角度，出攻击态后转回初始角。")]
        [SerializeField]
        private Transform viewRoot;

        [Tooltip("转 z 轴的角速度（度/秒）：0 表示瞬间对齐。")]
        [Min(0f)]
        [SerializeField]
        private float viewTurnSpeed = 720f;

        [Tooltip("弹反成功时 ReboundCollider 红光亮起的时长（秒）：0 表示不闪。")]
        [Min(0f)]
        [SerializeField]
        private float reboundFlashTime = 0.18f;

        private const string ReboundChildName = "ReboundCollider";
        private const string ViewChildName = "View";
        private const int BulletPoolSize = 6;
        private const float DirEps = 1e-6f;
        private const float MaxViewAngle = 90f;

        private readonly EnemySight _sight = new(false);

        private SpliterLogic _logic;
        private BulletLogic _bullets;
        private BulletField _bulletField;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private ReboundFlash _reboundFlash;
        private bool _tintBlack;

        private EnemyArea _patrolArea;
        private EnemyArea _chaseArea;

        private float _viewAngle;
        private float _baseViewAngle;
        private float _lockedViewAngle;
        private bool _aimLocked;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);
            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(SpliterController), _sight);

            CacheReboundZone();
        }

        protected override EnemySpawnState CaptureSpawn() => new(
            EnemyKind.Spliter,
            transform.position,
            moveMotor.FaceSign,
            blackForm,
            config.maxHealth);

        protected override bool BodyAlive => _logic != null && _logic.IsAlive;

        protected override void Assemble()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);
            _chaseArea = EnemyArea.Snapshot(transform.position, chaseOffset, chaseSize);

            Require.Assigned(moveMotor, nameof(SpliterController), nameof(moveMotor));
            Require.Assigned(config, nameof(SpliterController), nameof(config));

            moveMotor.BindConfig(config);

            _hurtFlash = new(Root.Effects, transform, config.flashTime);
            _logic = new SpliterLogic(moveMotor, config, _hurtFlash, _patrolArea, _chaseArea, blackForm, true, bulletMuzzle);

            HitBoxBinder.Bind(transform, _logic, _logic);

            AttachBullets();

            _tintBlack = _logic.Black;
            _tint.Apply(_tintBlack);

            CacheView();
        }

        protected override void OnTick(float deltaTime)
        {
            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, _sight.Sense());

            TickAimLock();

            _bullets?.Tick(deltaTime);

            TickTint();
            _reboundFlash?.Tick(deltaTime);
            _hurtFlash.SetCharging(_logic.ChargingShot, _logic.ChargingTime, _logic.Muzzle(Vector2.zero, moveMotor.FaceSign), chargeInColor);
        }

        /// <summary>渲染帧转 z 轴：白前摇实时跟人、锁定后停住、其余回初始角；放物理帧里会被 50Hz 卡成跳格</summary>
        public void RenderTick(float deltaTime)
        {
            if (!Attached || IsDead || viewRoot == null) return;

            float target = ResolveViewAngle();

            if (viewTurnSpeed <= 0f) _viewAngle = target;
            else
            {
                float step = viewTurnSpeed * deltaTime;
                _viewAngle += Mathf.Clamp(Mathf.DeltaAngle(_viewAngle, target), -step, step);
            }

            viewRoot.localRotation = Quaternion.Euler(0f, 0f, _viewAngle);
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

            // 这两样必须在反射之前取：TryReflect 会把子弹回池，Reset 会清零速度并把位置复位
            Vector2 reflectDir = bullet.ReflectDirection;
            Vector2 reboundAt = bullet.Position;

            if (!bullet.TryReflect(_bullets, damage.Amount, damage.KnockbackSpeed, 0f)) return;

            _logic.MarkRebounded(now);
            Root.Audio?.EnqueueSfx(AudioId.WeaponClashEnemy);

            // 弹成了才亮：冷却挡掉的那次不该有反馈
            _reboundFlash?.Play();

            if (reflectDir.sqrMagnitude > DirEps) _logic.FireParallel(reboundAt, reflectDir);
        }

        protected override void OnRecycle()
        {
            _bullets?.ClearAll();
        }

        protected override void ResetBody()
        {
            _logic?.Dispose();
            _logic = new SpliterLogic(moveMotor, config, _hurtFlash, _patrolArea, _chaseArea, SpawnState.InitialBlack, true, bulletMuzzle);
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

            _viewAngle = _baseViewAngle;
            _lockedViewAngle = _baseViewAngle;
            _aimLocked = false;

            if (viewRoot != null) viewRoot.localRotation = Quaternion.Euler(0f, 0f, _baseViewAngle);
        }

        protected override void OnDispose()
        {
            _logic?.Dispose();
            _reboundFlash?.Dispose();

            if (_bulletField != null) Destroy(_bulletField.gameObject);
        }

        private float ResolveViewAngle()
        {
            if (!_logic.BarrageAiming) return _baseViewAngle;

            return _logic.Phase == SpliterAttackPhase.Windup ? ClampViewAngle(AimAngle()) : _lockedViewAngle;
        }

        /// <summary>前摇结束那一帧把角度定格：与逻辑层锁方向用的是同一份玩家位置，两边不会打架</summary>
        private void TickAimLock()
        {
            if (!_logic.BarrageAiming || _logic.Phase == SpliterAttackPhase.Windup)
            {
                _aimLocked = false;
                return;
            }

            if (_aimLocked) return;

            _aimLocked = true;
            _lockedViewAngle = ClampViewAngle(AimAngle());
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

        /// <summary>View 的初始角当场记下：锁定与回位都以它为基准，不是硬编码的 0</summary>
        private void CacheView()
        {
            if (viewRoot == null) viewRoot = transform.Find(ViewChildName);
            if (viewRoot == null) return;

            _baseViewAngle = Mathf.DeltaAngle(0f, viewRoot.localEulerAngles.z);
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
                Debug.LogWarning($"[SpliterController] 找不到 {ReboundChildName} 子物体：这只怪不会弹反。");
                return;
            }

            Collider2D trigger = zone.GetComponent<Collider2D>();
            if (trigger == null || !trigger.isTrigger)
            {
                Debug.LogWarning($"[SpliterController] {ReboundChildName} 上没有触发器碰撞体（Is Trigger 未勾选）：弹反收不到子弹。");
                return;
            }

            SpliterRebound forwarder = zone.GetComponent<SpliterRebound>();
            if (forwarder == null) forwarder = zone.gameObject.AddComponent<SpliterRebound>();

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
            EnemyGizmos.DrawSight(transform, ReboundChildName);

            // 攻击触发距离：以自身为圆心，与逻辑层那套距离判定共用同一个数值
            Gizmos.color = EnemyGizmos.Chase;
            Gizmos.DrawWireSphere(transform.position, config != null ? config.attackRange : 0f);

            if (bulletPrefab != null) EnemyGizmos.DrawMuzzle(transform, bulletMuzzle);
        }
#endif
    }
}
