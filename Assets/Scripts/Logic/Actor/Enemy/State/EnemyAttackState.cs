using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击：前摇 → 冲刺 → 后摇三段自带计时，冲刺方向在前摇结束那一帧锁定，之后玩家跑开也照原方向冲</summary>
    /// <remarks>后摇结束若冷却已好且玩家还在范围内，原地重新进前摇：同状态再切一次会被状态机的幂等判断吃掉，不会重进</remarks>
    public sealed class EnemyAttackState : BaseState<EnemyStateTag, LogicContext>
    {
        public override EnemyStateTag Tag => EnemyStateTag.Attack;

        /// <summary>水平距离小于它就认为玩家正上/正下方，方向沿用当前朝向</summary>
        private const float FaceDeadZone = 0.05f;

        private readonly EnemyLogic _owner;
        private readonly IMoveMotor _motor;
        private readonly EnemyConfig _config;

        private EnemyAttackPhase _phase;
        private float _phaseStart;
        private Vector2 _lockedDir = Vector2.right;

        public EnemyAttackState(EnemyLogic owner, IMoveMotor motor, EnemyConfig config)
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
                case EnemyAttackPhase.Windup:
                    TickWindup(in ctx);
                    if (elapsed >= _config.attackWindupTime) BeginDash(in ctx);
                    break;

                case EnemyAttackPhase.Dash:
                    // 方向可能正对悬崖：前方一空就收手
                    if (ctx.WorldInfo.LedgeAhead)
                    {
                        BeginRecovery(in ctx);
                        break;
                    }

                    // 每帧重写：贴地吸附与碰撞都会改写横向速度
                    _motor.SetVelocityX(_lockedDir.x * _config.attackDashSpeed);
                    if (elapsed >= _config.attackDashDuration) BeginRecovery(in ctx);
                    break;

                case EnemyAttackPhase.Recovery:
                    _motor.BrakeTowardsX();
                    if (elapsed >= _config.attackRecoverTime
                        && _owner.TargetVisible
                        && _owner.CanAttack(ctx.GameTime.now))
                    {
                        BeginWindup(in ctx);
                    }
                    break;
            }
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return _phase == EnemyAttackPhase.Recovery
                && ctx.GameTime.now - _phaseStart >= _config.attackRecoverTime;
        }

        public override void Exit() { }

        /// <summary>前摇期间还跟着玩家转身，但方向要到结束才定；此时原地刹车，不往前挪</summary>
        private void TickWindup(in LogicContext ctx)
        {
            _motor.BrakeTowardsX();

            float dx = _owner.TargetPos.x - ctx.WorldInfo.Pos.x;
            if (Mathf.Abs(dx) > FaceDeadZone) _motor.FaceTowards(new Vector2(Mathf.Sign(dx), 0f));
        }

        private void BeginWindup(in LogicContext ctx)
        {
            _phase = EnemyAttackPhase.Windup;
            _phaseStart = ctx.GameTime.now;
            _owner.MarkAttacked(ctx.GameTime.now);
        }

        private void BeginDash(in LogicContext ctx)
        {
            float dx = _owner.TargetPos.x - ctx.WorldInfo.Pos.x;
            _lockedDir = Mathf.Abs(dx) > FaceDeadZone
                ? new Vector2(Mathf.Sign(dx), 0f)
                : new Vector2(ctx.WorldInfo.Face >= 0 ? 1f : -1f, 0f);

            _phase = EnemyAttackPhase.Dash;
            _phaseStart = ctx.GameTime.now;

            _motor.FaceTowards(_lockedDir);
            _motor.SetVelocityX(_lockedDir.x * _config.attackDashSpeed);
        }

        /// <summary>冲刺结束当帧就把横向速度清掉：否则后摇会被冲刺余速拖着滑一段</summary>
        private void BeginRecovery(in LogicContext ctx)
        {
            _phase = EnemyAttackPhase.Recovery;
            _phaseStart = ctx.GameTime.now;
            _motor.SetVelocityX(0f);
        }
    }
}
