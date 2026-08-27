using System;
using System.Collections.Generic;
using System.Reflection;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies Task 46 Bootstrap Error Handling semantics: the bootstrap classifies and
    /// exposes failures explicitly, preserves the original exception/context, reports once,
    /// never swallows, never auto-retries, never silently continues, and never polls.
    /// </summary>
    public class BootstrapErrorTests
    {
        private const string MatchScene = "Assets/Football/Scenes/Match/Match.unity";
        private const string MenuScene = "Assets/Football/Scenes/Bootstrap/Bootstrap.unity";

        private GameBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("BootstrapErrorTest");
            _bootstrap = go.AddComponent<GameBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrap != null)
                UnityEngine.Object.DestroyImmediate(_bootstrap.gameObject);
        }

        [Test]
        public void InitialState_HasNoError_AndIsNotFailed()
        {
            Assert.IsFalse(_bootstrap.HasFailed);
            Assert.IsFalse(_bootstrap.IsFailed);
            Assert.IsNull(_bootstrap.CurrentError);
            Assert.IsNull(_bootstrap.FailureCategory);
        }

        [Test]
        public void SuccessfulStartup_HasNoError()
        {
            _bootstrap.Initialize();

            Assert.AreEqual(BootstrapState.Ready, _bootstrap.CurrentState);
            Assert.IsFalse(_bootstrap.HasFailed);
            Assert.IsNull(_bootstrap.CurrentError);
            Assert.IsNull(_bootstrap.FailureCategory);
        }

        [Test]
        public void InitializationFailure_SetsFailedState()
        {
            ForceFailureDuringInitialize();

            Assert.IsTrue(_bootstrap.HasFailed);
            Assert.AreEqual(BootstrapState.Failed, _bootstrap.CurrentState);
        }

        [Test]
        public void InitializationFailure_SetsFailedPhase()
        {
            ForceFailureDuringInitialize();

            Assert.AreEqual(StartupPhase.Failed, _bootstrap.CurrentPhase);
        }

        [Test]
        public void InitializationFailure_PreventsLaterPhases()
        {
            bool loadCalled = false;
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => throw new InvalidOperationException("registration failed"),
                initializeServices: () => { },
                loadScene: path => { loadCalled = true; return true; },
                initialScenePath: MatchScene);

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.IsFalse(loadCalled, "Later phase (scene load) must not run after a failed prerequisite.");
            Assert.AreEqual(
                new[] { "Bootstrap", "Infrastructure", "RegisterServices", "Failed" },
                _bootstrap.StartupTrace);
        }

        [Test]
        public void InitializationFailure_DoesNotReachReady()
        {
            ForceFailureDuringInitialize();

            Assert.IsFalse(_bootstrap.IsReady);
            Assert.AreNotEqual(BootstrapState.Ready, _bootstrap.CurrentState);
        }

        [Test]
        public void InitializationFailure_DoesNotMarkStartupComplete()
        {
            ForceFailureDuringInitialize();

            Assert.AreNotEqual(StartupPhase.Complete, _bootstrap.CurrentPhase);
            Assert.AreNotEqual("StartupComplete", _bootstrap.StartupTrace[^1]);
        }

        [Test]
        public void Failure_PreservesOriginalException()
        {
            var original = new InvalidOperationException("Original failure");
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => { },
                initializeServices: () => throw original);

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.NotNull(_bootstrap.CurrentError);
            Assert.AreSame(original, _bootstrap.CurrentError.Exception,
                "The original exception must be preserved by reference, not replaced or copied.");
        }

        [Test]
        public void Failure_PreservesOriginalExceptionType()
        {
            var original = new ArgumentException("bad config value");
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => throw original);

            Assert.Throws<ArgumentException>(() => _bootstrap.Initialize());

            Assert.AreEqual(typeof(ArgumentException), _bootstrap.CurrentError.Exception.GetType());
        }

        [Test]
        public void Failure_PreservesOriginalMessage()
        {
            var original = new InvalidOperationException("the root cause message");
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => { },
                initializeServices: () => throw original);

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.AreEqual("the root cause message", _bootstrap.CurrentError.Exception.Message);
        }

        [Test]
        public void ServiceInitializationFailure_IsClassifiedAsService()
        {
            var original = new InvalidOperationException("service exploded");
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => { },
                initializeServices: () => throw original);

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.AreEqual(BootstrapErrorCategory.Service, _bootstrap.FailureCategory);
            Assert.AreEqual(BootstrapErrorCategory.Service, _bootstrap.CurrentError.Category);
            Assert.AreEqual(StartupPhase.ServicesInitialized, _bootstrap.CurrentError.Phase);
            Assert.AreEqual("InitializeServices", _bootstrap.CurrentError.System);
        }

        [Test]
        public void ServiceRegistrationFailure_IsClassifiedAsService()
        {
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => throw new InvalidOperationException("registration failed"));

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.AreEqual(BootstrapErrorCategory.Service, _bootstrap.FailureCategory);
            Assert.AreEqual(StartupPhase.ServicesRegistered, _bootstrap.CurrentError.Phase);
            Assert.AreEqual("RegisterServices", _bootstrap.CurrentError.System);
        }

        [Test]
        public void InfrastructureFailure_IsClassifiedAsConfiguration()
        {
            _bootstrap.Configure(
                prepareInfrastructure: () => throw new InvalidOperationException("infra failed"));

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.AreEqual(BootstrapErrorCategory.Configuration, _bootstrap.FailureCategory);
            Assert.AreEqual(StartupPhase.InfrastructureReady, _bootstrap.CurrentError.Phase);
            Assert.AreEqual("Infrastructure", _bootstrap.CurrentError.System);
        }

        [Test]
        public void SceneLoadFailure_IsClassifiedAsSceneLoading()
        {
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => { },
                initializeServices: () => { },
                loadScene: path => false, // load failure
                initialScenePath: MatchScene);

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.AreEqual(BootstrapErrorCategory.SceneLoading, _bootstrap.FailureCategory);
            Assert.AreEqual(StartupPhase.InitialSceneLoaded, _bootstrap.CurrentError.Phase);
            Assert.AreEqual("LoadInitialScene", _bootstrap.CurrentError.System);
        }

        [Test]
        public void SceneTransitionFailure_IsClassifiedWithoutReimplementingTransition()
        {
            // Wire a REAL SceneTransitionSystem (Task 45) into the initial-scene hook. We only
            // classify its boundary failure here; we do NOT reimplement transition mechanics.
            var available = new List<string> { MatchScene, MenuScene };
            var loader = new SceneLoader(available, _ => throw new InvalidOperationException("load boom"));
            var transitions = new SceneTransitionSystem(loader);

            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => { },
                initializeServices: () => { },
                loadScene: path => TransitionThrough(transitions, path),
                initialScenePath: MenuScene);

            // The delegated transition fails; SceneTransitionSystem surfaces its own
            // InvalidOperationException (original preserved), which GameBootstrap classifies
            // as a SceneLoading failure.
            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.AreEqual(BootstrapErrorCategory.SceneLoading, _bootstrap.FailureCategory);
            Assert.AreEqual(StartupPhase.InitialSceneLoaded, _bootstrap.CurrentError.Phase);
            Assert.IsTrue(_bootstrap.CurrentError.Exception is InvalidOperationException,
                "The classified transition failure must preserve the original InvalidOperationException.");
        }

        [Test]
        public void FailedState_IsTerminal_NoAutomaticReset()
        {
            ForceFailureDuringInitialize();

            // No automatic recovery: Failed never returns to Boot/Ready.
            Assert.AreEqual(BootstrapState.Failed, _bootstrap.CurrentState);
            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize(),
                "Initialize from Failed must be rejected; there is no automatic restart.");
        }

        [Test]
        public void NoAutomaticRetry_Occurs()
        {
            int initializeCalls = 0;
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => { },
                initializeServices: () =>
                {
                    initializeCalls++;
                    throw new InvalidOperationException("fails once");
                });

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.AreEqual(1, initializeCalls, "The failing service must be attempted exactly once; no auto-retry.");
            Assert.AreEqual(BootstrapState.Failed, _bootstrap.CurrentState);
        }

        [Test]
        public void NoSilentCatchAndContinue()
        {
            bool loadCalled = false;
            _bootstrap.Configure(
                prepareInfrastructure: () => throw new InvalidOperationException("infra"),
                registerServices: () => { },
                initializeServices: () => { },
                loadScene: path => { loadCalled = true; return true; },
                initialScenePath: MatchScene);

            // The failure must propagate (not be caught and swallowed), and later phases must not run.
            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());
            Assert.IsFalse(loadCalled);
            Assert.AreEqual("Failed", _bootstrap.StartupTrace[^1]);
            foreach (var entry in _bootstrap.StartupTrace)
            {
                Assert.AreNotEqual("StartupComplete", entry,
                    "Startup must not appear to succeed after a failure.");
            }
        }

        [Test]
        public void Failure_IsReportedExactlyOnce()
        {
            var reports = new List<BootstrapError>();
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => { },
                initializeServices: () => throw new InvalidOperationException("boom"),
                reportError: e => reports.Add(e));

            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());

            Assert.AreEqual(1, reports.Count,
                "One failure must produce exactly one authoritative bootstrap-level report.");
            Assert.AreSame(_bootstrap.CurrentError, reports[0]);
        }

        [Test]
        public void ErrorInfo_IsReadOnly()
        {
            var error = new BootstrapError(
                BootstrapErrorCategory.Service,
                StartupPhase.ServicesInitialized,
                "InitializeServices",
                new InvalidOperationException("inner"));

            foreach (var prop in typeof(BootstrapError).GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsNull(prop.SetMethod, $"BootstrapError.{prop.Name} must be read-only (no setter).");
            }
        }

        [Test]
        public void CurrentError_IsReadOnly_NoPublicSetter()
        {
            var prop = typeof(GameBootstrap).GetProperty(nameof(GameBootstrap.CurrentError));
            Assert.IsNotNull(prop);
            Assert.IsNotNull(prop.SetMethod, "GameBootstrap.CurrentError must have a setter (internal mutation).");
            Assert.AreNotEqual(MethodAttributes.Public, prop.SetMethod.Attributes & MethodAttributes.Public,
                "GameBootstrap.CurrentError must not expose a public setter.");
        }

        [Test]
        public void HasFailed_IsReadThrough_NoPublicSetter()
        {
            var prop = typeof(GameBootstrap).GetProperty(nameof(GameBootstrap.HasFailed));
            Assert.IsNotNull(prop);
            Assert.IsNull(prop.SetMethod, "GameBootstrap.HasFailed is a computed, read-only property (no setter).");
        }

        [Test]
        public void NoPerFrameErrorPolling()
        {
            foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(typeof(GameBootstrap).GetMethod(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                    $"GameBootstrap must not poll via {name}().");
            }
        }

        [Test]
        public void NoErrorEventBus_NoSingletonErrorManager()
        {
            // No global/static error bus introduced: GameBootstrap error state is instance-owned.
            var field = typeof(GameBootstrap).GetField("_reportError",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Reporter is an instance field (per-bootstrap), not a global bus.");
            Assert.IsFalse(field.IsStatic, "Error reporting must not be static/global.");
        }

        private void ForceFailureDuringInitialize()
        {
            _bootstrap.Configure(
                prepareInfrastructure: () => { },
                registerServices: () => { },
                initializeServices: () => throw new InvalidOperationException("forced init failure"));
            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());
        }

        private static bool TransitionThrough(SceneTransitionSystem transitions, string path)
        {
            // Delegate to the real SceneTransitionSystem; let its failures propagate (e.g. same-scene).
            transitions.RequestTransition(path);
            return true;
        }
    }
}
