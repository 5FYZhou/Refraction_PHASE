using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>敌人状态组：三态的切换判定都在这里，状态只描述自己怎么动</summary>
    /// <remarks>攻击态不被抢占：起手后一定走完三段，跑完交给 Fallback</remarks>
    public sealed class EnemyMoveGroup : StateGroup<EnemyStateTag, LogicContext>
    {
        protected override EnemyStateTag EmptyTag => EnemyStateTag.None;

        private readonly EnemyLogic _owner;

        public EnemyMoveGroup(EnemyLogic owner, IMoveMotor motor, EnemyConfig config)
        {
            _owner = owner;

            RegisterState(new EnemyPatrolState(motor, config));
            RegisterState(new EnemyChaseState(owner, motor, config));
            RegisterState(new EnemyAttackState(owner, motor, config));
        }

        public EnemyStateTag Current =>
            machine.CurrentState == null ? EnemyStateTag.None : machine.CurrentState.Tag;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        protected override EnemyStateTag Fallback(in LogicContext ctx)
        {
            if (Current != EnemyStateTag.Attack) return EnemyStateTag.Patrol;

            return _owner.Alert ? EnemyStateTag.Chase : EnemyStateTag.Patrol;
        }

        /// <summary>抢占：看见玩家就离开巡逻；冷却到位且玩家仍在范围内就起手</summary>
        /// <remarks>要 TargetVisible 而不只是 Alert：出了探测范围只剩追赶，不该再起手</remarks>
        protected override bool TryDecidePreempt(in LogicContext ctx, out EnemyStateTag target)
        {
            target = EmptyTag;

            switch (Current)
            {
                case EnemyStateTag.Patrol:
                    if (!_owner.TargetVisible) return false;
                    target = EnemyStateTag.Chase;
                    return true;

                case EnemyStateTag.Chase:
                    if (!_owner.TargetVisible || !_owner.CanAttack(ctx.GameTime.now)) return false;
                    target = EnemyStateTag.Attack;
                    return true;
            }

            return false;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, EnemyStateTag target) => true;
    }
}
