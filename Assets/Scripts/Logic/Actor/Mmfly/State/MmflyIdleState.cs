using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>未开战：原地悬浮，不做任何决策</summary>
    public sealed class MmflyIdleState : BaseState<MmflyStateTag, LogicContext>
    {
        public override MmflyStateTag Tag => MmflyStateTag.Idle;

        private readonly IMoveMotor _motor;

        public MmflyIdleState(IMoveMotor motor) => _motor = motor;

        public override void Enter(in LogicContext ctx) => Stop();

        public override void Tick(in LogicContext ctx) => Stop();

        public override bool IsDone(in LogicContext ctx) => false;

        public override void Exit() { }

        private void Stop() => _motor.SnapVelocity(Vector2.zero);
    }
}
