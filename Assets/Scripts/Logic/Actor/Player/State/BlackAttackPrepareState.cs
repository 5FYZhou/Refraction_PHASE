using Refraction.Data;
using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>黑攻击前摇：只按 blackAttackPrepareTime 计时，时间到交给 BlackAttack</summary>
    public sealed class BlackAttackPrepareState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.BlackAttackPrepare;

        private readonly PlayerConfig _config;
        private float _enterTime;

        public BlackAttackPrepareState(PlayerConfig config) => _config = config;

        public override void Enter(in LogicContext ctx) => _enterTime = ctx.GameTime.now;

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.blackAttackPrepareTime;

        public override void Exit() { }
    }
}
