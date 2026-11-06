using UnityEngine;

namespace Refraction.Logic.Level
{
    /// <summary>目标门的进门口数据；纯值，门销毁后仍然有效</summary>
    public readonly struct DoorEntry
    {
        /// <summary>门自身的位置，玩家脚底落到这里</summary>
        public readonly Vector2 Point;

        /// <summary>门朝向（单位向量），由 rotation.z 换算</summary>
        public readonly Vector2 Facing;

        public readonly Vector2 LeftLanding;
        public readonly Vector2 RightLanding;
        public readonly bool HasLeft;
        public readonly bool HasRight;

        public DoorEntry(Vector2 point, Vector2 facing, Vector2 left, bool hasLeft, Vector2 right, bool hasRight)
        {
            Point = point;
            Facing = facing;
            LeftLanding = left;
            RightLanding = right;
            HasLeft = hasLeft;
            HasRight = hasRight;
        }
    }
}
