using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    public sealed class MoveState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.Move;

        private IStateMove _motor;
        private float _speed;

        public MoveState(IStateMove motor, float speed) 
        {
            _motor = motor;
            _speed = speed;
        }

        public override void Enter(in LogicContext cxt)
        {
        }

        public override void Tick(in LogicContext cxt)
        {
            if (_motor != null)
            {
                _motor.MoveTowards(cxt.InputSnapshot.Move, _speed);
            }
        }

        public override bool IsDone(in LogicContext cxt)
        {
            return cxt.InputSnapshot.Move.magnitude <= 0f;
        }

        public override void Exit()
        {
        }
    }
}
