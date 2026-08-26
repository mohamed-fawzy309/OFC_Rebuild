namespace Football.Core
{
    /// <summary>
    /// Lifecycle-driven runtime state contract.
    /// Implement to define behavior for a discrete state within a state machine.
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Called once when this state becomes the active state.
        /// </summary>
        void Enter();

        /// <summary>
        /// Called once per frame while this state is the active state.
        /// </summary>
        /// <param name="deltaTime">Elapsed time since the previous frame.</param>
        void Tick(float deltaTime);

        /// <summary>
        /// Called once when this state is no longer the active state.
        /// </summary>
        void Exit();
    }
}
