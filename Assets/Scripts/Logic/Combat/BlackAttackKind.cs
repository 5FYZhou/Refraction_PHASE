using System;

namespace Refraction.Logic.Combat
{
    /// <summary>黑形态的攻击方向：平推（普攻）、斜上 45°、斜下 45°、水平（下蹲突进）</summary>
    /// <remarks>由开打那一帧的输入决定，判定盒在表现层</remarks>
    public enum BlackAttackKind
    {
        /// <summary>只有攻击键（j-ahead）</summary>
        Ahead = 0,

        /// <summary>按住上（w+j-up）</summary>
        Up = 1,

        /// <summary>按住下（s+j-down）</summary>
        Down = 2,

        Level = 3,
    }

    /// <summary>黑形态近战的表现端：开打时点亮对应方向的攻击盒，结束时全部关掉</summary>
    public interface IBlackAttackView
    {
        void BeginFlat();

        /// <summary>点亮突进盒与弹反盒，盒随玩家移动，每个受击盒只结算一次</summary>
        void BeginDash(BlackAttackKind kind);

        void End();

        void SetHitHandler(Action onHit);
    }
}
