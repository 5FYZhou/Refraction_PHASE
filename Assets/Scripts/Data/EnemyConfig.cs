using UnityEngine;

namespace Refraction.Data
{
    /// <summary>敌人参数：巡逻、索敌与接触伤害的数值；移动/重力的通用项继承自 CharacterConfig</summary>
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Refraction/Settings/Enemy")]
    public sealed class EnemyConfig : CharacterConfig
    {
        [Header("巡逻")]
        [Tooltip("巡逻速度（单位/秒），不乘 moveSpeed；追踪速度直接复用 moveSpeed。\n来回走的范围由预制体上的 PatrolCollider 决定，没有它就走到墙或悬崖。")]
        [Min(0f)]
        public float patrolSpeed = 2.5f;

        [Header("索敌")]
        [Tooltip("丢失目标多久（秒）后放弃：从最后一次看到玩家起算，超过它就回巡逻。\n0 表示一离开探测范围就回。")]
        [Min(0f)]
        public float loseTargetTime = 1.5f;

        [Tooltip("追踪时贴到多近（单位，两个身体的中心距）就停下：默认 1.5 ≈ 敌人身体半径 1 + 玩家半宽 0.45，再近就会把玩家一路顶走。")]
        [Min(0f)]
        public float chaseStopDistance = 1f;

        [Header("伤害")]
        [Tooltip("贴身接触造成的伤害点数：身体攻击盒碰到玩家就结算一次，之后由 contactInterval 决定多久再来一次。")]
        [Min(0f)]
        public float contactDamage = 1f;

        [Tooltip("两次接触伤害的最小间隔（秒）：玩家站在攻击盒里不走，就按这个节奏反复掉血。\n要 ≥ 玩家自己的 invulnerableTime（当前 0.4s），否则多出来的那几次会被无敌窗吞掉。")]
        [Min(0f)]
        public float contactInterval = 1f;

        [Tooltip("接触伤害的击退初速度（单位/秒）：0 表示不击退。\n滑多远还要乘上玩家自己的 KnockbackDecay（位移 = 本值² / (2 × 阻尼)）。")]
        [Min(0f)]
        public float contactKnockback = 10f;
    }
}
