namespace Football.Core
{
    /// <summary>
    /// Generic state machine contract identified by a strongly-typed state ID.
    /// Supports: <c>IStateMachine&lt;PlayerStateId&gt;</c>,
    /// <c>IStateMachine&lt;GameStateId&gt;</c>, and any future enum-based state ID.
    /// </summary>
    /// <typeparam name="TStateId">Enum type used to identify states.</typeparam>
    public interface IStateMachine<TStateId> where TStateId : System.Enum
    {
        /// <summary>
        /// The ID of the currently active state.
        /// Returns <c>default(TStateId)</c> before any state has been entered.
        /// </summary>
        TStateId CurrentStateId { get; }

        /// <summary>
        /// The currently active state instance. May be null before the first state is entered.
        /// </summary>
        IState CurrentState { get; }

        /// <summary>
        /// Makes the state associated with <paramref name="stateId"/> the active state.
        /// Implementations should call <see cref="IState.Exit"/> on the previous state
        /// and <see cref="IState.Enter"/> on the new state.
        /// </summary>
        /// <param name="stateId">The ID of the state to transition to.</param>
        void ChangeState(TStateId stateId);

        /// <summary>
        /// Registers a state instance for the given ID so it can be activated via <see cref="ChangeState"/>.
        /// </summary>
        /// <param name="stateId">The ID to associate with the state.</param>
        /// <param name="state">The state instance.</param>
        void RegisterState(TStateId stateId, IState state);

        /// <summary>
        /// Advances the active state by one frame.
        /// </summary>
        /// <param name="deltaTime">Elapsed time since the previous frame.</param>
        void Tick(float deltaTime);
    }
}
