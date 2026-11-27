using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>白 · 召唤：抖动闪烁预警 → 当帧召出小怪 → 随机方向浮动后摇</summary>
    /// <remarks>召唤是瞬发的，所以没有独立的攻击段时长；名额由 IMinionHost 的存活数决定</remarks>
    public sealed class MmflySummonState : BaseState<MmflyStateTag, LogicContext>
    {
        private readonly MmflyLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly MmflyConfig _config;

        private MmflySkillPhase _phase;
        private float _phaseStart;
        private Vector2 _floatDir = Vector2.up;
        private float _floatTime;
        private bool _done;

        public MmflySummonState(MmflyLogic owner, IMoveMotor motor, MmflyConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override MmflyStateTag Tag => MmflyStateTag.Summon;

        public MmflySkillPhase Phase => _phase;

        public override void Enter(in LogicContext ctx)
        {
            _phase = MmflySkillPhase.Windup;
            _phaseStart = ctx.GameTime.now;
            _done = false;

            _motor.SnapVelocity(Vector2.zero);

            _owner.BeginSummon(ctx.GameTime.now);
        }

        public override void Tick(in LogicContext ctx)
        {
            float elapsed = ctx.GameTime.now - _phaseStart;

            switch (_phase)
            {
                case MmflySkillPhase.Windup:
                    TickWindup(in ctx, elapsed);
                    break;

                case MmflySkillPhase.Recover:
                    TickRecover(in ctx, elapsed);
                    break;
            }
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }

        private void TickWindup(in LogicContext ctx, float elapsed)
        {
            _motor.SnapVelocity(Vector2.zero);

            _owner.FaceTarget(_owner.TargetPos - ctx.WorldInfo.Pos);

            if (elapsed < _config.summonWindupTime) return;

            _owner.SummonOnce();

            _phase = MmflySkillPhase.Recover;
            _phaseStart = ctx.GameTime.now;

            _owner.RollFloat(out _floatDir, out _floatTime);

            _motor.SnapVelocity(_floatDir * _config.floatSpeed);
        }

        private void TickRecover(in LogicContext ctx, float elapsed)
        {
            _motor.SnapVelocity(_floatDir * _config.floatSpeed);

            if (elapsed < _floatTime) return;

            _owner.SetPending(MmflyStateTag.Decide);
            _done = true;
        }
    }
}
