using UnityEngine;

namespace Refraction.Data
{
    /// <summary>Boss mmfly 的参数：决策 / 观察 / 逼近循环，黑俯冲与弹反，白弹幕与召唤</summary>
    [CreateAssetMenu(fileName = "MmflyConfig", menuName = "Refraction/Settings/Mmfly")]
    public sealed class MmflyConfig : CharacterConfig
    {
        [Header("决策")]
        [Tooltip("决策间隔（秒）：进入决策态后先等这么久再选下一个行为；二阶段按 phase2DecideScale 缩短。")]
        [Min(0f)]
        public float decideInterval = 0.6f;

        [Tooltip("进入二阶段的血量比例（0~1）：当前血量 ≤ 最大血量 × 本值时按二阶段跑。")]
        [Range(0f, 1f)]
        public float phase2HealthRatio = 0.5f;

        [Tooltip("二阶段决策间隔倍率：实际间隔 = decideInterval × 本值（<1 就是缩短）。")]
        [Min(0.01f)]
        public float phase2DecideScale = 0.6f;

        [Tooltip("二阶段逼近速度倍率：实际速度 = approachSpeed × 本值。")]
        [Min(0.01f)]
        public float phase2ApproachScale = 1.4f;

        [Header("观察")]
        [Tooltip("观察时长下限（秒）")]
        [Min(0f)]
        public float observeTimeMin = 1f;

        [Tooltip("观察时长上限（秒）：每次观察在「下限 ~ 上限」里随机。")]
        [Min(0f)]
        public float observeTimeMax = 1.5f;

        [Tooltip("观察期漂移速度（单位/秒，极缓慢）：方向随机且不会朝玩家靠近；0 = 原地悬浮。")]
        [Min(0f)]
        public float observeDriftSpeed = 0.6f;

        [Header("逼近")]
        [Tooltip("逼近速度（单位/秒）：二阶段按 phase2ApproachScale 加快。")]
        [Min(0f)]
        public float approachSpeed = 2.5f;

        [Tooltip("逼近时的横向摆幅（单位/秒）：叠加在朝玩家的直线上做轻微偏移，0 = 纯直线。")]
        [Min(0f)]
        public float approachSway = 0.8f;

        [Header("黑 · 俯冲")]
        [Tooltip("前摇时长（秒）：悬停蓄力，结束那一帧锁定玩家位置与俯冲距离。")]
        [Min(0f)]
        public float diveWindupTime = 0.5f;

        [Tooltip("俯冲速度（单位/秒）")]
        [Min(0f)]
        public float diveSpeed = 18f;

        [Tooltip("后摇时长（秒）：不移动、不锁定玩家，可被攻击。")]
        [Min(0f)]
        public float diveRecoverTime = 0.8f;

        [Tooltip("俯冲冷却（秒）：从前摇起手那一帧算，前摇 / 俯冲 / 后摇都算在间隔内。")]
        [Min(0f)]
        public float diveCooldown = 3f;

        [Header("黑 · 弹反")]
        [Tooltip("弹反冷却（秒）：只有黑形态能弹，从触发那一刻算；弹反不打断当前行为。")]
        [Min(0f)]
        public float reboundCooldown = 2f;

        [Header("白 · 弹幕")]
        [Tooltip("弹幕冷却（秒）：从前摇起手那一帧算。")]
        [Min(0f)]
        public float barrageCooldown = 4f;

        [Tooltip("前摇时长（秒）：原地不动只转身体，结束那一帧锁定方向。")]
        [Min(0f)]
        public float barrageWindupTime = 0.6f;

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

        [Header("白 · 召唤")]
        [Tooltip("召唤冷却（秒）：从前摇起手那一帧算。")]
        [Min(0f)]
        public float summonCooldown = 8f;

        [Tooltip("前摇时长（秒）：抖动 / 颜色闪烁预警，结束当帧生成小怪。")]
        [Min(0f)]
        public float summonWindupTime = 1f;

        [Tooltip("单次召唤数量：场上小怪达到上限时按剩余名额少召。")]
        [Min(1)]
        public int summonCount = 2;

        [Tooltip("场上小怪上限：达到后召唤不进技能池。")]
        [Min(0)]
        public int summonLimit = 4;

        [Header("后摇浮动")]
        [Tooltip("浮动速度（单位/秒，恒定）：后摇时长 = 本次随机浮动距离 / 本值，没有单独的后摇时长参数。")]
        [Min(0.01f)]
        public float floatSpeed = 4f;

        [Tooltip("浮动距离下限（单位）：每次后摇在这个区间里取一个随机值，方向也是随机的。")]
        [Min(0f)]
        public float floatDistanceMin = 1f;

        [Tooltip("浮动距离上限（单位）")]
        [Min(0f)]
        public float floatDistanceMax = 3f;

        [Header("攻击伤害")]
        [Tooltip("攻击盒 / 身体碰到玩家造成的伤害点数。")]
        [Min(0f)]
        public float contactDamage = 1f;

        [Tooltip("两次伤害的最小间隔（秒）：要 ≥ 玩家 invulnerableTime（当前 0.4s）。")]
        [Min(0f)]
        public float contactInterval = 1f;

        [Tooltip("伤害的击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float contactKnockback = 8f;
    }
}
