using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>Spliter 状态组：巡逻 / 追击 / 返回 / 攻击 的切换判定都在这里</summary>
    /// <remarks>攻击不被抢占，起手后一定走完三段；冷却与距离都不满足时退回追击</remarks>
    public sealed class SpliterMoveGroup : StateGroup<EnemyStateTag, LogicContext>
    {
        protected override EnemyStateTag EmptyTag => EnemyStateTag.None;

        private readonly SpliterLogic _owner;
        private readonly SpliterAttackState _attack;

        public SpliterMoveGroup(SpliterLogic owner, IMoveMotor motor, SpliterConfig config)
        {
            _owner = owner;

            RegisterState(new SpliterPatrolState(owner, motor, config));
            RegisterState(new SpliterChaseState(owner, motor, config));
            RegisterState(new SpliterReturnState(owner, motor, config));

            _attack = new SpliterAttackState(owner, motor, config);
            RegisterState(_attack);
        }

        public EnemyStateTag Current =>
            machine.CurrentState == null ? EnemyStateTag.None : machine.CurrentState.Tag;

        /// <summary>攻击内部走到哪一段：状态组只管动作进度，不掺表现</summary>
        public SpliterAttackPhase Phase => _attack.Phase;

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

            // 靠近到触发距离就起手，不再看视线：已经进了领地，遮挡不该让它收手
            if (_owner.Alert && _owner.CanAttack(ctx.GameTime.now) && _owner.InAttackRange(ctx.WorldInfo.Pos))
            {
                target = EnemyStateTag.Attack;
                return true;
            }

            if (!_owner.Visible || Current == EnemyStateTag.Chase) return false;

            target = EnemyStateTag.Chase;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, EnemyStateTag target) => true;
    }
}
