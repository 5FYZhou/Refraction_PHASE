namespace Refraction.Logic.Actor
{
    /// <summary>蓄力阶段：None 空闲，Charging 前摇中，Charged 蓄满等冲刺释放</summary>
    public enum ChargePhase
    {
        None = 0,
        Charging,
        Charged
    }
}
