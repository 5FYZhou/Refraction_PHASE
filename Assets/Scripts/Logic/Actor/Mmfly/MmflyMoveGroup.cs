using Refraction.Data;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>Boss mmfly 的状态组：悬浮 / 决策 / 观察 / 逼近 / 三个技能，切态判定都在这里</summary>
    /// <remarks>技能不被抢占，起手后一定走完三段；下一态由状态自己写进 Pending，状态组只负责取走</remarks>
    public sealed class MmflyMoveGroup : StateGroup<MmflyStateTag, LogicContext>
    {
        protected override MmflyStateTag EmptyTag => MmflyStateTag.None;

        private readonly MmflyLogic _owner;
        private readonly MmflyDiveState _dive;

        public MmflyMoveGroup(MmflyLogic owner, IMoveMotor motor, MmflyConfig config)
        {
            _owner = owner;

            RegisterState(new MmflyIdleState(motor));
            RegisterState(new MmflyDecideState(owner, motor));
            RegisterState(new MmflyObserveState(owner, motor));
            RegisterState(new MmflyApproachState(owner, motor));

            _dive = new MmflyDiveState(owner, motor, config);
            RegisterState(_dive);
            RegisterState(new MmflyBarrageState(owner, motor, config));
            RegisterState(new MmflySummonState(owner, motor, config));
        }

        public MmflyStateTag Current =>
            machine.CurrentState == null ? MmflyStateTag.None : machine.CurrentState.Tag;

        /// <summary>技能内部相位：只有三个技能态认这个字段，其余一律 None</summary>
        public MmflySkillPhase Phase => machine.CurrentState switch
        {
            MmflyDiveState dive => dive.Phase,
            MmflyBarrageState barrage => barrage.Phase,
            MmflySummonState summon => summon.Phase,
            _ => MmflySkillPhase.None,
        };

        public Vector2 DiveDir => _dive.LockedDir;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        /// <summary>没进战斗就悬浮；进了战斗取状态自己排的下一态，没排就回决策</summary>
        protected override MmflyStateTag Fallback(in LogicContext ctx)
        {
            if (!_owner.Engaged) return MmflyStateTag.Idle;

            MmflyStateTag pending = _owner.TakePending();

            return pending == MmflyStateTag.None ? MmflyStateTag.Decide : pending;
        }

        /// <summary>开战当帧从悬浮切进决策；技能与观察 / 逼近途中不许插队</summary>
        protected override bool TryDecidePreempt(in LogicContext ctx, out MmflyStateTag target)
        {
            target = EmptyTag;

            if (!_owner.Engaged) return false;
            if (Current != MmflyStateTag.Idle && Current != MmflyStateTag.None) return false;

            target = MmflyStateTag.Decide;

            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, MmflyStateTag target) => true;
    }
}
