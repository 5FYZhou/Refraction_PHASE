namespace Refraction.Logic.Actor
{
    public enum E_FirstStateTag
    {
        None = 0,
        Patrol,
        Attack,
    }

    /// <summary>攻击内部三段：前摇 → 攻击 → 后摇；后摇结束还看得见就直接回前摇</summary>
    public enum E_FirstAttackPhase
    {
        Windup,
        Strike,
        Recover,
    }

    /// <summary>表现层换色用的展示态：两个状态 + 攻击三段，黑白各一套色</summary>
    public enum E_FirstVisual
    {
        Patrol,
        Windup,
        Strike,
        Recover,
    }
}
