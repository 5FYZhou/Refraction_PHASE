using UnityEngine;

namespace Refraction.Logic.Level
{
    /// <summary>当前关卡的门索引：按门 id 取落点；逻辑层用它定位门，不引用表现层 Door</summary>
    /// <remarks>读取器绑在某一关卡上，门由「关卡 id + 门 id」唯一确定，不同关卡的同号门不会串</remarks>
    public interface IDoorReader
    {
        /// <summary>绑定的关卡 id；0 表示关卡无效，不会有任何门被索引到</summary>
        int LevelId { get; }

        /// <summary>取门 id 的落点（门 spawnAnchor 优先，没配则用门的位置）；返回 false 表示本关没这扇门，调用方必须原地不动，不能拿 (0,0) 兜底</summary>
        /// <remarks>命中会顺带屏蔽该门的进入触发一小段时间，因为调用方随即把玩家放进它的触发器</remarks>
        bool TryGetPos(int doorId, out Vector2 position);
    }
}
