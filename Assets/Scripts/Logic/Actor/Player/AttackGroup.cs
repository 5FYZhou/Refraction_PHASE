using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击状态组：白形态三段接力；黑形态三向各一只自带前后摇的状态</summary>
    public sealed class AttackGroup : StateGroup<AttackStateTag, LogicContext>
    {
        protected override AttackStateTag EmptyTag => AttackStateTag.None;

        private readonly PlayerConfig _config;
        private readonly Cooldown _cooldown = new();
        private readonly Cooldown _flatCooldown = new();
        private readonly Cooldown _dashUpCooldown = new();
        private readonly Cooldown _dashDownCooldown = new();
        private readonly WhiteAttackState _white;
        private readonly FlatAttackState _flat;
        private readonly BlackDashState _dashUp;
        private readonly BlackDashState _dashDown;
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

            _flat = new FlatAttackState(config, blackView, motor, posture);
            _dashUp = new BlackDashState(config, motor, blackView, conditions, AttackStateTag.BlackDashUp, BlackAttackKind.Up);
            _dashDown = new BlackDashState(config, motor, blackView, conditions, AttackStateTag.BlackDashDown, BlackAttackKind.Down);

            RegisterState(_flat);
            RegisterState(_dashUp);
            RegisterState(_dashDown);

            blackView?.SetHitHandler(OnBlackHit);

            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);
        }

        public bool IsBlack => _black;

        /// <summary>前摇与本体压住移动组，连状态都不推进</summary>
        public bool BlocksMovement => Phased != null && Phased.BlocksMovement;

        /// <summary>整段（含后摇）都挡跳跃与冲刺</summary>
        public bool BlocksActions => Phased != null && Phased.BlocksActions;

        /// <summary>正在后摇：别的攻击可以顶掉它</summary>
        public bool Cancelable => Phased != null && Phased.Cancelable;

        public void BindLauncher(IBulletLauncher launcher) => _white.Bind(launcher);

        public AttackStateTag Current => machine.CurrentState?.Tag ?? AttackStateTag.None;

        private AttackPhaseState Phased => machine.CurrentState as AttackPhaseState;

        public void Tick(in LogicContext ctx)
        {
            AbortOnHurt(in ctx);

            Phased?.Advance(in ctx);

            TickStates(ctx);
        }

        public void Dispose() => EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);

        private void AbortOnHurt(in LogicContext ctx)
        {
            if (_conditions == null || !_conditions.IsHurt) return;

            AttackPhaseState phased = Phased;

            if (phased == null || !phased.InPrepare || !phased.AbortsOnHurt) return;

            ChangeState(AttackStateTag.None, in ctx);
        }

        private void OnWorldPolarityChanged(WorldPolarityChanged evt) => _black = evt.World == WorldPolarity.Black;

        private void OnBlackHit()
        {
            switch (Current)
            {
                case AttackStateTag.FlatAttack:
                    _flat.NotifyHit();
                    break;
                case AttackStateTag.BlackDashUp:
                    _dashUp.NotifyHit();
                    break;
                case AttackStateTag.BlackDashDown:
                    _dashDown.NotifyHit();
                    break;
            }
        }

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

            if (_conditions != null && _conditions.IsHurt) return false;
            if (!ctx.InputSnapshot.Attack) return false;
            if (!CanPreempt()) return false;

            if (!_black)
            {
                if (!_cooldown.CanUse(ctx.GameTime.now)) return false;

                target = _config.attackPrepareTime > 0f
                    ? AttackStateTag.WhiteAttackPrepare
                    : AttackStateTag.WhiteAttack;

                return true;
            }

            target = BlackTarget(ctx.InputSnapshot.Move.y, ctx.WorldInfo.Grounded);

            return target != EmptyTag && CooldownOf(target).CanUse(ctx.GameTime.now);
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, AttackStateTag target)
        {
            CooldownOf(target).MarkUsed(ctx.GameTime.now, CooldownTimeOf(target));

            return true;
        }

        private bool CanPreempt()
        {
            AttackPhaseState phased = Phased;

            return phased != null ? phased.Cancelable : Current == AttackStateTag.None;
        }

        private AttackStateTag BlackTarget(float vertical, bool grounded)
        {
            switch (AttackAim.Kind(vertical))
            {
                case BlackAttackKind.Up:
                    return AttackStateTag.BlackDashUp;
                case BlackAttackKind.Down:
                    return grounded ? AttackStateTag.None : AttackStateTag.BlackDashDown;
                default:
                    return AttackStateTag.FlatAttack;
            }
        }

        private Cooldown CooldownOf(AttackStateTag target)
        {
            switch (target)
            {
                case AttackStateTag.FlatAttack:
                    return _flatCooldown;
                case AttackStateTag.BlackDashUp:
                    return _dashUpCooldown;
                case AttackStateTag.BlackDashDown:
                    return _dashDownCooldown;
                default:
                    return _cooldown;
            }
        }

        private float CooldownTimeOf(AttackStateTag target)
        {
            switch (target)
            {
                case AttackStateTag.FlatAttack:
                    return _config.flatAttackCooldown;
                case AttackStateTag.BlackDashUp:
                    return _config.blackDashUpCooldown;
                case AttackStateTag.BlackDashDown:
                    return _config.blackDashDownCooldown;
                default:
                    return _config.attackCooldown;
            }
        }
    }
}
