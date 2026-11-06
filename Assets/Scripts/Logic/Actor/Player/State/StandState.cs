using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>站立：基础态不施加任何限制，也不会自行结束，进下蹲靠 PostureGroup 的抢占判定</summary>
    public sealed class StandState : BaseState<PostureStateTag, LogicContext>
    {
        public override PostureStateTag Tag => PostureStateTag.Stand;

        public override void Enter(in LogicContext ctx)
        {
        }

        public override void Tick(in LogicContext ctx)
        {
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return false;
        }

        public override void Exit()
        {
        }
    }
}
