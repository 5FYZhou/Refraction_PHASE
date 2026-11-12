using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击状态组：None → 前摇 → WhiteAttack（开火那一帧）→ 后摇 → None，与移动、姿态正交</summary>
    /// <remarks>排在移动组之后：开火方向要用本帧 MoveState 刚写下的朝向；后坐力走外部速度，不靠帧序覆盖；攻击期间不响应新的攻击输入</remarks>
    public sealed class AttackGroup : StateGroup<AttackStateTag, LogicContext>
    {
        protected override AttackStateTag EmptyTag => AttackStateTag.None;

        private readonly PlayerConfig _config;
        private readonly Cooldown _cooldown = new();
        private readonly WhiteAttackState _white;

        public AttackGroup(PlayerConfig config, IStateMove motor, IBulletLauncher launcher, PostureGroup posture)
        {
            _config = config;
            _white = new WhiteAttackState(config, motor, launcher, posture);

            RegisterState(new NoneAttackState());
            RegisterState(new WhiteAttackPrepareState(config));
            RegisterState(_white);
            RegisterState(new WhiteAttackRecoverState(config));
        }

        public void BindLauncher(IBulletLauncher launcher) => _white.Bind(launcher);

        public AttackStateTag Current => machine.CurrentState?.Tag ?? AttackStateTag.None;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        protected override AttackStateTag Fallback(in LogicContext ctx)
        {
            switch (Current)
            {
                case AttackStateTag.WhiteAttackPrepare:
                    return AttackStateTag.WhiteAttack;
                case AttackStateTag.WhiteAttack:
                    return _config.attackRecoverTime > 0f
                        ? AttackStateTag.WhiteAttackRecover
                        : AttackStateTag.None;
                default:
                    return AttackStateTag.None;
            }
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out AttackStateTag target)
        {
            target = EmptyTag;

            if (Current != AttackStateTag.None) return false;
            if (!ctx.InputSnapshot.Attack) return false;
            if (!_cooldown.CanUse(ctx.GameTime.now)) return false;

            target = _config.attackPrepareTime > 0f
                ? AttackStateTag.WhiteAttackPrepare
                : AttackStateTag.WhiteAttack;
            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, AttackStateTag target)
        {
            _cooldown.MarkUsed(ctx.GameTime.now, _config.attackCooldown);
            return true;
        }
    }
}
