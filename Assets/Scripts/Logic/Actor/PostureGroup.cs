using Refraction.Data;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>
    /// 姿态对移动的约束。移动侧每帧读一次，不关心姿态是怎么来的。
    /// 移速不在这里：下蹲减速走 MoveLedger 的速度乘数，姿势与速度是两个通道。
    /// </summary>
    public interface IPostureGate
    {
        /// <summary>当前姿态是否允许跳跃（地面跳、土狼跳、空中跳一并受管）。</summary>
        bool CanJump { get; }

        /// <summary>当前姿态是否允许冲刺。</summary>
        bool CanDash { get; }
    }

    /// <summary>
    /// 踩地宽限。地面探测偶尔会掉一两帧（接触刷新、碰撞体当帧刚改过高度、
    /// 求解器把身体顶起一点点），直接按"离地"处理姿态，人就会被踢回站姿、
    /// 下一帧又蹲下，看起来是每两帧抽一次。
    ///
    /// 只有连续离地超过宽限时长才算真的离地。每帧先 <see cref="Feed"/> 再读 <see cref="Grounded"/>，
    /// 同帧内的所有判定看到的是同一个结论。
    /// </summary>
    public sealed class GroundGrace
    {
        private float _lastGroundedTime = float.NegativeInfinity;

        /// <summary>本帧算不算"还站在地上"。</summary>
        public bool Grounded { get; private set; }

        public void Feed(bool grounded, float now, float grace)
        {
            if (grounded) _lastGroundedTime = now;
            Grounded = grounded || now - _lastGroundedTime <= grace;
        }
    }

    /// <summary>
    /// 姿态状态组：Stand（站立）→ CrouchingTransition（前摇）→ Crouch（完全蹲下），起身从 Crouch 直接回 Stand。
    /// 只管姿态本身（前摇、允许的动作、身高倍率），不碰移动与物理：
    /// 移动侧通过 <see cref="IPostureGate"/> 读禁令，身体尺寸交给 IMoveMotor，View 缩放交给 PlayerController。
    /// </summary>
    public class PostureGroup : StateGroup<PostureStateTag, LogicContext>, IPostureGate
    {
        protected override PostureStateTag EmptyTag => PostureStateTag.None;

        private readonly PlayerConfig _config;
        private readonly IMoveMotor _motor;
        private readonly CrouchingTransitionState _transitionState;
        private readonly GroundGrace _ground = new();

        /// <summary>被打断后要松手才能重新蹲：否则按住 S 的玩家下一帧就被自动拖回前摇，打断等于没发生。</summary>
        private bool _needReleaseToCrouch;

        public PostureGroup(PlayerConfig config, IMoveMotor motor)
        {
            _config = config;
            _motor = motor;
            _transitionState = new CrouchingTransitionState(motor, config, _ground);

            RegisterState(new StandState());
            RegisterState(_transitionState);
            RegisterState(new CrouchState(motor, config, _ground));
        }

        /// <summary>当前姿态。首帧还没进过状态机时为 <see cref="PostureStateTag.None"/>，按站姿算。</summary>
        public PostureStateTag Current => machine.CurrentState?.Tag ?? PostureStateTag.None;

        /// <summary>
        /// 已完全蹲下（前摇已走完），限制才生效。
        /// 前摇期间为 false：身体正在变矮、移速正在降，但跳和冲仍按站姿给——
        /// 前摇被打断时人应该能直接起跳走人，而不是被半蹲姿态卡住。
        ///
        /// 例外是"进度已满但状态机还没切到 Crouch"的那一帧：此时身体已经是蹲姿，
        /// 限制要跟着身体走，否则会出现一帧"蹲着还能跳"。
        /// </summary>
        public bool IsCrouching =>
            Current == PostureStateTag.Crouch
            || (Current == PostureStateTag.CrouchingTransition && _transitionState.Progress >= 1f);

        /// <summary>下蹲前摇是否正在进行。</summary>
        public bool IsCrouchingTransition => Current == PostureStateTag.CrouchingTransition;

        /// <summary>下蹲进度：0 = 站立，1 = 完全蹲下。前摇期间按时间线性推进。</summary>
        public float CrouchProgress
        {
            get
            {
                if (Current == PostureStateTag.Crouch) return 1f;
                if (Current != PostureStateTag.CrouchingTransition) return 0f;

                return _transitionState.Progress;
            }
        }

        public bool CanJump => !IsCrouching || _config.crouchAllowJump;

        public bool CanDash => !IsCrouching || _config.crouchAllowDash;

        /// <summary>
        /// 表现层该用的 View 缩放（绝对值）。逻辑层只负责给值，具体落到 Transform 上由 PlayerController 做。
        /// 前摇期间按同一份进度插值，和碰撞体高度严格同步——两边各算一条曲线错开会看到"人矮了图还高"。
        /// </summary>
        public Vector2 ViewScale
        {
            get
            {
                float height = Current switch
                {
                    PostureStateTag.Crouch => _config.crouchHeightRatio,
                    PostureStateTag.CrouchingTransition =>
                        Mathf.Lerp(1f, _config.crouchHeightRatio, _transitionState.Progress),
                    _ => 1f
                };

                return new Vector2(1f, height);
            }
        }

        /// <summary>站立时的碰撞体高度，由表现层按实际碰撞体量出来。</summary>
        private float StandHeight => _motor.BodyHeight;

        /// <summary>蹲下后该有的碰撞体高度。</summary>
        private float CrouchHeight => _motor.BodyHeight * _config.crouchHeightRatio;

        /// <summary>
        /// 状态之外的兜底一律回站立。唯一的例外是前摇：它结束时有两个去处（蹲姿 / 站立），由它自己的进度决定。
        /// </summary>
        protected override PostureStateTag Fallback(in LogicContext ctx)
        {
            if (machine.CurrentState?.Tag == PostureStateTag.CrouchingTransition)
                return _transitionState.NextTag;

            return PostureStateTag.Stand;
        }

        /// <summary>推进姿态。必须在移动组之前调用，否则同帧读到的禁令会晚一帧。</summary>
        public void Tick(in LogicContext ctx)
        {
            // 先喂踩地宽限：同帧里前摇/Crouch 的结束判定要看到同一个结论
            _ground.Feed(ctx.WorldInfo.Grounded, ctx.GameTime.now, _config.crouchGroundGrace);

            if (_needReleaseToCrouch && !ctx.InputSnapshot.CrouchHeld)
                _needReleaseToCrouch = false;

            TickStates(ctx);
        }

        protected override bool TryDecidePreempt(in LogicContext ctx, out PostureStateTag target)
        {
            target = EmptyTag;

            switch (Current)
            {
                // 已经蹲着：只判起身，不重复抢占自己
                case PostureStateTag.Crouch:
                    if (ctx.InputSnapshot.CrouchHeld || !CanStandUp(ctx)) return false;

                    target = PostureStateTag.Stand;
                    return true;

                // 前摇自己推进、自己判断打断，抢占不插手：
                // 这里若再返回一次 CrouchingTransition，同帧会被判成"抢占成功"而反复重进
                case PostureStateTag.CrouchingTransition:
                    return false;
            }

            if (!CanCrouch(ctx)) return false;

            // 前摇为 0 时没有可过渡的时长，直接进蹲姿，省掉一帧停在半蹲的中间态
            target = _config.crouchPrepareTime > 0f
                ? PostureStateTag.CrouchingTransition
                : PostureStateTag.Crouch;

            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, PostureStateTag target)
        {
            // 下蹲没有要消费的输入，也不占用任何次数，切过去即可
            return target != EmptyTag;
        }

        /// <summary>触发条件：站立且按住下蹲键。空中不给蹲，落地后按着键会在落地那一帧进来。</summary>
        private bool CanCrouch(in LogicContext ctx)
        {
            if (_needReleaseToCrouch) return false;

            return ctx.InputSnapshot.CrouchHeld && ctx.WorldInfo.Grounded;
        }

        /// <summary>
        /// 能否起身：松开下蹲键只是"想起身"，还得头顶有空间才站得直。
        /// 只在 Crouch（已完全蹲下）时会问到，此时身体就是蹲姿高度。
        /// </summary>
        protected virtual bool CanStandUp(in LogicContext ctx)
        {
            return _motor.CanStandUp(StandHeight, CrouchHeight);
        }

        /// <summary>
        /// 打断当前的下蹲前摇：立刻回到站立，移速乘数与身体/View 缩放瞬间恢复，不播放回弹过渡。
        /// 预留给受击、强制起身、被其他系统抢占姿态这类外部打断；不在前摇里调用无副作用。
        ///
        /// 打断后要求松开下蹲键才能重新蹲（_needReleaseToCrouch），否则按住 S 的玩家下一帧又被拖回前摇。
        /// 头顶空间不在这里检查——"瞬间恢复"就是本接口的语义，头顶有硬顶时由打断方先自己判断。
        /// </summary>
        public bool InterruptToStand(in LogicContext ctx)
        {
            if (Current != PostureStateTag.CrouchingTransition) return false;

            // Exit 里会把身体推回站高；移速乘数再补一次，
            // 免得本帧移动组还没跑、上一帧留下的过渡值又被用一帧
            ChangeState(PostureStateTag.Stand, ctx);
            _motor.SetSpeedScaleX(1f);

            _needReleaseToCrouch = true;
            return true;
        }
    }
}
