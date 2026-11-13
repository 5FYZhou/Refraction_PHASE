namespace Refraction.Logic.Actor
{
    /// <summary>巡逻范围：由表现层的巡逻框给出，无界时只靠墙与悬崖掉头</summary>
    public readonly struct EnemyPatrolArea
    {
        public readonly bool HasBounds;

        /// <summary>左边界（世界 x）</summary>
        public readonly float MinX;

        /// <summary>右边界（世界 x）</summary>
        public readonly float MaxX;

        public EnemyPatrolArea(bool hasBounds, float minX, float maxX)
        {
            HasBounds = hasBounds;
            MinX = minX;
            MaxX = maxX;
        }
    }
}
