using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>地图态：横向只走 mapMoveSpeed，键松开或离地才退出</summary>
    public sealed class MapState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.Map;

        private readonly IStateMove _motor;
        private readonly float _speed;

        public MapState(IStateMove motor, float speed)
        {
            _motor = motor;
            _speed = speed;
        }

        public override void Enter(in LogicContext ctx) { }

        public override void Tick(in LogicContext ctx)
        {
            _motor.MoveTowardsX(ctx.InputSnapshot.Move, _speed);
        }

        public override bool IsDone(in LogicContext ctx)
            => !ctx.InputSnapshot.MapHeld || !ctx.WorldInfo.Grounded;

        public override void Exit() { }
    }
}
