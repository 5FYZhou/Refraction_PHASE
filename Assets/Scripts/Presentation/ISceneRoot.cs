using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation
{
    public static class SceneOrder
    {
        /// <summary>最先装配：其余场景根的 Attach 都排在它之后</summary>
        public const int Entry = int.MinValue;

        /// <summary>玩家必须先于世界侧：接触判定要用本帧站位，反过来会读上一帧</summary>
        public const int Player = -100;

        public const int World = 0;
    }

    /// <summary>场景级组合根：自己向 GameRoot 注册与注销，Attach 由驱动帧统一调用</summary>
    public interface ISceneRoot
    {
        /// <summary>装配顺序，小者先；同序按注册先后</summary>
        int Order { get; }

        /// <summary>装配入口，必须幂等：再调一次不能重建逻辑或叠加订阅</summary>
        void Attach();
    }

    /// <summary>渲染帧驱动：只许更新表现，逻辑与速度都归 FixedTick</summary>
    public interface IRenderTicked
    {
        /// <summary>渲染帧步长，低帧率下远大于固定步长，不能当物理 Δt 用</summary>
        void RenderTick(float deltaTime);
    }

    /// <summary>物理帧驱动：固定步长，逻辑推进只在这里做</summary>
    public interface IPhysicsTicked
    {
        void FixedTick(float deltaTime);
    }
}
