using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>毛毛虫 E_First：白世界站定射击，黑世界只巡逻</summary>
    /// <remarks>跑出巡逻框就退回整平台，与地面敌人同一套判法</remarks>
    public sealed class E_FirstLogic : EnemyLogicBase
    {
        private readonly E_FirstConfig _config;
        private readonly E_FirstMoveGroup _moveGroup;

        private EnemyArea _activeArea;

        public E_FirstLogic(IMoveMotor motor, E_FirstConfig config, bool aiEnabled, WorldPolarity world)
            : base(motor, config, aiEnabled, world)
        {
            _config = config;
            _moveGroup = new E_FirstMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        public EnemyVisual Visual => _moveGroup.Visual;

        /// <summary>本帧生效的巡逻范围：无界表示没画框或已经跑出去了</summary>
        public EnemyArea PatrolArea => _activeArea;

        /// <summary>白世界才动手：黑世界对玩家没有反应，一直巡逻</summary>
        public bool Hostile => !Black;

        protected override float ContactDamage => _config.contactDamage;

        protected override float ContactInterval => _config.contactInterval;

        protected override float ContactKnockback => _config.contactKnockback;

        /// <summary>表现层唯一入口：写入本帧索敌结果与巡逻范围，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target, in EnemyArea patrolArea)
        {
            _activeArea = patrolArea.HasBounds && patrolArea.ContainsX(ctx.WorldInfo.Pos.x) ? patrolArea : default;

            SetSight(target.Known, target.Visible, target.Pos);
            FixedTick(ctx);
        }

        /// <summary>枪口：x 随朝向镜像，往左射时不能从背后冒出来</summary>
        public Vector2 Muzzle(Vector2 pos, int sign) =>
            pos + new Vector2(Mathf.Abs(_config.bulletMuzzle.x) * sign, _config.bulletMuzzle.y);

        public void Fire(Vector2 origin, Vector2 direction)
        {
            if (Launcher == null || _config.bulletSpeed <= 0f) return;

            Launcher.Launch(new BulletSpawn(
                origin,
                direction,
                _config.bulletSpeed,
                _config.bulletDamage,
                _config.bulletKnockback));
        }

        protected override void StopOnSpot(in LogicContext ctx) => Motor.BrakeTowardsX();

        protected override void OnAiTick(in LogicContext ctx) => _moveGroup.Tick(in ctx);
    }
}
