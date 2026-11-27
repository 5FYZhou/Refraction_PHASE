namespace Refraction.Logic.Actor
{
    /// <summary>技能内部三段：前摇 → 攻击 → 后摇；各技能的时长来源不同，相位含义一致</summary>
    public enum MmflySkillPhase
    {
        None = 0,

        Windup,

        Strike,

        Recover,
    }
}
