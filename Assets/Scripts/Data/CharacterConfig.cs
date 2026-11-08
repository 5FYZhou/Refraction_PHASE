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

        [Header("跳跃")]
        [Tooltip("起跳瞬间直接写入的竖直速度（不是加速度）。\n跳跃高度 ≈ 该值² / (2 × 上升重力)；松键的 Jump Cut 会把这个速度截断。")]
        [Min(0f)]
        public float jumpSpeed = 9f;

        [Tooltip("土狼时间（秒）：离地后仍允许起跳的宽限时长。\n每次落地重新给一次机会，用过一次要再次落地才能再用。")]
        [Min(0f)]
        public float coyoteTime = 0.12f;

        [Tooltip("空中跳跃次数：一次离地期间最多还能在空中跳几次，1 表示二段跳，0 表示空中不给跳。\n每次重新落地才会恢复；土狼跳（离地宽限）算地面跳，不消耗这里的次数。")]
        [Min(0)]
        public int maxAirJumps = 1;

        [Tooltip("空中跳初速度（不是加速度）。AirJumpState 起跳那一帧直接写入该值，会覆盖当前竖直速度。\n同样受松键 Jump Cut 与顶点悬停影响。")]
        [Min(0f)]
        public float airJumpSpeed = 12f;

        [Header("冲刺")]
        [Tooltip("冲刺期间的水平速度：进入冲刺时直接覆盖当前速度（方向 × 该值），不是叠加冲量。\n冲刺会把竖直速度一并清零，上升/下落途中冲刺同样有效。")]
        [Min(0f)]
        public float dashSpeed;

        [Tooltip("冲刺持续时间（秒）。从触发那一帧开始计时，超过该时长即结束。")]
        [Min(0f)]
        public float dashDuration;

        [Tooltip("冲刺冷却（秒）。从触发冲刺的那一刻起算，而不是从冲刺结束起算。")]
        [Min(0f)]
        public float dashCooldown;

        [Tooltip("两次落地之间可用的空中冲刺次数：0 表示离地后不给冲，1 表示一次空中冲刺。\n踩地冲刺不占用这里的次数，每次都还是要过 dashCooldown 冷却；重新落地即回满。")]
        [Min(0)]
        public int maxAirDashes = 1;

        [Header("重力")]
        [Tooltip("是否由本逻辑接管竖直方向的重力。\n关闭时完全不施加逻辑重力，改由 Rigidbody2D 的 Gravity Scale 决定，同时冲刺也无法再临时关闭重力。\n开启时请把刚体 Gravity Scale 设为 0，否则两套重力会叠加。")]
        public bool enableGravity = false;

        [Tooltip("下落速度上限（绝对值，仅在启用重力时钳制 vy）。")]
        public float maxFallSpeed = 20f;

        [Tooltip("上升速度上限（仅在启用重力时钳制 vy）。")]
        public float maxRiseSpeed = 20f;

        [Tooltip("下落段与其余状态的重力加速度。\n跳跃结束时写入，也是初始重力值；Idle / Move / Fall 都不会再改它。")]
        public float fallGravity = 70f;

        [Tooltip("上升段按住跳跃键时使用的重力加速度。\n起跳那一帧就写入，之后每帧按是否处于顶点区间改用 apexGravity 或回到本值。")]
        public float riseGravity = 40f;

        [Tooltip("顶点悬停区间阈值：上升段按住跳跃键、且 0 < vy < 该值时，重力改用 apexGravity。\n该值越小，顶点加速的区间越窄。")]
        public float apexThreshold = 0.5f;

        [Tooltip("顶点悬停区间的重力加速度\n小于 riseGravity 才会变飘（悬停感），大于则更快越过顶点。")]
        public float apexGravity = 20;

        [Tooltip("上升期松开跳跃键时，把当前竖直速度乘以该系数（每段上升只生效一次）。")]
        [Range(0f, 1f)]
        public float jumpCutMultiplier = 0.5f;

        [Tooltip("上升期松开跳跃键后使用的重力加速度，配合 jumpCutMultiplier 让短按跳得更低。")]
        public float jumpCutGravity = 90f;

        [Header("外部速度")]
        [Tooltip("外部速度修正的衰减率（单位/秒²）：后坐力、击退等叠加在常规移动速度上的分量，每帧朝 0 线性衰减。\n0 表示不衰减，角色会一直被这份速度推着走。")]
        [Min(0f)]
        public float externalDeceleration = 40f;
    }
}
