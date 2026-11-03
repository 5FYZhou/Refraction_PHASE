using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    public class IdleState : BaseState<MovementStateTag, InputSnapshot>
    {
        public override MovementStateTag Tag => MovementStateTag.Idle;

        private IStateMove _motor;

        public IdleState(IStateMove motor)
        {
            _motor = motor;
        }

        public override void Enter()
        {
            _motor.StopMove();
        }

        public override void Tick(InputSnapshot ctx)
        {
        }

        public override bool IsDone(InputSnapshot ctx)
        {
            return ctx.Move.sqrMagnitude != 0f;
        }

        public override void Exit()
        {
        }
    }
}
