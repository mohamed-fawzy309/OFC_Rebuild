using System;

namespace Football.Core
{
    /// <summary>
    /// The structured, actionable diagnostic raised when a service type is REGISTERED more
    /// than once.
    ///
    /// Like <see cref="ServiceNotFoundException"/> (the MISSING case), this is a subtype of
    /// <see cref="InvalidOperationException"/> so the pre-existing "duplicate registration
    /// throws InvalidOperationException" contract remains backward compatible, while callers
    /// that need the precise cause catch this specific type. The two diagnostics are distinct
    /// and never conflated:
    ///   - missing service      → <see cref="ServiceNotFoundException"/>
    ///   - duplicate service    → DuplicateServiceException
    ///
    /// Structured context (the source of truth, not message parsing):
    /// - <see cref="DuplicateServiceType"/>: the registration key that collided (48.2).
    /// - <see cref="ExistingService"/>: the service already registered (48.3).
    /// - <see cref="AttemptedService"/>: the service that was rejected (48.4).
    ///
    /// Duplicate registration is a configuration/composition error. The FIRST registration is
    /// authoritative: the second is rejected, never stored, never silently replaced, and the
    /// existing registration remains the sole instance. The registry stores only instances and
    /// captures no stack traces, no reflection, and no registration-origin metadata here.
    /// </summary>
    public sealed class DuplicateServiceException : InvalidOperationException
    {
        /// <summary>The registration key (service type) that collided. Never null.</summary>
        public Type DuplicateServiceType { get; }

        /// <summary>The service instance that was already registered (the authoritative one). Never null.</summary>
        public IFootballService ExistingService { get; }

        /// <summary>The service instance that was rejected as a duplicate. Never null.</summary>
        public IFootballService AttemptedService { get; }

        public DuplicateServiceException(
            Type duplicateServiceType,
            IFootballService existingService,
            IFootballService attemptedService,
            Exception inner = null)
            : base(BuildMessage(duplicateServiceType, existingService, attemptedService), inner)
        {
            if (duplicateServiceType == null)
                throw new ArgumentNullException(nameof(duplicateServiceType));
            if (existingService == null)
                throw new ArgumentNullException(nameof(existingService));
            if (attemptedService == null)
                throw new ArgumentNullException(nameof(attemptedService));

            DuplicateServiceType = duplicateServiceType;
            ExistingService = existingService;
            AttemptedService = attemptedService;
        }

        private static string BuildMessage(Type type, IFootballService existing, IFootballService attempted)
        {
            var name = type?.Name ?? "<null>";
            return $"Service '{name}' is already registered. " +
                   $"Duplicate registration is a composition error: the existing registration must " +
                   $"remain the sole instance, and the attempted duplicate (instance '{attempted?.GetType().Name}') " +
                   $"has been rejected and is not stored. " +
                   $"Remove the duplicate registration from the Composition Root so that '{name}' is " +
                   $"registered exactly once.";
        }
    }
}
