using System;
using System.Collections.Generic;

namespace Football.Core
{
    /// <summary>
    /// Reusable state machine implementation identified by a strongly-typed enum state ID.
    /// Supports any <see cref="IStateMachine{TStateId}"/> where TStateId is an enum.
    /// Thread-safe for single-threaded game-loop usage.
    /// </summary>
    /// <typeparam name="TStateId">Enum type used to identify states.</typeparam>
    public class GenericStateMachine<TStateId> : IStateMachine<TStateId> where TStateId : Enum
    {
        private readonly Dictionary<TStateId, IState> _states = new();
        private IState _currentState;

        /// <summary>
        /// The ID of the currently active state.
        /// Returns <c>default(TStateId)</c> before any state has been entered.
        /// </summary>
        public TStateId CurrentStateId { get; private set; }

        /// <summary>
        /// The currently active state instance.
        /// Returns null before the first state is entered.
        /// </summary>
        public IState CurrentState => _currentState;

        /// <summary>
        /// Associates a state instance with the given ID so it can be activated via <see cref="ChangeState"/>.
        /// Throws if a null state is provided or if the ID is already registered.
        /// </summary>
        /// <param name="stateId">The ID to associate with the state.</param>
        /// <param name="state">The state instance. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="stateId"/> is already registered.</exception>
        public void RegisterState(TStateId stateId, IState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state), $"Cannot register null state for {stateId}.");

            if (_states.ContainsKey(stateId))
                throw new InvalidOperationException($"State {stateId} is already registered.");

            _states[stateId] = state;
        }

        /// <summary>
        /// Transitions to the state identified by <paramref name="stateId"/>.
        /// Calls <see cref="IState.Exit"/> on the previous state (if any), then <see cref="IState.Enter"/> on the new state.
        /// If the requested state is already the current state, the transition is skipped (idempotent).
        /// </summary>
        /// <param name="stateId">The ID of the state to transition to.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="stateId"/> has not been registered.</exception>
        public void ChangeState(TStateId stateId)
        {
            if (!_states.TryGetValue(stateId, out var newState))
                throw new InvalidOperationException($"State {stateId} not registered.");

            if (ReferenceEquals(_currentState, newState))
                return;

            _currentState?.Exit();
            CurrentStateId = stateId;
            _currentState = newState;
            _currentState.Enter();
        }

        /// <summary>
        /// Advances the active state by one frame.
        /// Does nothing if no state has been entered yet.
        /// </summary>
        /// <param name="deltaTime">Elapsed time since the previous frame.</param>
        public void Tick(float deltaTime)
        {
            _currentState?.Tick(deltaTime);
        }
    }
}
