using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    public class MoveState : BaseState<MovementStateTag, InputSnapshot>
    {
        public override MovementStateTag Tag => MovementStateTag.Move;

        private IStateMove motor;

        public MoveState(IStateMove motor) 
        {
            this.motor = motor;
        }

        public override void Enter()
        {
        }

        public override void Tick(InputSnapshot cxt)
        {
            if (motor != null)
            {
                motor.MoveTowards(cxt.Move, motor.Speed);
            }
        }

        public override bool IsDone(InputSnapshot cxt)
        {
            return cxt.Move.magnitude <= 0f;
        }

        public override void Exit()
        {
        }
    }
}
