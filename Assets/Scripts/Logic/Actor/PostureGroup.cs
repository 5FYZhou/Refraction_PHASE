using Refraction.Data;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>姿态对移动的禁令，移动侧每帧读一次；移速不在这里，下蹲减速走 MoveLedger 的速度乘数</summary>
    public interface IPostureGate
    {
        /// <summary>当前姿态是否允许跳跃（地面跳、土狼跳、空中跳一并受管）。</summary>
        bool CanJump { get; }

        /// <summary>当前姿态是否允许冲刺。</summary>
        bool CanDash { get; }
    }

    /// <summary>踩地宽限：连续离地超过 grace 才算真离地，否则接触刷新掉一帧就让姿态每两帧抽一次</summary>
    /// <remarks>每帧先 Feed 再读 Grounded，同帧所有判定看同一个结论</remarks>
    public sealed class GroundGrace
    {
        private float _lastGroundedTime = float.NegativeInfinity;

        /// <summary>本帧算不算还站在地上</summary>
        public bool Grounded { get; private set; }

        public void Feed(bool grounded, float now, float grace)
        {
            if (grounded) _lastGroundedTime = now;
            Grounded = grounded || now - _lastGroundedTime <= grace;
        }
    }

    /// <summary>姿态状态组：Stand → CrouchingTransition（前摇）→ Crouch，起身回 Stand；只管禁令与身高倍率，不碰移动与物理</summary>
    /// <remarks>移动侧通过 IPostureGate 读禁令，身体尺寸交给 IMoveMotor，View 缩放交给 PlayerController</remarks>
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

        /// <summary>当前姿态，首帧还没进过状态机时为 PostureStateTag.None，按站姿算</summary>
        public PostureStateTag Current => machine.CurrentState?.Tag ?? PostureStateTag.None;

        /// <summary>限制跟着身体走：前摇期间不限制跳/冲，进度已满那一帧按蹲姿算</summary>
        public bool IsCrouching =>
            Current == PostureStateTag.Crouch
            || (Current == PostureStateTag.CrouchingTransition && _transitionState.Progress >= 1f);

        public bool IsCrouchingTransition => Current == PostureStateTag.CrouchingTransition;

        /// <summary>下蹲进度：0 = 站立，1 = 完全蹲下</summary>
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

        /// <summary>表现层该用的 View 缩放绝对值；与碰撞体高度共用同一份进度，各算一条曲线会看到人矮了图还高</summary>
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

        private float StandHeight => _motor.BodyHeight;

        private float CrouchHeight => _motor.BodyHeight * _config.crouchHeightRatio;

        /// <summary>兜底一律回站立，只有前摇有两个去处，由它自己的进度决定</summary>
        protected override PostureStateTag Fallback(in LogicContext ctx)
        {
            if (machine.CurrentState?.Tag == PostureStateTag.CrouchingTransition)
                return _transitionState.NextTag;

            return PostureStateTag.Stand;
        }

        /// <summary>推进姿态。必须在移动组之前调用，否则同帧读到的禁令会晚一帧。</summary>
        public void Tick(in LogicContext ctx)
        {
            // 先喂踩地宽限：同帧内前摇与 Crouch 的结束判定要看到同一结论
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

                // 前摇自己推进与打断，抢占别插手：返回 CrouchingTransition 会被判成抢占成功而反复重进
                case PostureStateTag.CrouchingTransition:
                    return false;
            }

            if (!CanCrouch(ctx)) return false;

            // 前摇为 0 时没有可过渡的时长，直接进蹲姿，省掉一帧半蹲
            target = _config.crouchPrepareTime > 0f
                ? PostureStateTag.CrouchingTransition
                : PostureStateTag.Crouch;

            return true;
        }

        protected override bool TryCommitPreempt(in LogicContext ctx, PostureStateTag target)
        {
            return target != EmptyTag;
        }

        /// <summary>触发条件：踩地且按住下蹲键；空中不给蹲，落地那一帧才进来</summary>
        private bool CanCrouch(in LogicContext ctx)
        {
            if (_needReleaseToCrouch) return false;

            return ctx.InputSnapshot.CrouchHeld && ctx.WorldInfo.Grounded;
        }

        /// <summary>能否起身：松开下蹲键只是想起身，还得头顶有空间才站得直</summary>
        protected virtual bool CanStandUp(in LogicContext ctx)
        {
            return _motor.CanStandUp(StandHeight, CrouchHeight);
        }

        /// <summary>打断下蹲前摇：立刻回站立，移速乘数与身体/View 缩放瞬间恢复，不播回弹过渡；不在前摇里调用无副作用</summary>
        /// <remarks>打断后要松开下蹲键才能重新蹲（_needReleaseToCrouch）；头顶空间不在这里检查，"瞬间恢复"就是这个接口的语义</remarks>
        public bool InterruptToStand(in LogicContext ctx)
        {
            if (Current != PostureStateTag.CrouchingTransition) return false;

            // Exit 里会把身体推回站高；移速乘数再补一次，免得本帧移动组还没跑、上一帧的过渡值又被用一帧
            ChangeState(PostureStateTag.Stand, ctx);
            _motor.SetSpeedScaleX(1f);

            _needReleaseToCrouch = true;
            return true;
        }
    }
}
