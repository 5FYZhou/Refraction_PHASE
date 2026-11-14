using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>本帧的玩家信息：Known 为 false 表示场景里没有玩家，Pos 无意义</summary>
    public readonly struct E_SecondTargetInfo
    {
        public readonly bool Known;

        /// <summary>玩家在视线触发器里；出门后 Known 与位置仍在，领地判定还要用</summary>
        public readonly bool Visible;

        public readonly Vector2 Pos;

        /// <summary>受击盒的中心与半尺寸（世界坐标）：枢轴在脚下，冲刺撞人的几何判定要用盒子</summary>
        public readonly Vector2 BodyCenter;

        public readonly Vector2 HalfSize;

        public E_SecondTargetInfo(bool known, bool visible, Vector2 pos, Vector2 bodyCenter, Vector2 halfSize)
        {
            Known = known;
            Visible = visible;
            Pos = pos;
            BodyCenter = bodyCenter;
            HalfSize = halfSize;
        }
    }
}
