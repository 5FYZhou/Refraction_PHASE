namespace Refraction.Logic.Level
{
    /// <summary>随世界极性换色的场景物：表现层实现，在 Start 里调 Attach 向关卡根登记、OnDestroy 注销</summary>
    /// <remarks>Attach 必须幂等；关卡根还没装配时由它暂存，装配好后补一次当前极性；没有关卡根的场景直接忽略</remarks>
    public interface IReverse
    {
        void Attach();

        void Apply(WorldPolarity world);
    }
}
