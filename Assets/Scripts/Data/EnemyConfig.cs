using UnityEngine;

namespace Refraction.Data
{
    /// <summary>敌人参数：巡逻、索敌与三段攻击的数值；移动/重力的通用项继承自 CharacterConfig</summary>
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Refraction/Settings/Enemy")]
    public sealed class EnemyConfig : CharacterConfig
    {
        [Header("巡逻")]
        [Tooltip("巡逻半径（单位）：以巡逻起点 x 为中心，在 [x − 半径, x + 半径] 内来回走。\n0 表示原地转身，不挪窝。")]
        [Min(0f)]
        public float patrolRadius = 4f;

        [Tooltip("巡逻速度（单位/秒），不乘 moveSpeed；追踪速度直接复用 moveSpeed。")]
        [Min(0f)]
        public float patrolSpeed = 2.5f;

        [Header("索敌")]
        [Tooltip("丢失目标多久（秒）后放弃：追踪与攻击共用这一个计时，从最后一次看到玩家起算。\n超过它才回巡逻，0 表示一离开探测范围就回。")]
        [Min(0f)]
        public float loseTargetTime = 1.5f;

        [Tooltip("追踪时停下的水平距离（单位）：离玩家这么近就刹车等冷却，否则会把玩家一路顶走。")]
        [Min(0f)]
        public float chaseStopDistance = 2.5f;

        [Header("攻击")]
        [Tooltip("攻击冷却（秒）：从上一次进前摇那一帧起算，前摇/冲刺/后摇都算在间隔内。\n比整套动作短时，后摇一结束就能接下一次。")]
        [Min(0f)]
        public float attackCooldown = 1.8f;

        [Tooltip("前摇时长（秒）：原地蓄力，结束那一帧锁定冲刺方向。")]
        [Min(0f)]
        public float attackWindupTime = 0.45f;

        [Tooltip("冲刺时长（秒）：按锁定方向以 attackDashSpeed 平移。")]
        [Min(0f)]
        public float attackDashDuration = 0.22f;

        [Tooltip("冲刺水平速度（单位/秒）：前摇结束那一帧直接写入，不做加速。")]
        [Min(0f)]
        public float attackDashSpeed = 16f;

        [Tooltip("后摇时长（秒）：冲刺结束后原地停顿，期间不再发起新攻击。")]
        [Min(0f)]
        public float attackRecoverTime = 0.45f;
    }
}
