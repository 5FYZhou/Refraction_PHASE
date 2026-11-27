using Refraction.Foundation;
using Refraction.Logic.Move;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    /// <summary>观察：面向玩家但绝不主动靠近，只按随机方向极缓慢漂移，给玩家输出窗口</summary>
    /// <remarks>结束时按距离分流：超出理想距离上限就逼近，否则回决策继续悬浮</remarks>
    public sealed class MmflyObserveState : BaseState<MmflyStateTag, LogicContext>
    {
        private readonly MmflyLogic _owner;
        private readonly IMoveMotor _motor;

        private Vector2 _dir = Vector2.right;
        private float _start;
        private float _duration;
        private bool _done;

        public MmflyObserveState(MmflyLogic owner, IMoveMotor motor)
        {
            _owner = owner;
            _motor = motor;
        }

        public override MmflyStateTag Tag => MmflyStateTag.Observe;

        public override void Enter(in LogicContext ctx)
        {
            _start = ctx.GameTime.now;
            _duration = _owner.RollObserveTime();
            _dir = _owner.RollDriftDir(ctx.WorldInfo.Pos);
            _done = false;
        }

        public override void Tick(in LogicContext ctx)
        {
            Vector2 delta = _owner.TargetPos - ctx.WorldInfo.Pos;

            _owner.FaceTarget(delta);

            // 漂移撞墙就悬停：观察态没有绕路逻辑，顶着墙飞只会抖
            _motor.SnapVelocity(ctx.WorldInfo.TouchingWall ? Vector2.zero : _dir * _owner.ObserveDriftSpeed);

            if (_done || ctx.GameTime.now - _start < _duration) return;

            _done = true;

            _owner.SetPending(delta.magnitude > _owner.IdealDistance
                ? MmflyStateTag.Approach
                : MmflyStateTag.Decide);
        }

        public override bool IsDone(in LogicContext ctx) => _done;

        public override void Exit() { }
    }
}
