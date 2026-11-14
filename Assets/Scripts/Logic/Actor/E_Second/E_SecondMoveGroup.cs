using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>飞行敌人状态组：巡逻 / 追击 / 返回 / 攻击 的切换判定都在这里</summary>
    /// <remarks>攻击不被抢占，起手后一定走完三段；要不要接着打由 E_SecondAttackState 内部决定</remarks>
    public sealed class E_SecondMoveGroup : StateGroup<E_SecondStateTag, LogicContext>
    {
        protected override E_SecondStateTag EmptyTag => E_SecondStateTag.None;

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

        public E_SecondStateTag Current =>
            machine.CurrentState == null ? E_SecondStateTag.None : machine.CurrentState.Tag;

        public E_SecondVisual Visual
        {
            get
            {
                switch (Current)
                {
                    case E_SecondStateTag.Patrol: return E_SecondVisual.Patrol;
                    case E_SecondStateTag.Chase: return E_SecondVisual.Chase;
                    case E_SecondStateTag.Return: return E_SecondVisual.Return;
                    case E_SecondStateTag.Attack: return AttackVisual;
                }

                return E_SecondVisual.Patrol;
            }
        }

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        /// <summary>脱战后：玩家还在追击框里就接着追；否则身体中心还在巡逻框里就回巡逻，出去了就飞回去</summary>
        protected override E_SecondStateTag Fallback(in LogicContext ctx)
        {
            if (_owner.Alert) return E_SecondStateTag.Chase;

            return _owner.PatrolArea.Contains(ctx.WorldInfo.Pos.x) ? E_SecondStateTag.Patrol : E_SecondStateTag.Return;
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out E_SecondStateTag target)
        {
            target = EmptyTag;

            if (Current == E_SecondStateTag.Attack) return false;
            if (!_owner.Visible) return false;

            target = Current == E_SecondStateTag.Chase ? E_SecondStateTag.Attack : E_SecondStateTag.Chase;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, E_SecondStateTag target) => true;

        private E_SecondVisual AttackVisual
        {
            get
            {
                switch (_attack.Phase)
                {
                    case E_SecondAttackPhase.Windup: return E_SecondVisual.Windup;
                    case E_SecondAttackPhase.Strike: return E_SecondVisual.Strike;
                }

                return E_SecondVisual.Recover;
            }
        }
    }
}
