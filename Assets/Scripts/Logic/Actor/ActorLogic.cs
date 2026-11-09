using Refraction.Data;
using Refraction.Logic.Combat;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>角色逻辑基类：固定帧序 + 条件组，玩家与敌人共用</summary>
    public abstract class ActorLogic : IFixedTickable, ITickable, IAttackable, IAlivable
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

        /// <summary>受击唯一入口：扣血与无敌窗当帧生效，击退在下一个固定帧起步</summary>
        public virtual void TakeDamage(in Damage damage) => Conditions.TryApply(in damage);

        public void FixedTick(LogicContext ctx)
        {
            _motor.BeginStep(ctx.GameTime.now, ctx.GameTime.deltaTime);
            Conditions.Tick(in ctx);
            OnFixedTick(in ctx);
            _motor.ApplyGravity(ctx);
            _motor.Commit();

            // 门禁写绝对速度，必须压在 Commit 后面，否则会被本帧状态与重力盖回去
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
