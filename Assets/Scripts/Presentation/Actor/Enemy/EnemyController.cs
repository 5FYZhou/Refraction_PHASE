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
    /// <summary>地面敌人组合根：装配、采样、执行、复位；管理壳在 ManagedEnemyController</summary>
    public sealed class EnemyController : ManagedEnemyController
    {
        [Tooltip("敌人参数资产。巡逻、索敌与接触伤害的数值都在这里，运行时只读不改。")]
        [SerializeField]
        private EnemyConfig config;

        [Tooltip("驱动刚体的执行器。")]
        [SerializeField]
        private EnemyMotor moveMotor;

        [Tooltip("这只敌人的固有形态：勾上 = 黑形态，黑 / 白攻击与实体换色都跟它走，与当前世界极性无关。\n世界每换一次极性它会翻到另一态。")]
        [SerializeField]
        private bool blackForm;

        [Tooltip("是否启用 AI。关掉即站桩靶子：巡逻/追踪都不推进、原地不动，受击表现、命中反馈与子弹回收照常，但伤害值恒为 0（打不死）。")]
        [SerializeField]
        private bool OpenAI = true;

        [Tooltip("巡逻范围相对敌人的偏移（局部坐标）：编辑器里跟着敌人一起显示，方便对着摆。\n运行时以进入游戏那一刻的站位为准固化成世界坐标，之后敌人自己怎么走框都不动。")]
        [SerializeField]
        private Vector2 patrolOffset;

        [Tooltip("巡逻范围尺寸（单位）：宽度决定左右折返点，高度只用来画框。\n两个分量都留 0（默认）= 不启用巡逻框，按整平台走。")]
        [SerializeField]
        private Vector2 patrolSize;

        [Tooltip("敌人子弹的预制体：看见玩家时从这里出一发。留空 = 这只敌人不开火，只巡逻与追击。")]
        [SerializeField]
        private GameObject bulletPrefab;

        [Tooltip("出膛点相对敌人的局部偏移（单位，x 按开火方向镜像，y 向上为正）：只在接了子弹预制体时有用，Editor 里会画出这个点。\n要大于「身体半径 + 子弹半宽」，否则出膛那一帧子弹压在自己的受击盒里。")]
        [SerializeField]
        private Vector2 bulletMuzzle = new(1.6f, 0f);

        private readonly EnemySight _sight = new(true);

        private EnemyLogic _logic;
        private BulletLauncher _bullets;

        private EnemyArea _patrolArea;

        private EnemyTint _tint;
        private HurtFlash _hurtFlash;
        private bool _tintBlack;

        private void Awake()
        {
            _tint = new EnemyTint(transform);
            EnemyColliderSetup.Apply(transform);

            DetectZoneBinder.Bind(transform, EnemyColliderSetup.DetectChildName, nameof(EnemyController), _sight);
        }

        protected override EnemySpawnState CaptureSpawn() => new(
            EnemyKind.Ground,
            transform.position,
            moveMotor.FaceSign,
            blackForm,
            config.maxHealth);

        protected override bool BodyAlive => _logic != null && _logic.IsAlive;

        public bool AIEnabled => OpenAI;

        public bool HasTarget => _sight.HasTarget;

        public EnemyStateTag CurrentTag => _logic != null ? _logic.CurrentTag : EnemyStateTag.None;

        protected override void Assemble()
        {
            _patrolArea = EnemyArea.Snapshot(transform.position, patrolOffset, patrolSize);

            Require.Assigned(moveMotor, nameof(EnemyController), nameof(moveMotor));
            Require.Assigned(config, nameof(EnemyController), nameof(config));

            moveMotor.BindConfig(config);

            _hurtFlash = new(Root.Effects, transform, config.flashTime);
            _logic = new EnemyLogic(moveMotor, config, _hurtFlash, OpenAI, bulletMuzzle, blackForm);

            HitBoxBinder.Bind(transform, _logic, _logic);

            _tintBlack = _logic.Black;
            _tint.Apply(_tintBlack);
        }

        protected override void OnTick(float deltaTime)
        {
            EnsureLauncher();

            // 世界信息由 EnemyMotor 探测，本层只组装 Context
            WorldInfo worldInfo = moveMotor.SampleWorld();
            LogicContext context = new(default, worldInfo, new(Time.fixedTime, Time.fixedDeltaTime));

            _logic.FixedTick(context, _sight.Sense(), SamplePatrolArea());

            TickTint();
        }

        protected override void ResetBody()
        {
            _logic?.Dispose();
            _logic = new EnemyLogic(moveMotor, config, _hurtFlash, OpenAI, bulletMuzzle, SpawnState.InitialBlack);
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

        /// <summary>逻辑只认左右边界：快照里的高度留给编辑器画框，纵向不参与判定</summary>
        private EnemyArea SamplePatrolArea() => _patrolArea.HasBounds
            ? EnemyArea.SpanX(_patrolArea.MinX, _patrolArea.MaxX)
            : default;

#if UNITY_EDITOR
        private void Reset()
        {
            patrolOffset = Vector2.zero;
            patrolSize = new Vector2(18f, 3f);
        }

        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && Root != null) EnemyGizmos.DrawArea(_patrolArea, EnemyGizmos.Patrol);
            else EnemyGizmos.DrawZone(transform, patrolOffset, patrolSize, EnemyGizmos.Patrol);

            EnemyGizmos.DrawSight(transform, EnemyColliderSetup.DetectChildName);

            if (bulletPrefab != null) EnemyGizmos.DrawMuzzle(transform, bulletMuzzle);
        }
#endif
    }
}
