using Refraction.Foundation;
using Refraction.Logic.Move;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public sealed class DashState : BaseState<MovementStateTag, LogicContext>
    {
        public override MovementStateTag Tag => MovementStateTag.Dash;

        private IStateMove _motor;
        private float _startTime;
        private float _speed;
        private float _duration;
        private Vector2 _dir;

        public DashState(IStateMove motor, float speed, float duration)
        {
            _motor = motor;
            _speed = speed;
            _duration = duration;
        }

        /// <summary>状态组切换前喂入方向，零=保持上次</summary>
        public void Configure(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0f) return;

            _dir = direction.normalized;
        }

        public override void Enter(in LogicContext ctx)
        {
            _startTime = ctx.GameTime.now;
            _motor.AddImpulse(_dir * _speed);
        }

        public override void Exit()
        {
        }

        public override bool IsDone(in LogicContext ctx)
        {
            return ctx.GameTime.now - _startTime > _duration;
        }

        public override void Tick(in LogicContext cxt)
        {
        }
    }
}
