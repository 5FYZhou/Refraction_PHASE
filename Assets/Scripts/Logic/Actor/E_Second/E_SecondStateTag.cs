namespace Refraction.Logic.Actor
{
    public enum E_SecondStateTag
    {
        None = 0,
        Patrol,
        Chase,
        Return,
        Attack,
    }

    /// <summary>攻击内部三段：前摇 → 攻击 → 后摇；后摇结束若还看得见就直接回前摇</summary>
    public enum E_SecondAttackPhase
    {
        Windup,
        Strike,
        Recover,
    }

    /// <summary>表现层换色用的展示态：三个状态 + 攻击的三个阶段，共六种</summary>
    public enum E_SecondVisual
    {
        Patrol,
        Chase,
        Return,
        Windup,
        Strike,
        Recover,
    }
}
