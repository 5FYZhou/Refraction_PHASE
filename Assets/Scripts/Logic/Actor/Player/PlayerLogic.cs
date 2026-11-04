using Refraction.Data;
using Refraction.Logic.Input;
using Refraction.Logic.Move;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public sealed class PlayerLogic : ActorLogic
    {
        private MoveGroup _moveGroup;
        private IMoveMotor _motor;

        public PlayerLogic(IMoveMotor motor, PlayerConfig config, InputBuffer buffer) : base(motor)
        {
            _moveGroup = new(motor, config, buffer);
            _motor = motor;
        }

        protected override void OnTick(in LogicContext ctx)
        {
            _moveGroup.Tick(ctx);
        }

        public void SpawnTo(Vector2 pos)
        {
            _motor.SetPosition(pos);
            _motor.SetVelocityX(0);
            _motor.SetVelocityY(0);
        }
    }
}
