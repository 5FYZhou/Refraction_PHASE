using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>巡逻范围：世界坐标的 x 区间；无界表示整平台随便走</summary>
    public readonly struct E_FirstArea
    {
        public readonly bool HasBounds;

        public readonly float MinX;

        public readonly float MaxX;

        public E_FirstArea(bool hasBounds, float minX, float maxX)
        {
            HasBounds = hasBounds;
            MinX = Mathf.Min(minX, maxX);
            MaxX = Mathf.Max(minX, maxX);
        }

        public bool ContainsX(float x) => x >= MinX && x <= MaxX;
    }
}
