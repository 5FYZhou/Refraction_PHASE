using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Input;
using Refraction.Logic.Move;
using UnityEngine;

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
            RegisterState(new JumpState(_motor, _config));
            RegisterState(new FallState(_motor, _config.speed));
            _dashState = new(_motor, _config.dashSpeed, _config.dashDuration);
            RegisterState(_dashState);
        }
         
        protected override MovementStateTag Fallback(in LogicContext ctx)
        {
            // 离地：按竖直速度分成上升段和下落段（> 0 上升，≤ 0 下落）
            if (!ctx.WorldInfo.Grounded)
            {
                return ctx.WorldInfo.FrameStartVelocity.y > 0f ? MovementStateTag.Jump : MovementStateTag.Fall;
            }

            // 如果有速度 回到Move
            if (ctx.InputSnapshot.Move.sqrMagnitude > 0) return MovementStateTag.Move;
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
            // 踩地 + 跳跃键还在缓冲窗口里 才能起跳（空中不给跳，所以目前只有一段跳）
            if (ctx.WorldInfo.Grounded && CanJump(ctx))
            {
                target = MovementStateTag.Jump;
                return true;
            }
            return false;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, MovementStateTag target)
        {
            switch (target)
            {
                case MovementStateTag.Jump:
                    // 消费掉这次按下，避免同一次按键反复起跳
                    return TryConsumeJump(ctx.GameTime.now);
                case MovementStateTag.Dash:
                    if(!TryConsumeDash(ctx.GameTime.now)) return false;
                    _dashCooldown.MarkUsed(ctx.GameTime.now, _config.dashCooldown);
                    _dashState.Configure(Direction(ctx));
                    return true;
            }
            return false;
        }

        /// <summary>跳跃键的缓冲窗口沿用 inputBufferTime：落地前一点按下也能补跳</summary>
        private bool CanJump(in LogicContext ctx)
        {
            return _buffer.CanConsume(InputType.Jump, ctx.GameTime.now, _config.inputBufferTime);
        }

        private bool TryConsumeJump(float now)
        {
            return _buffer.TryConsume(InputType.Jump, now, _config.inputBufferTime);
        }

        private Vector2 Direction(in LogicContext ctx)
        {
            int x = ctx.WorldInfo.Face;
            if (ctx.InputSnapshot.Move.x > 0f) x = 1;
            if (ctx.InputSnapshot.Move.x < 0f) x = -1;
            return new Vector2(x, 0);
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
