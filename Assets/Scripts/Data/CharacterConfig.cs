using UnityEngine;

namespace Refraction.Data
{
    /// <summary>角色通用参数：移动/跳跃/冲刺/重力/外部速度。玩家与敌人共用这一套，MoveLedger 只认这个类型</summary>
    public abstract class CharacterConfig : ScriptableObject
    {
        [Header("移动")]
        [Tooltip("水平移动的目标速度。地面行走（Move）和空中横移（Jump / Fall）都用这个值。")]
        [Min(0f)]
        public float moveSpeed = 10f;

        [Tooltip("同向加速时的水平加速度（单位/秒²）：起步与继续同向时用它逼近 moveSpeed。\n<=0 表示零惯性：横向速度当帧直接写成目标值（松键也瞬停）。")]
        public float moveAcceleration = 50f;

        [Tooltip("目标速度为 0 时的水平减速度（单位/秒²）：松键滑停与 Idle 待机刹车都用它。\n越大停得越快，不影响反向转身。")]
        public float moveDeceleration = 60f;

        [Tooltip("反向输入时的水平加速度（单位/秒²）：目标速度与当前速度方向相反时用它。\n速度越过 0 之后自动改回 moveAcceleration。")]
        public float turnAcceleration = 80f;

        [Header("外部速度")]
        [Tooltip("外部速度修正的衰减率（单位/秒²）：后坐力、击退等叠加在常规移动速度上的分量，每帧朝 0 线性衰减。\n0 表示不衰减，角色会一直被这份速度推着走。")]
        [Min(0f)]
        public float externalDeceleration = 40f;

        [Header("生命")]
        [Tooltip("最大生命值（点）。伤害按 Damage.Amount 四舍五入扣点，扣到 0 即 IsAlive = false。")]
        [Min(1)]
        public int maxHealth = 3;

        [Tooltip("受击无敌时长（秒）：这段时间里再挨打既不扣血也不击退。\n贴身接触伤害每帧都可能触发，没有它玩家会被一发打成筛子。0 表示不设无敌窗。")]
        [Min(0f)]
        public float invulnerableTime = 0.4f;

        [Header("受击 · 击退")]
        [Tooltip("击退初速度（单位/秒）。0 表示按 Damage 给的格数与 KnockbackDecay 折算（v0 = √(2 × 阻尼 × 格)），这样滑出去的位移正好等于给的格数。")]
        [Min(0f)]
        public float KnockbackSpeed;

        [Tooltip("受击滑停减速度（单位/秒²）：击退速度每秒衰减这么多，衰减到 0 就退出受伤态。\n它同时决定同一次攻击滑多远：越大停得越快。")]
        [Min(0f)]
        public float KnockbackDecay = 30f;

        [Header("重力")]
        [Tooltip("是否由本逻辑接管竖直方向的重力。\n关闭时完全不施加逻辑重力，改由 Rigidbody2D 的 Gravity Scale 决定，同时冲刺也无法再临时关闭重力。\n开启时请把刚体 Gravity Scale 设为 0，否则两套重力会叠加。")]
        public bool enableGravity = false;

        [Tooltip("下落速度上限（绝对值，仅在启用重力时钳制 vy）。")]
        public float maxFallSpeed = 20f;

        [Tooltip("上升速度上限（仅在启用重力时钳制 vy）。")]
        public float maxRiseSpeed = 20f;

        [Tooltip("下落段与其余状态的重力加速度。\n跳跃结束时写入，也是初始重力值；Idle / Move / Fall 都不会再改它。")]
        public float fallGravity = 70f;
    }
}
