using UnityEngine;

namespace Refraction.Data
{
    /// <summary>玩家专属参数：输入缓冲、下蹲、白攻击；移动等通用值在基类</summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Refraction/Settings/Player")]
    public sealed class PlayerConfig : CharacterConfig
    {
        [Header("输入缓冲")]
        [Tooltip("冲刺输入缓冲窗口（秒）。\n冷却结束的那一帧，只有落在这个窗口内按下的冲刺才会触发；冷却期间按下的不会一直留到冷却结束后补触发（除非窗口大于剩余冷却）。\n建议 0.1 ~ 0.15。设成 0 表示不缓冲，必须精确按在可用的那一帧。")]
        [Min(0f)]
        public float dashBufferTime;

        [Tooltip("跳跃输入缓冲窗口（秒）：落地前这段时间内按下的跳跃会被记住，满足起跳条件那一帧直接起跳。\n冲刺过程中按跳不生效；同时参与决定输入缓冲区的容量（取它与冲刺缓冲时间的较大者）。")]
        [Min(0f)]
        public float inputBufferTime;

        [Header("姿态 · 下蹲")]
        [Tooltip("下蹲前摇时长（秒），也就是 CrouchingTransition 的时长：\n这段时间里身体高度与移速乘数一起从站姿值线性过渡到蹲姿值，跳跃/冲刺的限制要等过渡结束才生效。\n0 表示没有前摇，直接进蹲姿。")]
        [Min(0f)]
        public float crouchPrepareTime = 0f;

        [Tooltip("下蹲时的身高倍率（相对站立高度）：0.65 表示只留 65% 的高度。\n碰撞体、墙探针、View 一起按这个倍率压扁，脚底位置不变。")]
        [Range(0.05f, 1f)]
        public float crouchHeightRatio = 0.65f;

        [Tooltip("下蹲时的移速倍率：乘在 moveSpeed 上（走 MoveLedger 的速度乘数，不改加速度）。\n0.5 表示蹲下后只能走一半速度；设为 0 表示蹲下不能移动。")]
        [Range(0f, 1f)]
        public float crouchMoveSpeedScale = 0.5f;

        [Tooltip("下蹲期间是否允许跳跃。默认禁止，后续按设计调整。")]
        public bool crouchAllowJump = false;

        [Tooltip("下蹲期间是否允许冲刺。默认禁止，后续按设计调整。")]
        public bool crouchAllowDash = false;

        [Tooltip("下蹲的离地宽限（秒）：蹲姿/前摇要连续离地超过这个时长才结束。\n踩地判定偶尔会掉一两帧（接触刷新、碰撞体刚改过高度），没有宽限的话人会被踢回站姿、下一帧又蹲下，看起来是每两帧抽一次。\n设 0 表示离地一帧就结束。")]
        [Min(0f)]
        public float crouchGroundGrace = 0.08f;

        [Header("攻击 · 白")]
        [Tooltip("白色子弹命中的伤害点数（点）。生成时写进 Bullet，怪物受伤逻辑接入后由它读取。\n0 表示不造成伤害。")]
        [Min(0f)]
        public float bulletDamage = 1f;

        [Tooltip("两次攻击之间的最短间隔（秒）。从触发攻击那一帧起算，前摇与后摇都算在间隔内。\n0 表示无冷却，攻击组回到 None 后下一帧即可再攻击。")]
        [Min(0f)]
        public float attackCooldown = 0.35f;

        [Tooltip("子弹的飞行速度（单位/秒）。生成时直接写成子弹刚体的水平速度，不受重力影响。\n0 表示子弹原地不动。")]
        [Min(0f)]
        public float bulletSpeed = 18f;

        [Tooltip("开火时的后坐力：沿朝向反向叠加一份外部速度（单位/秒），不直接改写常规移动速度。\n最终速度 = 常规移动速度 + 外部速度，外部部分按 externalDeceleration 衰减；0 表示无后坐力。")]
        [Min(0f)]
        public float recoilSpeedX = 8f;

        [Tooltip("斜上 45° 开火时的后坐力（单位/秒）：沿弹道反向叠加，横向与竖直分量各为 0.707 × 该值。\n0 表示斜上射击没有后坐力。")]
        [Min(0f)]
        public float recoilSpeedUp = 8f;

        [Tooltip("斜下 45° 开火时的后坐力（单位/秒）：沿弹道反向叠加，横向与竖直分量各为 0.707 × 该值。\n0 表示斜下射击没有后坐力。")]
        [Min(0f)]
        public float recoilSpeedDown = 8f;

        [Tooltip("攻击前摇时长（秒）：按下攻击键到生成子弹之间的等待。\n0 表示没有前摇，按键当帧就开火。")]
        [Min(0f)]
        public float attackPrepareTime = 0.08f;

        [Tooltip("攻击后摇时长（秒）：开火后到攻击组回到 None 的等待，期间不响应新的攻击输入。\n0 表示没有后摇，开火后回到 None 即可再攻击（仍受 attackCooldown 限制）。")]
        [Min(0f)]
        public float attackRecoverTime = 0.12f;
    }
}
