using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies the deterministic startup ordering established by GameBootstrap.
    /// All sequencing here uses test seams (fakes) — no real gameplay, services, or
    /// runtime scene loading is required to prove ORDER.
    /// </summary>
    public class StartupOrderTests
    {
        private const string MatchScene = "Assets/Football/Scenes/Match/Match.unity";

        private GameBootstrap _bootstrap;
        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            _bootstrap = CreateBootstrap();
            _events = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrap != null)
                UnityEngine.Object.DestroyImmediate(_bootstrap.gameObject);
        }

        private static GameBootstrap CreateBootstrap()
        {
            var go = new GameObject("StartupOrderBootstrap");
            return go.AddComponent<GameBootstrap>();
        }

        private Func<string, bool> RecordingLoadScene()
        {
            return path =>
            {
                _events.Add($"load:{path}");
                return true;
            };
        }

        /// <summary>A fully configured bootstrap whose seams record the actual call order into _events.</summary>
        private GameBootstrap ConfiguredBootstrap()
        {
            return _bootstrap.Configure(
                prepareInfrastructure: () => _events.Add("infrastructure"),
                registerServices: () => _events.Add("register"),
                initializeServices: () => _events.Add("initialize"),
                loadScene: RecordingLoadScene(),
                initialScenePath: MatchScene);
        }

        private static readonly string[] ExpectedTrace =
        {
            "Bootstrap",
            "Infrastructure",
            "RegisterServices",
            "InitializeServices",
            "LoadInitialScene",
            "StartupComplete"
        };

        private class FakeService : IFootballService
        {
            public bool IsInitialized { get; private set; }
            public int InitializeCalls { get; private set; }

            public void Initialize()
            {
                InitializeCalls++;
                IsInitialized = true;
            }

            public void Shutdown()
            {
                IsInitialized = false;
            }
        }

        [Test]
        public void StartupTrace_MatchesExactSequence()
        {
            ConfiguredBootstrap().Initialize();

            Assert.AreEqual(ExpectedTrace, _bootstrap.StartupTrace);
            Assert.AreEqual(new[] { "infrastructure", "register", "initialize", "load:Assets/Football/Scenes/Match/Match.unity" }, _events);
        }

        [Test]
        public void StartupBeginsFromBoot()
        {
            Assert.AreEqual(BootstrapState.Boot, _bootstrap.CurrentState);
            Assert.AreEqual(StartupPhase.NotStarted, _bootstrap.CurrentPhase);

            ConfiguredBootstrap().Initialize();

            Assert.AreEqual(BootstrapState.Ready, _bootstrap.CurrentState);
            Assert.AreEqual(StartupPhase.Complete, _bootstrap.CurrentPhase);
            Assert.AreEqual("Bootstrap", _bootstrap.StartupTrace[0]);
        }

        [Test]
        public void StartupEntersExpectedPhase()
        {
            ConfiguredBootstrap().Initialize();

            Assert.AreEqual(StartupPhase.Complete, _bootstrap.CurrentPhase);
        }

        [Test]
        public void ServicesRegisteredBeforeInitialization()
        {
            ConfiguredBootstrap().Initialize();

            Assert.Less(
                _bootstrap.StartupTrace.ToList().IndexOf("RegisterServices"),
                _bootstrap.StartupTrace.ToList().IndexOf("InitializeServices"));
            Assert.Less(_events.IndexOf("register"), _events.IndexOf("initialize"));
        }

        [Test]
        public void InitializationBeforeInitialSceneLoad()
        {
            ConfiguredBootstrap().Initialize();

            Assert.Less(
                _bootstrap.StartupTrace.ToList().IndexOf("InitializeServices"),
                _bootstrap.StartupTrace.ToList().IndexOf("LoadInitialScene"));
            Assert.Less(_events.IndexOf("initialize"), _events.IndexOf("load:Assets/Football/Scenes/Match/Match.unity"));
        }

        [Test]
        public void InitialSceneLoadBeforeStartupComplete()
        {
            ConfiguredBootstrap().Initialize();

            Assert.Less(
                _bootstrap.StartupTrace.ToList().IndexOf("LoadInitialScene"),
                _bootstrap.StartupTrace.ToList().IndexOf("StartupComplete"));
            Assert.Less(_events.IndexOf("load:Assets/Football/Scenes/Match/Match.unity"), _events.Count);
            Assert.AreEqual("StartupComplete", _bootstrap.StartupTrace[^1]);
        }

        [Test]
        public void StartupCannotExecuteTwice()
        {
            ConfiguredBootstrap().Initialize();
            var traceSnapshot = _bootstrap.StartupTrace.ToList();
            var eventSnapshot = _events.ToList();

            _bootstrap.Initialize();
            _bootstrap.Initialize();

            Assert.AreEqual(BootstrapState.Ready, _bootstrap.CurrentState);
            Assert.AreEqual(traceSnapshot, _bootstrap.StartupTrace,
                "Repeated Initialize after completion must be a no-op reuse; the sequence must not re-run.");
            Assert.AreEqual(eventSnapshot, _events,
                "Phase work must not execute a second time.");
        }

        [Test]
        public void StartupCannotRaceWithItself()
        {
            var reentrant = _bootstrap;
            _events.Add("marker");
            _bootstrap.Configure(
                registerServices: () =>
                {
                    _events.Add("register");
                    try
                    {
                        reentrant.Initialize();
                    }
                    catch (InvalidOperationException)
                    {
                        _events.Add("reentrant-rejected");
                    }
                });

            _bootstrap.Initialize();

            Assert.Contains("reentrant-rejected", _events);
            Assert.AreEqual(BootstrapState.Ready, _bootstrap.CurrentState);
        }

        [Test]
        public void FailureStopsLaterPhases()
        {
            bool initializedCalled = false;
            bool loadCalled = false;
            _bootstrap.Configure(
                prepareInfrastructure: () => _events.Add("infrastructure"),
                registerServices: () => throw new InvalidOperationException("registration failed"),
                initializeServices: () => { initializedCalled = true; },
                loadScene: path => { loadCalled = true; return true; },
                initialScenePath: MatchScene);

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.IsTrue(_bootstrap.IsFailed);
            Assert.AreEqual(BootstrapState.Failed, _bootstrap.CurrentState);
            Assert.AreEqual(StartupPhase.Failed, _bootstrap.CurrentPhase);
            Assert.IsFalse(initializedCalled, "Later phase must not run after a prerequisite failure.");
            Assert.IsFalse(loadCalled, "Scene load must not run after a prerequisite failure.");
            Assert.AreEqual(
                new[] { "Bootstrap", "Infrastructure", "RegisterServices", "Failed" },
                _bootstrap.StartupTrace);
        }

        [Test]
        public void FailedSceneLoad_IsFailure()
        {
            _bootstrap.Configure(
                registerServices: () => _events.Add("register"),
                initializeServices: () => _events.Add("initialize"),
                loadScene: path => false, // load failure
                initialScenePath: MatchScene);

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.IsTrue(_bootstrap.IsFailed);
            Assert.AreEqual(StartupPhase.Failed, _bootstrap.CurrentPhase);
            Assert.AreEqual("Failed", _bootstrap.StartupTrace[^1]);
        }

        [Test]
        public void ShutdownDuringStartup_IsRejected()
        {
            bool shutdownAttempted = false;
            _bootstrap.Configure(
                registerServices: () =>
                {
                    _events.Add("register");
                    try
                    {
                        _bootstrap.Shutdown();
                    }
                    catch (InvalidOperationException)
                    {
                        shutdownAttempted = true;
                        _events.Add("shutdown-rejected");
                    }
                });

            _bootstrap.Initialize();

            Assert.IsTrue(shutdownAttempted);
            Assert.Contains("shutdown-rejected", _events);
            Assert.AreEqual(BootstrapState.Ready, _bootstrap.CurrentState,
                "Startup and shutdown cannot corrupt each other; startup wins the race.");
        }

        [Test]
        public void NoHiddenUpdatePolling()
        {
            Assert.IsNull(GetMethod("Update"));
            Assert.IsNull(GetMethod("LateUpdate"));
        }

        [Test]
        public void NoHiddenFixedUpdatePolling()
        {
            Assert.IsNull(GetMethod("FixedUpdate"));
        }

        private static MethodInfo GetMethod(string name)
        {
            return typeof(GameBootstrap).GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        [Test]
        public void NoGameplayLogicInStartup()
        {
            var references = typeof(GameBootstrap).Assembly.GetReferencedAssemblies();
            foreach (var r in references)
            {
                Assert.IsFalse(
                    r.FullName.StartsWith("Football.Players") ||
                    r.FullName.StartsWith("Football.Ball") ||
                    r.FullName.StartsWith("Football.Match") ||
                    r.FullName.StartsWith("Football.AI"),
                    $"Startup assembly must not reference gameplay assembly: {r.FullName}");
            }
        }

        [Test]
        public void StartupUsesGameBootstrapAsEntryPoint()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(GameBootstrap)),
                "GameBootstrap must be a MonoBehaviour to be the Unity lifecycle entry point.");
            var awake = typeof(GameBootstrap).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(awake, "GameBootstrap must expose an Awake entry point.");
        }

        [Test]
        public void ServiceRegistryDoesNotAutoInitialize()
        {
            var registry = new ServiceRegistry();
            var service = new FakeService();

            _bootstrap.Configure(
                registerServices: () =>
                {
                    registry.Register<FakeService>(service);
                    Assert.IsFalse(service.IsInitialized,
                        "Registration alone must not initialize a service.");
                },
                initializeServices: () =>
                {
                    Assert.IsFalse(service.IsInitialized);
                    service.Initialize();
                    Assert.IsTrue(service.IsInitialized);
                });

            _bootstrap.Initialize();

            Assert.AreEqual(1, service.InitializeCalls);
        }

        [Test]
        public void SceneLoaderDoesNotOwnStartup()
        {
            var methods = typeof(SceneLoader).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                Assert.IsFalse(method.Name == "Initialize" || method.Name == "Shutdown",
                    "SceneLoader must not expose service-style Initialize/Shutdown (it does not own startup).");
            }
        }

        [Test]
        public void StartupSequenceIsDeterministic()
        {
            var traces = new List<IReadOnlyList<string>>();
            for (int i = 0; i < 5; i++)
            {
                var bootstrap = CreateBootstrap();
                bootstrap.Configure(
                    prepareInfrastructure: () => { },
                    registerServices: () => { },
                    initializeServices: () => { },
                    loadScene: path => true,
                    initialScenePath: MatchScene);
                bootstrap.Initialize();
                traces.Add(bootstrap.StartupTrace);
                UnityEngine.Object.DestroyImmediate(bootstrap.gameObject);
            }

            for (int i = 1; i < traces.Count; i++)
            {
                Assert.AreEqual(traces[0], traces[i],
                    "Startup sequence must be deterministic across runs.");
            }
        }
    }
}
