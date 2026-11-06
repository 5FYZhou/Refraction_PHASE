using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>
    /// 地面行走。目标速度恒为 moveSpeed，姿态带来的减速由 MoveLedger 的速度乘数（SetSpeedScaleX）处理，
    /// 本状态不需要知道自己在站着还是蹲着。
    /// </summary>
    public sealed class MoveState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.Move;

        private readonly IStateMove _motor;
        private readonly float _speed;

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
