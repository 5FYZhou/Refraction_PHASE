using System;
using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

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
        private readonly Cooldown _dashLevelCooldown = new();
        private readonly WhiteAttackState _white;
        private readonly FlatAttackState _flat;
        private readonly BlackDashState _dashUp;
        private readonly BlackDashState _dashDown;
        private readonly BlackDashState _dashLevel;
        private readonly ConditionGroup _conditions;
        private readonly PostureGroup _posture;
        private readonly Func<bool> _isBlack;
        private int _airDash;
        private float _lastGroundedTime = float.NegativeInfinity;
        private bool _coyoteUsed;
        private bool _wasGrounded;

        public AttackGroup(PlayerConfig config, IStateMove motor, IBulletLauncher launcher, PostureGroup posture, IBlackAttackView blackView = null, ConditionGroup conditions = null, Func<bool> isBlack = null)
        {
            _config = config;
            _conditions = conditions;
            _posture = posture;
            _isBlack = isBlack;
            _airDash = config.blackDashAirCount;
            _white = new WhiteAttackState(config, motor, launcher, posture);

            RegisterState(new NoneAttackState());
            RegisterState(new WhiteAttackPrepareState(config));
            RegisterState(_white);
            RegisterState(new WhiteAttackRecoverState(config));

            _flat = new FlatAttackState(config, blackView, motor, posture);
            _dashUp = new BlackDashState(config, motor, blackView, conditions, AttackStateTag.BlackDashUp, BlackAttackKind.Up);
            _dashDown = new BlackDashState(config, motor, blackView, conditions, AttackStateTag.BlackDashDown, BlackAttackKind.Down);
            _dashLevel = new BlackDashState(config, motor, blackView, conditions, AttackStateTag.BlackDashLevel, BlackAttackKind.Level);

            RegisterState(_flat);
            RegisterState(_dashUp);
            RegisterState(_dashDown);
            RegisterState(_dashLevel);

            blackView?.SetHitHandler(OnBlackHit);
        }

        public bool IsBlack => _isBlack != null && _isBlack();

        /// <summary>前摇与本体压住移动组，连状态都不推进</summary>
        public bool BlocksMovement => Phased != null && Phased.BlocksMovement;

        /// <summary>整段（含后摇）都挡跳跃与冲刺</summary>
        public bool BlocksActions => Phased != null && Phased.BlocksActions;

        /// <summary>正在后摇：别的攻击可以顶掉它</summary>
        public bool Cancelable => Phased != null && Phased.Cancelable;

        /// <summary>地图打开：不接新攻击</summary>
        public bool Locked { get; set; }

        public bool HoldsMoveFall => Phased is BlackDashState && Phased.Tag != AttackStateTag.BlackDashLevel && Phased.BlocksMovement;

        public void BindLauncher(IBulletLauncher launcher) => _white.Bind(launcher);

        public void BindMuzzle(Vector2 stand, Vector2 crouch) => _white.BindMuzzle(stand, crouch);

        public AttackStateTag Current => machine.CurrentState?.Tag ?? AttackStateTag.None;

        private AttackPhaseState Phased => machine.CurrentState as AttackPhaseState;

        public void Tick(in LogicContext ctx)
        {
            TrackGround(in ctx);

            AbortOnHurt(in ctx);

            Phased?.Advance(in ctx);

            TickStates(ctx);
        }

        private void TrackGround(in LogicContext ctx)
        {
            bool grounded = ctx.WorldInfo.Grounded;

            if (grounded)
            {
                _airDash = _config.blackDashAirCount;
                _lastGroundedTime = ctx.GameTime.now;

                if (!_wasGrounded) _coyoteUsed = false;
            }

            _wasGrounded = grounded;
        }

        private void AbortOnHurt(in LogicContext ctx)
        {
            if (_conditions == null || !_conditions.IsHurt) return;

            AttackPhaseState phased = Phased;

            if (phased == null || !phased.InPrepare || !phased.AbortsOnHurt) return;

            ChangeState(AttackStateTag.None, in ctx);
        }

        private void OnBlackHit()
        {
            switch (Current)
            {
                case AttackStateTag.FlatAttack:
                    _flat.NotifyHit();
                    break;
                case AttackStateTag.BlackDashUp:
                    _dashUp.NotifyHit();
                    RefundAirDash(_config.blackDashHitRefund);
                    break;
                case AttackStateTag.BlackDashDown:
                    _dashDown.NotifyHit();
                    RefundAirDash(_config.blackDashHitRefund);
                    break;
                case AttackStateTag.BlackDashLevel:
                    _dashLevel.NotifyHit();
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

            if (Locked) return false;

            if (_conditions != null && _conditions.IsHurt) return false;
            if (!ctx.InputSnapshot.Attack) return false;
            if (!CanPreempt()) return false;

            if (!IsBlack)
            {
                if (!_cooldown.CanUse(ctx.GameTime.now)) return false;

                target = _config.attackPrepareTime > 0f
                    ? AttackStateTag.WhiteAttackPrepare
                    : AttackStateTag.WhiteAttack;

                return true;
            }

            target = BlackTarget(ctx.InputSnapshot.Move.y, GroundLike(in ctx), Crouching);

            return target != EmptyTag && CooldownOf(target).CanUse(ctx.GameTime.now)
                && (target == AttackStateTag.FlatAttack || target == AttackStateTag.BlackDashLevel
                    || GroundLike(in ctx) || _airDash > 0);
        }

        private bool Crouching => _posture != null && _posture.IsCrouching;

        private bool GroundLike(in LogicContext ctx)
        {
            if (ctx.WorldInfo.Grounded) return true;

            return !_coyoteUsed && ctx.GameTime.now - _lastGroundedTime <= _config.blackDashCoyoteTime;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, AttackStateTag target)
        {
            CooldownOf(target).MarkUsed(ctx.GameTime.now, CooldownTimeOf(target));

            bool dashStart = target == AttackStateTag.BlackDashUp || target == AttackStateTag.BlackDashDown;

            if (dashStart && !GroundLike(in ctx)) _airDash--;
            if (dashStart && !ctx.WorldInfo.Grounded) _coyoteUsed = true;

            return true;
        }

        public int AirDash => _airDash;

        public void RefundAirDash(int count)
        {
            if (count <= 0) return;

            int cap = _config.blackDashAirCount;
            int next = _airDash + count;

            _airDash = next < cap ? cap : next;
        }

        private bool CanPreempt()
        {
            AttackPhaseState phased = Phased;

            return phased != null ? phased.Cancelable : Current == AttackStateTag.None;
        }

        private AttackStateTag BlackTarget(float vertical, bool groundLike, bool crouching)
        {
            if (crouching) return AttackStateTag.BlackDashLevel;

            switch (AttackAim.Kind(vertical))
            {
                case BlackAttackKind.Up:
                    return AttackStateTag.BlackDashUp;
                case BlackAttackKind.Down:
                    return groundLike ? AttackStateTag.None : AttackStateTag.BlackDashDown;
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
                case AttackStateTag.BlackDashLevel:
                    return _dashLevelCooldown;
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
                case AttackStateTag.BlackDashLevel:
                    return _config.blackDashLevelCooldown;
                default:
                    return _config.attackCooldown;
            }
        }
    }
}
