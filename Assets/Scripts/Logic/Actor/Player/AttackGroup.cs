using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击状态组：None → 前摇 → 攻击 → 后摇 → None，白黑两套随世界极性切换</summary>
    /// <remarks>排在移动组之后：开火方向用本帧朝向；反转不打断进行中的一套；静态总线订阅必须 Dispose</remarks>
    public sealed class AttackGroup : StateGroup<AttackStateTag, LogicContext>
    {
        protected override AttackStateTag EmptyTag => AttackStateTag.None;

        private readonly PlayerConfig _config;
        private readonly Cooldown _cooldown = new();
        private readonly WhiteAttackState _white;
        private readonly BlackAttackState _blackState;
        private readonly ConditionGroup _conditions;
        private bool _black;

        public AttackGroup(PlayerConfig config, IStateMove motor, IBulletLauncher launcher, PostureGroup posture, IBlackAttackView blackView = null, ConditionGroup conditions = null)
        {
            _config = config;
            _conditions = conditions;
            _white = new WhiteAttackState(config, motor, launcher, posture);

            RegisterState(new NoneAttackState());
            RegisterState(new WhiteAttackPrepareState(config));
            RegisterState(_white);
            RegisterState(new WhiteAttackRecoverState(config));
            RegisterState(new BlackAttackPrepareState(config));
            _blackState = new BlackAttackState(config, blackView, motor, posture);
            RegisterState(_blackState);
            RegisterState(new BlackAttackRecoverState(config));

            blackView?.SetHitHandler(_blackState.NotifyHit);

            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);
        }

        public bool IsBlack => _black;

        public void BindLauncher(IBulletLauncher launcher) => _white.Bind(launcher);

        public AttackStateTag Current => machine.CurrentState?.Tag ?? AttackStateTag.None;

        public void Tick(in LogicContext ctx) => TickStates(ctx);

        public void Dispose() => EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);

        private void OnWorldPolarityChanged(WorldPolarityChanged evt) => _black = evt.World == WorldPolarity.Black;

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
                case AttackStateTag.BlackAttackPrepare:
                    return AttackStateTag.BlackAttack;
                case AttackStateTag.BlackAttack:
                    return _config.blackAttackRecoverTime > 0f
                        ? AttackStateTag.BlackAttackRecover
                        : AttackStateTag.None;
                default:
                    return AttackStateTag.None;
            }
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out AttackStateTag target)
        {
            target = EmptyTag;

            if (Current != AttackStateTag.None) return false;
            if (_conditions != null && _conditions.IsHurt) return false;
            if (!ctx.InputSnapshot.Attack) return false;
            if (!_cooldown.CanUse(ctx.GameTime.now)) return false;

            if (_black)
            {
                target = _config.blackAttackPrepareTime > 0f
                    ? AttackStateTag.BlackAttackPrepare
                    : AttackStateTag.BlackAttack;
            }
            else
            {
                target = _config.attackPrepareTime > 0f
                    ? AttackStateTag.WhiteAttackPrepare
                    : AttackStateTag.WhiteAttack;
            }

            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, AttackStateTag target)
        {
            _cooldown.MarkUsed(ctx.GameTime.now, _config.attackCooldown);
            return true;
        }
    }
}
