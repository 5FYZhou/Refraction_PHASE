using Refraction.Data;
using Refraction.Foundation;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击前摇：只按 attackPrepareTime 计时，时间到交给 WhiteAttack 开火</summary>
    public sealed class WhiteAttackPrepareState : BaseState<AttackStateTag, LogicContext>
    {
        public override AttackStateTag Tag => AttackStateTag.WhiteAttackPrepare;

        private readonly PlayerConfig _config;
        private float _enterTime;

        public WhiteAttackPrepareState(PlayerConfig config)
        {
            _config = config;
        }

        public override void Enter(in LogicContext ctx) => _enterTime = ctx.GameTime.now;

        public override void Tick(in LogicContext ctx) { }

        public override bool IsDone(in LogicContext ctx)
            => ctx.GameTime.now - _enterTime >= _config.attackPrepareTime;

        public override void Exit() { }
    }
}
