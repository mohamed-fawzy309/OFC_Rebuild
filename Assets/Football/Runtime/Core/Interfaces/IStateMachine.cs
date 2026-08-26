namespace Football.Core
{
    /// <summary>
    /// Non-generic state machine contract.
    /// Exposes the current active state, state transitions, and per-frame ticking.
    /// </summary>
    public interface IStateMachine
    {
        /// <summary>
        /// The currently active state. May be null before any state has been entered.
        /// </summary>
        IState CurrentState { get; }

        /// <summary>
        /// Makes <paramref name="newState"/> the active state.
        /// Implementations should call <see cref="IState.Exit"/> on the previous
        /// state and <see cref="IState.Enter"/> on the new state.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        void ChangeState(IState newState);

        /// <summary>
        /// Advances the active state by one frame.
        /// </summary>
        /// <param name="deltaTime">Elapsed time since the previous frame.</param>
        void Tick(float deltaTime);
    }
}
