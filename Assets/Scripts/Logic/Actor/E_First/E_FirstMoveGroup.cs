using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>毛毛虫状态组：巡逻 / 射击；黑世界与看不见玩家都不起手</summary>
    /// <remarks>没有追击态，攻击走完一律回巡逻；要不要接着打由 E_FirstAttackState 内部决定</remarks>
    public sealed class E_FirstMoveGroup : StateGroup<EnemyStateTag, LogicContext>
    {
        protected override EnemyStateTag EmptyTag => EnemyStateTag.None;

        private readonly E_FirstLogic _owner;
        private readonly E_FirstAttackState _attack;

        public E_FirstMoveGroup(E_FirstLogic owner, IMoveMotor motor, E_FirstConfig config)
        {
            _owner = owner;

            RegisterState(new E_FirstPatrolState(owner, motor, config));

            _attack = new E_FirstAttackState(owner, motor, config);
            RegisterState(_attack);
        }

        public EnemyStateTag Current =>
            machine.CurrentState == null ? EnemyStateTag.None : machine.CurrentState.Tag;

        /// <summary>攻击内部走到哪一段：状态组只管动作进度，不掺表现</summary>
        public E_FirstAttackPhase Phase => _attack.Phase;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        protected override EnemyStateTag Fallback(in LogicContext ctx) => EnemyStateTag.Patrol;

        protected override bool TryDecidePreempt(in LogicContext ctx, out EnemyStateTag target)
        {
            target = EmptyTag;

            // 攻击不被抢占：起手后一定走完三段
            if (Current == EnemyStateTag.Attack) return false;
            if (!_owner.Hostile || !_owner.Visible) return false;

            target = EnemyStateTag.Attack;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, EnemyStateTag target) => true;
    }
}
