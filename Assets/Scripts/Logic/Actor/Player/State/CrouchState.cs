using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>
    /// 已完全蹲下：身体高度与移速都停在蹲姿值上，跳跃/冲刺按配置限。
    /// 进来只有两条路：前摇走完（CrouchingTransition → 本态），或前摇配成 0 直接进本态。
    /// 起身由 <see cref="PostureGroup"/> 判（松开键且头顶有空间），本状态只在自己离地时结束。
    /// </summary>
    public sealed class CrouchState : BaseState<PostureStateTag, LogicContext>
    {
        public override PostureStateTag Tag => PostureStateTag.Crouch;

        private readonly IMoveMotor _motor;
        private readonly PlayerConfig _config;
        private readonly GroundGrace _ground;

        public CrouchState(IMoveMotor motor, PlayerConfig config, GroundGrace ground)
        {
            _motor = motor;
            _config = config;
            _ground = ground;
        }

        public override void Enter(in LogicContext ctx)
        {
            // 从前摇过来时这里是把同一个值再写一遍，不会跳；前摇为 0 时靠这一下直接压到位。
            // 身体缩放走绝对值而不是增量，所以重复写是安全的。
            _motor.SetBodyScale(_config.crouchHeightRatio);
        }

        public override void Tick(in LogicContext ctx)
        {
            // 速度乘数在 MoveLedger 里是"一次性"的（帧首复位），必须每帧重新提交；
            // 漏报一帧，那一帧就恢复成全程 moveSpeed
            _motor.SetSpeedScaleX(_config.crouchMoveSpeedScale);
        }

        /// <summary>
        /// 离地即结束：空中不给蹲，落地后如果还按着键会重新进前摇。
        /// 走 crouchGroundGrace 宽限——踩地判定偶尔掉一两帧不算真的离地，
        /// 否则会被踢回站姿、下一帧又蹲下，形成每两帧一次的抽搐。
        /// </summary>
        public override bool IsDone(in LogicContext ctx)
        {
            return !_ground.Grounded;
        }

        public override void Exit()
        {
            // 起身：还原站立身高
            _motor.SetBodyScale(1f);
        }
    }
}
