using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>角色逻辑基类：玩家与敌人共用一套固定帧序、条件组与血量</summary>
    /// <remarks>帧序 BeginStep → 条件组 → OnFixedTick → 重力 → Commit → ApplyGates</remarks>
    public abstract class ActorLogic : IFixedTickable, ITickable, IHurtable
    {
        private readonly IMoveMotor _motor;
        private readonly CharacterConfig _config;

        private int _curHealth;

        protected readonly ConditionGroup Conditions;

        public ActorLogic(IMoveMotor motor, CharacterConfig config)
        {
            _motor = motor;
            _config = config;
            Conditions = new ConditionGroup(motor, config);
            _curHealth = Mathf.Max(1, config.maxHealth);
        }

        public bool IsAlive => _curHealth > 0;

        public int CurHealth => _curHealth;

        public int MaxHealth => Mathf.Max(1, _config.maxHealth);

        public bool IsHurt => Conditions.IsHurt;

        /// <summary>受击入口：扣血当帧生效，无敌窗与击退交给条件组；返回是否真吃到伤害</summary>
        /// <remarks>子类覆盖后必须落到基类，绕过去血量再也不会变</remarks>
        public virtual bool TakeDamage(in Damage damage)
        {
            if (!IsAlive) return false;
            if (Conditions.IsInvulnerable) return false;

            int amount = Mathf.Max(0, Mathf.RoundToInt(damage.Amount));
            if (amount <= 0 && !damage.HasKnockback) return false;

            if (amount > 0) _curHealth = Mathf.Max(0, _curHealth - amount);

            Conditions.RegisterHit(in damage, amount > 0);

            return amount > 0;
        }

        /// <summary>血量回满，不动无敌窗与受伤态</summary>
        public void ResetHealth() => _curHealth = MaxHealth;

        public void FixedTick(LogicContext ctx)
        {
            _motor.BeginStep(ctx.GameTime.now, ctx.GameTime.deltaTime);
            Conditions.Tick(in ctx);
            OnFixedTick(in ctx);
            _motor.ApplyGravity(ctx);
            _motor.Commit();

            // 门禁写绝对速度，必须压在 Commit 之后：放前面会被本帧状态与重力盖掉
            _motor.ApplyGates();
        }

        public void Tick(LogicContext ctx)
        {
            OnTick(ctx);
        }

        protected abstract void OnFixedTick(in LogicContext ctx);

        protected abstract void OnTick(in LogicContext ctx);
    }
}
