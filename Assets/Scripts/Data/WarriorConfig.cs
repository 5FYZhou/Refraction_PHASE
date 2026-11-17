using UnityEngine;

namespace Refraction.Data
{
    /// <summary>Warrior 的参数：巡逻、黑白两种扑击与弹反</summary>
    [CreateAssetMenu(fileName = "WarriorConfig", menuName = "Refraction/Settings/Warrior")]
    public sealed class WarriorConfig : CharacterConfig
    {
        [Header("巡逻")]
        [Tooltip("巡逻速度（单位/秒）：只沿 x 轴走，竖直交给重力。")]
        [Min(0f)]
        public float patrolSpeed = 2f;

        [Header("扑击")]
        [Tooltip("抛物线最高点相对起跳点的抬升（单位）：玩家与怪物同高时，它就是顶点与怪物位置的 y 差。")]
        [Min(0f)]
        public float pounceApexHeight = 2.5f;

        [Tooltip("沿轨迹推进的速度（单位/秒）：按弧长算，转弯处不会突然变快。")]
        [Min(0.01f)]
        public float pounceSpeed = 9f;

        [Tooltip("扑击冷却（秒）：从动作结束那一刻起算；撞墙中断也算结束，立刻进冷却。")]
        [Min(0f)]
        public float pounceCooldown = 1.5f;

        [Tooltip("黑 · 前摇时长（秒）：立定不动，开始那一帧锁定玩家位置当落点。")]
        [Min(0f)]
        public float windupTime = 0.35f;

        [Tooltip("黑 · 后摇时长（秒）：落地后立定，期间不能再起手。")]
        [Min(0f)]
        public float recoverTime = 0.4f;

        [Header("白 · 后跃射击")]
        [Tooltip("白 · 悬停时长（秒）：到抛物线顶点就停住，期间忽略重力、速度为 0；结束后朝玩家开一枪。")]
        [Min(0f)]
        public float hoverTime = 0.5f;

        [Tooltip("子弹速度（单位/秒）：0 = 不开火。")]
        [Min(0f)]
        public float bulletSpeed = 12f;

        [Tooltip("子弹伤害点数：打玩家时会被玩家配置里的破坏力顶掉。")]
        [Min(0f)]
        public float bulletDamage = 1f;

        [Tooltip("子弹击退初速度（单位/秒）：0 表示不击退。")]
        [Min(0f)]
        public float bulletKnockback = 5f;

        [Header("弹反")]
        [Tooltip("弹反冷却（秒）：只有黑形态弹，前摇 / 扑击 / 后摇中都不弹。")]
        [Min(0f)]
        public float reboundCooldown = 1f;

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
