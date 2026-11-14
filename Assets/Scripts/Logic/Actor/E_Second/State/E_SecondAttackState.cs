using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击：前摇 → 攻击 → 后摇三段自带计时；黑冲撞、白发子弹，都只看当前世界极性</summary>
    /// <remarks>无冷却：后摇结束只要还看得见就原地重进前摇，同状态再切会被状态机的幂等挡掉</remarks>
    public sealed class E_SecondAttackState : BaseState<E_SecondStateTag, LogicContext>
    {
        public override E_SecondStateTag Tag => E_SecondStateTag.Attack;

        public E_SecondAttackPhase Phase => _phase;

        private const float FaceDeadZone = 0.05f;
        private const float DirEps = 1e-6f;

        private readonly E_SecondLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly E_SecondConfig _config;

        private E_SecondAttackPhase _phase;
        private float _phaseStart;
        private Vector2 _lockedDir = Vector2.right;
        private float _riseTime;
        private bool _done;

        public E_SecondAttackState(E_SecondLogic owner, IMoveMotor motor, E_SecondConfig config)
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
                case E_SecondAttackPhase.Windup:
                    TickWindup(in ctx, elapsed);
                    break;

                case E_SecondAttackPhase.Strike:
                    TickStrike(in ctx, elapsed);
                    break;

                case E_SecondAttackPhase.Recover:
                    TickRecover(in ctx, elapsed);
                    break;
            }
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }

        private float WindupTime => _owner.Black ? _config.blackWindupTime : _config.whiteWindupTime;

        private void BeginWindup(in LogicContext ctx)
        {
            _phase = E_SecondAttackPhase.Windup;
            _phaseStart = ctx.GameTime.now;
            _done = false;

            _motor.SnapVelocity(Vector2.zero);
        }

        /// <summary>前摇：白朝玩家靠到 whiteChaseRadius 以内，黑原地蓄力；两者都跟着玩家转身，方向到结束那一帧才锁</summary>
        private void TickWindup(in LogicContext ctx, float elapsed)
        {
            if (_owner.Black) Hover(in ctx);
            else MoveWhite(in ctx);

            if (elapsed >= WindupTime) BeginStrike(in ctx);
        }

        /// <summary>黑前摇：原地不动，只跟着玩家转身</summary>
        private void Hover(in LogicContext ctx)
        {
            _motor.SnapVelocity(Vector2.zero);

            FaceTarget(_owner.TargetPos - ctx.WorldInfo.Pos);
        }

        /// <summary>白走位：朝玩家飞到 whiteChaseRadius 以内就刹住，只进不退；速度是斜的，朝向要盖回水平</summary>
        private void MoveWhite(in LogicContext ctx)
        {
            Vector2 delta = _owner.TargetPos - ctx.WorldInfo.Pos;
            float distance = delta.magnitude;

            bool approach = distance > _config.whiteChaseRadius && distance > DirEps;

            _motor.SnapVelocity(approach ? delta / distance * _config.whiteChaseSpeed : Vector2.zero);

            FaceTarget(delta);
        }

        /// <summary>玩家正上 / 正下方时保持原朝向，别让 x 分量接近 0 的抖动把朝向翻来覆去</summary>
        private void FaceTarget(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) <= FaceDeadZone) return;

            _motor.FaceTowards(new Vector2(Mathf.Sign(delta.x), 0f));
        }

        private void BeginStrike(in LogicContext ctx)
        {
            Vector2 pos = ctx.WorldInfo.Pos;
            Vector2 delta = _owner.TargetPos - pos;

            _lockedDir = delta.sqrMagnitude > DirEps
                ? delta.normalized
                : new Vector2(ctx.WorldInfo.Face >= 0 ? 1f : -1f, 0f);

            _phase = E_SecondAttackPhase.Strike;
            _phaseStart = ctx.GameTime.now;

            _motor.FaceTowards(new Vector2(_lockedDir.x, 0f));

            if (_owner.Black)
            {
                _motor.SnapVelocity(_lockedDir * _config.blackDashSpeed);
                return;
            }

            // 前摇是靠过去的，开火这一帧要停住，别带着走位的速度继续漂
            _motor.SnapVelocity(Vector2.zero);

            FireWhite(pos);
        }

        private void TickStrike(in LogicContext ctx, float elapsed)
        {
            if (!_owner.Black)
            {
                // 白：开火在进 Strike 那一帧就完成了，这里只留一帧给表现层上色
                if (elapsed > 0f) BeginRecover(in ctx);
                return;
            }

            _motor.SnapVelocity(_lockedDir * _config.blackDashSpeed);

            // 撞到就进后摇：圆环不分方向，斜冲、竖冲都算；人看受击盒相交，不断言伤害是否真扣
            if (ctx.WorldInfo.TouchingWall || _owner.TouchingTarget(ctx.WorldInfo.Pos))
            {
                BeginRecover(in ctx);
                return;
            }

            if (elapsed >= _config.blackDashMaxTime) BeginRecover(in ctx);
        }

        private void BeginRecover(in LogicContext ctx)
        {
            _phase = E_SecondAttackPhase.Recover;
            _phaseStart = ctx.GameTime.now;

            if (_owner.Black) _riseTime = _owner.RollRiseDistance() / Mathf.Max(0.01f, _config.riseSpeed);
        }

        private void TickRecover(in LogicContext ctx, float elapsed)
        {
            if (_owner.Black)
            {
                // 水平停住，只按固定速度上浮：后摇时长 = 本次随机距离 / 上浮速度
                _motor.SetVelocityX(0f);
                _motor.SetVelocityY(_config.riseSpeed);

                if (elapsed >= _riseTime) EndRecover(in ctx);
                return;
            }

            MoveWhite(in ctx);

            if (elapsed >= _config.whiteRecoverTime) EndRecover(in ctx);
        }

        private void EndRecover(in LogicContext ctx)
        {
            if (_owner.Visible)
            {
                BeginWindup(in ctx);
                return;
            }

            _done = true;
        }

        /// <summary>枪口相对身体，x 分量随朝向镜像：往左射时不能从背后冒出来</summary>
        private void FireWhite(Vector2 pos)
        {
            int sign = _lockedDir.x < 0f ? -1 : 1;
            Vector2 muzzle = pos + new Vector2(_config.muzzle.x * sign, _config.muzzle.y);

            _owner.FireWhite(muzzle, _lockedDir);
        }
    }
}
