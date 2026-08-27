using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Football.Core;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies the deterministic service initialization ordering established by ServiceInitializer.
    /// Uses fake IFootballService implementations (no real gameplay) to prove ordering behaviour.
    /// </summary>
    public class ServiceInitializationOrderTests
    {
        private List<string> _trace;

        [SetUp]
        public void SetUp()
        {
            _trace = new List<string>();
        }

        private class ServiceA : TrackingService { }
        private class ServiceB : TrackingService { }
        private class ServiceC : TrackingService { }

        private abstract class TrackingService : IFootballService
        {
            public string Name;
            public List<string> Trace;
            public Action OnInitialize;
            public int InitializeCount;
            public bool IsInitialized { get; protected set; }

            public void Initialize()
            {
                InitializeCount++;
                OnInitialize?.Invoke(); // may throw BEFORE marking initialized
                IsInitialized = true;
                Trace.Add($"{Name}.Initialize");
            }

            public void Shutdown()
            {
                IsInitialized = false;
                Trace.Add($"{Name}.Shutdown");
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
        public void AllServicesRegisteredBeforeInitialization()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            ForTypes(new Type[] { typeof(ServiceA), typeof(ServiceB), typeof(ServiceC) })
                .Initialize(registry);

            Assert.IsTrue(a.IsInitialized);
            Assert.IsTrue(b.IsInitialized);
            Assert.IsTrue(c.IsInitialized);
        }

        [Test]
        public void ServiceInitializesOnlyAfterRegistration()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            registry.Register<ServiceA>(a);
            // B is declared but NOT registered.

            Assert.Throws<ServiceNotFoundException>(() =>
                ForTypes(new Type[] { typeof(ServiceA), typeof(ServiceB) }).Initialize(registry));

            Assert.IsFalse(a.IsInitialized,
                "No service may initialize while a required service is unregistered.");
            Assert.AreEqual(0, a.InitializeCount);
        }

        [Test]
        public void DependencyInitializesBeforeDependent()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);

            ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceB), new[] { typeof(ServiceA) } }
                }).Initialize(registry);

            Assert.AreEqual(
                new List<string> { "A.Initialize", "B.Initialize" },
                _trace);
            Assert.IsTrue(a.IsInitialized);
            Assert.IsTrue(b.IsInitialized);
        }

        [Test]
        public void DependencyChain_ExactOrder()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            // A depends on B, B depends on C => expected order C, B, A
            ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB), typeof(ServiceC) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceA), new[] { typeof(ServiceB) } },
                    { typeof(ServiceB), new[] { typeof(ServiceC) } }
                }).Initialize(registry);

            Assert.AreEqual(
                new List<string> { "C.Initialize", "B.Initialize", "A.Initialize" },
                _trace);
        }

        [Test]
        public void InitializeExactlyOnce()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            registry.Register<ServiceA>(a);

            ForTypes(new Type[] { typeof(ServiceA) }).Initialize(registry);

            Assert.AreEqual(1, a.InitializeCount);
            Assert.IsTrue(a.IsInitialized);
        }

        [Test]
        public void GetDoesNotInitialize()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            registry.Register<ServiceA>(a);

            registry.Get<ServiceA>();

            Assert.AreEqual(0, a.InitializeCount);
            Assert.IsFalse(a.IsInitialized);
        }

        [Test]
        public void RegisterDoesNotInitialize()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");

            registry.Register<ServiceA>(a);

            Assert.AreEqual(0, a.InitializeCount);
            Assert.IsFalse(a.IsInitialized);
        }

        [Test]
        public void InitializationFailureStopsLaterServices()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            b.OnInitialize = () => throw new InvalidOperationException("B explode");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            // C depends on B; order A, B, C
            Assert.Throws<InvalidOperationException>(() =>
                ForTypes(
                    new Type[] { typeof(ServiceA), typeof(ServiceB), typeof(ServiceC) },
                    new Dictionary<Type, IReadOnlyList<Type>>
                    {
                        { typeof(ServiceC), new[] { typeof(ServiceB) } }
                    }).Initialize(registry));

            Assert.AreEqual(1, a.InitializeCount, "A (already passed) must remain initialized.");
            Assert.AreEqual(1, b.InitializeCount, "B was attempted (and failed).");
            Assert.AreEqual(0, c.InitializeCount, "C must not initialize after its dependency failed.");
        }

        [Test]
        public void InitializationFailurePropagates()
        {
            var registry = new ServiceRegistry();
            var b = New<ServiceB>("B");
            b.OnInitialize = () => throw new ArgumentException("bad init");
            registry.Register<ServiceB>(b);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ForTypes(new Type[] { typeof(ServiceB) }).Initialize(registry));

            Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
        }

        [Test]
        public void FailedServiceIsNotMarkedInitialized()
        {
            var registry = new ServiceRegistry();
            var b = New<ServiceB>("B");
            b.OnInitialize = () => throw new InvalidOperationException("B explode");
            registry.Register<ServiceB>(b);

            Assert.Throws<InvalidOperationException>(() =>
                ForTypes(new Type[] { typeof(ServiceB) }).Initialize(registry));

            Assert.IsFalse(b.IsInitialized, "A service that threw must not be reported initialized.");
        }

        [Test]
        public void DependentServiceDoesNotInitializeAfterDependencyFailure()
        {
            var registry = new ServiceRegistry();
            var b = New<ServiceB>("B");
            b.OnInitialize = () => throw new InvalidOperationException("B explode");
            var c = New<ServiceC>("C");
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            Assert.Throws<InvalidOperationException>(() =>
                ForTypes(
                    new Type[] { typeof(ServiceB), typeof(ServiceC) },
                    new Dictionary<Type, IReadOnlyList<Type>>
                    {
                        { typeof(ServiceC), new[] { typeof(ServiceB) } }
                    }).Initialize(registry));

            Assert.AreEqual(0, c.InitializeCount);
            Assert.IsFalse(c.IsInitialized);
        }

        [Test]
        public void IndependentServicesHaveDeterministicOrder()
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
            initializer.Initialize(registry); // repeated run must not change order/list

            Assert.AreEqual(
                new List<string> { "A.Initialize", "B.Initialize", "C.Initialize" },
                _trace);
        }

        [Test]
        public void CircularDependencyIsRejected()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);

            Assert.Throws<InvalidOperationException>(() =>
                ForTypes(
                    new Type[] { typeof(ServiceA), typeof(ServiceB) },
                    new Dictionary<Type, IReadOnlyList<Type>>
                    {
                        { typeof(ServiceA), new[] { typeof(ServiceB) } },
                        { typeof(ServiceB), new[] { typeof(ServiceA) } }
                    }).Initialize(registry));

            Assert.AreEqual(0, a.InitializeCount, "Cycle must abort before any service initializes.");
            Assert.AreEqual(0, b.InitializeCount);
            Assert.IsFalse(a.IsInitialized);
            Assert.IsFalse(b.IsInitialized);
        }

        [Test]
        public void MultipleIndependentServicesCanInitialize()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            ForTypes(new Type[] { typeof(ServiceA), typeof(ServiceB), typeof(ServiceC) })
                .Initialize(registry);

            Assert.IsTrue(a.IsInitialized);
            Assert.IsTrue(b.IsInitialized);
            Assert.IsTrue(c.IsInitialized);
            Assert.AreEqual(3, _trace.Count);
        }

        [Test]
        public void InitializationDoesNotDependOnAwakeOrder()
        {
            // The coordinator is pure C# with no Unity lifecycle hook, so ordering cannot be
            // influenced by arbitrary Awake ordering.
            Assert.IsFalse(typeof(ServiceInitializer).IsSubclassOf(typeof(UnityEngine.Object)));
            Assert.IsNull(typeof(ServiceInitializer).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(ServiceInitializer).GetMethod("OnEnable",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        [Test]
        public void InitializationDoesNotDependOnStartOrder()
        {
            Assert.IsNull(typeof(ServiceInitializer).GetMethod("Start",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(ServiceInitializer).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(ServiceInitializer).GetMethod("FixedUpdate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        [Test]
        public void ServicesRemainAccessibleAfterInitialization()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);

            ForTypes(
                new Type[] { typeof(ServiceA), typeof(ServiceB) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceB), new[] { typeof(ServiceA) } }
                }).Initialize(registry);

            Assert.AreSame(a, registry.Get<ServiceA>());
            Assert.AreSame(b, registry.Get<ServiceB>());
            Assert.IsTrue(registry.Get<ServiceA>().IsInitialized);
            Assert.IsTrue(registry.Get<ServiceB>().IsInitialized);
        }

        [Test]
        public void RegistryRemainsSeparateFromInitialization()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");

            // Registration (no coordinator) must never initialize the service.
            registry.Register<ServiceA>(a);
            Assert.IsFalse(a.IsInitialized);

            // The registry must not expose an initialization entry point.
            Assert.IsNull(typeof(ServiceRegistry).GetMethod("Initialize",
                BindingFlags.Instance | BindingFlags.Public));
        }

        [Test]
        public void RepeatedStartupDoesNotReinitializeServices()
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
            initializer.Initialize(registry);

            Assert.AreEqual(1, a.InitializeCount);
            Assert.AreEqual(1, b.InitializeCount);
            Assert.AreEqual(
                new List<string> { "A.Initialize", "B.Initialize" },
                _trace);
        }

        [Test]
        public void ResolvedOrder_IsDeterministic()
        {
            var registry = new ServiceRegistry();
            var a = New<ServiceA>("A");
            var b = New<ServiceB>("B");
            var c = New<ServiceC>("C");
            registry.Register<ServiceA>(a);
            registry.Register<ServiceB>(b);
            registry.Register<ServiceC>(c);

            var initializer = ForTypes(
                new Type[] { typeof(ServiceC), typeof(ServiceA), typeof(ServiceB) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceA), new[] { typeof(ServiceB) } },
                    { typeof(ServiceB), new[] { typeof(ServiceC) } }
                });

            initializer.Initialize(registry);
            var first = initializer.ResolvedOrder.ToList();

            // Fresh coordinator, identical declaration -> identical order
            var second = ForTypes(
                new Type[] { typeof(ServiceC), typeof(ServiceA), typeof(ServiceB) },
                new Dictionary<Type, IReadOnlyList<Type>>
                {
                    { typeof(ServiceA), new[] { typeof(ServiceB) } },
                    { typeof(ServiceB), new[] { typeof(ServiceC) } }
                });
            second.Initialize(registry);

            Assert.AreEqual(first, second.ResolvedOrder.ToList());
            Assert.AreEqual(
                new List<Type> { typeof(ServiceC), typeof(ServiceB), typeof(ServiceA) },
                first);
        }
    }
}
