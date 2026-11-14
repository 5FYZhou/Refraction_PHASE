using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>活动范围框：世界坐标的中点 + 半宽；巡逻与追击各用一份</summary>
    public readonly struct E_SecondArea
    {
        public readonly bool HasBounds;

        public readonly Vector2 Center;

        public readonly float HalfWidth;

        public E_SecondArea(bool hasBounds, Vector2 center, float halfWidth)
        {
            HasBounds = hasBounds;
            Center = center;
            HalfWidth = Mathf.Max(0f, halfWidth);
        }

        public float MinX => Center.x - HalfWidth;

        public float MaxX => Center.x + HalfWidth;

        public bool Contains(float x) => !HasBounds || (x >= MinX && x <= MaxX);
    }
}
