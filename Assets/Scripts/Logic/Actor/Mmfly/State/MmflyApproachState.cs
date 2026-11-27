using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>逼近：朝玩家缓慢直线靠近并带轻微横向摆动，进到理想距离就停</summary>
    /// <remarks>途中技能冷却好了且进距离，就当决策已完成直接起手；不做绕圈</remarks>
    public sealed class MmflyApproachState : BaseState<MmflyStateTag, LogicContext>
    {
        private const float SwayRate = 2f;
        private const float ArriveEps = 1e-4f;

        private readonly MmflyLogic _owner;
        private readonly IMoveMotor _motor;

        private bool _done;

        public MmflyApproachState(MmflyLogic owner, IMoveMotor motor)
        {
            _owner = owner;
            _motor = motor;
        }

        public override MmflyStateTag Tag => MmflyStateTag.Approach;

        public override void Enter(in LogicContext ctx) => _done = false;

        public override void Tick(in LogicContext ctx)
        {
            Vector2 delta = _owner.TargetPos - ctx.WorldInfo.Pos;
            float distance = delta.magnitude;

            _owner.FaceTarget(delta);

            if (distance <= _owner.IdealDistance || distance <= ArriveEps)
            {
                Stop();
                _owner.SetPending(MmflyStateTag.Decide);

                return;
            }

            MmflyStateTag skill = _owner.PickSkill(ctx.WorldInfo.Pos, ctx.GameTime.now);

            if (skill != MmflyStateTag.None)
            {
                Stop();
                _owner.SetPending(skill);

                return;
            }

            Vector2 dir = delta / distance;
            Vector2 side = new(-dir.y, dir.x);
            float sway = Mathf.Sin(ctx.GameTime.now * SwayRate) * _owner.ApproachSway;

            _motor.SnapVelocity(dir * _owner.ApproachSpeed + side * sway);
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }

        private void Stop()
        {
            _done = true;
            _motor.SnapVelocity(Vector2.zero);
        }
    }
}
