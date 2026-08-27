using System;
using System.Collections.Generic;

namespace Football.Core
{
    /// <summary>
    /// A controlled, type-aware registry for <see cref="IFootballService"/> instances.
    ///
    /// Responsibilities:
    /// - Explicit service registration and lookup (creation AND ownership stay external).
    /// - Compile-time type safety via generic constraints.
    /// - Clear, actionable errors for null, duplicate, and missing registration.
    ///
    /// This class is deliberately NOT:
    /// - a Singleton (no static instance; the Composition Root owns one instance).
    /// - a Service Locator (no hidden global lookup; references are injected/owned).
    /// - a factory (it never constructs services).
    /// - a DI container, a reflection engine, or a lifecycle owner.
    ///
    /// Services must be created and initialized by their owner (the Composition Root).
    /// The registry performs registration and lookup only; it does NOT call
    /// <see cref="IFootballService.Initialize"/> or <see cref="IFootballService.Shutdown"/>.
    /// </summary>
    public class ServiceRegistry
    {
        private readonly Dictionary<Type, IFootballService> _services = new();

        /// <summary>
        /// Registers a service instance under its concrete compiled type.
        /// Must be an <see cref="IFootballService"/>. The service must not be null
        /// and must not already be registered. Duplicate registration fails loudly
        /// rather than silently replacing the existing entry.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="service"/> is null.</exception>
        /// <exception cref="DuplicateServiceException">
        /// If a service was already registered for <typeparamref name="TService"/>.</exception>
        public void Register<TService>(TService service) where TService : IFootballService
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            if (_services.TryGetValue(typeof(TService), out var existing))
            {
                throw new DuplicateServiceException(
                    typeof(TService),
                    existing,
                    service,
                    inner: null);
            }

            _services.Add(typeof(TService), service);
        }

        /// <summary>
        /// Returns the registered service for <typeparamref name="TService"/>.
        /// Throws <see cref="ServiceNotFoundException"/> if no such service has been registered.
        /// </summary>
        /// <param name="requestingSystem">Optional name of the subsystem requesting the service,
        /// used to enrich the missing-service diagnostic.</param>
        /// <exception cref="ServiceNotFoundException">
        /// If no service was registered for <typeparamref name="TService"/>.</exception>
        public TService Get<TService>(string requestingSystem = null) where TService : IFootballService
        {
            return (TService)Get(typeof(TService), requestingSystem);
        }

        /// <summary>
        /// Returns the registered service for <paramref name="serviceType"/> (resolved
        /// through a runtime <see cref="System.Type"/>). This is the non-generic counterpart
        /// to <see cref="Get{TService}"/>, used when the concrete service type is only known
        /// at runtime (e.g. dependency-order coordination). It performs lookup only and never
        /// initializes or constructs a service.
        /// </summary>
        /// <param name="requestingSystem">Optional name of the subsystem requesting the service,
        /// used to enrich the missing-service diagnostic.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="serviceType"/> is null.</exception>
        /// <exception cref="ServiceNotFoundException">
        /// If no service was registered for <paramref name="serviceType"/>.</exception>
        public IFootballService Get(Type serviceType, string requestingSystem = null)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            if (_services.TryGetValue(serviceType, out var service))
                return service;

            throw new ServiceNotFoundException(serviceType, requestingSystem);
        }

        /// <summary>
        /// Detects whether a service has been REGISTERED for <typeparamref name="TService"/>.
        ///
        /// This is the primitive that distinguishes a MISSING service (not registered; false)
        /// from a PRESENT service that may simply not have been initialized yet (true). To tell
        /// "present but not initialized" apart from "present and ready", combine this with the
        /// resolved service's <see cref="IFootballService.IsInitialized"/>.
        /// </summary>
        public bool IsRegistered<TService>() where TService : IFootballService
        {
            return _services.ContainsKey(typeof(TService));
        }

        /// <summary>
        /// Detects whether a service has been REGISTERED for <paramref name="serviceType"/>.
        /// Null-safe. See <see cref="IsRegistered{TService}"/> for the missing-vs-not-initialized
        /// distinction.
        /// </summary>
        public bool IsRegistered(Type serviceType)
        {
            return serviceType != null && _services.ContainsKey(serviceType);
        }

        /// <summary>
        /// Attempts to retrieve a registered service for <typeparamref name="TService"/>.
        /// Returns false without throwing when the service is absent.
        /// </summary>
        public bool TryGet<TService>(out TService service) where TService : IFootballService
        {
            if (_services.TryGetValue(typeof(TService), out var value))
            {
                service = (TService)value;
                return true;
            }

            service = default;
            return false;
        }
    }
}
