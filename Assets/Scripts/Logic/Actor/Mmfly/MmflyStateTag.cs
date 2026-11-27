namespace Refraction.Logic.Actor
{
    /// <summary>Boss mmfly 的独立状态：不复用小怪的巡逻 / 返回那套</summary>
    public enum MmflyStateTag
    {
        None = 0,

        /// <summary>未进战斗：原地悬浮</summary>
        Idle,

        /// <summary>等决策间隔，再从技能 / 观察 / 逼近里挑一个</summary>
        Decide,

        /// <summary>不主动贴近，只缓慢漂移</summary>
        Observe,

        /// <summary>朝玩家靠到理想距离</summary>
        Approach,

        Dive,

        Barrage,

        Summon,
    }
}
