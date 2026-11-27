using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>Fortress：地面敌人，白形态多波散射弹幕、黑形态只巡逻并能弹反玩家子弹</summary>
    /// <remarks>巡逻与索敌跟 Enemy 同一套（黑不追），弹反与弹幕是本类新增；状态推进交给 FortressMoveGroup</remarks>
    public sealed class FortressLogic : EnemyLogicBase
    {
        private readonly FortressConfig _config;
        private readonly FortressMoveGroup _moveGroup;
        private readonly Cooldown _barrageCooldown = new();
        private readonly Cooldown _reboundCooldown = new();
        private readonly float _stopDistance;
        private readonly float _barrageRange;
        private readonly Vector2 _muzzle;

        private EnemyArea _patrolArea;

        public FortressLogic(
            IMoveMotor motor,
            FortressConfig config,
            IHurtView hurtView,
            bool aiEnabled,
            float stopDistance,
            float barrageRange,
            Vector2 muzzle = default,
            bool black = false)
            : base(motor, config, hurtView, aiEnabled, black)
        {
            _config = config;
            _stopDistance = stopDistance;
            _barrageRange = barrageRange;
            _muzzle = muzzle;
            _moveGroup = new FortressMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        public FortressAttackPhase Phase => _moveGroup.Phase;

        /// <summary>黑世界不追：和 Enemy 一样，黑形态只巡逻，靠弹反自保</summary>
        public override bool ChaseAllowed => !Black;

        /// <summary>本帧的巡逻范围：无界表示整平台随便走</summary>
        public EnemyArea PatrolArea => _patrolArea;

        /// <summary>追到多近就停：由预制体上那个圆给出</summary>
        public float StopDistance => _stopDistance;

        /// <summary>白形态在弹幕前摇蓄力，开火那一帧变回 false</summary>
        public override bool ChargingShot =>
            !Black && Launcher != null && _config.bulletSpeed > 0f &&
            CurrentTag == EnemyStateTag.Attack && _moveGroup.Phase == FortressAttackPhase.Windup;

        public override float ChargingTime => _config.barrageWindupTime;

        protected override float ContactDamage => _config.contactDamage;

        protected override float ContactInterval => _config.contactInterval;

        protected override float ContactKnockback => _config.contactKnockback;

        protected override float LoseTargetTime => _config.loseTargetTime;

        /// <summary>表现层唯一入口：写入本帧索敌结果与巡逻范围，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target, in EnemyArea patrolArea)
        {
            _patrolArea = patrolArea;

            SetSight(target.Known, target.Visible, target.Pos);
            FixedTick(ctx);
        }

        /// <summary>够不够近：两个中心的直线距离</summary>
        public bool InStopRange(Vector2 pos) =>
            (TargetPos - pos).sqrMagnitude <= _stopDistance * _stopDistance;

        /// <summary>够不够近：弹幕的触发距离，由预制体上那个圆给出</summary>
        public bool InBarrageRange(Vector2 pos) =>
            (TargetPos - pos).sqrMagnitude <= _barrageRange * _barrageRange;

        /// <summary>弹幕能不能起手：白形态、冷却就绪，距离由状态组的抢占判</summary>
        public bool CanBarrage(float now) => !Black && _barrageCooldown.CanUse(now);

        public void BeginBarrage(float now) => _barrageCooldown.MarkUsed(now, _config.barrageCooldown);

        /// <summary>弹反：只有黑形态能弹，冷却之内只能弹一发</summary>
        public bool CanRebound(float now) => Black && _reboundCooldown.CanUse(now);

        public void MarkRebounded(float now) => _reboundCooldown.MarkUsed(now, _config.reboundCooldown);

        /// <summary>枪口：x 随朝向镜像，往左射时不能从背后冒出来</summary>
        public Vector2 Muzzle(Vector2 pos, int sign) =>
            pos + new Vector2(Mathf.Abs(_muzzle.x) * sign, _muzzle.y);

        /// <summary>一波散射：以 dir 为中心左右对称张开，spreadDegrees 是相邻两发的夹角</summary>
        public void FireWave(Vector2 origin, Vector2 dir, int count, float spreadDegrees)
        {
            if (Launcher == null || _config.bulletSpeed <= 0f || count <= 0) return;

            float half = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Fire(origin, Rotate(dir, (i - half) * spreadDegrees));
            }
        }

        protected override void StopOnSpot(in LogicContext ctx) => Motor.BrakeTowardsX();

        protected override void OnAiTick(in LogicContext ctx) => _moveGroup.Tick(in ctx);

        private void Fire(Vector2 origin, Vector2 dir)
        {
            Launcher.Launch(new BulletSpawn(
                origin,
                dir,
                _config.bulletSpeed,
                _config.bulletDamage,
                _config.bulletKnockback));
        }

        private static Vector2 Rotate(Vector2 dir, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            return new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
        }
    }
}
