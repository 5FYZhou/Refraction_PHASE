using UnityEngine;

namespace Refraction.Logic.Combat
{
    /// <summary>残影生成入口：上层只报时机与位置，这一帧到底生不生成由残影系统自己判断</summary>
    public interface IAfterimageSpawner
    {
        /// <summary>充能走满，下一次 TrySpawn 才允许落影</summary>
        void Arm();

        /// <summary>冲刺起手时请求落影；返回 true 表示真的落了，调用方据此起冷却</summary>
        bool TrySpawn(Vector2 pos, float now);

        /// <summary>清空残影与待释放状态，重生与跨场景用</summary>
        void Clear();
    }
}
