namespace Refraction.Logic.Combat
{
    /// <summary>命中回报：伤害真的扣掉目标血量之后才回调</summary>
    /// <remarks>判据与 IHurtable.TakeDamage 的返回值同源——无敌窗内、目标已死、0 伤害都不回调，
    /// 所以可以放心当成"有效攻击"来用；只做伤害结算、不真心扣血的攻击者（如敌人接触伤害）不实现本接口</remarks>
    public interface IHitLanded
    {
        void OnHitLanded();
    }
}
