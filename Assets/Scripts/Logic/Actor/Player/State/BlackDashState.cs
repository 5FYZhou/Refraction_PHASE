using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>黑突进：前摇锁死 → 定速冲（整段无敌）→ 后摇，斜上与斜下各一只实例</summary>
    /// <remarks>方向在按下那一刻就定了（状态本身就是方向），前摇期间改输入不换方向；
    /// 打到第一个目标就提前进后摇并向上弹起（y 换成 blackDashBounceSpeed，x 保留），撞墙则提前进后摇并清零</remarks>
    public sealed class BlackDashState : AttackPhaseState
    {
        private readonly PlayerConfig _config;
        private readonly IStateMove _motor;
        private readonly IBlackAttackView _view;
        private readonly ConditionGroup _conditions;
        private readonly AttackStateTag _tag;
        private readonly BlackAttackKind _kind;
        private bool _hit;
        private bool _blocked;

        public BlackDashState(PlayerConfig config, IStateMove motor, IBlackAttackView view, ConditionGroup conditions, AttackStateTag tag, BlackAttackKind kind)
        {
            _config = config;
            _motor = motor;
            _view = view;
            _conditions = conditions;
            _tag = tag;
            _kind = kind;
        }

        public override AttackStateTag Tag => _tag;

        public override bool AbortsOnHurt => true;

        protected override float PrepareTime => _config.blackDashPrepareTime;

        protected override float ActiveTime
            => _kind == BlackAttackKind.Up ? _config.blackDashTimeUp : _config.blackDashTimeDown;

        protected override float RecoverTime => _config.blackDashRecoverTime;

        /// <summary>命中回报：物理回调里不能改段，只置标记，下一帧 Advance 收</summary>
        public void NotifyHit() => _hit = true;

        protected override void OnPrepareEnter(in LogicContext ctx)
        {
            _hit = false;
            _blocked = false;

            _motor?.SetGravityEnable(false);
            _motor?.SnapVelocity(Vector2.zero);
        }

        protected override void OnActiveEnter(in LogicContext ctx)
        {
            Vector2 dir = AttackAim.Direction(ctx.WorldInfo.Face, _kind == BlackAttackKind.Up ? 1f : -1f);
            float speed = _kind == BlackAttackKind.Up ? _config.blackDashSpeedUp : _config.blackDashSpeedDown;

            _motor?.SetVelocityX(dir.x * speed);
            _motor?.SetVelocityY(dir.y * speed);

            _view?.BeginDash(_kind);
            _conditions?.BeginDashInvulnerable(ActiveTime);
        }

        protected override void OnAdvance(in LogicContext ctx)
        {
            if (!InActive) return;

            if (ctx.WorldInfo.TouchingWall) _blocked = true;

            if (_hit || _blocked) SkipToRecover(in ctx);
        }

        protected override void OnRecoverEnter(in LogicContext ctx)
        {
            _motor?.SetGravityEnable(true);
            _view?.End();

            if (_hit) _motor?.SetVelocityY(_config.blackDashBounceSpeed);
            else if (_blocked) _motor?.SnapVelocity(Vector2.zero);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            if (InPrepare) _motor?.SnapVelocity(Vector2.zero);
        }

        /// <summary>前摇挨打整段作废时本体不会来接手，重力必须在这里还回去</summary>
        protected override void OnExit()
        {
            _motor?.SetGravityEnable(true);
            _view?.End();
        }
    }
}
