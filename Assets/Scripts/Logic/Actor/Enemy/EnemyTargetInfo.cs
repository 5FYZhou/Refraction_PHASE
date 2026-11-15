using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>索敌结果：表现层每物理帧写入，逻辑层只读</summary>
    public readonly struct EnemyTargetInfo
    {
        public readonly bool Known;

        /// <summary>玩家在视线触发器里；出视线后位置仍有效</summary>
        public readonly bool Visible;

        public readonly Vector2 Pos;

        /// <summary>受击盒中心与半尺寸（世界坐标）</summary>
        public readonly Vector2 BodyCenter;

        public readonly Vector2 HalfSize;

        public EnemyTargetInfo(
            bool known,
            bool visible,
            Vector2 pos,
            Vector2 bodyCenter = default,
            Vector2 halfSize = default)
        {
            Known = known;
            Visible = visible;
            Pos = pos;
            BodyCenter = bodyCenter;
            HalfSize = halfSize;
        }
    }
}
