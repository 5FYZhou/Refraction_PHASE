using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>黑 · 俯冲：悬停蓄力 → 沿锁定的落点直线俯冲 → 原地后摇</summary>
    /// <remarks>俯冲距离在锁定那一帧定死；撞墙也收手，免得顶在墙上一直推</remarks>
    public sealed class MmflyDiveState : BaseState<MmflyStateTag, LogicContext>
    {
        private const float MinDiveDistance = 0.5f;

        private readonly MmflyLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly MmflyConfig _config;

        private MmflySkillPhase _phase;
        private float _phaseStart;
        private Vector2 _lockedDir = Vector2.right;
        private float _diveDistance;
        private float _travelled;
        private bool _done;

        public MmflyDiveState(MmflyLogic owner, IMoveMotor motor, MmflyConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override MmflyStateTag Tag => MmflyStateTag.Dive;

        public MmflySkillPhase Phase => _phase;

        public Vector2 LockedDir => _lockedDir;

        public override void Enter(in LogicContext ctx)
        {
            _phase = MmflySkillPhase.Windup;
            _phaseStart = ctx.GameTime.now;
            _done = false;

            _motor.SnapVelocity(Vector2.zero);

            _owner.BeginDive(ctx.GameTime.now);
        }

        public override void Tick(in LogicContext ctx)
        {
            float elapsed = ctx.GameTime.now - _phaseStart;

            switch (_phase)
            {
                case MmflySkillPhase.Windup:
                    TickWindup(in ctx, elapsed);
                    break;

                case MmflySkillPhase.Strike:
                    TickStrike(in ctx);
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

            if (elapsed < _config.diveWindupTime) return;

            Vector2 from = ctx.WorldInfo.Pos;

            _lockedDir = _owner.LockedDirTo(from, ctx.WorldInfo.Face);
            _diveDistance = Mathf.Max((_owner.TargetPos - from).magnitude, MinDiveDistance);
            _travelled = 0f;

            _phase = MmflySkillPhase.Strike;
            _phaseStart = ctx.GameTime.now;

            _motor.SnapVelocity(_lockedDir * _config.diveSpeed);
        }

        private void TickStrike(in LogicContext ctx)
        {
            _motor.SnapVelocity(_lockedDir * _config.diveSpeed);

            _travelled += _config.diveSpeed * ctx.GameTime.deltaTime;

            if (_travelled >= _diveDistance || ctx.WorldInfo.TouchingWall) BeginRecover(in ctx);
        }

        /// <summary>后摇：不移动也不锁玩家，纯挨打窗口</summary>
        private void BeginRecover(in LogicContext ctx)
        {
            _phase = MmflySkillPhase.Recover;
            _phaseStart = ctx.GameTime.now;

            _motor.SnapVelocity(Vector2.zero);
        }

        private void TickRecover(in LogicContext ctx, float elapsed)
        {
            _motor.SnapVelocity(Vector2.zero);

            if (elapsed < _config.diveRecoverTime) return;

            _owner.SetPending(MmflyStateTag.Decide);
            _done = true;
        }
    }
}
