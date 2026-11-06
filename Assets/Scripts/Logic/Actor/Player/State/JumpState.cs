using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>上升段：只在起跳那一帧写入竖直速度，之后交给重力接管；vy 转为 ≤ 0 交给 FallState</summary>
    public sealed class JumpState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.Jump;

        private readonly IStateMove _motor;
        private readonly PlayerConfig _config;
        private bool _jumpReleased;

        public JumpState(IStateMove motor, PlayerConfig config)
        {
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx)
        {
            _jumpReleased = false;

            // 上升途中被 Fallback 归回本态时不重复给速度；踩地起跳与土狼跳（已离地但 vy ≤ 0）都必须真起跳
            if (ctx.WorldInfo.FrameStartVelocity.y > 0f) return;

            _motor.SetVelocityY(_config.jumpSpeed);

            _motor.SetGravity(_config.riseGravity);

            // 输入缓冲触发起跳时，可能已经松开跳跃键
            if (!ctx.InputSnapshot.JumpHeld)
                ApplyJumpCut(_config.jumpSpeed);
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
