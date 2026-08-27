using System;

namespace Football.Core
{
    /// <summary>
    /// The structured, actionable diagnostic raised when a REQUIRED service cannot be
    /// resolved because it was never registered.
    ///
    /// This is deliberately a subtype of <see cref="InvalidOperationException"/> so that the
    /// pre-existing contract — "lookup of an absent service throws InvalidOperationException" —
    /// remains backward compatible, while callers that need the precise cause can catch the
    /// more specific type and read the structured context:
    /// - <see cref="MissingServiceType"/>: exactly WHICH service type is missing (47.3).
    /// - <see cref="RequestingSystem"/>: WHICH system tried to resolve it (47.2).
    /// - <see cref="Message"/>: an actionable instruction (47.4).
    ///
    /// This exception represents the "missing" (never-registered) state only. A service that
    /// IS registered but has not yet finished initialization is a DIFFERENT state and must not
    /// be reported as missing — see <see cref="ServiceRegistry.IsRegistered"/> paired with
    /// <see cref="IFootballService.IsInitialized"/> for the not-initialized case (47.5).
    /// </summary>
    public sealed class ServiceNotFoundException : InvalidOperationException
    {
        /// <summary>The type of service that was requested but never registered. Never null.</summary>
        public Type MissingServiceType { get; }

        /// <summary>
        /// The subsystem that requested the missing service (e.g. "ServiceInitializer", or the
        /// specific dependent-service name when known). "Unknown" when no context was supplied.
        /// </summary>
        public string RequestingSystem { get; }

        public ServiceNotFoundException(
            Type missingServiceType,
            string requestingSystem = null,
            Exception inner = null)
            : base(BuildMessage(missingServiceType, requestingSystem), inner)
        {
            if (missingServiceType == null)
                throw new ArgumentNullException(nameof(missingServiceType));
            MissingServiceType = missingServiceType;
            RequestingSystem = string.IsNullOrWhiteSpace(requestingSystem) ? "Unknown" : requestingSystem;
        }

        private static string BuildMessage(Type type, string requestingSystem)
        {
            var name = type?.Name ?? "<null>";
            var requester = string.IsNullOrWhiteSpace(requestingSystem)
                ? "a system"
                : $"the '{requestingSystem}' system";
            return $"A required service of type '{name}' is not registered. " +
                   $"It was requested by {requester}, but the ServiceRegistry contains no entry " +
                   $"for it, so it cannot be initialized or resolved. " +
                   $"Register the service (Register&lt;{name}&gt;) in the Composition Root before " +
                   $"startup attempts to initialize it.";
        }
    }
}
