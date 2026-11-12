namespace Refraction.Logic.Combat
{
    /// <summary>持续型攻击者：贴着不松手也按自己的间隔反复造成伤害</summary>
    /// <remarks>ReadyToHit 只回答能否结算，命中成功后由调用方 MarkHit 才真正进冷却；被无敌窗挡掉的那次不消耗间隔</remarks>
    public interface IIntervalAttacker
    {
        bool ReadyToHit(float now);

        void MarkHit(float now);
    }
}
