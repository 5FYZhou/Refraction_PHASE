using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击：前摇 → 攻击 → 后摇三段自带计时；黑是按距离走满的冲撞，白是多波散射弹幕</summary>
    /// <remarks>后摇都是随机方向浮动；结束条件满足就近原地重进前摇，否则交给状态组</remarks>
    public sealed class SpliterAttackState : BaseState<EnemyStateTag, LogicContext>
    {
        public override EnemyStateTag Tag => EnemyStateTag.Attack;

        public SpliterAttackPhase Phase => _phase;

        private const float FaceDeadZone = 0.05f;
        private const float DirEps = 1e-6f;

        private readonly SpliterLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly SpliterConfig _config;

        private SpliterAttackPhase _phase;
        private float _phaseStart;
        private Vector2 _lockedDir = Vector2.right;
        private float _chargeDistance;
        private float _travelled;
        private Vector2 _floatDir = Vector2.up;
        private float _floatTime;
        private int _wave;
        private float _nextWaveAt;
        private bool _done;

        public SpliterAttackState(SpliterLogic owner, IMoveMotor motor, SpliterConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx) => BeginWindup(in ctx);

        public override void Tick(in LogicContext ctx)
        {
            float elapsed = ctx.GameTime.now - _phaseStart;

            switch (_phase)
            {
                case SpliterAttackPhase.Windup:
                    TickWindup(in ctx, elapsed);
                    break;

                case SpliterAttackPhase.Strike:
                    TickStrike(in ctx, elapsed);
                    break;

                case SpliterAttackPhase.Recover:
                    TickRecover(in ctx, elapsed);
                    break;
            }
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }

        private float WindupTime => _owner.Black ? _config.chargeWindupTime : _config.barrageWindupTime;

        /// <summary>起手就记一次冷却：上一轮的后摇刚走完也一样要重新等</summary>
        private void BeginWindup(in LogicContext ctx)
        {
            _phase = SpliterAttackPhase.Windup;
            _phaseStart = ctx.GameTime.now;
            _done = false;

            _motor.SnapVelocity(Vector2.zero);

            _owner.BeginAttackCooldown(ctx.GameTime.now);
        }

        /// <summary>前摇：立定不动，只把水平朝向对准玩家；方向到结束那一帧才锁</summary>
        private void TickWindup(in LogicContext ctx, float elapsed)
        {
            _motor.SnapVelocity(Vector2.zero);

            Vector2 delta = _owner.TargetPos - ctx.WorldInfo.Pos;
            FaceTarget(delta);

            if (elapsed < WindupTime) return;

            float distance = delta.magnitude;

            _lockedDir = distance > DirEps
                ? delta / distance
                : new Vector2(ctx.WorldInfo.Face >= 0 ? 1f : -1f, 0f);

            if (_owner.Black) BeginCharge(in ctx, distance);
            else BeginBarrage(in ctx);
        }

        private void BeginCharge(in LogicContext ctx, float distance)
        {
            _phase = SpliterAttackPhase.Strike;
            _phaseStart = ctx.GameTime.now;

            // 冲撞距离 = 起手那一刻两者连线距离的两倍；没有时间上限，走满就进后摇
            _chargeDistance = 2f * distance;
            _travelled = 0f;

            _motor.SnapVelocity(_lockedDir * _config.chargeSpeed);
        }

        private void BeginBarrage(in LogicContext ctx)
        {
            _phase = SpliterAttackPhase.Strike;
            _phaseStart = ctx.GameTime.now;
            _wave = 0;
            _nextWaveAt = 0f;

            _motor.SnapVelocity(Vector2.zero);

            LaunchWave(in ctx, 0f);
        }

        private void TickStrike(in LogicContext ctx, float elapsed)
        {
            if (!_owner.Black)
            {
                TickBarrage(in ctx, elapsed);
                return;
            }

            _motor.SnapVelocity(_lockedDir * _config.chargeSpeed);

            _travelled += _config.chargeSpeed * ctx.GameTime.deltaTime;

            // 撞墙也收手：冲撞没有时间上限，顶在墙上会一直推
            if (_travelled >= _chargeDistance || ctx.WorldInfo.TouchingWall) BeginRecover(in ctx);
        }

        /// <summary>弹幕：第一波在起手当帧打完，之后每 waveInterval 一波，最后一波打完就进后摇</summary>
        private void TickBarrage(in LogicContext ctx, float elapsed)
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
            _phase = SpliterAttackPhase.Recover;
            _phaseStart = ctx.GameTime.now;

            _owner.RollFloat(out _floatDir, out _floatTime);

            _motor.SnapVelocity(_floatDir * _config.floatSpeed);
        }

        private void TickRecover(in LogicContext ctx, float elapsed)
        {
            _motor.SnapVelocity(_floatDir * _config.floatSpeed);

            if (elapsed >= _floatTime) EndRecover(in ctx);
        }

        private void EndRecover(in LogicContext ctx)
        {
            if (_owner.Alert && _owner.InAttackRange(ctx.WorldInfo.Pos) && _owner.CanAttack(ctx.GameTime.now))
            {
                BeginWindup(in ctx);
                return;
            }

            _done = true;
        }

        /// <summary>玩家正上 / 正下方时保持原朝向，别让 x 分量接近 0 的抖动把朝向翻来覆去</summary>
        private void FaceTarget(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) <= FaceDeadZone) return;

            _motor.FaceTowards(new Vector2(Mathf.Sign(delta.x), 0f));
        }
    }
}
