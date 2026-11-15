using UnityEngine;

namespace Refraction.Data
{
    /// <summary>毛毛虫 E_First 的参数：巡逻、瞄准射击与后摇追人</summary>
    [CreateAssetMenu(fileName = "E_FirstConfig", menuName = "Refraction/Settings/E_First")]
    public sealed class E_FirstConfig : CharacterConfig
    {
        [Header("巡逻")]
        [Tooltip("巡逻速度（单位/秒）：只沿 x 轴爬，竖直交给重力。")]
        [Min(0f)]
        public float patrolSpeed = 2f;

        [Header("白 · 射击")]
        [Tooltip("前摇时长（秒）：原地不动，结束那一帧锁定子弹方向。")]
        [Min(0f)]
        public float windupTime = 0.5f;

        [Tooltip("攻击时长（秒）：开火后仍原地不动，走完才进后摇。")]
        [Min(0f)]
        public float strikeTime = 0.15f;

        [Tooltip("后摇时长（秒）：走完若还看得见玩家就立刻接下一次前摇。")]
        [Min(0f)]
        public float recoverTime = 0.6f;

        [Tooltip("后摇时朝玩家爬的速度（单位/秒），恒速、不受 moveAcceleration 影响：0 = 后摇原地不动。")]
        [Min(0f)]
        public float recoverChaseSpeed = 1.5f;

        [Tooltip("后摇爬到这么近就站住（单位，两个身体中心的 x 距离）：再近会把玩家顶走。0 = 一直爬到玩家身上。")]
        [Min(0f)]
        public float recoverChaseRadius = 1.5f;

        [Tooltip("子弹速度（单位/秒）：0 = 不开火，只走动作。")]
        [Min(0f)]
        public float bulletSpeed = 10f;

        [Tooltip("子弹伤害点数：打玩家时会被玩家配置里的破坏力顶掉。")]
        [Min(0f)]
        public float bulletDamage = 1f;

        [Tooltip("子弹击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float bulletKnockback = 5f;

        [Tooltip("枪口相对身体的偏移：x 随朝向镜像，y 向上为正。子弹从这里出膛。")]
        public Vector2 bulletMuzzle = new(0.6f, 0.2f);

        [Header("接触伤害")]
        [Tooltip("碰到玩家造成的伤害点数，多久再来一次由 contactInterval 决定。")]
        [Min(0f)]
        public float contactDamage = 1f;

        [Tooltip("两次接触伤害的最小间隔（秒）：要 ≥ 玩家的 invulnerableTime（0.4s），否则多出来的会被无敌窗吞掉。")]
        [Min(0f)]
        public float contactInterval = 1f;

        [Tooltip("接触伤害的击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float contactKnockback = 7f;
    }
}
