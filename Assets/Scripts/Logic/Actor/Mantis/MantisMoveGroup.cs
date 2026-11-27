using Refraction.Data;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>Mantis 状态组：巡逻 / 追击 / 攻击；视线内进追击，追到停止距离当帧起手</summary>
    public sealed class MantisMoveGroup : StateGroup<EnemyStateTag, LogicContext>
    {
        protected override EnemyStateTag EmptyTag => EnemyStateTag.None;

        private readonly MantisLogic _owner;
        private readonly MantisAttackState _attack;

        public MantisMoveGroup(MantisLogic owner, IMoveMotor motor, MantisConfig config)
        {
            _owner = owner;

            RegisterState(new MantisPatrolState(owner, motor, config));
            RegisterState(new MantisChaseState(owner, motor, config));

            _attack = new MantisAttackState(owner, motor, config);
            RegisterState(_attack);
        }

        public EnemyStateTag Current =>
            machine.CurrentState == null ? EnemyStateTag.None : machine.CurrentState.Tag;

        public MantisAttackPhase Phase => _attack.Phase;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        /// <summary>攻击走完就接着追；丢目标了回巡逻</summary>
        protected override EnemyStateTag Fallback(in LogicContext ctx) =>
            _owner.Alert ? EnemyStateTag.Chase : EnemyStateTag.Patrol;

        protected override bool TryDecidePreempt(in LogicContext ctx, out EnemyStateTag target)
        {
            target = EmptyTag;

            // 攻击不被抢占：起手后一定走完自己那套阶段
            if (Current == EnemyStateTag.Attack) return false;

            // 已经追上了就起手，不再等视线：追击本来就是朝最后已知位置走的
            if (_owner.Alert && _owner.InStopRange(ctx.WorldInfo.Pos))
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
