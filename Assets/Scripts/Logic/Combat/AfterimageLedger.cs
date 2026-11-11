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

    /// <summary>残影账本：只按生成顺序存已落下的残影，上限、过期与连线段都由 AfterimageLogic 判</summary>
    public sealed class AfterimageLedger
    {
        private readonly List<Afterimage> _items = new(8);

        public int Count => _items.Count;

        public Afterimage this[int i] => _items[i];

        public void Add(in Afterimage item) => _items.Add(item);

        /// <summary>移除最早的一枚；调用方保证 Count &gt; 0</summary>
        public void RemoveFirst() => _items.RemoveAt(0);

        public void Clear() => _items.Clear();
    }
}
