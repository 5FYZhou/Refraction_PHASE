using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>黑突进后摇：重力已恢复、冲出去的横向速度按移动减速自己掉，计时 blackDashRecoverTime</summary>
    /// <remarks>移动组整段被压住，所以刹车只能在这里踩；不清空速度，后摇结束时还剩多少就交给移动组接着算</remarks>
    public sealed class BlackDashRecoverState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.BlackDashRecover;

        private readonly PlayerConfig _config;
        private readonly IStateMove _motor;
        private float _enterTime;

        public BlackDashRecoverState(PlayerConfig config, IStateMove motor)
        {
            _config = config;
            _motor = motor;
        }

        public override void Enter(in LogicContext ctx) => _enterTime = ctx.GameTime.now;

        public override void Tick(in LogicContext ctx) => _motor?.BrakeTowardsX();

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.blackDashRecoverTime;

        public override void Exit() { }
    }
}
