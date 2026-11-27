using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>散射弹幕：原地转身锁方向 → 多波散射 → 原地后摇；三段都不移动</summary>
    /// <remarks>第一波在攻击段起手当帧打出，之后每 waveInterval 一波，最后一波打完就进后摇</remarks>
    public sealed class FortressBarrageState : BaseState<EnemyStateTag, LogicContext>
    {
        private const float FaceDeadZone = 0.05f;
        private const float DirEps = 1e-6f;

        private readonly FortressLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly FortressConfig _config;

        private FortressAttackPhase _phase;
        private float _phaseStart;
        private Vector2 _lockedDir = Vector2.right;
        private int _wave;
        private float _nextWaveAt;
        private bool _done;

        public FortressBarrageState(FortressLogic owner, IMoveMotor motor, FortressConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override EnemyStateTag Tag => EnemyStateTag.Attack;

        public FortressAttackPhase Phase => _phase;

        public override void Enter(in LogicContext ctx)
        {
            _phase = FortressAttackPhase.Windup;
            _phaseStart = ctx.GameTime.now;
            _done = false;

            _motor.BrakeTowardsX();

            _owner.BeginBarrage(ctx.GameTime.now);
        }

        public override void Tick(in LogicContext ctx)
        {
            float elapsed = ctx.GameTime.now - _phaseStart;

            switch (_phase)
            {
                case FortressAttackPhase.Windup:
                    TickWindup(in ctx, elapsed);
                    break;

                case FortressAttackPhase.Strike:
                    TickStrike(in ctx, elapsed);
                    break;

                case FortressAttackPhase.Recover:
                    TickRecover(in ctx, elapsed);
                    break;
            }
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }

        /// <summary>前摇：立定不动，只把水平朝向对准玩家；方向到结束那一帧才锁</summary>
        private void TickWindup(in LogicContext ctx, float elapsed)
        {
            _motor.BrakeTowardsX();

            Vector2 delta = _owner.TargetPos - ctx.WorldInfo.Pos;

            FaceTarget(delta);

            if (elapsed < _config.barrageWindupTime) return;

            float distance = delta.magnitude;

            // 锁的是玩家当前位置的方向（含纵向）：玩家正好重合时退回朝向
            _lockedDir = distance > DirEps
                ? delta / distance
                : new Vector2(ctx.WorldInfo.Face >= 0 ? 1f : -1f, 0f);

            _phase = FortressAttackPhase.Strike;
            _phaseStart = ctx.GameTime.now;
            _wave = 0;
            _nextWaveAt = 0f;

            LaunchWave(in ctx, 0f);
        }

        private void TickStrike(in LogicContext ctx, float elapsed)
        {
            _motor.BrakeTowardsX();

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

        /// <summary>后摇：原地不动，不追不转向，时间由 barrageRecoverTime 给</summary>
        private void BeginRecover(in LogicContext ctx)
        {
            _phase = FortressAttackPhase.Recover;
            _phaseStart = ctx.GameTime.now;

            _motor.BrakeTowardsX();
        }

        private void TickRecover(in LogicContext ctx, float elapsed)
        {
            _motor.BrakeTowardsX();

            if (elapsed >= _config.barrageRecoverTime) _done = true;
        }

        /// <summary>玩家正上 / 正下方时保持原朝向，别让 x 分量接近 0 的抖动把朝向翻来覆去</summary>
        private void FaceTarget(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) <= FaceDeadZone) return;

            _motor.FaceTowards(new Vector2(Mathf.Sign(delta.x), 0f));
        }
    }
}
