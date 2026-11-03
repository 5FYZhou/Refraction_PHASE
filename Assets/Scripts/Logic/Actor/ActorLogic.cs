using Refraction.Foundation;
using Refraction.Logic.Move;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public abstract class ActorLogic : IFixedTickable
    {
        // 移动
        private IMoveMotor _motor;

        public ActorLogic(IMoveMotor motor) 
        {
            _motor = motor;
        }

        public void FixedTick(LogicContext ctx)
        {
            _motor.BeginStep(ctx.GameTime.now, ctx.GameTime.deltaTime);
            _motor.ApplyGravity(ctx, false);

            OnTick(in ctx);

            _motor.Commit();
        }

        protected abstract void OnTick(in LogicContext ctx);
    }
}
