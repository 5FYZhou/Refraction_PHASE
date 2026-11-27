namespace Refraction.Logic.Actor
{
    /// <summary>Boss 的小怪场：逻辑层只问存活数与要几只，落点避墙与预制体归表现层</summary>
    public interface IMinionHost
    {
        /// <summary>场上还活着的小怪数（只算这个 Boss 召出来的）</summary>
        int AliveCount { get; }

        /// <summary>有没有可用的预制体：没配就整条技能都不该进池</summary>
        bool Available { get; }

        void Summon(int count);
    }
}
