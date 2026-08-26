using System;
using System.Collections.Generic;

namespace Football.Core
{
    public class GenericStateMachine<TStateId> : IStateMachine<TStateId> where TStateId : Enum
    {
        private readonly Dictionary<TStateId, IState> _states = new();
        private IState _currentState;

        public TStateId CurrentStateId { get; private set; }
        public IState CurrentState => _currentState;

        public void RegisterState(TStateId stateId, IState state)
        {
            _states[stateId] = state;
        }

        public void ChangeState(TStateId stateId)
        {
            if (!_states.TryGetValue(stateId, out var newState))
                throw new InvalidOperationException($"State {stateId} not registered.");

            _currentState?.Exit();
            CurrentStateId = stateId;
            _currentState = newState;
            _currentState.Enter();
        }

        public void Tick(float deltaTime)
        {
            _currentState?.Tick(deltaTime);
        }
    }
}
