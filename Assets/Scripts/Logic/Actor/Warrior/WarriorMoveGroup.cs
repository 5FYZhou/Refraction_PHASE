using Refraction.Data;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>Warrior 状态组：巡逻 / 扑击；进扑击圈且冷却好了才起手，攻击不被抢占</summary>
    public sealed class WarriorMoveGroup : StateGroup<EnemyStateTag, LogicContext>
    {
        protected override EnemyStateTag EmptyTag => EnemyStateTag.None;

        private readonly WarriorLogic _owner;
        private readonly WarriorAttackState _attack;

        public WarriorMoveGroup(WarriorLogic owner, IMoveMotor motor, WarriorConfig config)
        {
            _owner = owner;

            RegisterState(new WarriorPatrolState(owner, motor, config));

            _attack = new WarriorAttackState(owner, motor, config);
            RegisterState(_attack);
        }

        public EnemyStateTag Current =>
            machine.CurrentState == null ? EnemyStateTag.None : machine.CurrentState.Tag;

        public WarriorAttackPhase Phase => _attack.Phase;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        protected override EnemyStateTag Fallback(in LogicContext ctx) => EnemyStateTag.Patrol;

        protected override bool TryDecidePreempt(in LogicContext ctx, out EnemyStateTag target)
        {
            target = EmptyTag;

            // 攻击不被抢占：起手后一定走完自己那套阶段
            if (Current == EnemyStateTag.Attack) return false;
            if (!_owner.Visible) return false;
            if (!_owner.CanPounce(ctx.GameTime.now)) return false;
            if (Vector2.Distance(ctx.WorldInfo.Pos, _owner.TargetPos) > _owner.PounceRange) return false;

            target = EnemyStateTag.Attack;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, EnemyStateTag target) => true;
    }
}
