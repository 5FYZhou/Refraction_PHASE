using Refraction.Logic.Actor;
using UnityEngine;

namespace Refraction.Presentation.UI
{
    /// <summary>玩家调试快照：玩家侧每个物理帧发布一次，面板只订阅，不反向索要</summary>
    public readonly struct PlayerDebugSnapshot
    {
        /// <summary>物理帧开始时刚体的真实速度，含外部速度（后坐力、外力）</summary>
        public readonly Vector2 FrameStartVelocity;

        /// <summary>账本算完、尚未写回刚体的速度；与上一行不等就说明外部速度或提交没对上</summary>
        public readonly Vector2 LedgerVelocity;

        public readonly Vector2 HurtVelocity;

        /// <summary>物理帧开始时地面探针给出的踩地判定</summary>
        public readonly bool Grounded;

        public readonly ConditionStateTag Condition;

        public readonly MovementStateTag Move;

        public readonly PostureStateTag Posture;

        public readonly AttackStateTag Attack;

        public readonly int Polarity;

        public readonly int MaxPolarity;

        public PlayerDebugSnapshot(
            Vector2 frameStartVelocity,
            Vector2 ledgerVelocity,
            Vector2 hurtVelocity,
            bool grounded,
            ConditionStateTag condition,
            MovementStateTag move,
            PostureStateTag posture,
            AttackStateTag attack,
            int polarity,
            int maxPolarity)
        {
            FrameStartVelocity = frameStartVelocity;
            LedgerVelocity = ledgerVelocity;
            HurtVelocity = hurtVelocity;
            Grounded = grounded;
            Condition = condition;
            Move = move;
            Posture = posture;
            Attack = attack;
            Polarity = polarity;
            MaxPolarity = maxPolarity;
        }
    }
}
