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
            _motor.StopMove();
        }

        public override void Tick(in LogicContext ctx)
        {
        }

        public override bool IsDone(in LogicContext ctx)
        {
            // 离地就不再是站立态，交给 Jump / Fall
            if (!ctx.WorldInfo.Grounded) return true;

            return ctx.InputSnapshot.Move.sqrMagnitude != 0f;
        }

        public override void Exit()
        {
        }
    }
}
