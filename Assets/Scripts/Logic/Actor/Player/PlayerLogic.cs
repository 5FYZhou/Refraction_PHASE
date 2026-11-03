using Refraction.Logic.Move;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public class PlayerLogic : ActorLogic
    {
        private MoveGroup _moveGroup;

        public PlayerLogic(IMoveMotor motor) : base(motor)
        {
            _moveGroup = new(motor);
        }

        protected override void OnTick(in LogicContext cxt)
        {
            _moveGroup.Tick(cxt.InputSnapshot);
        }
    }
}
