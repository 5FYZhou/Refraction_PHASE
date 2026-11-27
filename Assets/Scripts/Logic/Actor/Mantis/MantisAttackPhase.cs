namespace Refraction.Logic.Actor
{
    /// <summary>攻击内部三段：前摇 → 快速位移 → 后摇；黑白共用相位，动作与时长各走各的</summary>
    public enum MantisAttackPhase
    {
        Windup,

        Dash,

        Recover,
    }
}
