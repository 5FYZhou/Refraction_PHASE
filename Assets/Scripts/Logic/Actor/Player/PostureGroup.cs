using Refraction.Data;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>姿态对移动的禁令；下蹲减速不在这里，走 MoveLedger 的速度乘数</summary>
    public interface IPostureGate
    {
        /// <summary>地面跳、土狼跳、空中跳一并受管</summary>
        bool CanJump { get; }

        bool CanDash { get; }
    }

    /// <summary>踩地宽限：连续离地超过 grace 才算真离地，否则接触刷新掉一帧就让姿态每两帧抽一次</summary>
    public sealed class GroundGrace
    {
        private float _lastGroundedTime = float.NegativeInfinity;

        public bool Grounded { get; private set; }

        public void Feed(bool grounded, float now, float grace)
        {
            if (grounded) _lastGroundedTime = now;
            Grounded = grounded || now - _lastGroundedTime <= grace;
        }
    }

    /// <summary>姿态组：Stand → CrouchingTransition（前摇）→ Crouch，起身回 Stand；只管禁令与身高，不碰移动与物理</summary>
    /// <remarks>禁令走 IPostureGate，身高走 IMoveMotor，View 缩放走 PlayerController</remarks>
    public class PostureGroup : StateGroup<PostureStateTag, LogicContext>, IPostureGate
    {
        protected override PostureStateTag EmptyTag => PostureStateTag.None;

        private readonly PlayerConfig _config;
        private readonly IMoveMotor _motor;
        private readonly CrouchingTransitionState _transitionState;
        private readonly GroundGrace _ground = new();

        /// <summary>被打断后必须松手才能重新蹲，否则按住 S 的下一帧又被拖回前摇，打断等于没发生</summary>
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

        /// <summary>首帧未进状态机时为 None，按站姿算</summary>
        public PostureStateTag Current => machine.CurrentState?.Tag ?? PostureStateTag.None;

        /// <summary>限制跟着身体走：前摇期间不限制跳/冲，进度已满那一帧按蹲姿算</summary>
        public bool IsCrouching =>
            Current == PostureStateTag.Crouch
            || (Current == PostureStateTag.CrouchingTransition && _transitionState.Progress >= 1f);

        public bool IsCrouchingTransition => Current == PostureStateTag.CrouchingTransition;

        /// <summary>0 = 站立，1 = 蹲满</summary>
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

        /// <summary>表现层该用的 View 缩放绝对值；与碰撞体高度共用同一份进度</summary>
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

        protected override PostureStateTag Fallback(in LogicContext ctx)
        {
            if (machine.CurrentState?.Tag == PostureStateTag.CrouchingTransition)
                return _transitionState.NextTag;

            return PostureStateTag.Stand;
        }

        /// <summary>必须在移动组之前调用，否则同帧读到的禁令会晚一帧。</summary>
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

        protected virtual bool CanStandUp(in LogicContext ctx)
        {
            return _motor.CanStandUp(StandHeight, CrouchHeight);
        }

        /// <summary>打断前摇回站立：乘数与身体 / View 缩放立刻恢复，不播回弹过渡</summary>
        /// <remarks>不在前摇里调用无副作用；打断后要松手才能重新蹲（_needReleaseToCrouch）；头顶空间不在这里检查</remarks>
        public bool InterruptToStand(in LogicContext ctx)
        {
            if (Current != PostureStateTag.CrouchingTransition) return false;

            // Exit 已把身体推回站高；乘数再补一次，否则本帧移动组还读上一帧的过渡值
            ChangeState(PostureStateTag.Stand, ctx);
            _motor.SetSpeedScaleX(1f);

            _needReleaseToCrouch = true;
            return true;
        }
    }
}
