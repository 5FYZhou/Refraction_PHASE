using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>飞行敌人状态组：巡逻 / 追击 / 返回 / 攻击 的切换判定都在这里</summary>
    /// <remarks>攻击不被抢占，起手后一定走完三段；要不要接着打由 E_SecondAttackState 内部决定</remarks>
    public sealed class E_SecondMoveGroup : StateGroup<EnemyStateTag, LogicContext>
    {
        protected override EnemyStateTag EmptyTag => EnemyStateTag.None;

        private readonly E_SecondLogic _owner;
        private readonly E_SecondAttackState _attack;

        public E_SecondMoveGroup(E_SecondLogic owner, IMoveMotor motor, E_SecondConfig config)
        {
            _owner = owner;

            RegisterState(new E_SecondPatrolState(owner, motor, config));
            RegisterState(new E_SecondChaseState(owner, motor, config));
            RegisterState(new E_SecondReturnState(owner, motor, config));

            _attack = new E_SecondAttackState(owner, motor, config);
            RegisterState(_attack);
        }

        public EnemyStateTag Current =>
            machine.CurrentState == null ? EnemyStateTag.None : machine.CurrentState.Tag;

        /// <summary>攻击内部走到哪一段：状态组只管动作进度，不掺表现</summary>
        public E_SecondAttackPhase Phase => _attack.Phase;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        /// <summary>脱战后：玩家还在追击框里就接着追；否则身体中心还在巡逻框里就回巡逻，出去了就飞回去</summary>
        protected override EnemyStateTag Fallback(in LogicContext ctx)
        {
            if (_owner.Alert) return EnemyStateTag.Chase;

            return _owner.PatrolArea.ContainsX(ctx.WorldInfo.Pos.x) ? EnemyStateTag.Patrol : EnemyStateTag.Return;
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out EnemyStateTag target)
        {
            target = EmptyTag;

            if (Current == EnemyStateTag.Attack) return false;
            if (!_owner.Visible) return false;

            target = Current == EnemyStateTag.Chase ? EnemyStateTag.Attack : EnemyStateTag.Chase;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, EnemyStateTag target) => true;
    }
}
