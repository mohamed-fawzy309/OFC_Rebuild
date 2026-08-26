namespace Football.Core
{
    /// <summary>
    /// Provides a reusable default implementation for <see cref="IState"/>.
    /// Concrete states derive from this and override only the methods they need.
    /// </summary>
    public abstract class StateBase : IState
    {
        /// <summary>
        /// Called when the state becomes active. Empty by default.
        /// </summary>
        public virtual void Enter() { }

        /// <summary>
        /// Called while the state is active. Empty by default.
        /// </summary>
        /// <param name="deltaTime">Elapsed time since the previous frame.</param>
        public virtual void Tick(float deltaTime) { }

        /// <summary>
        /// Called when the state stops being active. Empty by default.
        /// </summary>
        public virtual void Exit() { }
    }
}
