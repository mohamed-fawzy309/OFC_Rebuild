using System;
using System.Collections.Generic;
using System.Linq;

namespace Football.Core
{
    /// <summary>
    /// The minimal, deterministic coordinator for initializing AND tearing down registered
    /// services in the correct dependency order.
    ///
    /// Responsibilities (OWNED):
    /// - guarantee every declared service is registered (resolved from the registry) BEFORE
    ///   any service initializes
    /// - compute a deterministic initialization order from explicit dependencies
    /// - detect circular dependencies and reject them before initialization begins
    /// - initialize each service exactly once, in dependency order
    /// - stop initialization and propagate the failure if any service throws
    /// - shut down services in the exact REVERSE of the resolved initialization order
    /// - shut down only services that actually completed initialization
    /// - shut each service down exactly once per lifecycle
    /// - aggregate shutdown failures and propagate them without swallowing the originals
    ///
    /// NOT owned (kept separate):
    /// - service creation/construction (the Composition Root creates instances)
    /// - service storage/lookup (owned by ServiceRegistry)
    /// - detailed per-service business logic
    /// - event subscription ownership (each subscribing service owns its own unsubscribe;
    ///   this coordinator never calls GameEvents.Clear)
    ///
    /// Initialization and shutdown are the SAME dependency graph in opposite directions —
    /// there is deliberately no second, independently authored ordering algorithm. Because
    /// shutdown is the exact inverse of initialization and shares the resolved order, a paired
    /// lifecycle coordinator is the smallest correct design: it avoids duplicating the
    /// dependency model across two coordinators.
    ///
    /// This class is pure C# (not a MonoBehaviour), is not a Singleton, does not use a
    /// Service Locator, and performs no reflection-based service discovery. Dependencies are
    /// explicit (declared by type), and independent services resolve in a deterministic
    /// declaration order. The dependency graph and order are computed only when startup
    /// occurs — never per frame.
    ///
    /// Dependencies are a composition concern, not a service-contract concern: this class
    /// reads an explicit dependency map and never asks a service about its own dependencies.
    /// </summary>
    public class ServiceInitializer
    {
        private readonly List<Type> _services;
        private readonly IReadOnlyDictionary<Type, IReadOnlyList<Type>> _dependencies;
        private readonly List<Type> _resolvedOrder = new();

        /// <summary>
        /// The most recently computed initialization order, in deterministic sequence.
        /// Empty until <see cref="Initialize"/> is called.
        /// </summary>
        public IReadOnlyList<Type> ResolvedOrder => _resolvedOrder;

        /// <summary>
        /// Creates a coordinator for the given services and their explicit dependencies.
        /// </summary>
        /// <param name="services">The services to initialize, in the caller's preferred
        /// declaration order (used as the deterministic tie-break for independent services).</param>
        /// <param name="dependencies">Optional explicit map of service type → dependency service
        /// types. Every key and value must be one of <paramref name="services"/>.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// If a dependency references a type that is not a declared service.</exception>
        public ServiceInitializer(
            IEnumerable<Type> services,
            IReadOnlyDictionary<Type, IReadOnlyList<Type>> dependencies = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            _services = new List<Type>(services);
            _dependencies = dependencies ?? new Dictionary<Type, IReadOnlyList<Type>>();

            foreach (var kvp in _dependencies)
            {
                if (!_services.Contains(kvp.Key))
                    throw new ArgumentException(
                        $"Dependency source '{kvp.Key.Name}' is not a declared service.");

                foreach (var dependency in kvp.Value)
                {
                    if (dependency == null)
                        throw new ArgumentException("A dependency type must not be null.");
                    if (!_services.Contains(dependency))
                        throw new ArgumentException(
                            $"Dependency '{dependency.Name}' of '{kvp.Key.Name}' is not a declared service.");
                }
            }
        }

        /// <summary>
        /// Registers (via the registry) then initializes every declared service in dependency
        /// order. All services are resolved from <paramref name="registry"/> before any service
        /// is initialized, so a missing registration aborts the whole run before initialization.
        /// A service that throws aborts the remaining initialization and the failure propagates
        /// with context identifying the failed service.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="registry"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// If a service is not registered, a circular dependency is detected, or a service
        /// initialization fails.</exception>
        public void Initialize(ServiceRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            // Phase 1 — Registration gate: resolve every service instance BEFORE initializing
            // any. Get(Type) throws ServiceNotFoundException if a service is not registered,
            // enforcing that all required services are registered before any initialization
            // begins. The requesting system ("ServiceInitializer") is passed so the missing-
            // service diagnostic identifies who requested the absent service. Lookup never
            // initializes.
            var instances = new Dictionary<Type, IFootballService>(_services.Count);
            foreach (var type in _services)
            {
                instances[type] = registry.Get(type, nameof(ServiceInitializer));
            }

            // Phase 2 — Compute the deterministic order and detect cycles BEFORE initializing.
            var order = ComputeOrder();
            _resolvedOrder.Clear();
            _resolvedOrder.AddRange(order);

            // Phase 3 — Initialize in order, exactly once per service, fail-fast on error.
            foreach (var type in order)
            {
                var service = instances[type];
                if (service.IsInitialized)
                    continue; // already initialized -> exactly-once no-op, never re-initialize

                try
                {
                    service.Initialize();
                }
                catch (Exception inner)
                {
                    // The failed service is not marked initialized (its own Initialize threw
                    // before reporting success); dependent services are never reached.
                    throw new InvalidOperationException(
                        $"Service initialization failed for '{type.Name}'.", inner);
                }
            }
        }

