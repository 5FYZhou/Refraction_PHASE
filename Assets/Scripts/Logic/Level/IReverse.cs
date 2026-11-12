namespace Refraction.Logic.Level
{
    /// <summary>随世界极性换色的场景物：表现层实现，装配时由 CombatRoot 登记到 LevelManager</summary>
    public interface IReverse
    {
        void Apply(WorldPolarity world);
    }
}
