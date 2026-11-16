namespace Refraction.Logic.Level
{
    /// <summary>随世界极性换色的场景物：表现层实现，自己在 Start 里调 Attach 拿 GameRoot 注册、OnDestroy 注销</summary>
    /// <remarks>Attach 必须幂等；LevelManager 还没建时由 GameRoot 暂存，建好后补一次当前极性</remarks>
    public interface IReverse
    {
        void Attach();

        void Apply(WorldPolarity world);
    }
}
