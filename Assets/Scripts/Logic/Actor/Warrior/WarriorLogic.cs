using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>Warrior：黑形态扑向玩家并弹反玩家子弹，白形态反身跃起、悬停后射击</summary>
    /// <remarks>巡逻框画出后一直有效，被扑击或击退带出框外就永久退回整平台；弹反只看黑形态与当前阶段</remarks>
    public sealed class WarriorLogic : EnemyLogicBase
    {
        private readonly WarriorConfig _config;
        private readonly WarriorMoveGroup _moveGroup;
        private readonly Cooldown _pounceCooldown = new();
        private readonly Cooldown _reboundCooldown = new();
        private readonly float _pounceRange;
        private readonly Vector2 _muzzle;

        private EnemyArea _activeArea;
        private bool _areaLost;

        public WarriorLogic(
            IMoveMotor motor,
            WarriorConfig config,
            bool aiEnabled,
            float pounceRange,
            Vector2 muzzle = default,
            bool black = false)
            : base(motor, config, aiEnabled, black)
        {
            _config = config;
            _pounceRange = pounceRange;
            _muzzle = muzzle;
            _moveGroup = new WarriorMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        public WarriorAttackPhase Phase => _moveGroup.Phase;

        public EnemyVisual Visual
        {
            get
            {
                if (CurrentTag != EnemyStateTag.Attack) return EnemyVisual.Patrol;

                switch (_moveGroup.Phase)
                {
                    case WarriorAttackPhase.Windup: return EnemyVisual.Windup;
                    case WarriorAttackPhase.Pounce: return EnemyVisual.Strike;
                    case WarriorAttackPhase.Recover: return EnemyVisual.Recover;
                    case WarriorAttackPhase.Leap: return EnemyVisual.Windup;
                }

                return EnemyVisual.Strike;
            }
        }

        /// <summary>本帧生效的巡逻范围：无界 = 没画框或已经被甩出去，此时按整平台走</summary>
        public EnemyArea PatrolArea => _activeArea;

        /// <summary>触发扑击的距离：由预制体上那个圆给出，黑白共用</summary>
        public float PounceRange => _pounceRange;

        public bool CanPounce(float now) => _pounceCooldown.CanUse(now);

        public void MarkPounced(float now) => _pounceCooldown.MarkUsed(now, _config.pounceCooldown);

        /// <summary>弹反：只有黑形态弹，前摇 / 扑击 / 后摇三段里都不弹</summary>
        public bool CanRebound(float now) =>
            Black && CurrentTag != EnemyStateTag.Attack && _reboundCooldown.CanUse(now);

        public void MarkRebounded(float now) => _reboundCooldown.MarkUsed(now, _config.reboundCooldown);

        protected override float ContactDamage => _config.contactDamage;

        protected override float ContactInterval => _config.contactInterval;

        protected override float ContactKnockback => _config.contactKnockback;

        /// <summary>表现层唯一入口：写入本帧索敌结果与巡逻范围，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target, in EnemyArea patrolArea)
        {
            _activeArea = ResolveArea(in ctx, in patrolArea);

            SetSight(target.Known, target.Visible, target.Pos);
            FixedTick(ctx);
        }

        /// <summary>被扑击或击退带出框外就永久退回整平台：掉头那一下的滑行仍算在框里</summary>
        private EnemyArea ResolveArea(in LogicContext ctx, in EnemyArea area)
        {
            if (_areaLost || !area.HasBounds) return default;

            float x = ctx.WorldInfo.Pos.x;
            float slack = AreaSlack(ctx.GameTime.deltaTime);

            if (x >= area.MinX - slack && x <= area.MaxX + slack) return area;

            _areaLost = true;

            return default;
        }

        /// <summary>容差 = 掉头刹停的滑行距离 + 一帧位移：再多就不是巡逻自己走出来的</summary>
        private float AreaSlack(float dt)
        {
            float speed = _config.patrolSpeed;
            float brake = _config.turnAcceleration > 0f
                ? speed * speed / (2f * _config.turnAcceleration)
                : 0f;

            return brake + speed * dt;
        }

        /// <summary>枪口：x 随朝向镜像，往左射时不能从背后冒出来</summary>
        public Vector2 Muzzle(Vector2 pos, int sign) =>
            pos + new Vector2(Mathf.Abs(_muzzle.x) * sign, _muzzle.y);

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
