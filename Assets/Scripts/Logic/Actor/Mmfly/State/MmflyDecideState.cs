using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>决策：原地悬浮面向玩家，等满决策间隔再挑下一个行为，挑完就结束</summary>
    public sealed class MmflyDecideState : BaseState<MmflyStateTag, LogicContext>
    {
        public override MmflyStateTag Tag => MmflyStateTag.Decide;

        private readonly MmflyLogic _owner;
        private readonly IMoveMotor _motor;

        private float _start;
        private bool _done;

        public MmflyDecideState(MmflyLogic owner, IMoveMotor motor)
        {
            _owner = owner;
            _motor = motor;
        }

        public override void Enter(in LogicContext ctx)
        {
            _start = ctx.GameTime.now;
            _done = false;

            _motor.SnapVelocity(Vector2.zero);
        }

        public override void Tick(in LogicContext ctx)
        {
            _motor.SnapVelocity(Vector2.zero);

            _owner.FaceTarget(_owner.TargetPos - ctx.WorldInfo.Pos);

            if (_done) return;
            if (ctx.GameTime.now - _start < _owner.DecideInterval) return;

            _owner.SetPending(_owner.DecideNext(ctx.WorldInfo.Pos, ctx.GameTime.now));
            _done = true;
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }
    }
}
