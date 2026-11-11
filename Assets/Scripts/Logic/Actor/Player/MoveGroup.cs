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
        private IPostureGate _posture;
        private DashState _dashState;
        private Cooldown _dashCooldown = new(); 
        private float _lastGroundedTime = float.NegativeInfinity;
        private bool _wasGrounded;
        private bool _coyoteConsumed;
        private int _airJumpsUsed;
        private int _airDashesUsed;

        public MoveGroup(IMoveMotor motor, PlayerConfig config, InputBuffer buffer, IPostureGate posture) 
        {
            _motor = motor;
            _config = config;
            _buffer = buffer;
            _posture = posture;
            Init();
        }

        public void Init()
        {
            RegisterState(new IdleState(_motor));
            RegisterState(new MoveState(_motor, _config.moveSpeed));
            RegisterState(new JumpState(_motor, _config));
            RegisterState(new AirJumpState(_motor, _config));
            RegisterState(new FallState(_motor, _config.moveSpeed));
            _dashState = new(_motor, _config.dashSpeed, _config.dashDuration);
            RegisterState(_dashState);
        }

        /// <summary>当前移动状态，门按它决定过门后的运动</summary>
        public MovementStateTag CurrentTag =>
            machine.CurrentState == null ? MovementStateTag.None : machine.CurrentState.Tag;
         
        protected override MovementStateTag Fallback(in LogicContext ctx)
        {
            // 离地按竖直速度分上升/下落（> 0 上升，≤ 0 下落）
            if (!ctx.WorldInfo.Grounded)
            {
                return ctx.WorldInfo.FrameStartVelocity.y > 0f ? MovementStateTag.Jump : MovementStateTag.Fall;
            }

            if (ctx.InputSnapshot.Move.sqrMagnitude > 0) return MovementStateTag.Move;
            return MovementStateTag.Idle;
        }

        public void Tick(in LogicContext ctx)
        {
            bool grounded = ctx.WorldInfo.Grounded;

            if (grounded)
            {
                _lastGroundedTime = ctx.GameTime.now;

                // 只在落地那一帧恢复土狼机会与空中次数
                if (!_wasGrounded)
                {
                    _coyoteConsumed = false;
                    _airJumpsUsed = 0;
                    _airDashesUsed = 0;
                }
            }

            _wasGrounded = grounded;
            TickStates(ctx);
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out MovementStateTag target)
        {
            target = EmptyTag;

            if (CanDash(ctx))
            {
                target = MovementStateTag.Dash;
                return true;
            }

            target = DecideJump(ctx);
            return target != EmptyTag;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, MovementStateTag target)
        {
            switch (target)
            {
                case MovementStateTag.Jump:
                    // 消费掉这次按下，避免同一次按键反复起跳
                    return TryConsumeJump(ctx.GameTime.now);
                case MovementStateTag.AirJump:
                    // 同样要消费输入，成功才扣一次机会
                    if (!TryConsumeJump(ctx.GameTime.now)) return false;
                    _airJumpsUsed++;
                    return true;
                case MovementStateTag.Dash:
                    if(!TryConsumeDash(ctx)) return false;
                    _dashCooldown.MarkUsed(ctx.GameTime.now, _config.dashCooldown);
                    _dashState.Configure(Direction(ctx));
                    return true;
            }
            return false;
        }

        private MovementStateTag DecideJump(in LogicContext ctx)
        {
            float now = ctx.GameTime.now;

            // 冲刺期间不接受起跳
            if (machine.CurrentState?.Tag == MovementStateTag.Dash) return EmptyTag;

            // 下蹲期间一键挡掉三种跳；这里不消费输入，起身后缓冲窗口内的跳仍照常生效
            if (!_posture.CanJump) return EmptyTag;

            if (!_buffer.CanConsume(InputType.Jump, now, _config.inputBufferTime)) return EmptyTag;

            if (ctx.WorldInfo.Grounded) return MovementStateTag.Jump;

            // 土狼时间：刚离地仍算一次地面跳
            if (!_coyoteConsumed && now - _lastGroundedTime <= _config.coyoteTime)
                return MovementStateTag.Jump;

            // 土狼用掉或过期后，按剩余次数空中跳
            if (_airJumpsUsed < _config.maxAirJumps) return MovementStateTag.AirJump;

            return EmptyTag;
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

        /// <summary>
        /// 冲刺是否可用：冷却已过 + 输入还在缓冲窗口 +（踩地，或空中冲刺次数没用完）。
        /// </summary>
        private bool CanDash(in LogicContext ctx)
        {
            float now = ctx.GameTime.now;

            // 下蹲期间不给冲（含空中冲），同样不消费输入
            if (!_posture.CanDash) return false;

            if (!_dashCooldown.CanUse(now)) return false;

            bool hasInput = _buffer.CanConsume(InputType.Dash, now, _config.dashBufferTime);

            bool canAirDash = _airDashesUsed < _config.maxAirDashes;

            return hasInput && (ctx.WorldInfo.Grounded || canAirDash);
        }

        private bool TryConsumeDash(in LogicContext ctx)
        {
            if (!CanDash(ctx)) return false;
            if (!_buffer.TryConsume(InputType.Dash, ctx.GameTime.now, _config.dashBufferTime)) return false;

            // 只有真的在空中冲出去才扣次数
            if (!ctx.WorldInfo.Grounded) _airDashesUsed++;

            return true;
        }
    }
}
