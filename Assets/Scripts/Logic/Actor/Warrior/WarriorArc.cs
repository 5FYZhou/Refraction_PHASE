using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>扑击抛物线：x 匀速从起点拉到终点，y 再叠一条 4·apex·s(1-s) 的拱形，s ∈ [0,1]，顶点在 s = 0.5</summary>
    public readonly struct WarriorArc
    {
        private readonly Vector2 _start;
        private readonly Vector2 _end;
        private readonly float _apex;

        public WarriorArc(Vector2 start, Vector2 end, float apex)
        {
            _start = start;
            _end = end;
            _apex = apex;
        }

        /// <summary>顶点参数：白形态到这里就悬停</summary>
        public const float ApexS = 0.5f;

        /// <summary>对 s 求导：推进速度要按它的模长折算成弧长，否则陡的地方会突然窜出去</summary>
        public Vector2 TangentAt(float s)
        {
            Vector2 span = _end - _start;

            return new Vector2(span.x, span.y + 4f * _apex * (1f - 2f * s));
        }
    }
}
