namespace Refraction.Logic.Actor
{
    /// <summary>攻击内部三段：前摇 → 攻击 → 后摇；后摇结束若还看得见就直接回前摇</summary>
    public enum E_SecondAttackPhase
    {
        Windup,
        Strike,
        Recover,
    }
}
