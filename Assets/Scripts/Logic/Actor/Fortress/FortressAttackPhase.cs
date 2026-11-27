namespace Refraction.Logic.Actor
{
    /// <summary>弹幕内部三段：前摇 → 多波散射 → 后摇；三段都原地不动</summary>
    public enum FortressAttackPhase
    {
        Windup,

        Strike,

        Recover,
    }
}
