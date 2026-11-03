using Refraction.Foundation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public abstract class StateGroup<TTag, TContext>
    {
        private StateMachine<TTag, TContext> _machine = new StateMachine<TTag, TContext>();

        protected abstract TTag EmptyTag { get; }
        protected abstract TTag Fallback(TContext ctx);

        public void RegisterState(in IState<TTag, TContext> state)
        {
            _machine.RegisterState(state);
        }

        public void ChangeState(TTag tag)
        {
            _machine.ChangeState(tag);
        }

        protected void TickStates(in TContext ctx)
        {
            _machine.ChangeState(CheckChange(ctx));

            _machine.CurrentState?.Tick(ctx);
        }


        /// <summary> 检查是否能抢占 </summary>
        protected virtual bool TryDecidePreempt(in TContext ctx, out TTag target)
        {
            target = EmptyTag;
            return false;
        }

        /// <summary> 提交抢占 </summary>
        protected virtual bool TryCommitPreempt(in TContext ctx, TTag target)
        {
            return false;
        }

        public TTag CheckChange(in TContext ctx)
        {
            IState<TTag, TContext> current = _machine.CurrentState;

            // 首帧无当前状态，抢占照常判定，判不中才落到基础态
            if (TryDecidePreempt(ctx, out TTag target)
                && (current == null || !target.Equals(current))
                && TryCommitPreempt(ctx, target))
            {
                return target;
            }

            if (current == null) return Fallback(ctx);

            return current.IsDone(ctx) ? Fallback(ctx) : current.Tag;
        }
    }
}
