using Refraction.Logic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Refraction.Foundation
{
    public interface IState<TTag, TContext>
    {
        public TTag Tag { get; }
        void Enter(in TContext cxt);
        void Tick(in TContext cxt);
        bool IsDone(in TContext cxt);
        void Exit();
    }

    public abstract class BaseState<TTag, TContext> : IState<TTag, TContext>
    {
        public abstract TTag Tag { get; }
        public abstract void Enter(in TContext cxt);
        public abstract void Tick(in TContext cxt);
        public abstract bool IsDone(in TContext cxt);
        public abstract void Exit();
    }

    public sealed class StateMachine<TTag, TContext>
    {
        private readonly Dictionary<TTag, IState<TTag, TContext>> _states = new();

        private IState<TTag, TContext> _currentState;
        public IState<TTag, TContext> CurrentState { get => _currentState; }

        public bool RegisterState(IState<TTag, TContext> state)
        {
            if (_states.ContainsKey(state.Tag))
                return false;

            _states[state.Tag] = state;
            return true;
        }

        public bool ChangeState(TTag tag, in TContext ctx)
        {
            if (!_states.TryGetValue(tag, out var nextState))
                return false;

            // 幂等
            if (_currentState == nextState)
                return true;

            _currentState?.Exit();
            _currentState = nextState;
            _currentState.Enter(ctx);

            Debug.Log(_currentState.Tag.ToString());

            return true;
        }
    }

}
