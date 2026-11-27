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

        [Header("地图")]
        [Tooltip("打开地图时的水平移动速度（单位/秒）：地图态只走这条速度，跳跃与冲刺都不接。\n0 表示地图打开期间不能移动。")]
        [Min(0f)]
        public float mapMoveSpeed = 3f;

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

        [Tooltip("白形态两次攻击之间的最短间隔（秒）。从触发攻击那一帧起算，前摇与后摇都算在间隔内。\n0 表示无冷却，攻击组回到 None 后下一帧即可再攻击。黑形态的三只各读自己那份冷却。")]
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

        [Header("攻击 · 黑 · 普攻")]
        [Tooltip("普攻前摇时长（秒）：黑形态只按攻击键走普攻，这里是按下到判定盒点亮之间的等待。\n0 表示没有前摇，按键当帧就开打。")]
        [Min(0f)]
        public float flatAttackPrepareTime = 0.15f;

        [Tooltip("普攻本体持续时间（秒）：Player/BlackDash/AheadCollider 点亮到熄灭的时长，时间到进后摇。")]
        [Min(0f)]
        public float flatAttackTime = 0.2f;

        [Tooltip("普攻后摇时长（秒）：本体结束到这只状态收尾的等待，期间能走、能跳、能冲刺，但按别的方向攻击会直接顶掉这段后摇。\n0 表示没有后摇，结束即可再攻击（仍受 flatAttackCooldown 限制）。")]
        [Min(0f)]
        public float flatAttackRecoverTime = 0.2f;

        [Tooltip("普攻冷却（秒）：从按攻击键那一刻起算，只有平劈走这份；斜上 / 斜下各有一份自己的冷却。")]
        [Min(0f)]
        public float flatAttackCooldown = 0.35f;

        [Tooltip("普攻的伤害点数：判定盒碰到怪物受击盒时按这个值扣血。\n0 表示不造成伤害，但仍然算一次攻击。")]
        [Min(0f)]
        public float flatAttackDamage = 20f;

        [Tooltip("普攻的击退初速度（单位/秒）：方向沿朝向；0 表示不击退。\n滑多远由目标自己的 KnockbackDecay 决定。")]
        [Min(0f)]
        public float flatAttackKnockback = 4f;

        [Tooltip("普攻空挥（没砍到人）时的后坐力（单位/秒）：沿劈击方向反向叠加一份外部速度，不直接改常规移动速度。\n0 表示空挥不后退。")]
        [Min(0f)]
        public float flatAttackMissRecoil = 3f;

        [Tooltip("普攻命中敌人时的后坐力（单位/秒）：命中会把空挥那一份整体换成这份，不是两者相加。\n同样乘下蹲倍率；0 表示命中不后退，但空挥那份仍会被撤掉。")]
        [Min(0f)]
        public float flatAttackHitRecoil = 10f;

        [Header("攻击 · 黑突进")]
        [Tooltip("黑突进前摇时长（秒）：按下攻击键到冲出去之间的等待，期间被打中会整段打断；方向由按下那一刻的竖直输入决定。\n这段时间速度锁成 0、重力也关掉，能挨打但不接受任何移动输入。")]
        [Min(0f)]
        public float blackDashPrepareTime = 0.12f;

        [Tooltip("黑突进后摇时长（秒）：冲完到这只状态收尾的等待，期间重力已恢复、也能照常移动，但跳跃 / 冲刺不接受，按别的方向攻击会直接顶掉这段后摇。\n0 表示冲完立刻收尾。")]
        [Min(0f)]
        public float blackDashRecoverTime = 0.15f;

        [Tooltip("斜上突进的冷却（秒）：从按攻击键那一刻起算，只占斜上这一份，平劈与斜下各有自己的冷却。")]
        [Min(0f)]
        public float blackDashUpCooldown = 0.6f;

        [Tooltip("斜下突进的冷却（秒）：从按攻击键那一刻起算，只占斜下这一份。")]
        [Min(0f)]
        public float blackDashDownCooldown = 0.6f;

        [Tooltip("水平突进的冷却（秒）：下蹲时按攻击键走的那一份，只占它自己。")]
        [Min(0f)]
        public float blackDashLevelCooldown = 0.6f;

        [Tooltip("斜上 45° 突进的初速度（单位/秒）：横向取朝向、竖直向上，两个分量各为 0.707 × 该值。")]
        [Min(0f)]
        public float blackDashSpeedUp = 18f;

        [Tooltip("斜下 45° 突进的初速度（单位/秒）：横向取朝向、竖直向下，两个分量各为 0.707 × 该值。")]
        [Min(0f)]
        public float blackDashSpeedDown = 20f;

        [Tooltip("斜上 45° 突进的持续时间（秒）：这段时间内重力关闭、速度不变，时间到进后摇，之后按重力与移动减速自己落回来。")]
        [Min(0f)]
        public float blackDashTimeUp = 0.18f;

        [Tooltip("斜下 45° 突进的持续时间（秒）：同样在结束时进后摇，撞到地面后由移动减速停住。")]
        [Min(0f)]
        public float blackDashTimeDown = 0.2f;

        [Tooltip("水平突进的初速度（单位/秒）：只取横向，沿当前朝向冲出去，竖直速度清零。")]
        [Min(0f)]
        public float blackDashSpeedLevel = 16f;

        [Tooltip("水平突进的持续时间（秒）：这段时间重力关闭、速度不变，时间到进后摇；命中或撞墙当帧就进后摇。")]
        [Min(0f)]
        public float blackDashTimeLevel = 0.2f;

        [Tooltip("黑突进的伤害点数：突进盒（斜上/斜下）碰到怪物受击盒时按这个值扣血。\n一次突进里同一个受击盒只结算一次，穿过去也只扣一次；0 表示不造成伤害。")]
        [Min(0f)]
        public float blackDashDamage = 20f;

        [Tooltip("黑突进的击退初速度（单位/秒）：方向沿突进方向（斜上/斜下按 45°）。\n滑多远由目标自己的 KnockbackDecay 决定；0 表示不击退。")]
        [Min(0f)]
        public float blackDashKnockback = 4f;

        [Tooltip("突进撞到第一个目标后向上弹起的竖直速度（单位/秒）：直接覆盖当前竖直速度，横向保留冲出去的那一份。\n命中即结束本体进后摇；0 表示命中不弹起，只停在原地。")]
        [Min(0f)]
        public float blackDashBounceSpeed = 14f;

        [Tooltip("空中黑突进的可用次数（次）：离地后最多连突进这么多次，落地当帧就补满。\n地面起手的突进不扣次数；0 表示空中不能突进。")]
        [Min(0)]
        public int blackDashAirCount = 1;

        [Tooltip("黑突进的土狼时间（秒）：离地后这段时间内起手仍按地面算——不吃空中次数，斜下突进也照旧不给。\n一次离地只借一次，落地才还回来；跳跃那份是 coyoteTime，两者互不影响。0 表示离地即算空中。")]
        [Min(0f)]
        public float blackDashCoyoteTime = 0.12f;

        [Tooltip("空中突进劈到敌人返还的次数（次）：每命中一个受击盒加回这么多，钳在上限内不会累积超过 blackDashAirCount。\n0 表示命中不返还。")]
        [Min(0)]
        public int blackDashHitRefund = 1;

        [Tooltip("弹反打回一发敌弹返还的空中突进次数（次）：每弹回一颗加回这么多，同样钳在上限内。\n0 表示弹反不返还。")]
        [Min(0)]
        public int blackDashReboundRefund = 1;

        [Header("受击")]
        [Tooltip("破坏力：敌人子弹打到玩家时扣的血量（点）。\n敌人只知道开了火，不知道玩家的数值——敌弹自带的伤害在这里一律被顶掉。0 表示敌弹不扣血。")]
        [Min(0f)]
        public float destruction = 1f;

        [Header("打击感 · 顿帧")]
        [Tooltip("近战命中敌人（普攻 / 突进）的顿帧时长（秒）：命中当帧整个游戏冻结，到点自动恢复。\n子弹命中不顿帧；0 表示不顿帧。")]
        [Min(0f)]
        public float meleeHitStopTime = 0.06f;

        [Tooltip("被敌人撞到或吃到敌弹（Contact / EnemyBullet）的顿帧时长（秒）：真扣到血才触发，被无敌窗挡掉的那次不顿帧。\n0 表示不顿帧。")]
        [Min(0f)]
        public float contactHitStopTime = 0.1f;

        [Tooltip("弹反打回一发敌弹的顿帧时长（秒）：每弹回一颗触发一次，打不回去的不算。\n0 表示不顿帧。")]
        [Min(0f)]
        public float reboundHitStopTime = 0.08f;

        [Header("打击感 · 震屏")]
        [Tooltip("受击震屏的冲量强度（倍率，1 = Cinemachine 标准冲量）：乘以受击击退方向得到冲量向量。0 表示不震屏。")]
        [Min(0f)]
        public float cameraShakeIntensity = 0.5f;

        [Tooltip("受击震屏的持续时间（秒）：从受击那帧起震这么久。0 表示沿用冲量源自己的时长。")]
        [Min(0f)]
        public float cameraShakeDuration = 0.2f;

        [Tooltip("世界黑白反转时的震屏强度（倍率，1 = Cinemachine 标准冲量）：方向固定朝上。0 表示不震屏。")]
        [Min(0f)]
        public float worldShakeIntensity = 0.8f;

        [Tooltip("世界黑白反转时的震屏持续时间（秒）：0 表示沿用冲量源自己的时长。")]
        [Min(0f)]
        public float worldShakeDuration = 0.3f;

        [Tooltip("弹反打回一发敌弹的震屏强度（倍率，1 = Cinemachine 标准冲量）：方向取子弹被弹回的方向。0 表示不震屏。")]
        [Min(0f)]
        public float reboundShakeIntensity = 1.2f;

        [Tooltip("弹反震屏的持续时间（秒）：0 表示沿用冲量源自己的时长。")]
        [Min(0f)]
        public float reboundShakeDuration = 0.12f;


        [Header("极性值")]
        [Tooltip("极性值上限（点）：每次有效攻击累加 polarityPerHit，涨到上限就停，不会溢出。")]
        [Min(1)]
        public int maxPolarity = 10;

        [Tooltip("每次有效攻击获得的极性值（点）：子弹真正扣掉怪物血量才算有效。\n打在受击无敌窗上、目标已死、或伤害值为 0 都不算，不会涨。")]
        [Min(0)]
        public int polarityPerHit = 5;

        [Tooltip("折跃连线每命中一个目标获得的极性值（点）：与子弹的 polarityPerHit 分开配。\n子弹穿一段线会打中线上所有目标，每个目标各算一次。")]
        [Min(0)]
        public int afterimageLinkPolarity = 2;

        [Header("充能 · 残影")]
        [Tooltip("充能前摇时长（秒）：按一下充能键开始蓄力，走满进入蓄满态（玩家变橙）。\n0 表示按下当帧就蓄满。")]
        [Min(0f)]
        public float chargePrepareTime = 0.5f;

        [Tooltip("充能冷却（秒）：从冲刺落影那一刻起算，不是从按键起算。\n冷却期间按充能键不响应；0 表示落影后立刻能再蓄。")]
        [Min(0f)]
        public float chargeCooldown = 1.5f;

        [Tooltip("充能冲刺的无敌时长（秒）：从落影那一刻起算，这段时间内不吃任何伤害、也不进受伤态。\n只对真正落了影的那次冲刺生效；0 表示不给无敌。")]
        [Min(0f)]
        public float dashInvulnerableTime = 0.15f;

        [Tooltip("场上残影的最大个数：超过上限时新残影会顶掉最早留下的那一枚，而不是拒绝放置。")]
        [Min(1)]
        public int maxAfterimages = 4;

        [Tooltip("残影持续时间（秒）：从落地那一刻起算，到点自动消失；0 表示不会自然消失，只等被数量上限顶掉。")]
        [Min(0f)]
        public float afterimageDuration = 8f;

        [Tooltip("折跃连线的伤害（点）：子弹穿过这条连线时，对线上的目标结算一次；0 表示不伤人。")]
        [Min(0f)]
        public float afterimageLinkDamage = 1f;

        [Tooltip("残影的感应范围（米）：只有生成顺序相邻、且间距在这个范围内的一对残影才连线。\n防止在超远的两处落影，拉出一条横穿地图的长线；0 表示不限距离。")]
        [Min(0f)]
        public float afterimageLinkSenseRadius = 6f;

        [Tooltip("子弹在残影之间传输时的移动速度（单位/秒）：从当前残影中心直线走向下一枚的速度。\n0 表示瞬间抵达下一枚，看不到中间过程。")]
        [Min(0f)]
        public float bulletTransferSpeed = 26f;

        [Header("弹反")]
        [Tooltip("一次弹反最长能开多久（秒）：与按住攻击键的时长取小，到点就关。\n0 表示不设上限，按住就一直开。")]
        [Min(0f)]
        public float reboundMaxTime = 0.4f;

        [Tooltip("弹反冷却（秒）：弹反关掉的那一刻起算，冷却没走完再按攻击键不开弹反。\n0 表示没有冷却。")]
        [Min(0f)]
        public float reboundCooldown = 0.6f;

        [Header("存档")]
        [Tooltip("存档成功时玩家闪白的时长（秒）：与受击闪白分开配，存档的短一些才不糊脸。\n0 表示不闪白。")]
        [Min(0f)]
        public float saveFlashTime = 0.1f;

        [Tooltip("存档成功后玩家不响应输入的时长（秒）：从存档成功那一刻起算，期间移动、跳跃、攻击、反转、存档全部停住，刚体与重力照常。\n0 表示不锁输入（会有连按存两次的风险）。")]
        [Min(0f)]
        public float saveInputLockTime = 0.5f;

        [Header("死亡")]
        [Tooltip("死亡演出时长（秒）：这段时间玩家视图淡到全透明，死亡粒子在尸体中点播一次，逻辑与输入全停。\n到点生成人头；0 表示当帧就淡完并生成人头。")]
        [Min(0f)]
        public float deathFadeTime = 0.6f;

        [Tooltip("人头停留时长（秒）：人头生成后再过这么久才复活玩家。\n0 表示人头一出现就复活（基本看不到）。")]
        [Min(0f)]
        public float deathHeadTime = 1f;
    }
}
