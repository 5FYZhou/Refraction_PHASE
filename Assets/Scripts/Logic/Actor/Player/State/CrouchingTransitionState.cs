using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>
    /// 下蹲前摇：从站姿过渡到蹲姿。
    /// 身体高度与移速乘数在 crouchPrepareTime 内按同一条线性曲线推进到蹲姿值，走完交给 <see cref="CrouchState"/>。
    ///
    /// 本态是可打断的：松开下蹲键、离地、或被外部调用 <see cref="PostureGroup.InterruptToStand"/>，
    /// 都立刻回站立，且移速乘数与身体/View 缩放瞬间恢复，不播放回弹过渡。
    /// </summary>
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

        /// <summary>本态结束时该去哪个姿态：走完进蹲姿，被打断回站立。</summary>
        /// <remarks>只在 <see cref="IsDone"/> 为 true 时被读到，所以未走完就一定是被打断了。</remarks>
        public PostureStateTag NextTag => _finished ? PostureStateTag.Crouch : PostureStateTag.Stand;

        public override void Enter(in LogicContext ctx)
        {
            _enterTime = ctx.GameTime.now;
            _progress = 0f;
            _finished = false;
            _running = true;

            // 进来先写一次 0 进度：过渡起点与站姿严格一致，
            // 中途被打断时"恢复到什么值"也就只有一处定义
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

        /// <summary>
        /// 把进度落到身体高度与移速乘数上。
        /// 两者共用同一条线性曲线：变矮和变慢是同一件事的两面，曲线分开会出现"人已经蹲下却还在全速跑"。
        ///
        /// 移速乘数每帧都必须重报——MoveLedger 帧首会把它复位成 1，漏报一帧那帧就恢复全程速度。
        /// </summary>
        private void Apply(float progress)
        {
            _motor.SetBodyScale(Mathf.Lerp(1f, _config.crouchHeightRatio, progress));
            _motor.SetSpeedScaleX(Mathf.Lerp(1f, _config.crouchMoveSpeedScale, progress));
        }

        public override bool IsDone(in LogicContext ctx)
        {
            // 松键 / 真正离地：被打断，立刻中止，不能停在半蹲。
            //
            // 打断时把 _finished 清掉：否则"进度刚好走满的那一帧同时离地"会走 NextTag 的蹲姿分支，
            // 人在空中被压扁一帧，下一帧再被 CrouchState 的离地判定踢回站姿。
            // 去向只看 _finished，两条路径就只有一个出口，不会再分叉。
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

            // 一律把身体推回站高：
            // · 被打断 → 这就是"瞬间恢复"，不播回弹（头顶空间不在这里检查，见 InterruptToStand 的说明）；
            // · 正常走完 → CrouchState.Enter 会在同一次 ChangeState 里立刻写回蹲姿高度，
            //   两次写入落在同一帧，中间不会被渲染，也就不存在空档。
            // 写成"无条件复原"是为了让打断路径只有一个出口：哪怕在"进度已满但状态机还没切"的那一帧
            // 被外部打断，身体也不会留在半蹲高度上。
            _motor.SetBodyScale(1f);

            _progress = 0f;
            _finished = false;
        }
    }
}
