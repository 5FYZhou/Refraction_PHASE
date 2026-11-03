using Refraction.Logic.Move;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public class MoveGroup : StateGroup<MovementStateTag, InputSnapshot>
    {
        protected override MovementStateTag EmptyTag => MovementStateTag.None;

        private IStateMove motor;

        public MoveGroup(IStateMove motor) 
        {
            this.motor = motor;
            Init();
        }

        public void Init()
        {
            RegisterState(new IdleState(motor));
            RegisterState(new MoveState(motor));
        }
         
        protected override MovementStateTag Fallback(InputSnapshot ctx)
        {
            // 如果有速度 回到Move
            if (ctx.Move.sqrMagnitude > 0) return MovementStateTag.Move;
            // Fall?
            return MovementStateTag.Idle;
        }

        public void Tick(InputSnapshot ctx)
        {
            TickStates(ctx);
        }

        protected override bool TryDecidePreempt(in InputSnapshot ctx, out MovementStateTag target)
        {
            // 如果冲刺没在冷却 冲刺能抢占
            target = EmptyTag;
            return false;
        }

        protected override bool TryCommitPreempt(in InputSnapshot ctx, MovementStateTag target)
        {
            return false;
        }
    }
}
