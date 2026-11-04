using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    public sealed class IdleState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.Idle;

        private IStateMove _motor;

        public IdleState(IStateMove motor)
        {
            _motor = motor;
        }

        public override void Enter(in LogicContext cxt)
        {
        }

        public override void Tick(in LogicContext ctx)
        {
            _motor.BrakeTowardsX();
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return !ctx.WorldInfo.Grounded || ctx.InputSnapshot.Move.x != 0f;
        }

        public override void Exit()
        {
        }
    }
}
