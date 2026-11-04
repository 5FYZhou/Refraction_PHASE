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
        private float _lastGroundedTime = float.NegativeInfinity;
        private bool _wasGrounded;
        private bool _coyoteConsumed;

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
            bool grounded = ctx.WorldInfo.Grounded;

            if (grounded)
            {
                _lastGroundedTime = ctx.GameTime.now;

                // 只有重新落地时才恢复土狼机会
                if (!_wasGrounded)
                    _coyoteConsumed = false;
            }

            _wasGrounded = grounded;
            TickStates(ctx);
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out MovementStateTag target)
        {
            target = EmptyTag;
            // 如果冲刺没在冷却 冲刺能抢占
            if (CanDash(ctx.GameTime.now))
            {
                target = MovementStateTag.Dash;
                return true;
            }
            // 踩地 + 跳跃键还在缓冲窗口里 才能起跳（空中不给跳，所以目前只有一段跳）
            if (CanJump(ctx))
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

        private bool CanJump(in LogicContext ctx)
        {
            float now = ctx.GameTime.now;
            bool hasInput = _buffer.CanConsume(InputType.Jump, now, _config.inputBufferTime);

            bool canGroundJump = ctx.WorldInfo.Grounded;
            bool canCoyoteJump = !_coyoteConsumed && now - _lastGroundedTime <= _config.coyoteTime;

            return hasInput && (canGroundJump || canCoyoteJump);
        }

        private bool TryConsumeJump(float now)
        {
            if (!_buffer.TryConsume(InputType.Jump, now, _config.inputBufferTime))
                return false;

            _coyoteConsumed = true;
            return true;
        }

        private Vector2 Direction(in LogicContext ctx)
        {
            int x = ctx.WorldInfo.Face;
            if (ctx.InputSnapshot.Move.x > 0f) x = 1;
            if (ctx.InputSnapshot.Move.x < 0f) x = -1;
            return new Vector2(x, 0);
        }

        private bool CanDash(float now)
        {
            return _dashCooldown.CanUse(now) 
                && _buffer.CanConsume(InputType.Dash, now, _config.dashBufferTime);
        }

        private bool TryConsumeDash(float now)
        {
            return _dashCooldown.CanUse(now)
                && _buffer.TryConsume(InputType.Dash, now, _config.dashBufferTime);
        }
    }
}
