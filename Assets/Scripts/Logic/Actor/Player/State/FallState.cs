using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>
    /// 下落段：竖直速度已 ≤ 0，只做空中横移；重新踩地即结束，由状态组切回 Idle / Move。
    /// </summary>
    public sealed class FallState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.Fall;

        private readonly IStateMove _motor;
        private readonly float _moveSpeed;

        public FallState(IStateMove motor, float moveSpeed)
        {
            _motor = motor;
            _moveSpeed = moveSpeed;
        }

        public override void Enter(in LogicContext ctx)
        {
        }

        public override void Tick(in LogicContext ctx)
        {
            Vector2 dir = ctx.InputSnapshot.Move;

            _motor.SetVelocityX(ctx.InputSnapshot.Move.x * _moveSpeed);
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return ctx.WorldInfo.Grounded;
        }

        public override void Exit()
        {
        }
    }
}
