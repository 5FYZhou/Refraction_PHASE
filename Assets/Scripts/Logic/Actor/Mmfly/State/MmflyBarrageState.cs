using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>白 · 弹幕：原地转身体锁方向 → 多波散射 → 随机方向浮动后摇</summary>
    /// <remarks>波次间隔与浮动时长都是算出来的：第一波在攻击起手当帧打出，后摇时长 = 浮动距离 / 速度</remarks>
    public sealed class MmflyBarrageState : BaseState<MmflyStateTag, LogicContext>
    {
        private readonly MmflyLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly MmflyConfig _config;

        private MmflySkillPhase _phase;
        private float _phaseStart;
        private Vector2 _lockedDir = Vector2.right;
        private Vector2 _floatDir = Vector2.up;
        private float _floatTime;
        private int _wave;
        private float _nextWaveAt;
        private bool _done;

        public MmflyBarrageState(MmflyLogic owner, IMoveMotor motor, MmflyConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override MmflyStateTag Tag => MmflyStateTag.Barrage;

        public MmflySkillPhase Phase => _phase;

        public override void Enter(in LogicContext ctx)
        {
            _phase = MmflySkillPhase.Windup;
            _phaseStart = ctx.GameTime.now;
            _done = false;

            _motor.SnapVelocity(Vector2.zero);

            _owner.BeginBarrage(ctx.GameTime.now);
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
                    TickStrike(in ctx, elapsed);
                    break;

                case MmflySkillPhase.Recover:
                    TickRecover(in ctx, elapsed);
                    break;
            }
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }

        /// <summary>前摇：立定不动，只把水平朝向对准玩家；方向到结束那一帧才锁</summary>
        private void TickWindup(in LogicContext ctx, float elapsed)
        {
            _motor.SnapVelocity(Vector2.zero);

            Vector2 delta = _owner.TargetPos - ctx.WorldInfo.Pos;

            _owner.FaceTarget(delta);

            if (elapsed < _config.barrageWindupTime) return;

            _lockedDir = _owner.LockedDirTo(ctx.WorldInfo.Pos, ctx.WorldInfo.Face);

            _phase = MmflySkillPhase.Strike;
            _phaseStart = ctx.GameTime.now;
            _wave = 0;
            _nextWaveAt = 0f;

            LaunchWave(in ctx, 0f);
        }

        private void TickStrike(in LogicContext ctx, float elapsed)
        {
            _motor.SnapVelocity(Vector2.zero);

            if (_wave >= _config.waveCount)
            {
                BeginRecover(in ctx);
                return;
            }

            if (elapsed < _nextWaveAt) return;

            LaunchWave(in ctx, elapsed);
        }

        private void LaunchWave(in LogicContext ctx, float elapsed)
        {
            int sign = _lockedDir.x >= 0f ? 1 : -1;

            _owner.FireWave(
                _owner.Muzzle(ctx.WorldInfo.Pos, sign),
                _lockedDir,
                _config.bulletsPerWave,
                _config.spreadAngle);

            _wave++;
            _nextWaveAt = elapsed + Mathf.Max(0f, _config.waveInterval);
        }

        /// <summary>后摇：方向随机、距离随机，速度恒定，所以时长是算出来的</summary>
        private void BeginRecover(in LogicContext ctx)
        {
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
