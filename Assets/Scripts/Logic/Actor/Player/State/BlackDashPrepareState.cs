using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>黑突进前摇：每帧跟着竖直输入重选方向，计时 blackDashPrepareTime，走完把最后一次采到的方向交给 BlackDash</summary>
    /// <remarks>移动组在这一段已被压住，所以这里自己踩刹车；被打断时 Direction 由 AttackGroup 在下次起手时清掉</remarks>
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

        /// <summary>前摇最后一帧定下的方向；零表示这一段没跑过（前摇时长为 0）</summary>
        public Vector2 Direction { get; private set; }

        public void Clear()
        {
            Kind = BlackAttackKind.Ahead;
            Direction = Vector2.zero;
        }

        public override void Enter(in LogicContext ctx)
        {
            _enterTime = ctx.GameTime.now;
            Sample(in ctx);
        }

        public override void Tick(in LogicContext ctx)
        {
            Sample(in ctx);
            _motor?.BrakeTowardsX();
        }

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.blackDashPrepareTime;

        public override void Exit() { }

        private void Sample(in LogicContext ctx)
        {
            Kind = AttackAim.Kind(ctx.InputSnapshot.Move.y);
            Direction = AttackAim.Direction(ctx.WorldInfo.Face, ctx.InputSnapshot.Move.y);
        }
    }
}
