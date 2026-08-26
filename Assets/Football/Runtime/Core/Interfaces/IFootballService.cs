namespace Football.Core
{
    /// <summary>
    /// Lifecycle contract for project-level gameplay services.
    /// Services implementing this interface are expected to be managed
    /// by a Composition Root or service lifecycle system, not by Singletons.
    /// </summary>
    public interface IFootballService
    {
        /// <summary>
        /// Called once when the service becomes active.
        /// The service should become operational after a successful call.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Called when the service is being torn down.
        /// Should release service-owned resources and unsubscribe from owned events.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Whether the service has been successfully initialized.
        /// False before Initialize() and after Shutdown().
        /// </summary>
        bool IsInitialized { get; }
    }
}
