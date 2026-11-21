using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Level;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>飞行敌人 E_Second：持有玩家身体盒、巡逻领地与追击框，状态推进交给 E_SecondMoveGroup</summary>
    /// <remarks>领地是编辑器画的追击框，玩家出框即脱战，所以 Alert 不看丢目标计时</remarks>
    public sealed class E_SecondLogic : EnemyLogicBase
    {
        private readonly E_SecondConfig _config;
        private readonly E_SecondMoveGroup _moveGroup;
        private readonly System.Random _rng = new();
        private readonly Vector2 _muzzle;

        private EnemyTargetInfo _target;
        private EnemyArea _patrolArea;
        private EnemyArea _chaseArea;

        public E_SecondLogic(
            IMoveMotor motor,
            E_SecondConfig config,
            IHurtView hurtView,
            in EnemyArea patrolArea,
            in EnemyArea chaseArea,
            bool black = false,
            bool aiEnabled = true,
            Vector2 muzzle = default)
            : base(motor, config, hurtView, aiEnabled, black)
        {
            _config = config;
            _patrolArea = patrolArea;
            _chaseArea = chaseArea;
            _muzzle = muzzle;
            _moveGroup = new E_SecondMoveGroup(this, motor, config);
        }

        public EnemyStateTag CurrentTag => _moveGroup.Current;

        /// <summary>只有白形态开火；黑形态前摇接的是近身突进，不该起鼓</summary>
        public override bool ChargingShot =>
            !Black && Launcher != null && _config.bulletSpeed > 0f &&
            CurrentTag == EnemyStateTag.Attack && _moveGroup.Phase == E_SecondAttackPhase.Windup;

        public override float ChargingTime => _config.whiteWindupTime;

        public EnemyVisual Visual
        {
            get
            {
                switch (CurrentTag)
                {
                    case EnemyStateTag.Chase: return EnemyVisual.Chase;
                    case EnemyStateTag.Return: return EnemyVisual.Return;
                    case EnemyStateTag.Attack:
                        switch (_moveGroup.Phase)
                        {
                            case E_SecondAttackPhase.Windup: return EnemyVisual.Windup;
                            case E_SecondAttackPhase.Strike: return EnemyVisual.Strike;
                        }

                        return EnemyVisual.Recover;
                }

                return EnemyVisual.Patrol;
            }
        }

        /// <summary>巡逻范围：也是返回态的落点</summary>
        public EnemyArea PatrolArea => _patrolArea;

        /// <summary>追击范围：玩家出框即脱战，随 Recenter 与巡逻框一起平移</summary>
        public EnemyArea ChaseArea => _chaseArea;

        /// <summary>领地：玩家还在追击框里（x、y 都要在框内）；框宽 ≤ 0 时无界</summary>
        public override bool Alert => TargetKnown && _chaseArea.Contains(TargetPos);

        protected override float ContactDamage => _config.contactDamage;

        protected override float ContactInterval => _config.contactInterval;

        protected override float ContactKnockback => _config.contactKnockback;

        /// <summary>表现层唯一入口：写入本帧玩家信息，再走基类帧序</summary>
        public void FixedTick(in LogicContext ctx, in EnemyTargetInfo target)
        {
            _target = target;
            SetSight(target.Known, target.Visible, target.Pos);
            FixedTick(ctx);
        }

        /// <summary>冲刺撞上玩家：身体圆与玩家受击盒相交；不看伤害回报，无敌窗里也算撞到</summary>
        public bool TouchingTarget(Vector2 pos)
        {
            if (!_target.Known) return false;

            float reach = Mathf.Max(0.01f, Motor.BodyHeight * 0.5f);

            return Mathf.Abs(pos.x - _target.BodyCenter.x) <= _target.HalfSize.x + reach
                && Mathf.Abs(pos.y - _target.BodyCenter.y) <= _target.HalfSize.y + reach;
        }

        /// <summary>返回态撞墙：把当前位置当新的巡逻中心，宽度不变；追击框按同一段位移跟着平移</summary>
        public void Recenter(Vector2 center)
        {
            Vector2 delta = center - _patrolArea.Center;

            _patrolArea = new EnemyArea(true, center, _patrolArea.HalfSize);

            if (_chaseArea.HasBounds) _chaseArea = new EnemyArea(true, _chaseArea.Center + delta, _chaseArea.HalfSize);
        }

        /// <summary>后摇上浮的随机距离，落在 [riseMin, riseMax]</summary>
        public float RollRiseDistance()
        {
            float min = Mathf.Min(_config.riseMin, _config.riseMax);
            float max = Mathf.Max(_config.riseMin, _config.riseMax);

            return Mathf.Lerp(min, max, (float)_rng.NextDouble());
        }

        public Vector2 Muzzle(Vector2 pos, int sign) =>
            pos + new Vector2(Mathf.Abs(_muzzle.x) * sign, _muzzle.y);

        public void FireWhite(Vector2 origin, Vector2 direction)
        {
            if (Launcher == null || _config.bulletSpeed <= 0f) return;

            Launcher.Launch(new BulletSpawn(
                origin,
                direction,
                _config.bulletSpeed,
                _config.bulletDamage,
                _config.bulletKnockback));
        }

        protected override void StopOnSpot(in LogicContext ctx) => Motor.SnapVelocity(Vector2.zero);

        protected override void OnAiTick(in LogicContext ctx) => _moveGroup.Tick(in ctx);
    }
}
