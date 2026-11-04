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

        public override void Enter(in LogicContext ctx)
        {
        }

        public override void Tick(in LogicContext ctx)
        {
            _motor.MoveTowardsX(ctx.InputSnapshot.Move, _speed);
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return !ctx.WorldInfo.Grounded || ctx.InputSnapshot.Move.x == 0f;
        }

        public override void Exit()
        {
        }
    }
}
