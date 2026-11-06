using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>
    /// 站立：姿态的基础态，不施加任何限制。
    /// 不会自行结束——进下蹲由 <see cref="PostureGroup"/> 的抢占判定发起。
    /// </summary>
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
