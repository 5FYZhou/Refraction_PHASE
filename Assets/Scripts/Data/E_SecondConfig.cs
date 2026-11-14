using UnityEngine;

namespace Refraction.Data
{
    /// <summary>飞行敌人 E_Second 的参数：巡逻、三种范围与黑白两种攻击</summary>
    [CreateAssetMenu(fileName = "E_SecondConfig", menuName = "Refraction/Settings/E_Second")]
    public sealed class E_SecondConfig : CharacterConfig
    {
        [Header("巡逻")]
        [Tooltip("巡逻速度（单位/秒）：只沿 x 轴飞，竖直速度每帧清零。")]
        [Min(0f)]
        public float patrolSpeed = 2f;

        [Header("感知")]
        [Tooltip("追击时停下的距离（单位，两个中心之间）：再近会把玩家顶走。")]
        [Min(0f)]
        public float chaseStopDistance = 3f;

        [Header("黑 · 冲刺")]
        [Tooltip("前摇时长（秒）：悬停蓄力，结束那一帧锁定冲刺方向。")]
        [Min(0f)]
        public float blackWindupTime = 0.4f;

        [Tooltip("冲刺速度（单位/秒）")]
        [Min(0f)]
        public float blackDashSpeed = 16f;

        [Tooltip("冲刺最长时长（秒）：撞到玩家或墙就提前结束后摇，一直没撞到才冲这么久，防止冲进虚空。")]
        [Min(0.01f)]
        public float blackDashMaxTime = 1f;

        [Tooltip("后摇上浮距离下限（单位）：每次后摇在这个区间里取一个随机值。")]
        [Min(0f)]
        public float riseMin = 1f;

        [Tooltip("后摇上浮距离上限（单位）")]
        [Min(0f)]
        public float riseMax = 3f;

        [Tooltip("上浮速度（单位/秒，恒定）：后摇时长 = 本次随机上浮距离 / 本值，所以没有单独的后摇时长参数。")]
        [Min(0.01f)]
        public float riseSpeed = 5f;

        [Header("白 · 走位")]
        [Tooltip("白攻击时的逼近速度（单位/秒）：前摇与后摇都朝玩家飞，进到下面的半径里就刹住。0 = 原地不动。")]
        [Min(0f)]
        public float whiteChaseSpeed = 4f;

        [Tooltip("白攻击时要靠到多近（单位，两个中心之间）：只看靠没靠够，进了就停、不后退，所以贴脸时不会把人顶走。")]
        [Min(0f)]
        public float whiteChaseRadius = 5f;

        [Header("白 · 子弹")]
        [Tooltip("前摇时长（秒）：悬停瞄准，结束那一帧锁定方向并开火。")]
        [Min(0f)]
        public float whiteWindupTime = 0.4f;

        [Tooltip("后摇时长（秒）：开火后悬停，结束再判一次要不要继续打。")]
        [Min(0f)]
        public float whiteRecoverTime = 0.5f;

        [Tooltip("子弹速度（单位/秒）")]
        [Min(0f)]
        public float bulletSpeed = 12f;

        [Tooltip("子弹伤害点数：只写进子弹自己的 Damage，打玩家时会被玩家配置里的「破坏力」顶掉。")]
        [Min(0f)]
        public float bulletDamage = 1f;

        [Tooltip("子弹击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float bulletKnockback = 6f;

        [Tooltip("枪口相对敌人的偏移（单位，局部）：x 分量会随朝向镜像。")]
        public Vector2 muzzle = new(1f, 0f);

        [Header("接触伤害")]
        [Tooltip("冲刺撞到玩家造成的伤害点数：接触盒碰到玩家就结算一次，之后由 contactInterval 决定多久再来一次。")]
        [Min(0f)]
        public float contactDamage = 1f;

        [Tooltip("两次接触伤害的最小间隔（秒）：玩家贴着不走，就按这个节奏反复掉血。\n留 0 = 不设间隔（每个物理帧都试一次，基本全靠玩家自己的无敌窗兜着，不建议）。\n要 ≥ 玩家 invulnerableTime（当前 0.4s），否则多出来的那几次会被无敌窗吞掉。")]
        [Min(0f)]
        public float contactInterval = 1f;

        [Tooltip("接触伤害的击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float contactKnockback = 8f;
    }
}
