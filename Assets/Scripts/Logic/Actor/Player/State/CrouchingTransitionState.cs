using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>下蹲前摇：身体高度与移速乘数在 crouchPrepareTime 内共用一条线性曲线，走完交给 CrouchState</summary>
    /// <remarks>松键、被 InterruptToStand 打断时去向由 PostureGroup.Fallback 按头顶空间定：站不直就地蹲下，不硬回站高</remarks>
    public sealed class CrouchingTransitionState : BaseState<PostureStateTag, LogicContext>
    {
        public override PostureStateTag Tag => PostureStateTag.CrouchingTransition;

        private readonly IMoveMotor _motor;
        private readonly PlayerConfig _config;
        private readonly GroundGrace _ground;

        private float _enterTime;
        private float _progress;
        private bool _finished;
        private bool _running;

        public CrouchingTransitionState(IMoveMotor motor, PlayerConfig config, GroundGrace ground)
        {
            _motor = motor;
            _config = config;
            _ground = ground;
        }

        /// <summary>过渡进度：0 = 完全站立，1 = 完全蹲下。</summary>
        public float Progress => _progress;

        /// <summary>进度是否走满；未走满就结束一定是被打断，去向由 PostureGroup 按头顶空间决定</summary>
        public bool Finished => _finished;

        public override void Enter(in LogicContext ctx)
        {
            _enterTime = ctx.GameTime.now;
            _progress = 0f;
            _finished = false;
            _running = true;

            // 进来先写一次 0 进度：过渡起点与站姿严格一致，被打断时恢复到什么值也只有一处定义
            Apply(0f);
        }

        public override void Tick(in LogicContext ctx)
        {
            // 被打断退出后同一帧里还可能被调到，不能再往刚体上写值
            if (!_running) return;

            _progress = EvalProgress(ctx.GameTime.now);

            if (_progress >= 1f) _finished = true;

            Apply(_progress);
        }

        /// <summary>进度按时间线性推进。前摇配成 0 时视为一步到位，顺便躲开除零。</summary>
        private float EvalProgress(float now)
        {
            float duration = _config.crouchPrepareTime;
            if (duration <= 0f) return 1f;

            return Mathf.Clamp01((now - _enterTime) / duration);
        }

        /// <summary>把进度落到身体高度与移速乘数上，两者必须共用同一条曲线，分开会出现人已蹲下却还在全速跑</summary>
        /// <remarks>移速乘数每帧都要重报：MoveLedger 帧首复位成 1，漏报一帧那帧就恢复全程速度</remarks>
        private void Apply(float progress)
        {
            _motor.SetBodyScale(Mathf.Lerp(1f, _config.crouchHeightRatio, progress));
            _motor.SetSpeedScaleX(Mathf.Lerp(1f, _config.crouchMoveSpeedScale, progress));
        }

        public override bool IsDone(in LogicContext ctx)
        {
            // 松键 / 真正离地：被打断，立刻中止，不能停在半蹲
            // 打断时清掉 _finished：否则"进度刚好走满那帧同时离地"会走蹲姿分支，人在空中被压扁一帧
            if (!ctx.InputSnapshot.CrouchHeld || !_ground.Grounded)
            {
                _finished = false;
                return true;
            }

            return _finished;
        }

        public override void Exit()
        {
            _running = false;

            // 无条件复原站高：正常走完时 CrouchState.Enter 在同一帧写回蹲姿（中间不渲染），打断时这就是"瞬间恢复"
            _motor.SetBodyScale(1f);

            _progress = 0f;
            _finished = false;
        }
    }
}
