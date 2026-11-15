using Refraction.Data;
using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>黑突进后摇：重力已回归、移动也照常，只有攻击 / 跳跃 / 冲刺被挡掉，计时 blackDashRecoverTime</summary>
    public sealed class BlackDashRecoverState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.BlackDashRecover;

        private readonly PlayerConfig _config;
        private float _enterTime;

        public BlackDashRecoverState(PlayerConfig config) => _config = config;

        public override void Enter(in LogicContext ctx) => _enterTime = ctx.GameTime.now;

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.blackDashRecoverTime;

        public override void Exit() { }
    }
}
