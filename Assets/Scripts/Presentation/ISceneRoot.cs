using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Presentation
{
    public static class SceneOrder
    {
        /// <summary>场景入口声明：其余场景根的 Attach 都要排在它之后</summary>
        public const int Entry = int.MinValue;

        /// <summary>玩家侧：必须早于世界侧，否则接触判定读上一帧站位</summary>
        public const int Player = -100;

        public const int World = 0;
    }

    /// <summary>场景级组合根：自己注册/注销</summary>
    public interface ISceneRoot
    {
        /// <summary>驱动顺序，小者先</summary>
        int Order { get; }

        /// <summary>GameRoot 调一次，必须幂等</summary>
        void Attach();
    }
}
