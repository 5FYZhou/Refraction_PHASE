using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击空闲态：IsDone 恒 false，只能由 AttackGroup 的抢占切走</summary>
    public sealed class NoneAttackState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.None;

        public override void Enter(in LogicContext ctx) { }
        public override void Tick(in LogicContext ctx) { }
        public override bool IsDone(in LogicContext ctx) => false;
        public override void Exit() { }
    }
}
