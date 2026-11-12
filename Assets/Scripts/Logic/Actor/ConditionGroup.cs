using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>条件组：玩家与敌人共用，受击无敌与击退归它，每帧把门禁交给移动层</summary>
    /// <remarks>血量在 ActorLogic，这里只管状态与门禁；击退排到下一个物理帧</remarks>
    public sealed class ConditionGroup : StateGroup<ConditionStateTag, LogicContext>
    {
        private readonly CharacterConfig _config;
        private readonly IMoveMotor _motor;
        private readonly HurtState _hurt;

        private Damage _pending;
        private bool _hasPending;
        private float _now;
        private float _invulnerableUntil;

        public ConditionGroup(IMoveMotor motor, CharacterConfig config)
        {
            _motor = motor;
            _config = config;
            _hurt = new HurtState(config);

            RegisterState(new NormalState());
            RegisterState(_hurt);
        }

        protected override ConditionStateTag EmptyTag => ConditionStateTag.Normal;

        protected override ConditionStateTag Fallback(in LogicContext ctx) => ConditionStateTag.Normal;

        public ConditionStateTag Current =>
            machine.CurrentState == null ? ConditionStateTag.None : machine.CurrentState.Tag;

        public bool IsHurt => Current == ConditionStateTag.Hurt;

        public bool IsInvulnerable => _now < _invulnerableUntil;

        /// 受伤期接管速度（方向 × 速率），非受伤态为零
        public Vector2 HurtVelocity => _hurt.ForcedVelocity;

        /// <summary>受击登记：只有真掉血才开无敌窗（纯击退不算），击退排到下一个固定帧</summary>
        public void RegisterHit(in Damage damage, bool damaged)
        {
            if (damaged && _config.invulnerableTime > 0f) _invulnerableUntil = _now + _config.invulnerableTime;

            if (!damage.HasKnockback) return;

            _pending = damage;
            _hasPending = true;
        }

        /// <summary>清掉无敌窗与排队中的击退，回常态；血量归 ActorLogic 管</summary>
        public void ClearHurt()
        {
            _invulnerableUntil = 0f;
            _hasPending = false;
            ChangeState(ConditionStateTag.Normal, default);
        }

        /// <summary>每物理帧：先结算排队的击退，再推进状态，最后提交本帧门禁</summary>
        public void Tick(in LogicContext ctx)
        {
            _now = ctx.GameTime.now;

            ApplyPending(in ctx);

            TickStates(in ctx);

            _motor.CommitGates(BuildGates(in ctx));
        }

        private void ApplyPending(in LogicContext ctx)
        {
            if (!_hasPending) return;

            _hasPending = false;

            _hurt.Configure(_pending.Direction * _pending.KnockbackSpeed);
            ChangeState(ConditionStateTag.Hurt, in ctx);
        }

        /// <summary>受伤期间整条接管速度（含竖直），其它状态这帧写什么都盖掉；没在受伤就不提交，等于没有门禁</summary>
        /// <remarks>纯横向击退不接管竖直：缓存的竖直速度是这一帧唯一的"下落"来源</remarks>
        private MoveGates BuildGates(in LogicContext ctx)
        {
            if (!IsHurt) return MoveGates.None;

            Vector2 velocity = _hurt.ForcedVelocity;

            if (Mathf.Abs(velocity.y) < 0.01f) velocity.y = ctx.WorldInfo.FrameStartVelocity.y;

            return MoveGates.Forced(velocity);
        }
    }
}
