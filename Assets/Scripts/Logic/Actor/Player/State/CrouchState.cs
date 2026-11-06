using Refraction.Data;
using Refraction.Foundation;
using Refraction.Logic.Move;

namespace Refraction.Logic.Actor
{
    /// <summary>已完全蹲下：身体与移速停在蹲姿值；起身由 PostureGroup 判（松键且头顶有空间），本态不自建结束条件</summary>
    public sealed class CrouchState : BaseState<PostureStateTag, LogicContext>
    {
        public override PostureStateTag Tag => PostureStateTag.Crouch;

        private readonly IMoveMotor _motor;
        private readonly PlayerConfig _config;

        public CrouchState(IMoveMotor motor, PlayerConfig config)
        {
            _motor = motor;
            _config = config;
        }

        public override void Enter(in LogicContext ctx)
        {
            // 身体缩放走绝对值，重复写不动；前摇配成 0 时靠这一下直接压到位
            _motor.SetBodyScale(_config.crouchHeightRatio);
        }

        public override void Tick(in LogicContext ctx)
        {
            // 速度乘数在 MoveLedger 里是一次性的（帧首复位），必须每帧重报，漏报一帧就恢复全程 moveSpeed
            _motor.SetSpeedScaleX(_config.crouchMoveSpeedScale);
        }

        /// <summary>永不自结束：姿态与踩地解耦（空中也蹲），起身只走 PostureGroup 抢占那一处判据</summary>
        public override bool IsDone(in LogicContext ctx) => false;

        public override void Exit()
        {
            _motor.SetBodyScale(1f);
        }
    }
}
