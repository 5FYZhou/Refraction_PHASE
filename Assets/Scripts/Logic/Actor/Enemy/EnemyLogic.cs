using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>地面敌人：白世界看见玩家就追并平射一发，黑世界只巡逻</summary>
    public sealed class EnemyLogic : EnemyLogicBase
    {
        private readonly EnemyConfig _config;
        private readonly EnemyMoveGroup _moveGroup;
        private readonly Cooldown _fireCooldown = new();

        private EnemyPatrolArea _patrolArea;
        private bool _visibleLast;

        public EnemyLogic(IMoveMotor motor, EnemyConfig config, bool aiEnabled, WorldPolarity world = WorldPolarity.White)
            : base(motor, config, aiEnabled, world)
        {
            _config = config;
            _moveGroup = new EnemyMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        /// <summary>黑世界不追：转黑的当帧由 EnemyChaseState.IsDone 结束追击</summary>
        public override bool ChaseAllowed => !Black;

        /// <summary>本帧的巡逻范围：无界表示整平台随便走</summary>
        public EnemyPatrolArea PatrolArea => _patrolArea;

        protected override float ContactDamage => _config.contactDamage;

        protected override float ContactInterval => _config.contactInterval;

        protected override float ContactKnockback => _config.contactKnockback;

        protected override float LoseTargetTime => _config.loseTargetTime;

        /// <summary>表现层唯一入口：写入本帧索敌结果与巡逻范围，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target, in EnemyPatrolArea patrolArea)
        {
            _patrolArea = patrolArea;
            SetSight(target.Known, target.Visible, target.Pos);
            FixedTick(ctx);
        }

        protected override void StopOnSpot(in LogicContext ctx)
        {
            // 刚体没有线性阻尼，站桩时被撞一下会一直滑
            Motor.BrakeTowardsX();
        }

        protected override void OnAiTick(in LogicContext ctx)
        {
            TryFire(in ctx);

            _moveGroup.Tick(in ctx);
        }

        private void TryFire(in LogicContext ctx)
        {
            bool visible = Visible;
            bool entered = visible && !_visibleLast;

            _visibleLast = visible;

            if (!entered || Launcher == null) return;
            if (_config.bulletSpeed <= 0f) return;
            if (!_fireCooldown.CanUse(ctx.GameTime.now)) return;

            Vector2 origin = ctx.WorldInfo.Pos;
            float dx = LastSeenPos.x - origin.x;

            // 平射：方向只取横向
            int sign = Mathf.Abs(dx) > 0.01f
                ? (dx > 0f ? 1 : -1)
                : (ctx.WorldInfo.Face >= 0 ? 1 : -1);

            Vector2 muzzle = origin + new Vector2(sign * Mathf.Abs(_config.bulletMuzzle.x), _config.bulletMuzzle.y);

            Launcher.Launch(new BulletSpawn(
                muzzle,
                new Vector2(sign, 0f),
                _config.bulletSpeed,
                _config.bulletDamage,
                _config.bulletKnockback));

            _fireCooldown.MarkUsed(ctx.GameTime.now, _config.fireInterval);
        }
    }
}
