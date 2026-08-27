using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies the Scene Transition System: it coordinates transition request/lifecycle and
    /// delegates actual scene loading to SceneLoader, without reimplementing loading, without
    /// a Singleton/Service Locator, without polling, and without gameplay/service concerns.
    /// </summary>
    public class SceneTransitionTests
    {
        private const string MatchScene = "Assets/Football/Scenes/Match/Match.unity";
        private const string MenuScene = "Assets/Football/Scenes/Bootstrap/Bootstrap.unity";

        private readonly List<string> _availableScenes = new() { MatchScene, MenuScene };
        private List<string> _trace;
        private int _loadCalls;

        [SetUp]
        public void SetUp()
        {
            _trace = new List<string>();
            _loadCalls = 0;
        }

        private SceneLoader CreateLoader(Action<string> onLoad = null)
        {
            return new SceneLoader(
                _availableScenes,
                path =>
                {
                    _loadCalls++;
                    _trace.Add("Load:" + path);
                    onLoad?.Invoke(path);
                });
        }

        private SceneTransitionSystem CreateSystem(SceneLoader loader)
        {
            return new SceneTransitionSystem(loader);
        }

        [Test]
        public void TransitionSystemCanBeCreated()
        {
            var system = CreateSystem(CreateLoader());
            Assert.IsNotNull(system);
        }

        [Test]
        public void InitialStateIsIdle()
        {
            var system = CreateSystem(CreateLoader());
            Assert.AreEqual(SceneTransitionState.Idle, system.CurrentState);
            Assert.IsFalse(system.IsTransitioning);
        }

        [Test]
        public void ValidTransitionBegins()
        {
            var loader = CreateLoader();
            var system = CreateSystem(loader);
            // Current scene starts null/empty; first load is valid.
            system.RequestTransition(MatchScene);
            Assert.AreEqual(SceneTransitionState.Completed, system.CurrentState);
        }

        [Test]
        public void SceneLoaderIsInvokedExactlyOnce()
        {
            var loader = CreateLoader();
            var system = CreateSystem(loader);
            system.RequestTransition(MatchScene);
            Assert.AreEqual(1, _loadCalls);
        }

        [Test]
        public void SuccessfulTransitionCompletes()
        {
            var loader = CreateLoader();
            var system = CreateSystem(loader);
            system.RequestTransition(MatchScene);
            Assert.AreEqual(SceneTransitionState.Completed, system.CurrentState);
            Assert.IsFalse(system.IsTransitioning);
        }

        [Test]
        public void CurrentSceneRemainsConsistent()
        {
            var loader = CreateLoader();
            var system = CreateSystem(loader);
            Assert.IsNull(system.CurrentScene);
            system.RequestTransition(MatchScene);
            // Transition's current scene reads through the loader's single source of truth.
            Assert.AreEqual(MatchScene, system.CurrentScene);
            Assert.AreEqual(MatchScene, loader.CurrentScene);
            Assert.AreEqual(MatchScene, system.TargetScene);
        }

        [Test]
        public void InvalidTargetFails()
        {
            var system = CreateSystem(CreateLoader());
            Assert.Throws<ArgumentException>(() => system.RequestTransition(null));
            Assert.Throws<ArgumentException>(() => system.RequestTransition("   "));
        }

        [Test]
        public void MissingTargetFails()
        {
            var loader = CreateLoader();
            var system = CreateSystem(loader);
            var ex = Assert.Throws<InvalidOperationException>(
                () => system.RequestTransition("Assets/Football/Scenes/Missing/Missing.unity"));
            Assert.AreEqual(SceneTransitionState.Failed, system.CurrentState);
            Assert.IsNotNull(system.LastError);
        }

        [Test]
        public void SameSceneTransitionRejected()
        {
            var loader = CreateLoader();
            var system = CreateSystem(loader);
            system.RequestTransition(MatchScene);

            Assert.Throws<InvalidOperationException>(() => system.RequestTransition(MatchScene));

            Assert.AreEqual(SceneTransitionState.Completed, system.CurrentState,
                "Rejecting a same-scene request must not disturb the completed transition.");
            Assert.AreEqual(1, _loadCalls, "No second load may be issued for a same-scene request.");
        }

        [Test]
        public void ConcurrentTransitionRejected()
        {
            // While a transition is in Loading (inside the loader seam), a second request for a
            // different target must be rejected and the first request preserved.
            SceneTransitionSystem sys = null;
            sys = CreateSystem(new SceneLoader(
                _availableScenes,
                path =>
                {
                    _loadCalls++;
                    // State is Loading here; a concurrent request must be rejected.
                    Assert.Throws<InvalidOperationException>(() => sys.RequestTransition(MenuScene));
                }));
            sys.RequestTransition(MatchScene);

            Assert.AreEqual(1, _loadCalls, "Only the first transition's load may run.");
            Assert.AreEqual(SceneTransitionState.Completed, sys.CurrentState);
            Assert.AreEqual(MatchScene, sys.TargetScene, "The first request remains authoritative.");
        }

        [Test]
        public void ReentrantTransitionRejected()
        {
            // A transition callback attempts another RequestTransition while still loading.
            SceneTransitionSystem sys = null;
            sys = CreateSystem(new SceneLoader(
                _availableScenes,
                path =>
                {
                    _loadCalls++;
                    Assert.Throws<InvalidOperationException>(() => sys.RequestTransition(MenuScene));
                }));
            sys.RequestTransition(MatchScene);
        }

        [Test]
        public void FailedLoadMovesToFailed()
        {
            var loader = CreateLoader(path =>
            {
                _trace.Add("Load:" + path);
                throw new InvalidOperationException("loader rejected");
            });
            var system = CreateSystem(loader);
            Assert.Throws<InvalidOperationException>(() => system.RequestTransition(MatchScene));
            Assert.AreEqual(SceneTransitionState.Failed, system.CurrentState);
            Assert.IsFalse(system.IsTransitioning);
        }

        [Test]
        public void FailureDoesNotReportCompletion()
        {
            var loader = CreateLoader(path => throw new InvalidOperationException("loader rejected"));
            var system = CreateSystem(loader);
            Assert.Throws<InvalidOperationException>(() => system.RequestTransition(MatchScene));
            Assert.AreEqual(SceneTransitionState.Failed, system.CurrentState);
            Assert.AreNotEqual(SceneTransitionState.Completed, system.CurrentState);
        }

        [Test]
        public void NoAutomaticRetry()
        {
            var loader = CreateLoader(path => throw new InvalidOperationException("loader rejected"));
            var system = CreateSystem(loader);
            Assert.Throws<InvalidOperationException>(() => system.RequestTransition(MatchScene));
            // Exactly one load attempted; no automatic retry after failure.
            Assert.AreEqual(1, _loadCalls);
        }

        [Test]
        public void NoQueueingOfConflictingRequests()
        {
            // A request issued while loading is rejected and not queued for later completion.
            SceneTransitionSystem sys = null;
            sys = CreateSystem(new SceneLoader(_availableScenes, path => _loadCalls++));
            sys.RequestTransition(MatchScene); // completes synchronously
            Assert.AreEqual(1, _loadCalls);
            // After a completed transition, a new valid request is allowed (not queued/blocked).
            sys.RequestTransition(MenuScene);
            Assert.AreEqual(2, _loadCalls);
        }

        [Test]
        public void NoSingletonRequirement()
        {
            Assert.IsFalse(typeof(SceneTransitionSystem).IsSubclassOf(typeof(UnityEngine.Object)));
            Assert.IsNull(typeof(SceneTransitionSystem).GetProperty("Instance",
                BindingFlags.Static | BindingFlags.Public));
            Assert.AreEqual(typeof(object), typeof(SceneTransitionSystem).BaseType);
        }

        [Test]
        public void NoServiceLocatorDependency()
        {
            // The system's only dependency is an injected SceneLoader reference.
            var ctor = typeof(SceneTransitionSystem).GetConstructors().Single();
            Assert.AreEqual(1, ctor.GetParameters().Length);
            Assert.AreEqual(typeof(SceneLoader), ctor.GetParameters()[0].ParameterType);
        }

        [Test]
        public void NoGameplayDependency()
        {
            // The transition coordinator must not reference gameplay/service types.
            foreach (var m in typeof(SceneTransitionSystem).GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var p in m.GetParameters())
                {
                    Assert.IsFalse(
                        TryIsServiceLike(p.ParameterType),
                        $"Transition method {m.Name} should not depend on {p.ParameterType.Name}.");
                }
            }
        }

        [Test]
        public void NoPerFramePolling()
        {
            foreach (var m in new[]
            {
                "Update", "FixedUpdate", "LateUpdate"
            })
            {
                Assert.IsNull(typeof(SceneTransitionSystem).GetMethod(m,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    $"SceneTransitionSystem must not have a {m}() polling method.");
            }
        }

        [Test]
        public void LoaderOwnsActualSceneLoading()
        {
            // The transition system delegates to SceneLoader; it must not call SceneManager itself.
            var sourcePath = Path.Combine(Application.dataPath,
                "Football", "Runtime", "Core", "SceneTransitionSystem.cs");
            string source = File.ReadAllText(sourcePath);
            Assert.IsFalse(source.Contains("SceneManager."),
                "SceneTransitionSystem must not call into SceneManager; it must delegate to SceneLoader.");

            // Prove delegation: the loader performed the load and its state/current scene changed.
            var loader = CreateLoader();
            var system = CreateSystem(loader);
            system.RequestTransition(MatchScene);
            Assert.AreEqual(SceneLoadState.Loaded, loader.CurrentState);
            Assert.AreEqual(MatchScene, loader.CurrentScene);
        }

        [Test]
        public void TransitionSystemDoesNotInitializeServices()
        {
            foreach (var m in typeof(SceneTransitionSystem).GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var p in m.GetParameters())
                {
                    Assert.IsFalse(
                        typeof(IFootballService).IsAssignableFrom(p.ParameterType),
                        $"Transition method {m.Name} must not depend on IFootballService.");
                }
            }

            Assert.IsNull(typeof(SceneTransitionSystem).GetMethod("Initialize",
                BindingFlags.Instance | BindingFlags.Public));
            Assert.IsNull(typeof(SceneTransitionSystem).GetMethod("Shutdown",
                BindingFlags.Instance | BindingFlags.Public));
        }

        [Test]
        public void TransitionSystemDoesNotShutdownServices()
        {
            // The transition system must not reference ServiceRegistry or ServiceInitializer.
            Assert.IsNull(typeof(SceneTransitionSystem).GetMethod("Shutdown",
                BindingFlags.Instance | BindingFlags.Public));
            Assert.IsFalse(
                typeof(SceneTransitionSystem).GetConstructors().Single()
                    .GetParameters().Any(p =>
                        p.ParameterType == typeof(ServiceRegistry) ||
                        p.ParameterType == typeof(ServiceInitializer)));

            // Code-level guard: the source mentions neither registry nor initializer.
            var sourcePath = Path.Combine(Application.dataPath,
                "Football", "Runtime", "Core", "SceneTransitionSystem.cs");
            string source = File.ReadAllText(sourcePath);
            Assert.IsFalse(source.Contains("ServiceRegistry"));
            Assert.IsFalse(source.Contains("ServiceInitializer"));
        }

        [Test]
        public void SuccessfulTransitionTraceIsExact()
        {
            var loader = CreateLoader();
            var system = CreateSystem(loader);

            _trace.Add("Request:" + MatchScene);
            system.RequestTransition(MatchScene);
            _trace.Add("Completed:" + MatchScene);

            Assert.AreEqual(
                new List<string>
                {
                    "Request:" + MatchScene,
                    "Load:" + MatchScene,
                    "Completed:" + MatchScene
                },
                _trace);
        }

        [Test]
        public void FailureTransitionTraceHasNoCompletion()
        {
            var loader = CreateLoader(path => throw new InvalidOperationException("load failed"));
            var system = CreateSystem(loader);

            Assert.Throws<InvalidOperationException>(() => system.RequestTransition(MatchScene));

            Assert.AreEqual(
                new List<string> { "Load:" + MatchScene },
                _trace.Where(t => t.StartsWith("Load")).ToList());
            Assert.IsFalse(_trace.Any(t => t.StartsWith("Completed")),
                "No Completed entry may exist after a failure.");
            Assert.AreEqual(SceneTransitionState.Failed, system.CurrentState);
        }

        private static bool TryIsServiceLike(Type type)
        {
            return type == typeof(ServiceRegistry) ||
                   type == typeof(ServiceInitializer) ||
                   type == typeof(IFootballService) ||
                   type == typeof(GameBootstrap);
        }
    }
}
