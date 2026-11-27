using UnityEngine;

namespace Refraction.Data
{
    /// <summary>敌人 fortress 的参数：巡逻 / 索敌、白形态散射弹幕、黑形态弹反与接触伤害</summary>
    [CreateAssetMenu(fileName = "FortressConfig", menuName = "Refraction/Settings/Fortress")]
    public sealed class FortressConfig : CharacterConfig
    {
        [Header("巡逻")]
        [Tooltip("巡逻速度（单位/秒）：只沿 x 走，离地不推进。追踪速度直接用 moveSpeed。")]
        [Min(0f)]
        public float patrolSpeed = 2f;

        [Header("索敌")]
        [Tooltip("丢失目标多久（秒）后放弃追击：从最后一次看到玩家起算。0 表示一出视线就回巡逻。")]
        [Min(0f)]
        public float loseTargetTime = 1.5f;

        [Header("白 · 散射弹幕")]
        [Tooltip("弹幕冷却（秒）：从前摇起手那一帧算。")]
        [Min(0f)]
        public float barrageCooldown = 4f;

        [Tooltip("前摇时长（秒）：原地不动，只把身体转向玩家，结束那一帧锁定方向。")]
        [Min(0f)]
        public float barrageWindupTime = 0.5f;

        [Tooltip("后摇时长（秒）：原地不动，不追不打。")]
        [Min(0f)]
        public float barrageRecoverTime = 0.4f;

        [Tooltip("波次数：第一波在攻击段起手当帧打出，之后每 waveInterval 秒一波。")]
        [Min(1)]
        public int waveCount = 3;

        [Tooltip("每波子弹数：以锁定方向为中心左右对称散开。")]
        [Min(1)]
        public int bulletsPerWave = 5;

        [Tooltip("同一波里相邻两颗子弹的夹角（度）：总张角 = (子弹数 − 1) × 本值。")]
        [Min(0f)]
        public float spreadAngle = 12f;

        [Tooltip("波次之间的间隔（秒）：0 表示每物理帧一波。")]
        [Min(0f)]
        public float waveInterval = 0.15f;

        [Tooltip("子弹速度（单位/秒）")]
        [Min(0f)]
        public float bulletSpeed = 12f;

        [Tooltip("子弹伤害点数")]
        [Min(0f)]
        public float bulletDamage = 1f;

        [Tooltip("子弹击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float bulletKnockback = 6f;

        [Header("黑 · 弹反")]
        [Tooltip("弹反冷却（秒）：只有黑形态能弹，从触发那一刻算；弹反不打断当前行为。")]
        [Min(0f)]
        public float reboundCooldown = 2f;

        [Header("接触伤害")]
        [Tooltip("身体碰到玩家造成的伤害点数：接触盒碰到就结算一次，之后由 contactInterval 决定多久再来一次。")]
        [Min(0f)]
        public float contactDamage = 1f;

        [Tooltip("两次接触伤害的最小间隔（秒）：要 ≥ 玩家 invulnerableTime（当前 0.4s）。")]
        [Min(0f)]
        public float contactInterval = 1f;

        [Tooltip("接触伤害的击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float contactKnockback = 8f;
    }
}
