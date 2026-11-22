namespace Refraction.Logic.Actor
{
    /// <summary>攻击内部三段：前摇 → 攻击 → 后摇；黑白共用，只是每段里的动作不同</summary>
    public enum SpliterAttackPhase
    {
        Windup,
        Strike,
        Recover,
    }
}
