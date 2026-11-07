using Refraction.Data;
using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击后摇：只按 attackRecoverTime 计时，期间 AttackGroup 不响应新的攻击输入</summary>
    public sealed class WhiteAttackRecoverState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.WhiteAttackRecover;

        private readonly PlayerConfig _config;
        private float _enterTime;

        public WhiteAttackRecoverState(PlayerConfig config)
        {
            _config = config;
        }

        public override void Enter(in LogicContext ctx) => _enterTime = ctx.GameTime.now;

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.attackRecoverTime;

        public override void Exit() { }
    }
}
