using UnityEngine;

namespace Refraction.Data
{
    /// <summary>飞行敌人 Spliter 的参数：巡逻、黑冲撞、白散射弹幕、后摇浮动与弹反</summary>
    [CreateAssetMenu(fileName = "SpliterConfig", menuName = "Refraction/Settings/Spliter")]
    public sealed class SpliterConfig : CharacterConfig
    {
        [Header("巡逻")]
        [Tooltip("巡逻速度（单位/秒）：只沿 x 轴飞，竖直速度每帧清零。")]
        [Min(0f)]
        public float patrolSpeed = 2f;

        [Header("感知")]
        [Tooltip("追击时停下的距离（单位，两个中心之间）：再近会把玩家顶走。")]
        [Min(0f)]
        public float chaseStopDistance = 3f;

        [Header("攻击触发")]
        [Tooltip("触发攻击的最长距离（单位，两个中心之间）：黑白共用这一个判定，进了这个圈且冷却好了才起手。")]
        [Min(0f)]
        public float attackRange = 6f;

        [Header("冲撞 · 黑")]
        [Tooltip("冲撞速度（单位/秒）")]
        [Min(0f)]
        public float chargeSpeed = 18f;

        [Tooltip("前摇时长（秒）：立定瞄准，结束那一帧锁定方向，冲撞距离同时定为此刻两者连线距离的两倍。")]
        [Min(0f)]
        public float chargeWindupTime = 0.35f;

        [Tooltip("冲撞冷却（秒）：从前摇起手那一帧算，前摇 / 冲撞 / 后摇都算在间隔内。")]
        [Min(0f)]
        public float chargeCooldown = 2f;

        [Header("后摇浮动")]
        [Tooltip("浮动速度（单位/秒，恒定）：后摇时长 = 本次随机浮动距离 / 本值，所以没有单独的后摇时长参数。")]
        [Min(0.01f)]
        public float floatSpeed = 4f;

        [Tooltip("浮动距离下限（单位）：每次后摇在这个区间里取一个随机值，方向也是随机的。")]
        [Min(0f)]
        public float floatDistanceMin = 1f;

        [Tooltip("浮动距离上限（单位）")]
        [Min(0f)]
        public float floatDistanceMax = 3f;

        [Header("散射弹幕 · 白")]
        [Tooltip("弹幕冷却（秒）：从前摇起手那一帧算。")]
        [Min(0f)]
        public float barrageCooldown = 3f;

        [Tooltip("前摇时长（秒）：原地不动，只把身体转向玩家，结束那一帧锁定方向。")]
        [Min(0f)]
        public float barrageWindupTime = 0.5f;

        [Tooltip("波次数：第一波在起手当帧打出，之后每 waveInterval 秒一波。")]
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

        [Header("弹反")]
        [Tooltip("弹反冷却（秒）：只有黑形态能弹，从触发那一刻算。")]
        [Min(0f)]
        public float reboundCooldown = 2f;

        [Tooltip("弹反后平行两颗子弹到被弹反子弹轨迹的垂直距离（单位）：弹道两边各一颗。")]
        [Min(0f)]
        public float reboundSpread = 0.5f;

        [Header("接触伤害")]
        [Tooltip("身体碰到玩家造成的伤害点数：接触盒碰到就结算一次，之后由 contactInterval 决定多久再来一次。")]
        [Min(0f)]
        public float contactDamage = 1f;

        [Tooltip("两次接触伤害的最小间隔（秒）：要 ≥ 玩家 invulnerableTime（当前 0.4s），否则多出来的那几次会被无敌窗吞掉。")]
        [Min(0f)]
        public float contactInterval = 1f;

        [Tooltip("接触伤害的击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float contactKnockback = 8f;
    }
}
