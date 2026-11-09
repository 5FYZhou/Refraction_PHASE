namespace Refraction.Logic.Actor
{
    public enum EnemyStateTag
    {
        None = 0,
        Patrol,
        Chase,
        Attack
    }

    /// <summary>攻击内部的三段：前摇 → 冲刺 → 后摇，只在 EnemyAttackState 内部使用</summary>
    public enum EnemyAttackPhase
    {
        Windup,
        Dash,
        Recovery
    }
}
