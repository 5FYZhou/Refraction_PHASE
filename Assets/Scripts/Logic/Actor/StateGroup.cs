using Refraction.Foundation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Logic.Actor
{
    public abstract class StateGroup<TTag, TContext>
    {
        protected StateMachine<TTag, TContext> machine = new StateMachine<TTag, TContext>();

        protected abstract TTag EmptyTag { get; }
        protected abstract TTag Fallback(in TContext ctx);

        public void RegisterState(in IState<TTag, TContext> state)
        {
            machine.RegisterState(state);
        }

        public void ChangeState(TTag tag, in TContext ctx)
        {
            machine.ChangeState(tag, ctx);
        }

        protected void TickStates(in TContext ctx)
        {
            machine.ChangeState(CheckChange(ctx), ctx);

            machine.CurrentState?.Tick(ctx);
        }


        /// <summary>只做判定：Commit 可能因目标重复而不执行，副作用别放这里</summary>
        protected virtual bool TryDecidePreempt(in TContext ctx, out TTag target)
        {
            target = EmptyTag;
            return false;
        }

        protected virtual bool TryCommitPreempt(in TContext ctx, TTag target)
        {
            return false;
        }

        public TTag CheckChange(in TContext ctx)
        {
            IState<TTag, TContext> current = machine.CurrentState;

            // 首帧没有当前状态，抢占照常判定，判不中才落到基础态
            if (TryDecidePreempt(ctx, out TTag target)
                && (current == null || !target.Equals(current.Tag))
                && TryCommitPreempt(ctx, target))
            {
                return target;
            }

            if (current == null) return Fallback(ctx);

            return current.IsDone(ctx) ? Fallback(ctx) : current.Tag;
        }
    }
}
