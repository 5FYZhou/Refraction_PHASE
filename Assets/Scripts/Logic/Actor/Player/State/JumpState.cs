using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>
    /// 上升段：只在起跳那一帧写入竖直速度，之后交给重力接管；空中允许横移。
    /// 竖直速度转为 ≤ 0 时结束，由状态组切到 <see cref="FallState"/>。
    /// </summary>
    public sealed class JumpState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.Jump;

        private readonly IStateMove _motor;
        private readonly float _jumpSpeed;
        private readonly float _moveSpeed;

        public JumpState(IStateMove motor, float jumpSpeed, float moveSpeed)
        {
            _motor = motor;
            _jumpSpeed = jumpSpeed;
            _moveSpeed = moveSpeed;
        }

        public override void Enter(in LogicContext ctx)
        {
            // 只有踩地起跳才给速度；离地后被归到本态（例如上升途中）不重复起跳
            if (!ctx.WorldInfo.Grounded) return;

            _motor.SetVelocityY(_jumpSpeed);
        }

        public override void Tick(in LogicContext ctx)
        {
            Vector2 dir = ctx.InputSnapshot.Move;
            if (dir.sqrMagnitude <= 0f)
            {
                _motor.SetVelocityX(0f);
                return;
            }

            _motor.SetVelocityX(ctx.InputSnapshot.Move.x * _moveSpeed);
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return ctx.WorldInfo.Velocity.y <= 0f;
        }

        public override void Exit()
        {
        }
    }
}
