namespace Refraction.Logic.Actor
{
    /// <summary>攻击内部阶段：黑走前摇 → 扑击 → 后摇，白走后跃 → 悬停；白的悬停结束就收手，没有前后摇</summary>
    public enum WarriorAttackPhase
    {
        Windup,
        Pounce,
        Recover,
        Leap,
        Hover,
    }
}
