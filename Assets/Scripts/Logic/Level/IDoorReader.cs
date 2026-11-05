using UnityEngine;

namespace Refraction.Logic.Level
{
    /// <summary>
    /// 当前关卡里门的只读索引：按门 id 取落点。
    /// 逻辑层通过它定位门，不引用表现层的 Door 组件。
    ///
    /// 一扇门由「关卡 id + 门 id」唯一确定，读取器自己也绑在某一关卡上，
    /// 所以不同关卡里同号的门不会互相串。
    /// </summary>
    public interface IDoorReader
    {
        /// <summary>本读取器绑定的关卡 id。0 表示关卡无效，不会有任何门被索引到。</summary>
        int LevelId { get; }

        /// <summary>
        /// 取门 id 对应的落点（门自己的 spawnAnchor 优先，没配就用门的位置）。
        /// 返回 false 表示本关没有这扇门——调用方必须原地不动，不能拿 (0,0) 兜底。
        /// 命中时会顺带屏蔽该门的进入触发一小段时间，因为调用方接下来就把玩家放进它的触发器里了。
        /// </summary>
        bool TryGetPos(int doorId, out Vector2 position);
    }
}
