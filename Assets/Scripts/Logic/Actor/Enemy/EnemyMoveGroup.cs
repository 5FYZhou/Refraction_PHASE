using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>敌人状态组：两态的切换判定都在这里，状态只描述自己怎么动</summary>
    public sealed class EnemyMoveGroup : StateGroup<EnemyStateTag, LogicContext>
    {
        protected override EnemyStateTag EmptyTag => EnemyStateTag.None;

        private readonly EnemyLogic _owner;

        public EnemyMoveGroup(EnemyLogic owner, IMoveMotor motor, EnemyConfig config)
        {
            _owner = owner;

            RegisterState(new EnemyPatrolState(owner, motor, config));
            RegisterState(new EnemyChaseState(owner, motor, config));
        }

        public EnemyStateTag Current =>
            machine.CurrentState == null ? EnemyStateTag.None : machine.CurrentState.Tag;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        protected override EnemyStateTag Fallback(in LogicContext ctx) => EnemyStateTag.Patrol;

        /// <summary>抢占：看见玩家就离开巡逻；丢目标后靠 Chase 的 IsDone 回巡逻</summary>
        protected override bool TryDecidePreempt(in LogicContext ctx, out EnemyStateTag target)
        {
            target = EmptyTag;

            if (Current != EnemyStateTag.Patrol || !_owner.TargetVisible) return false;

            target = EnemyStateTag.Chase;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, EnemyStateTag target) => true;
    }
}
