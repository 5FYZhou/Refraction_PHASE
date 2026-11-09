using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>本物理帧的索敌结果：由表现层的探测触发器写入，逻辑层只读</summary>
    public readonly struct EnemyTargetInfo
    {
        /// <summary>玩家是否在探测范围内；false 时 Pos 不可用</summary>
        public readonly bool Visible;

        public readonly Vector2 Pos;

        public EnemyTargetInfo(bool visible, Vector2 pos)
        {
            Visible = visible;
            Pos = pos;
        }
    }
}