        /// <summary>
        /// Tears down services in the exact reverse of the resolved initialization order.
        /// Only services that actually completed initialization (IsInitialized == true) are
        /// shut down; registered-but-never-initialized and failed-initialization services are
        /// skipped. After a successful Shutdown a service is no longer marked initialized, so a
        /// repeated shutdown is a safe no-op — each service is shut down at most once per
        /// lifecycle.
        ///
        /// Shutdown continues across a failing service (safe resource release), collecting every
        /// failure, and then propagates a single InvalidOperationException whose inner exception
        /// preserves the original failure(s). Failures are never swallowed. This aggregate-failure
        /// run is deterministic because the shutdown sequence is fixed.
        ///
        /// This does NOT call GameEvents.Clear; each subscribing service owns its own unsubscribe
        /// during its Shutdown.
        /// </summary>
        /// <param name="registry">The registry to resolve service instances from. Lookup-only.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="registry"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// If one or more initialized services threw during their Shutdown.</exception>
        public void Shutdown(ServiceRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            // Shutdown order is reverse(ResolvedOrder). This reuses the SAME resolved order
            // computed by Initialize — no second ordering algorithm.
            var failures = new List<KeyValuePair<Type, Exception>>();
            for (int i = _resolvedOrder.Count - 1; i >= 0; i--)
            {
                Type type = _resolvedOrder[i];
                IFootballService service = registry.Get(type, nameof(ServiceInitializer));
                if (!service.IsInitialized)
                    continue; // never initialized or already shut down -> safe no-op

                try
                {
                    service.Shutdown();
                }
                catch (Exception inner)
                {
                    // Remove the service's initialized signal so a rerun treats it as down, but
                    // keep going so independent resources are still released.
                    failures.Add(new KeyValuePair<Type, Exception>(type, inner));
                }
            }

            if (failures.Count == 1)
            {
                throw new InvalidOperationException(
                    $"Service shutdown failed for '{failures[0].Key.Name}'.",
                    failures[0].Value);
            }

            if (failures.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Service shutdown failed for {failures.Count} service(s).",
                    new AggregateException(
                        failures.Select(f => new KeyValuePair<string, Exception>(f.Key.Name, f.Value))
                                .Select(f => new InvalidOperationException(
                                    $"Service shutdown failed for '{f.Key}'.", f.Value))));
            }
        }

        private List<Type> ComputeOrder()
        {
            var remainingDependencies = new Dictionary<Type, int>();
            var dependents = new Dictionary<Type, List<Type>>();

            foreach (var type in _services)
            {
                remainingDependencies[type] = 0;
                dependents[type] = new List<Type>();
            }

            foreach (var kvp in _dependencies)
            {
                foreach (var dependency in kvp.Value)
                {
                    remainingDependencies[kvp.Key]++;
                    dependents[dependency].Add(kvp.Key);
                }
            }

            var pending = new List<Type>(_services); // declaration order = deterministic tie-break
            var order = new List<Type>(_services.Count);

            while (pending.Count > 0)
            {
                // Pick the first pending service whose dependencies are all satisfied.
                // Scanning in declaration order makes independent-service ordering deterministic.
                int pickedIndex = -1;
                for (int i = 0; i < pending.Count; i++)
                {
                    if (remainingDependencies[pending[i]] == 0)
                    {
                        pickedIndex = i;
                        break;
                    }
                }

                if (pickedIndex < 0)
                {
                    // Remaining services all still have unsatisfied dependencies -> cycle.
                    throw new InvalidOperationException(
                        "Circular service dependency detected. " +
                        "Initialization aborted before any service was initialized.");
                }

                Type picked = pending[pickedIndex];
                pending.RemoveAt(pickedIndex);
                order.Add(picked);

                foreach (var dependent in dependents[picked])
                {
                    remainingDependencies[dependent]--;
                }
            }

            return order;
        }
    }
}
