using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击状态组：None → 前摇 → 本体 → 后摇 → None；黑形态平劈走普攻、斜上 / 斜下走突进</summary>
    public sealed class AttackGroup : StateGroup<AttackStateTag, LogicContext>
    {
        protected override AttackStateTag EmptyTag => AttackStateTag.None;

        private readonly PlayerConfig _config;
        private readonly Cooldown _cooldown = new();
        private readonly WhiteAttackState _white;
        private readonly FlatAttackState _flat;
        private readonly BlackDashPrepareState _dashPrepare;
        private readonly BlackDashState _dash;
        private readonly BlackDashRecoverState _dashRecover;
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

            RegisterState(new FlatAttackPrepareState(config));
            _flat = new FlatAttackState(config, blackView, motor, posture);
            RegisterState(_flat);
            RegisterState(new FlatAttackRecoverState(config));

            _dashPrepare = new(config, motor);
            _dash = new(config, motor, blackView, conditions, _dashPrepare);
            _dashRecover = new(config);
            RegisterState(_dashPrepare);
            RegisterState(_dash);
            RegisterState(_dashRecover);

            blackView?.SetHitHandler(OnBlackHit);

            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);
        }

        public bool IsBlack => _black;

        /// <summary>前摇与本体压住移动组，连状态都不推进</summary>
        public bool BlocksMovement
            => Current == AttackStateTag.BlackDashPrepare || Current == AttackStateTag.BlackDash;

        /// <summary>后摇只挡跳跃与冲刺</summary>
        public bool BlocksActions => BlocksMovement || Current == AttackStateTag.BlackDashRecover;

        public void BindLauncher(IBulletLauncher launcher) => _white.Bind(launcher);

        public AttackStateTag Current => machine.CurrentState?.Tag ?? AttackStateTag.None;

        public void Tick(in LogicContext ctx)
        {
            // 前摇挨打整段作废
            if (Current == AttackStateTag.BlackDashPrepare && _conditions != null && _conditions.IsHurt)
                ChangeState(AttackStateTag.None, in ctx);

            TickStates(ctx);
        }

        public void Dispose() => EventBus<WorldPolarityChanged>.Unsubscribe(OnWorldPolarityChanged);

        private void OnWorldPolarityChanged(WorldPolarityChanged evt) => _black = evt.World == WorldPolarity.Black;

        private void OnBlackHit()
        {
            if (Current == AttackStateTag.FlatAttack) _flat.NotifyHit();
            else if (Current == AttackStateTag.BlackDash) _dash.NotifyHit();
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
                case AttackStateTag.FlatAttackPrepare:
                    return AttackStateTag.FlatAttack;
                case AttackStateTag.FlatAttack:
                    return _config.flatAttackRecoverTime > 0f
                        ? AttackStateTag.FlatAttackRecover
                        : AttackStateTag.None;
                case AttackStateTag.BlackDashPrepare:
                    return AttackStateTag.BlackDash;
                case AttackStateTag.BlackDash:
                    return _config.blackDashRecoverTime > 0f
                        ? AttackStateTag.BlackDashRecover
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
                target = BlackTarget(ctx.InputSnapshot.Move.y, ctx.WorldInfo.Grounded);

                return target != EmptyTag;
            }

            target = _config.attackPrepareTime > 0f
                ? AttackStateTag.WhiteAttackPrepare
                : AttackStateTag.WhiteAttack;

            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, AttackStateTag target)
        {
            bool dash = target == AttackStateTag.BlackDashPrepare;
            bool flat = target == AttackStateTag.FlatAttackPrepare || target == AttackStateTag.FlatAttack;

            _cooldown.MarkUsed(ctx.GameTime.now, dash
                ? _config.blackDashCooldown
                : flat ? _config.flatAttackCooldown : _config.attackCooldown);

            return true;
        }

        /// <summary>平劈走普攻；站在地上不给下突进</summary>
        private AttackStateTag BlackTarget(float vertical, bool grounded)
        {
            BlackAttackKind kind = AttackAim.Kind(vertical);

            if (kind == BlackAttackKind.Ahead)
                return _config.flatAttackPrepareTime > 0f ? AttackStateTag.FlatAttackPrepare : AttackStateTag.FlatAttack;

            if (kind == BlackAttackKind.Down && grounded) return AttackStateTag.None;

            return AttackStateTag.BlackDashPrepare;
        }
    }
}
