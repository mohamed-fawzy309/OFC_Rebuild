using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Football.Core;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies deterministic reverse-dependency-order shutdown established by ServiceInitializer.
    /// Uses fake IFootballService implementations (no real gameplay) to prove ordering,
    /// initialized-subset, exactly-once, failure, and event-cleanup behaviour.
    /// </summary>
    public class ShutdownOrderTests
    {
        private List<string> _trace;

        [SetUp]
        public void SetUp()
        {
            _trace = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            // GameEvents is static and shared across tests; always clear to isolate.
            GameEvents.Clear();
        }

        private class ServiceA : TrackingService { }
        private class ServiceB : TrackingService { }
        private class ServiceC : TrackingService { }

        private abstract class TrackingService : IFootballService
        {
            public string Name;
            public List<string> Trace;
            public Action OnInitialize;
            public Action OnShutdown;
            public int InitializeCount;
            public int ShutdownCount;
            public bool IsInitialized { get; protected set; }

            public void Initialize()
            {
                InitializeCount++;
                OnInitialize?.Invoke(); // may throw BEFORE marking initialized
                IsInitialized = true;
                Trace.Add($"Initialize:{Name}");
            }

            public void Shutdown()
            {
                ShutdownCount++;
                OnShutdown?.Invoke(); // may throw
                IsInitialized = false;
                Trace.Add($"Shutdown:{Name}");
            }
        }

        private T New<T>(string name) where T : TrackingService, new()
        {
            return new T { Name = name, Trace = _trace };
        }

        private static ServiceInitializer ForTypes(
            IReadOnlyList<Type> services,
            IReadOnlyDictionary<Type, IReadOnlyList<Type>> dependencies = null)
        {
            return new ServiceInitializer(services, dependencies);
        }

        [Test]
        public void ShutdownUsesReverseDependencyOrder()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            // A -> B -> C  (A depends on B, B depends on C)
            var initializer = ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB), typeof(ServiceC) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceA), new[] { typeof(ServiceB) } },
                    { typeof(ServiceB), new[] { typeof(ServiceC) } }
                });

            initializer.Initialize(registry);
            initializer.Shutdown(registry);

            Assert.AreEqual(
                new List<string>
                {
                    "Initialize:C", "Initialize:B", "Initialize:A",
                    "Shutdown:A", "Shutdown:B", "Shutdown:C"
                },
                _trace);
            Assert.IsFalse(a.IsInitialized);
            Assert.IsFalse(b.IsInitialized);
            Assert.IsFalse(c.IsInitialized);
        }

        [Test]
        public void ServiceShutdownCalledAfterInitialization()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            registry.Register<ServiceA>(a);

            var initializer = ForTypes(new Type[] { typeof(ServiceA) });

            initializer.Initialize(registry);
            initializer.Shutdown(registry);

            Assert.AreEqual(
                new List<string> { "Initialize:A", "Shutdown:A" },
                _trace);
            Assert.AreEqual(1, a.InitializeCount);
            Assert.AreEqual(1, a.ShutdownCount);
        }

        [Test]
        public void ServiceShutdownCalledOnlyOnce()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            registry.Register<ServiceA>(a);

            var initializer = ForTypes(new Type[] { typeof(ServiceA) });

            initializer.Initialize(registry);
            initializer.Shutdown(registry);

            Assert.AreEqual(1, a.ShutdownCount);
        }

        [Test]
        public void RegisteredButUninitializedServiceIsNotShutdown()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);

            // A throws on Initialize -> never successfully initialized; B never runs.
            a.OnInitialize = () => throw new InvalidOperationException("A explode");

            var initializer = ForTypes(new Type[] { typeof(ServiceA), typeof(ServiceB) });

            Assert.Throws<InvalidOperationException>(() => initializer.Initialize(registry));
            initializer.Shutdown(registry); // must not throw for uninitialized services

            Assert.AreEqual(0, a.ShutdownCount, "A never initialized -> not shut down.");
            Assert.AreEqual(0, b.ShutdownCount, "B never initialized -> not shut down.");
        }

        [Test]
        public void PartialInitializationShutdownsOnlyInitializedServices()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            // A -> B -> C. A fails to initialize; B and C succeed.
            a.OnInitialize = () => throw new InvalidOperationException("A explode");

            var initializer = ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB), typeof(ServiceC) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceA), new[] { typeof(ServiceB) } },
                    { typeof(ServiceB), new[] { typeof(ServiceC) } }
                });

            Assert.Throws<InvalidOperationException>(() => initializer.Initialize(registry));
            Assert.IsTrue(b.IsInitialized);
            Assert.IsTrue(c.IsInitialized);
            Assert.IsFalse(a.IsInitialized);

            initializer.Shutdown(registry);

            Assert.AreEqual(0, a.ShutdownCount, "A never initialized -> not shut down.");
            Assert.AreEqual(1, b.ShutdownCount);
            Assert.AreEqual(1, c.ShutdownCount);
            // Reverse of resolved init order [C, B, A] -> B then C (A skipped).
            Assert.AreEqual(
                new List<string> { "Shutdown:B", "Shutdown:C" },
                _trace.Where(t => t.StartsWith("Shutdown")).ToList());
        }

        [Test]
        public void DependentServiceShutsDownBeforeDependency()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);

            // A depends on B.
            var initializer = ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceA), new[] { typeof(ServiceB) } }
                });

            initializer.Initialize(registry);
            initializer.Shutdown(registry);

            Assert.AreEqual(
                new List<string>
                {
                    "Initialize:B", "Initialize:A",
                    "Shutdown:A", "Shutdown:B"
                },
                _trace);
        }

        [Test]
        public void IndependentServicesShutdownDeterministically()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            var initializer = ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB), typeof(ServiceC) });

            initializer.Initialize(registry);
            initializer.Shutdown(registry);

            // Deterministic reverse of declaration-order init [A, B, C] -> [C, B, A].
            Assert.AreEqual(
                new List<string> { "Shutdown:C", "Shutdown:B", "Shutdown:A" },
                _trace.Where(t => t.StartsWith("Shutdown")).ToList());
        }

        [Test]
        public void ShutdownDoesNotModifyRegistryContents()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);

            var initializer = ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceB), new[] { typeof(ServiceA) } }
                });
            initializer.Initialize(registry);
            initializer.Shutdown(registry);

            Assert.AreSame(a, registry.Get<ServiceA>());
            Assert.AreSame(b, registry.Get<ServiceB>());
        }

        [Test]
        public void ShutdownDoesNotInitializeServices()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            registry.Register<ServiceA>(a);

            // Never initialize, but trigger a shutdown; no service should be touched.
            var initializer = ForTypes(new Type[] { typeof(ServiceA) });
            initializer.Shutdown(registry);

            Assert.AreEqual(0, a.InitializeCount);
            Assert.AreEqual(0, a.ShutdownCount);
            Assert.IsFalse(a.IsInitialized);
        }

        [Test]
        public void ShutdownDoesNotRequireSingleton()
        {
            Assert.IsFalse(typeof(ServiceInitializer).IsSubclassOf(typeof(UnityEngine.Object)));
            Assert.IsNull(typeof(ServiceInitializer).GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public));
            Assert.AreEqual(typeof(object), typeof(ServiceInitializer).BaseType);
        }

        [Test]
        public void ShutdownDoesNotUseServiceLocator()
        {
            // The coordinator resolves instances through an injected registry reference only.
            Assert.IsNull(typeof(ServiceInitializer).GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public));
            Assert.IsNull(typeof(ServiceInitializer).GetMethod("GetService",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        [Test]
        public void EventSubscriptionIsRemovedDuringShutdown()
        {
            var registry = new ServiceRegistry();
            var svc = New<ServiceA>("A");
            int calls = 0;
            Action<MatchStartedEvent> handler = _ => calls++;
            svc.OnInitialize = () => GameEvents.Subscribe(handler);
            svc.OnShutdown = () => GameEvents.Unsubscribe(handler);
            registry.Register<ServiceA>(svc);

            var initializer = ForTypes(new Type[] { typeof(ServiceA) });
            initializer.Initialize(registry);

            GameEvents.Raise(new MatchStartedEvent(1.0f, 1, 2));
            Assert.AreEqual(1, calls, "Subscribed handler received the event before shutdown.");

            initializer.Shutdown(registry);

            GameEvents.Raise(new MatchStartedEvent(2.0f, 1, 2));
            Assert.AreEqual(1, calls, "Handler must not receive events after its subscription is removed during shutdown.");
        }

        [Test]
        public void UnrelatedEventSubscribersRemainRegistered()
        {
            var registry = new ServiceRegistry();
            var svc = New<ServiceA>("A");
            int serviceCalls = 0;
            Action<MatchStartedEvent> serviceHandler = _ => serviceCalls++;
            svc.OnInitialize = () => GameEvents.Subscribe(serviceHandler);
            svc.OnShutdown = () => GameEvents.Unsubscribe(serviceHandler);
            registry.Register<ServiceA>(svc);

            int unrelatedCalls = 0;
            Action<MatchStartedEvent> unrelatedHandler = _ => unrelatedCalls++;
            GameEvents.Subscribe(unrelatedHandler);

            var initializer = ForTypes(new Type[] { typeof(ServiceA) });
            initializer.Initialize(registry);
            initializer.Shutdown(registry);

            // The unrelated subscriber was registered outside the service and is preserved.
            GameEvents.Raise(new MatchStartedEvent(3.0f, 1, 2));
            Assert.AreEqual(0, serviceCalls, "Service's own handler removed.");
            Assert.AreEqual(1, unrelatedCalls, "Unrelated subscriber preserved.");
        }

        [Test]
        public void ShutdownFailureDoesNotHideOriginalException()
        {
            var registry = new ServiceRegistry();
            var b = New<ServiceB>("B");
            b.OnShutdown = () => throw new ArgumentException("bad shutdown");
            registry.Register<ServiceB>(b);

            var initializer = ForTypes(new Type[] { typeof(ServiceB) });
            initializer.Initialize(registry);

            var ex = Assert.Throws<InvalidOperationException>(() => initializer.Shutdown(registry));
            Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
        }

        [Test]
        public void MultipleShutdownCallsAreSafeAccordingToChosenPolicy()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            registry.Register<ServiceA>(a);

            var initializer = ForTypes(new Type[] { typeof(ServiceA) });
            initializer.Initialize(registry);
            initializer.Shutdown(registry);
            initializer.Shutdown(registry);

            // Safe no-op: each service shut down at most once per lifecycle.
            Assert.AreEqual(1, a.ShutdownCount);
            Assert.AreEqual(1, a.InitializeCount);
        }

        [Test]
        public void ShutdownOrderMatchesReverseResolvedInitializationOrder()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            var initializer = ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB), typeof(ServiceC) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceA), new[] { typeof(ServiceB) } },
                    { typeof(ServiceB), new[] { typeof(ServiceC) } }
                });

            initializer.Initialize(registry);
            var resolved = initializer.ResolvedOrder.ToList(); // [C, B, A]

            initializer.Shutdown(registry);

            // Each resolved Type maps to the instance trace name by stripping the "Service" prefix.
            var expectedShutdown = resolved.AsEnumerable().Reverse()
                                           .Select(ty => "Shutdown:" + ty.Name.Replace("Service", ""))
                                           .ToList();
            var actualShutdown = _trace.Where(t => t.StartsWith("Shutdown")).ToList();

            Assert.AreEqual(expectedShutdown, actualShutdown);
        }

        [Test]
        public void CircularDependencyDoesNotProduceShutdownOrder()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);

            var initializer = ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceA), new[] { typeof(ServiceB) } },
                    { typeof(ServiceB), new[] { typeof(ServiceA) } }
                });

            // Cycle rejects initialization before any service runs -> ResolvedOrder stays empty.
            Assert.Throws<InvalidOperationException>(() => initializer.Initialize(registry));

            initializer.Shutdown(registry); // no-op, must not throw

            Assert.AreEqual(0, a.ShutdownCount);
            Assert.AreEqual(0, b.ShutdownCount);
        }

        [Test]
        public void EmptyRegistryShutdownIsSafe()
        {
            var registry = new ServiceRegistry();
            var initializer = ForTypes(new Type[] { });

            initializer.Shutdown(registry); // no services -> no-op
        }
    }
}
