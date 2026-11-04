using UnityEngine;

namespace Refraction.Data
{
    /// <summary>
    /// 玩家角色的全部可调参数。逻辑层只读取、不修改，改这里即可调手感。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Refraction/Settings/Player")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("移动")]
        [Tooltip("水平移动速度")]
        [Min(0f)]
        public float speed = 10f;

        [Tooltip("水平加速度")]
        public float moveAcceleration = 60f;

        [Tooltip("反向输入的转向衰减率（1/秒），越大转身越快")]
        public float turnDecayRate = 20f;

        [Header("跳跃")]
        [Tooltip("起跳瞬间的竖直速度。跳跃高度 ≈ 该值² / (2 × 重力加速度)。")]
        [Min(0f)]
        public float jumpSpeed = 9f;

        [Tooltip("土狼时间，单位 秒。")]
        [Min(0f)]
        public float coyoteTime = 0.12f;

        [Header("冲刺")]
        [Tooltip("冲刺时叠加到当前速度上的冲量")]
        [Min(0f)]
        public float dashSpeed;

        [Tooltip("冲刺状态的持续时间")]
        [Min(0f)]
        public float dashDuration;

        [Tooltip("两次冲刺之间的冷却时间")]
        [Min(0f)]
        public float dashCooldown;

        [Header("输入缓冲")]
        [Tooltip("冲刺按键的缓冲窗口，单位 秒。\n在这段时间内按下的冲刺会被记住：即使当时正在冷却中，冷却一结束就自动补触发，不用玩家重新按。\n建议 0.1 ~ 0.15。设成 0 表示不缓冲，必须精确按在可用的那一帧。")]
        [Min(0f)]
        public float dashBufferTime;

        [Tooltip("输入缓冲区的总回看窗口，单位 秒，决定最多往回记多久的按键。")]
        [Min(0f)]
        public float inputBufferTime;

        [Header("重力")]
        [Tooltip("是否由本逻辑接管竖直方向的重力。\n关闭时完全依赖 Rigidbody2D 的 Gravity Scale")]
        public bool enableGravity = false;

        [Tooltip("最大下落速度")]
        public float maxFallSpeed = 20f;

        [Tooltip("最大上升速度")]
        public float maxRiseSpeed = 20f;

        [Tooltip("上升期且按住跳跃键时的重力加速度")]
        public float riseGravity = 40f;

        [Tooltip("顶点悬停y方向速度阈值，y速度<该值&&>0时应用顶点悬停重力")]
        public float apexThreshold = 0.5f;

        [Tooltip("顶点悬停重力")]
        public float apexGravity = 40f;

        [Tooltip("其余情况的重力加速度")]
        public float fallGravity = 70f;

        [Tooltip("上升期松开跳跃键时的垂直速度截断系数")]
        [Range(0f, 1f)]
        public float jumpCutMultiplier = 0.5f; 
        
        [Tooltip("上升期松开跳跃键时的重力加速度")]
        public float jumpCutGravity = 90f;
    }
}
