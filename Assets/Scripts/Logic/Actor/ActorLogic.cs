using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>角色逻辑基类：玩家与敌人共用一套固定帧序与条件组</summary>
    /// <remarks>帧序 BeginStep → 条件组 → OnFixedTick → 重力 → Commit → ApplyGates；子类只在 OnFixedTick 里提交速度，写刚体由 Commit 一次完成</remarks>
    public abstract class ActorLogic : IFixedTickable, ITickable, IHurtable
    {
        private readonly IMoveMotor _motor;

        protected readonly ConditionGroup Conditions;

        public ActorLogic(IMoveMotor motor, CharacterConfig config)
        {
            _motor = motor;
            Conditions = new ConditionGroup(motor, config);
        }

        public bool IsAlive => Conditions.IsAlive;

        public int CurHealth => Conditions.CurHealth;

        public int MaxHealth => Conditions.MaxHealth;

        public bool IsHurt => Conditions.IsHurt;

        /// <summary>受击入口：扣血与无敌窗当帧生效，击退排到下一个固定帧</summary>
        /// <remarks>子类覆盖后必须落到 Conditions.TryApply，绕过去血量再也不会变</remarks>
        public virtual bool TakeDamage(in Damage damage)
        {
            int before = Conditions.CurHealth;
            Conditions.TryApply(in damage);
            return Conditions.CurHealth < before;
        }

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
