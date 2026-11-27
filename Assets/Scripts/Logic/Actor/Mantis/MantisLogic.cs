using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>Mantis：地面敌人，巡逻 / 追击 / 近身攻击；黑形态弹反玩家子弹，黑白两套攻击</summary>
    /// <remarks>巡逻框画出后一直有效，被位移或击退带出框外就永久退回整平台；范围伤害由表现层按请求结算</remarks>
    public sealed class MantisLogic : EnemyLogicBase
    {
        private readonly MantisConfig _config;
        private readonly MantisMoveGroup _moveGroup;
        private readonly Cooldown _reboundCooldown = new();
        private readonly float _stopDistance;
        private readonly Vector2 _muzzle;

        private EnemyArea _activeArea;
        private bool _areaLost;
        private bool _hitPending;

        public MantisLogic(
            IMoveMotor motor,
            MantisConfig config,
            IHurtView hurtView,
            bool aiEnabled,
            float stopDistance,
            Vector2 muzzle = default,
            bool black = false)
            : base(motor, config, hurtView, aiEnabled, black)
        {
            _config = config;
            _stopDistance = stopDistance;
            _muzzle = muzzle;
            _moveGroup = new MantisMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        public MantisAttackPhase Phase => _moveGroup.Phase;

        /// <summary>本帧生效的巡逻范围：无界 = 没画框或已经被甩出去，此时按整平台走</summary>
        public EnemyArea PatrolArea => _activeArea;

        /// <summary>追到多近就停：进了这个距离当帧就该起手</summary>
        public float StopDistance => _stopDistance;

        /// <summary>白形态前摇开火，前摇态本身就是蓄力窗口</summary>
        public override bool ChargingShot =>
            !Black && Launcher != null && _config.bulletSpeed > 0f &&
            CurrentTag == EnemyStateTag.Attack && _moveGroup.Phase == MantisAttackPhase.Windup;

        public override float ChargingTime => _config.whiteWindupTime;

        protected override float ContactDamage => _config.contactDamage;

        protected override float ContactInterval => _config.contactInterval;

        protected override float ContactKnockback => _config.contactKnockback;

        protected override float LoseTargetTime => _config.loseTargetTime;

        /// <summary>表现层唯一入口：写入本帧索敌结果与巡逻范围，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target, in EnemyArea patrolArea)
        {
            _activeArea = ResolveArea(in ctx, in patrolArea);

            SetSight(target.Known, target.Visible, target.Pos);
            FixedTick(ctx);
        }

        /// <summary>够不够近：两个中心的直线距离，黑白共用</summary>
        public bool InStopRange(Vector2 pos) =>
            (TargetPos - pos).sqrMagnitude <= _stopDistance * _stopDistance;

        /// <summary>弹反：只有黑形态能弹，冷却之内只能弹一发；不打断当前攻击</summary>
        public bool CanRebound(float now) => Black && _reboundCooldown.CanUse(now);

        public void MarkRebounded(float now) => _reboundCooldown.MarkUsed(now, _config.reboundCooldown);

        /// <summary>攻击段要结算一次范围伤害：逻辑只举旗，落点与命中由表现层按编辑器画的框算</summary>
        public void RequestHit() => _hitPending = true;

        /// <summary>取一次本帧的伤害请求并清零：一帧内两次结算也只算一次</summary>
        public bool ConsumeHit()
        {
            if (!_hitPending) return false;

            _hitPending = false;

            return true;
        }

        /// <summary>前方范围伤害：方向按面朝侧推，别用零向量让击退失效</summary>
        public Damage ProduceAreaDamage() =>
            new(ContactDamage, DamageSource.Contact, new Vector2(Motor.FaceSign, 0f), ContactKnockback);

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

        /// <summary>被位移或击退带出框外就永久退回整平台：掉头那一下的滑行仍算在框里</summary>
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
    }
}
