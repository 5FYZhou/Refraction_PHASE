using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>条件组：玩家与敌人共用一套，生命、受击无敌与击退都归它，每帧把门禁交给移动层</summary>
    /// <remarks>扣血在挨打当帧就落地（调用方要能立刻问 IsAlive），击退排到下一个物理帧，门禁在该帧末统一覆盖速度</remarks>
    public sealed class ConditionGroup : StateGroup<ConditionStateTag, LogicContext>
    {
        private readonly CharacterConfig _config;
        private readonly IMoveMotor _motor;
        private readonly HurtState _hurt;

        private Damage _pending;
        private bool _hasPending;
        private float _now;
        private float _invulnerableUntil;
        private int _curHealth;

        public ConditionGroup(IMoveMotor motor, CharacterConfig config)
        {
            _motor = motor;
            _config = config;
            _hurt = new HurtState(config);

            RegisterState(new NormalState());
            RegisterState(_hurt);

            _curHealth = Mathf.Max(1, config.maxHealth);
        }

        protected override ConditionStateTag EmptyTag => ConditionStateTag.Normal;

        protected override ConditionStateTag Fallback(in LogicContext ctx) => ConditionStateTag.Normal;

        public ConditionStateTag Current =>
            machine.CurrentState == null ? ConditionStateTag.None : machine.CurrentState.Tag;

        public bool IsHurt => Current == ConditionStateTag.Hurt;

        /// 受伤期接管速度（方向 × 速率），非受伤态为零
        public Vector2 HurtVelocity => _hurt.ForcedVelocity;

        public bool IsAlive => _curHealth > 0;

        public int CurHealth => _curHealth;

        public int MaxHealth => Mathf.Max(1, _config.maxHealth);

        /// <summary>受击入口：无敌窗内整下吞掉。返回 false 表示没吃到，血量与击退都没变</summary>
        public bool TryApply(in Damage damage)
        {
            if (!IsAlive) return false;
            if (_now < _invulnerableUntil) return false;

            if (damage.HasDamage)
            {
                _curHealth = Mathf.Max(0, _curHealth - Mathf.RoundToInt(damage.Amount));

                // 只有真掉血才进无敌：0 伤害的纯效果（击退、标记）不该把下一次真伤害挡掉
                if (_config.invulnerableTime > 0f) _invulnerableUntil = _now + _config.invulnerableTime;
            }

            if (damage.HasKnockback)
            {
                _pending = damage;
                _hasPending = true;
            }

            return true;
        }

        /// <summary>复活：血量回满、清无敌与排队击退，回常态。位置由调用方决定</summary>
        public void Revive()
        {
            _curHealth = MaxHealth;
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
