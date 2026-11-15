using Refraction.Data;
using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>普攻前摇：只按 flatAttackPrepareTime 计时，时间到交给 FlatAttack</summary>
    public sealed class FlatAttackPrepareState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.FlatAttackPrepare;

        private readonly PlayerConfig _config;
        private float _enterTime;

        public FlatAttackPrepareState(PlayerConfig config) => _config = config;

        public override void Enter(in LogicContext ctx) => _enterTime = ctx.GameTime.now;

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.flatAttackPrepareTime;

        public override void Exit() { }
    }
}
