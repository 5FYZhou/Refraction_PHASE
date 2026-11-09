using Refraction.Foundation;
using Refraction.Logic.Move;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public abstract class ActorLogic : IFixedTickable, ITickable
    {
        private IMoveMotor _motor;

        public ActorLogic(IMoveMotor motor) 
        {
            _motor = motor;
        }

        public void FixedTick(LogicContext ctx)
        {
            _motor.BeginStep(ctx.GameTime.now, ctx.GameTime.deltaTime);

            OnFixedTick(in ctx);

            _motor.ApplyGravity(ctx);
            _motor.Commit();

            _motor.ApplyGates();
        }

        public void Tick(LogicContext ctx)
        {
            OnTick(ctx);
        }

        protected abstract void OnFixedTick(in LogicContext ctx);

        protected abstract void OnTick(in LogicContext ctx);
    }
}
