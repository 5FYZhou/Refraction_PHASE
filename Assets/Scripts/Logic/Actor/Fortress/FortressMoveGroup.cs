using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>Fortress 状态组：巡逻 / 追击 / 弹幕；白形态在弹幕距离内且冷却好了就起手</summary>
    public sealed class FortressMoveGroup : StateGroup<EnemyStateTag, LogicContext>
    {
        protected override EnemyStateTag EmptyTag => EnemyStateTag.None;

        private readonly FortressLogic _owner;
        private readonly FortressBarrageState _attack;

        public FortressMoveGroup(FortressLogic owner, IMoveMotor motor, FortressConfig config)
        {
            _owner = owner;

            RegisterState(new FortressPatrolState(owner, motor, config));
            RegisterState(new FortressChaseState(owner, motor, config));

            _attack = new FortressBarrageState(owner, motor, config);
            RegisterState(_attack);
        }

        public EnemyStateTag Current =>
            machine.CurrentState == null ? EnemyStateTag.None : machine.CurrentState.Tag;

        public FortressAttackPhase Phase => _attack.Phase;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        /// <summary>弹幕走完就接着追（还看得见）或回巡逻</summary>
        protected override EnemyStateTag Fallback(in LogicContext ctx) =>
            _owner.Alert && _owner.ChaseAllowed ? EnemyStateTag.Chase : EnemyStateTag.Patrol;

        protected override bool TryDecidePreempt(in LogicContext ctx, out EnemyStateTag target)
        {
            target = EmptyTag;

            // 攻击不被抢占：起手后一定走完前摇 + 多波散射 + 后摇
            if (Current == EnemyStateTag.Attack) return false;

            if (_owner.Alert && _owner.CanBarrage(ctx.GameTime.now) && _owner.InBarrageRange(ctx.WorldInfo.Pos))
            {
                target = EnemyStateTag.Attack;
                return true;
            }

            if (Current != EnemyStateTag.Patrol || !_owner.Visible || !_owner.ChaseAllowed) return false;

            target = EnemyStateTag.Chase;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, EnemyStateTag target) => true;
    }
}
