using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    public class NormalState : IState<ConditionStateTag, LogicContext>
    {
        public ConditionStateTag Tag => ConditionStateTag.Normal;

        public void Enter(in LogicContext cxt) { }

        public void Exit() { }

        public bool IsDone(in LogicContext cxt) => false;

        public void Tick(in LogicContext cxt) { }
    }
}
