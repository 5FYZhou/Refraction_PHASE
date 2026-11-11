using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>一枚残影：位置、落地时刻与生成序号</summary>
    public readonly struct Afterimage
    {
        public readonly Vector2 Pos;

        public readonly float SpawnTime;

        /// <summary>&lt;= 0 表示不按时间消失</summary>
        public readonly float Duration;

        /// <summary>单调递增，Clear 不归零，表现层靠它认出换新</summary>
        public readonly int Index;

        public Afterimage(Vector2 pos, float spawnTime, float duration, int index)
        {
            Pos = pos;
            SpawnTime = spawnTime;
            Duration = duration;
            Index = index;
        }

        public bool IsExpired(float now) => Duration > 0f && now - SpawnTime >= Duration;
    }

    /// <summary>残影账本：位置与存活时间，连线段按生成顺序切</summary>
    /// <remarks>末枚接回首枚；超出感应范围的相邻对不连</remarks>
    public sealed class AfterimageLedger
    {
        private readonly List<Afterimage> _items = new(8);

        private int _nextIndex;

        public int Count => _items.Count;

        public Afterimage this[int i] => _items[i];

        /// <summary>连线段数：不足两枚为 0，两枚为 1</summary>
        public int LinkCapacity => _items.Count < 2 ? 0 : _items.Count == 2 ? 1 : _items.Count;

        /// <summary>落一枚新残影；满了顶掉最早的</summary>
        public void Place(Vector2 pos, float now, int max, float duration)
        {
            if (max < 1) return;

            while (_items.Count >= max) _items.RemoveAt(0);

            _items.Add(new Afterimage(pos, now, duration, _nextIndex++));
        }

        /// <summary>按时间清过期的；只按生成顺序过期，扫到活的即停</summary>
        public void Prune(float now)
        {
            while (_items.Count > 0 && _items[0].IsExpired(now)) _items.RemoveAt(0);
        }

        /// <summary>取第 i 段连线；超出感应范围返回 false</summary>
        /// <remarks>senseRadius &lt;= 0 不限距离；两端重合也返回 false</remarks>
        public bool TryGetLink(int i, float senseRadius, out Vector2 from, out Vector2 to)
        {
            from = default;
            to = default;

            if (i < 0 || i >= LinkCapacity) return false;

            from = _items[i].Pos;
            to = _items[(i + 1) % _items.Count].Pos;

            float sqr = (to - from).sqrMagnitude;
            if (sqr <= 1e-6f) return false;

            return senseRadius <= 0f || sqr <= senseRadius * senseRadius;
        }

        public void Clear() => _items.Clear();
    }
}
