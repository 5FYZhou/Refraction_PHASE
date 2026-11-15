using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>黑突进本体：关重力按方向定速冲，速度与时长斜上 / 斜下各一套</summary>
    /// <remarks>方向在前摇最后一帧已定死，本体期间读输入不生效；整段无敌，突进盒与弹反盒同时点亮。
    /// 打到第一个目标就提前结束并向上弹起，撞墙同样提前结束但把速度清成 0，两者都靠 Exit 收尾</remarks>
    public sealed class BlackDashState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.BlackDash;

        private readonly PlayerConfig _config;
        private readonly IStateMove _motor;
        private readonly IBlackAttackView _view;
        private readonly ConditionGroup _conditions;
        private readonly BlackDashPrepareState _prepare;
        private float _enterTime;
        private bool _hit;
        private bool _blocked;

        public BlackDashState(PlayerConfig config, IStateMove motor, IBlackAttackView view, ConditionGroup conditions, BlackDashPrepareState prepare)
        {
            _config = config;
            _motor = motor;
            _view = view;
            _conditions = conditions;
            _prepare = prepare;
        }

        public BlackAttackKind Kind { get; private set; }

        /// <summary>命中回报：物理回调里不能切状态，只置标记，下一帧 IsDone 收</summary>
        public void NotifyHit() => _hit = true;

        public override void Enter(in LogicContext ctx)
        {
            _enterTime = ctx.GameTime.now;
            _hit = false;
            _blocked = false;

            Kind = _prepare.Kind == BlackAttackKind.Ahead ? BlackAttackKind.Up : _prepare.Kind;

            Vector2 dir = _prepare.Direction == Vector2.zero
                ? AttackAim.Direction(ctx.WorldInfo.Face, 1f)
                : _prepare.Direction;

            float speed = SpeedOf(Kind);

            _motor.SetGravityEnable(false);
            _motor.SetVelocityX(dir.x * speed);
            _motor.SetVelocityY(dir.y * speed);

            _view?.BeginDash(Kind);
            _conditions?.BeginDashInvulnerable(TimeOf(Kind));
        }

        public override void Tick(in LogicContext ctx)
        {
            if (ctx.WorldInfo.TouchingWall) _blocked = true;
        }

        public override bool IsDone(in LogicContext ctx)
            => _hit || _blocked || ctx.GameTime.now - _enterTime >= TimeOf(Kind);

        public override void Exit()
        {
            if (_hit)
            {
                _motor?.SetVelocityX(0);
                _motor?.SetVelocityY(_config.blackDashBounceSpeed);
            }
            else if (_blocked) _motor?.SnapVelocity(Vector2.zero);

            _motor?.SetGravityEnable(true);
            _view?.End();
        }

        private float SpeedOf(BlackAttackKind kind)
            => kind == BlackAttackKind.Up ? _config.blackDashSpeedUp : _config.blackDashSpeedDown;

        private float TimeOf(BlackAttackKind kind)
            => kind == BlackAttackKind.Up ? _config.blackDashTimeUp : _config.blackDashTimeDown;
    }
}
