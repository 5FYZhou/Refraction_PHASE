using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>近身攻击：前摇 → 快速位移 → 后摇；黑打两段前方范围伤害，白打两枪</summary>
    /// <remarks>形态在进状态那一帧锁死，中途翻极性不改动作；范围伤害只举旗，命中由表现层结算</remarks>
    public sealed class MantisAttackState : BaseState<EnemyStateTag, LogicContext>
    {
        private const float FaceDeadZone = 0.05f;
        private const float DirEps = 1e-6f;

        private readonly MantisLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly MantisConfig _config;

        private MantisAttackPhase _phase;
        private float _phaseStart;
        private bool _blackForm;
        private int _dashSign = 1;
        private Vector2 _lockedTarget;
        private bool _done;

        public MantisAttackState(MantisLogic owner, IMoveMotor motor, MantisConfig config)
        {
            _owner = owner;
            _motor = motor;
            _config = config;
        }

        public override EnemyStateTag Tag => EnemyStateTag.Attack;

        public MantisAttackPhase Phase => _phase;

        public override void Enter(in LogicContext ctx)
        {
            _blackForm = _owner.Black;
            _done = false;

            _phase = MantisAttackPhase.Windup;
            _phaseStart = ctx.GameTime.now;

            _motor.SetVelocityX(0f);
        }

        public override void Tick(in LogicContext ctx)
        {
            float elapsed = ctx.GameTime.now - _phaseStart;

            switch (_phase)
            {
                case MantisAttackPhase.Windup:
                    TickWindup(in ctx, elapsed);
                    break;

                case MantisAttackPhase.Dash:
                    TickDash(in ctx, elapsed);
                    break;

                case MantisAttackPhase.Recover:
                    TickRecover(in ctx, elapsed);
                    break;
            }
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }

        private float WindupTime => _blackForm ? _config.blackWindupTime : _config.whiteWindupTime;

        private float RecoverTime => _blackForm ? _config.blackRecoverTime : _config.whiteRecoverTime;

        /// <summary>前摇：立定不动，全程跟着玩家转身；结束那一帧锁位置并打出第一段</summary>
        private void TickWindup(in LogicContext ctx, float elapsed)
        {
            _motor.SetVelocityX(0f);

            FaceTarget(_owner.TargetPos - ctx.WorldInfo.Pos);

            if (elapsed < WindupTime) return;

            _lockedTarget = _owner.TargetPos;
            _dashSign = ctx.WorldInfo.Face >= 0 ? 1 : -1;

            if (_blackForm) _owner.RequestHit();
            else FireAt(in ctx, _lockedTarget);

            _phase = MantisAttackPhase.Dash;
            _phaseStart = ctx.GameTime.now;

            _motor.SetVelocityX(_dashSign * _config.dashSpeed);
        }

        /// <summary>快速位移：定时推进，撞墙交给物理拦；走完结算第二段</summary>
        private void TickDash(in LogicContext ctx, float elapsed)
        {
            _motor.SetVelocityX(_dashSign * _config.dashSpeed);

            if (elapsed < _config.dashTime) return;

            if (_blackForm) _owner.RequestHit();
            else
            {
                _lockedTarget = _owner.TargetPos;
                FireAt(in ctx, _lockedTarget);
            }

            BeginRecover(in ctx);
        }

        /// <summary>后摇：黑原地不动；白朝刚才位移的反方向超快后撤</summary>
        private void BeginRecover(in LogicContext ctx)
        {
            _phase = MantisAttackPhase.Recover;
            _phaseStart = ctx.GameTime.now;

            _motor.SetVelocityX(_blackForm
                ? 0f
                : -_dashSign * _config.dashSpeed * _config.dashBackMultiplier);
        }

        private void TickRecover(in LogicContext ctx, float elapsed)
        {
            _motor.SetVelocityX(_blackForm
                ? 0f
                : -_dashSign * _config.dashSpeed * _config.dashBackMultiplier);

            if (elapsed < RecoverTime) return;

            _done = true;
        }

        private void FireAt(in LogicContext ctx, Vector2 target)
        {
            Vector2 pos = ctx.WorldInfo.Pos;
            Vector2 delta = target - pos;

            Vector2 dir = delta.sqrMagnitude > DirEps
                ? delta.normalized
                : new Vector2(_dashSign, 0f);

            _owner.Fire(_owner.Muzzle(pos, dir.x >= 0f ? 1 : -1), dir);
        }

        /// <summary>只认方向不看速度：玩家正好同 x 时保持原朝向</summary>
        private void FaceTarget(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) <= FaceDeadZone) return;

            _motor.FaceTowards(new Vector2(Mathf.Sign(delta.x), 0f));
        }
    }
}
