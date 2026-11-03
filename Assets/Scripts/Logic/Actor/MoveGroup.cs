using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Input;
using Refraction.Logic.Move;
using static UnityEngine.Rendering.DebugUI.Table;

namespace Refraction.Logic.Actor
{
    public class MoveGroup : StateGroup<MovementStateTag, LogicContext>
    {
        protected override MovementStateTag EmptyTag => MovementStateTag.None;

        private IMoveMotor _motor;
        private PlayerConfig _config;
        private InputBuffer _buffer;
        private DashState _dashState;
        private Cooldown _dashCooldown = new();

        public MoveGroup(IMoveMotor motor, PlayerConfig config, InputBuffer buffer) 
        {
            _motor = motor;
            _config = config;
            _buffer = buffer;
            Init();
        }

        public void Init()
        {
            RegisterState(new IdleState(_motor));
            RegisterState(new MoveState(_motor, _config.speed));
            _dashState = new(_motor, _config.dashSpeed, _config.dashDuration);
            RegisterState(_dashState);
        }
         
        protected override MovementStateTag Fallback(in LogicContext ctx)
        {
            // 如果有速度 回到Move
            if (ctx.InputSnapshot.Move.sqrMagnitude > 0) return MovementStateTag.Move;
            // Fall?
            return MovementStateTag.Idle;
        }

        public void Tick(in LogicContext ctx)
        {
            TickStates(ctx);
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out MovementStateTag target)
        {
            target = EmptyTag;
            // 如果冲刺没在冷却 冲刺能抢占
            if (CanDash(ctx))
            {
                target = MovementStateTag.Dash;
                return true;
            }
            return false;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, MovementStateTag target)
        {
            switch (target)
            {
                case MovementStateTag.Dash:
                    if(!TryConsumeDash(ctx.GameTime.now)) return false;
                    _dashCooldown.MarkUsed(ctx.GameTime.now, _config.dashCooldown);
                    _dashState.Configure(_motor.Face);
                    return true;
            }
            return false;
        }

        private bool CanDash(in LogicContext ctx)
        {
            return _dashCooldown.CanUse(ctx.GameTime.now) 
                && _buffer.CanConsume(InputType.Dash, ctx.GameTime.now, _config.dashBufferTime);
        }

        private bool TryConsumeDash(float now)
        {
            return _dashCooldown.CanUse(now)
                && _buffer.TryConsume(InputType.Dash, now, _config.dashBufferTime);
        }
    }
}
