using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>毛毛虫状态组：巡逻 / 射击；黑世界与看不见玩家都不起手</summary>
    /// <remarks>没有追击态，攻击走完一律回巡逻；要不要接着打由 E_FirstAttackState 内部决定</remarks>
    public sealed class E_FirstMoveGroup : StateGroup<E_FirstStateTag, LogicContext>
    {
        protected override E_FirstStateTag EmptyTag => E_FirstStateTag.None;

        private readonly E_FirstLogic _owner;
        private readonly E_FirstAttackState _attack;

        public E_FirstMoveGroup(E_FirstLogic owner, IMoveMotor motor, E_FirstConfig config)
        {
            _owner = owner;

            RegisterState(new E_FirstPatrolState(owner, motor, config));

            _attack = new E_FirstAttackState(owner, motor, config);
            RegisterState(_attack);
        }

        public E_FirstStateTag Current =>
            machine.CurrentState == null ? E_FirstStateTag.None : machine.CurrentState.Tag;

        public E_FirstVisual Visual
        {
            get
            {
                switch (Current)
                {
                    case E_FirstStateTag.Patrol: return E_FirstVisual.Patrol;
                    case E_FirstStateTag.Attack: return AttackVisual;
                }

                return E_FirstVisual.Patrol;
            }
        }

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        protected override E_FirstStateTag Fallback(in LogicContext ctx) => E_FirstStateTag.Patrol;

        protected override bool TryDecidePreempt(in LogicContext ctx, out E_FirstStateTag target)
        {
            target = EmptyTag;

            // 攻击不被抢占：起手后一定走完三段
            if (Current == E_FirstStateTag.Attack) return false;
            if (!_owner.Hostile || !_owner.Visible) return false;

            target = E_FirstStateTag.Attack;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, E_FirstStateTag target) => true;

        private E_FirstVisual AttackVisual
        {
            get
            {
                switch (_attack.Phase)
                {
                    case E_FirstAttackPhase.Windup: return E_FirstVisual.Windup;
                    case E_FirstAttackPhase.Strike: return E_FirstVisual.Strike;
                }

                return E_FirstVisual.Recover;
            }
        }
    }
}
