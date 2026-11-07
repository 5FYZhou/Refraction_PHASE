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

        public readonly MovementStateTag Move;

        public readonly PostureStateTag Posture;

        public readonly AttackStateTag Attack;

        public PlayerDebugSnapshot(
            Vector2 frameStartVelocity,
            Vector2 ledgerVelocity,
            MovementStateTag move,
            PostureStateTag posture,
            AttackStateTag attack)
        {
            FrameStartVelocity = frameStartVelocity;
            LedgerVelocity = ledgerVelocity;
            Move = move;
            Posture = posture;
            Attack = attack;
        }
    }
}
