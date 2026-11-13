using System;

namespace Refraction.Logic.Combat
{
    /// <summary>黑攻击的三种判定：平推、斜上 45°、斜下 45°</summary>
    /// <remarks>由开打那一帧的竖直输入决定，判定盒在表现层</remarks>
    public enum BlackAttackKind
    {
        /// <summary>只有攻击键（j-ahead）</summary>
        Ahead = 0,

        /// <summary>按住上（w+j-up）</summary>
        Up = 1,

        /// <summary>按住下（s+j-down）</summary>
        Down = 2,
    }

    /// <summary>黑攻击的表现端：开打时点亮对应方向的攻击盒，结束时全部关掉</summary>
    public interface IBlackAttackView
    {
        void Begin(BlackAttackKind kind);

        void End();

        void SetHitHandler(Action onHit);
    }
}
