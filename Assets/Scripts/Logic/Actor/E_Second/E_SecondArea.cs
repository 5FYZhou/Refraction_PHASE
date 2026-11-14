using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>活动范围框：世界坐标的中点与半尺寸；巡逻只用 x，追击 x / y 都判</summary>
    public readonly struct E_SecondArea
    {
        public readonly bool HasBounds;

        public readonly Vector2 Center;

        public readonly Vector2 HalfSize;

        public E_SecondArea(bool hasBounds, Vector2 center, Vector2 halfSize)
        {
            HasBounds = hasBounds;
            Center = center;
            HalfSize = new Vector2(Mathf.Max(0f, halfSize.x), Mathf.Max(0f, halfSize.y));
        }

        public float MinX => Center.x - HalfSize.x;

        public float MaxX => Center.x + HalfSize.x;

        public float MinY => Center.y - HalfSize.y;

        public float MaxY => Center.y + HalfSize.y;

        /// <summary>只看横向：巡逻是同一水平线上的往返，高度不影响掉头</summary>
        public bool ContainsX(float x) => !HasBounds || (x >= MinX && x <= MaxX);

        /// <summary>横向与纵向都要在框内；半高 ≤ 0 表示不看纵向，等价于只管 x</summary>
        public bool Contains(Vector2 pos) =>
            !HasBounds
            || (ContainsX(pos.x) && (HalfSize.y <= 0f || (pos.y >= MinY && pos.y <= MaxY)));
    }
}
