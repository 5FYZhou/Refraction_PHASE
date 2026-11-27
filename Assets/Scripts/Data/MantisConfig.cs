using UnityEngine;

namespace Refraction.Data
{
    /// <summary>普通敌人 mantis 的参数：巡逻、追击、黑白两套近身攻击与弹反</summary>
    [CreateAssetMenu(fileName = "MantisConfig", menuName = "Refraction/Settings/Mantis")]
    public sealed class MantisConfig : CharacterConfig
    {
        [Header("巡逻")]
        [Tooltip("巡逻速度（单位/秒）：只沿 x 走，离地不推进。追踪速度直接用 moveSpeed。")]
        [Min(0f)]
        public float patrolSpeed = 2.5f;

        [Header("索敌")]
        [Tooltip("丢失目标多久（秒）后放弃追击：从最后一次看到玩家起算。0 表示一出视线就回巡逻。")]
        [Min(0f)]
        public float loseTargetTime = 1.5f;

        [Header("黑 · 攻击")]
        [Tooltip("前摇时长（秒）：立定不动，结束那一帧结算第一次范围伤害并开始前冲。")]
        [Min(0f)]
        public float blackWindupTime = 0.4f;

        [Tooltip("后摇时长（秒）：立定不动，不追不转向。")]
        [Min(0f)]
        public float blackRecoverTime = 0.5f;

        [Header("位移（黑白共用）")]
        [Tooltip("快速位移的速度（单位/秒）：黑的攻击后前冲、白的两枪之间前冲都用它。")]
        [Min(0f)]
        public float dashSpeed = 14f;

        [Tooltip("快速位移的时长（秒）：黑白共用；白的后撤超快位移也用它。")]
        [Min(0.01f)]
        public float dashTime = 0.12f;

        [Header("黑 · 弹反")]
        [Tooltip("弹反冷却（秒）：只有黑形态能弹，从触发那一刻算。")]
        [Min(0f)]
        public float reboundCooldown = 2f;

        [Header("白 · 攻击")]
        [Tooltip("前摇时长（秒）：立定不动，结束那一帧锁定玩家位置并开出第一枪。")]
        [Min(0f)]
        public float whiteWindupTime = 0.5f;

        [Tooltip("后摇时长（秒）：向后超快位移的持续时间，与快速位移时长相同。")]
        [Min(0f)]
        public float whiteRecoverTime = 0.12f;

        [Tooltip("后摇后撤的加速倍率：后撤速度 = dashSpeed × 本值。")]
        [Min(0f)]
        public float dashBackMultiplier = 2f;

        [Tooltip("子弹速度（单位/秒）")]
        [Min(0f)]
        public float bulletSpeed = 14f;

        [Tooltip("子弹伤害点数")]
        [Min(0f)]
        public float bulletDamage = 1f;

        [Tooltip("子弹击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float bulletKnockback = 6f;

        [Header("伤害")]
        [Tooltip("攻击段前方范围伤害的点数：每次范围伤害结算一次；身体接触盒也读这个值。")]
        [Min(0f)]
        public float contactDamage = 1f;

        [Tooltip("身体接触伤害的最小间隔（秒）：要 ≥ 玩家 invulnerableTime（当前 0.4s）。\n范围伤害不走它，按攻击段各自的时机结算。")]
        [Min(0f)]
        public float contactInterval = 1f;

        [Tooltip("击退初速度（单位/秒）：0 表示不击退。范围伤害按面朝方向推。")]
        [Min(0f)]
        public float contactKnockback = 8f;
    }
}
