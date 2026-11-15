using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Events;
using Refraction.Logic.Level;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>攻击状态组：None → 前摇 → 本体 → 后摇 → None，黑形态按配置走劈砍或突进</summary>
    /// <remarks>排在移动组之后：开火方向用本帧朝向；反转不打断进行中的一套；订阅必须 Dispose</remarks>
    public sealed class AttackGroup : StateGroup<AttackStateTag, LogicContext>
    {
        protected override AttackStateTag EmptyTag => AttackStateTag.None;

        private readonly PlayerConfig _config;
        private readonly Cooldown _cooldown = new();
        private readonly WhiteAttackState _white;
        private readonly BlackAttackState _blackState;
        private readonly BlackDashPrepareState _dashPrepare;
        private readonly BlackDashState _dash;
        private readonly BlackDashRecoverState _dashRecover;
        private readonly ConditionGroup _conditions;
        private bool _black;
        private bool _attackHeld;
        private bool _attackPressed;

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

            _dashPrepare = new(config, motor);
            _dash = new(config, motor, blackView, conditions, _dashPrepare);
            _dashRecover = new(config, motor);
            RegisterState(_dashPrepare);
            RegisterState(_dash);
            RegisterState(_dashRecover);

            blackView?.SetHitHandler(_blackState.NotifyHit);

            EventBus<WorldPolarityChanged>.Subscribe(OnWorldPolarityChanged);
        }

        public bool IsBlack => _black;

        public bool BlocksMovement
        {
            get
            {
                switch (Current)
                {
                    case AttackStateTag.BlackDashPrepare:
                    case AttackStateTag.BlackDash:
                    case AttackStateTag.BlackDashRecover:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public void BindLauncher(IBulletLauncher launcher) => _white.Bind(launcher);

        public AttackStateTag Current => machine.CurrentState?.Tag ?? AttackStateTag.None;

        public void Tick(in LogicContext ctx)
        {
            _attackPressed = ctx.InputSnapshot.Attack && !_attackHeld;
            _attackHeld = ctx.InputSnapshot.Attack;

            // 前摇挨打整段作废
            if (Current == AttackStateTag.BlackDashPrepare && _conditions != null && _conditions.IsHurt)
                ChangeState(AttackStateTag.None, in ctx);

            TickStates(ctx);
        }

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

            if (_conditions != null && _conditions.IsHurt) return false;

            // 后摇再按一次取消后摇，这条不吃冷却，否则连不起来
            if (Current == AttackStateTag.BlackDashRecover)
            {
                if (!_attackPressed || !_config.useBlackDash) return false;

                target = DashTarget();
                return true;
            }

            if (Current != AttackStateTag.None) return false;
            if (!ctx.InputSnapshot.Attack) return false;
            if (!_cooldown.CanUse(ctx.GameTime.now)) return false;

            if (_black)
            {
                target = _config.useBlackDash ? DashTarget() : BlackAttackTarget();
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
            if (target == AttackStateTag.BlackDashPrepare || target == AttackStateTag.BlackDash)
            {
                // 起新一段前清掉上一段锁的方向（前摇 0 时按当帧输入取）
                _dashPrepare.Clear();
                _cooldown.MarkUsed(ctx.GameTime.now, _config.blackDashCooldown);
                return true;
            }

            _cooldown.MarkUsed(ctx.GameTime.now, _config.attackCooldown);
            return true;
        }

        private AttackStateTag DashTarget()
            => _config.blackDashPrepareTime > 0f ? AttackStateTag.BlackDashPrepare : AttackStateTag.BlackDash;

        private AttackStateTag BlackAttackTarget()
            => _config.blackAttackPrepareTime > 0f ? AttackStateTag.BlackAttackPrepare : AttackStateTag.BlackAttack;
    }
}
