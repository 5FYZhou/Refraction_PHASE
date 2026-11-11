using UnityEngine;

namespace Refraction.Data
{
    /// <summary>玩家专属参数：输入缓冲、下蹲、白攻击；移动等通用值在基类</summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Refraction/Settings/Player")]
    public sealed class PlayerConfig : CharacterConfig
    {

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

        [Tooltip("下蹲时的后坐力倍率：乘在 recoilSpeedX / recoilSpeedUp / recoilSpeedDown 算出的后坐力上。\n随下蹲前摇进度从 1 线性过渡到该值，与身高、移速共用同一条曲线；0.5 表示蹲满后坐力减半，1 表示下蹲不影响后坐力。")]
        [Range(0f, 1f)]
        public float crouchRecoilScale = 0.7f;

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

        [Tooltip("子弹命中后的击退初速度（单位/秒）：0 表示不击退。\n滑多远还要乘上目标自己的 KnockbackDecay（位移 = 本值² / (2 × 阻尼)）。")]
        [Min(0f)]
        public float bulletKnockback = 10f;

        [Tooltip("攻击后摇时长（秒）：开火后到攻击组回到 None 的等待，期间不响应新的攻击输入。\n0 表示没有后摇，开火后回到 None 即可再攻击（仍受 attackCooldown 限制）。")]
        [Min(0f)]
        public float attackRecoverTime = 0.12f;

        [Header("充能 · 残影")]
        [Tooltip("充能前摇时长（秒）：按一下充能键开始蓄力，走满进入蓄满态（玩家变橙）。\n0 表示按下当帧就蓄满。")]
        [Min(0f)]
        public float chargePrepareTime = 0.5f;

        [Tooltip("充能冷却（秒）：从冲刺落影那一刻起算，不是从按键起算。\n冷却期间按充能键不响应；0 表示落影后立刻能再蓄。")]
        [Min(0f)]
        public float chargeCooldown = 1.5f;

        [Tooltip("场上残影的最大个数：超过上限时新残影会顶掉最早留下的那一枚，而不是拒绝放置。")]
        [Min(1)]
        public int maxAfterimages = 4;

        [Tooltip("残影持续时间（秒）：从落地那一刻起算，到点自动消失；0 表示不会自然消失，只等被数量上限顶掉。")]
        [Min(0f)]
        public float afterimageDuration = 8f;

        [Tooltip("折跃连线的伤害（点）：子弹穿过这条连线时，对线上的目标结算一次；0 表示不伤人。")]
        [Min(0f)]
        public float afterimageLinkDamage = 1f;

        [Tooltip("折跃连线的判定粗细（米）：连线的判定方块宽度与画面上线的粗细共用这一个值。")]
        [Min(0.01f)]
        public float afterimageLinkThickness = 0.25f;

        [Tooltip("残影的感应范围（米）：只有生成顺序相邻、且间距在这个范围内的一对残影才连线。\n防止在超远的两处落影，拉出一条横穿地图的长线；0 表示不限距离。")]
        [Min(0f)]
        public float afterimageLinkSenseRadius = 6f;

        [Tooltip("残影落地时的透明度（起始值，不是恒定值）：之后随时间淡到表现层写死的终止透明度。")]
        [Range(0f, 1f)]
        public float afterimageAlpha = 0.35f;

        [Tooltip("折跃连线的颜色。")]
        public Color afterimageLinkColor = new(0.55f, 0.9f, 1f, 0.9f);

        [Tooltip("子弹在残影之间传输时的移动速度（单位/秒）：从当前残影中心直线走向下一枚的速度。\n0 表示瞬间抵达下一枚，看不到中间过程。")]
        [Min(0f)]
        public float bulletTransferSpeed = 26f;
    }
}
