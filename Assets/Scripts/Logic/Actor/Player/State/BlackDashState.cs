using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>黑突进本体：关重力按方向定速冲，速度与时长各方向一套，时间到进后摇</summary>
    /// <remarks>方向在前摇最后一帧已定死，本体期间读输入不生效；起手给一段无敌，突进盒与弹反盒整段点亮</remarks>
    public sealed class BlackDashState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.BlackDash;

        private readonly PlayerConfig _config;
        private readonly IStateMove _motor;
        private readonly IBlackAttackView _view;
        private readonly ConditionGroup _conditions;
        private readonly BlackDashPrepareState _prepare;
        private float _enterTime;

        public BlackDashState(PlayerConfig config, IStateMove motor, IBlackAttackView view, ConditionGroup conditions, BlackDashPrepareState prepare)
        {
            _config = config;
            _motor = motor;
            _view = view;
            _conditions = conditions;
            _prepare = prepare;
        }

        public BlackAttackKind Kind { get; private set; }

        public override void Enter(in LogicContext ctx)
        {
            _enterTime = ctx.GameTime.now;

            // 前摇被跳过时没有锁定的方向，退回按当帧输入取
            bool locked = _prepare != null && _prepare.Direction != Vector2.zero;

            Kind = locked ? _prepare.Kind : AttackAim.Kind(ctx.InputSnapshot.Move.y);

            Vector2 dir = locked ? _prepare.Direction : AttackAim.Direction(ctx.WorldInfo.Face, ctx.InputSnapshot.Move.y);
            float speed = SpeedOf(Kind);

            _motor.SetGravityEnable(false);
            _motor.SetVelocityX(dir.x * speed);
            _motor.SetVelocityY(dir.y * speed);

            _view?.BeginDash(Kind);
            _conditions?.BeginDashInvulnerable(_config.blackDashInvulnerableTime);
        }

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= TimeOf(Kind);

        public override void Exit()
        {
            _motor?.SetGravityEnable(true);
            _view?.End();
        }

        private float SpeedOf(BlackAttackKind kind)
        {
            switch (kind)
            {
                case BlackAttackKind.Up: return _config.blackDashSpeedUp;
                case BlackAttackKind.Down: return _config.blackDashSpeedDown;
                default: return _config.blackDashSpeedAhead;
            }
        }

        private float TimeOf(BlackAttackKind kind)
        {
            switch (kind)
            {
                case BlackAttackKind.Up: return _config.blackDashTimeUp;
                case BlackAttackKind.Down: return _config.blackDashTimeDown;
                default: return _config.blackDashTimeAhead;
            }
        }
    }
}
