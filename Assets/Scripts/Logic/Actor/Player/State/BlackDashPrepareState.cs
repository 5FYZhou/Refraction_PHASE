using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>黑突进前摇：速度锁成 0、重力关掉，每帧只按斜向输入重选方向，走完把方向交给 BlackDash</summary>
    /// <remarks>松开方向保留上一次的斜向：突进没有平推，平劈走普攻那条链</remarks>
    public sealed class BlackDashPrepareState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.BlackDashPrepare;

        private readonly PlayerConfig _config;
        private readonly IStateMove _motor;
        private float _enterTime;

        public BlackDashPrepareState(PlayerConfig config, IStateMove motor)
        {
            _config = config;
            _motor = motor;
        }

        public BlackAttackKind Kind { get; private set; }

        /// <summary>前摇最后一帧定下的方向</summary>
        public Vector2 Direction { get; private set; }

        public override void Enter(in LogicContext ctx)
        {
            _enterTime = ctx.GameTime.now;

            _motor?.SetGravityEnable(false);
            _motor?.SnapVelocity(Vector2.zero);

            Sample(in ctx);
        }

        public override void Tick(in LogicContext ctx)
        {
            Sample(in ctx);
            _motor?.SnapVelocity(Vector2.zero);
        }

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.blackDashPrepareTime;

        /// <summary>前摇被打断时本体不会来接手，重力必须在这里还回去</summary>
        public override void Exit() => _motor?.SetGravityEnable(true);

        private void Sample(in LogicContext ctx)
        {
            BlackAttackKind kind = AttackAim.Kind(ctx.InputSnapshot.Move.y);

            if (kind == BlackAttackKind.Ahead) return;

            Kind = kind;
            Direction = AttackAim.Direction(ctx.WorldInfo.Face, ctx.InputSnapshot.Move.y);
        }
    }
}
