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

        public PlayerLogic(IMoveMotor motor, PlayerConfig config, InputBuffer buffer) : base(motor)
        {
            _moveGroup = new(motor, config, buffer);
        }

        protected override void OnTick(in LogicContext cxt)
        {
            _moveGroup.Tick(cxt);
        }
    }
}
