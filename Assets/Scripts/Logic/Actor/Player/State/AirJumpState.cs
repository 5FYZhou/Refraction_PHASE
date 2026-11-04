using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public sealed class AirJumpState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.AirJump;

        private readonly IStateMove _motor;
        private readonly PlayerConfig _config;
        private bool _jumpReleased;

        public AirJumpState(IStateMove motor, PlayerConfig config)
        {
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx)
        {
            _jumpReleased = false;

            // 每次空中跳都是一次全新的起跳，无论当前是下坠还是还在上升，都直接覆盖竖直速度
            _motor.SetVelocityY(_config.airJumpSpeed);

            _motor.SetGravity(_config.riseGravity);

            // 输入缓冲触发时，按键可能已经松开了
            if (!ctx.InputSnapshot.JumpHeld)
                ApplyJumpCut(_config.airJumpSpeed);
        }

        public override void Tick(in LogicContext ctx)
        {
            _motor.MoveTowardsX(ctx.InputSnapshot.Move, _config.moveSpeed);

            float vy = ctx.WorldInfo.FrameStartVelocity.y;
            if (!ctx.InputSnapshot.JumpHeld)
            {
                ApplyJumpCut(vy);
                return;
            }
            // 顶点悬停
            if (vy > 0f && vy < _config.apexThreshold)
            {
                _motor.SetGravity(_config.apexGravity);
            }
            else
            {
                _motor.SetGravity(_config.riseGravity);
            }
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return ctx.WorldInfo.FrameStartVelocity.y <= 0f;
        }

        public override void Exit()
        {
            _motor.SetGravity(_config.fallGravity);
        }

        private void ApplyJumpCut(float vy)
        {
            if (_jumpReleased) return;
            _jumpReleased = true;

            if (vy > 0f)
            {
                _motor.ScaleVelocityY(_config.jumpCutMultiplier);
                _motor.SetGravity(_config.jumpCutGravity);
            }
        }
    }
}
