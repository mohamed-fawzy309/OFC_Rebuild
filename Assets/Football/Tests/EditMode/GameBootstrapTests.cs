using System;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    public class GameBootstrapTests
    {
        private GameBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestBootstrap");
            _bootstrap = go.AddComponent<GameBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrap != null)
                UnityEngine.Object.DestroyImmediate(_bootstrap.gameObject);
        }

        [Test]
        public void InitialState_IsBoot_UntilInitialized()
        {
            Assert.AreEqual(BootstrapState.Boot, _bootstrap.CurrentState);
        }

        [Test]
        public void IsReady_IsFalse_BeforeInitialization()
        {
            Assert.IsFalse(_bootstrap.IsReady);
        }

        [Test]
        public void IsFailed_IsFalse_Initially()
        {
            Assert.IsFalse(_bootstrap.IsFailed);
        }

        [Test]
        public void IsStopped_IsFalse_Initially()
        {
            Assert.IsFalse(_bootstrap.IsStopped);
        }

        [Test]
        public void Initialize_TransitionsBootToInitializingToReady()
        {
            _bootstrap.Initialize();

            Assert.AreEqual(BootstrapState.Ready, _bootstrap.CurrentState);
            Assert.IsTrue(_bootstrap.IsReady);
        }

        [Test]
        public void Initialize_LeavesNoIntermediateState()
        {
            _bootstrap.Initialize();

            Assert.AreEqual(BootstrapState.Ready, _bootstrap.CurrentState);
        }

        [Test]
        public void Shutdown_TransitionsReadyToStopped()
        {
            _bootstrap.Initialize();
            _bootstrap.Shutdown();

            Assert.AreEqual(BootstrapState.Stopped, _bootstrap.CurrentState);
            Assert.IsTrue(_bootstrap.IsStopped);
        }

        [Test]
        public void FailedFromBoot_IsInvalid()
        {
            Assert.Throws<InvalidOperationException>(() => _bootstrap.MarkFailed());
        }

        [Test]
        public void ShutdownFromBoot_IsInvalid()
        {
            Assert.Throws<InvalidOperationException>(() => _bootstrap.Shutdown());
        }

        [Test]
        public void FailedToInitializing_IsInvalid()
        {
            var go = new GameObject("FInit");
            var bs = go.AddComponent<BootstrapWithForcedFail>();
            bs.FailDuringInit();
            Assert.Throws<InvalidOperationException>(() => bs.Initialize());
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void FailedToShuttingDown_IsInvalid()
        {
            var go = new GameObject("FShut");
            var bs = go.AddComponent<BootstrapWithForcedFail>();
            bs.FailDuringInit();
            Assert.Throws<InvalidOperationException>(() => bs.Shutdown());
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void StoppedToInitializing_IsInvalid()
        {
            _bootstrap.Initialize();
            _bootstrap.Shutdown();
            Assert.Throws<InvalidOperationException>(() => _bootstrap.Initialize());
        }

        [Test]
        public void StoppedToShuttingDown_IsInvalid()
        {
            _bootstrap.Initialize();
            _bootstrap.Shutdown();
            Assert.Throws<InvalidOperationException>(() => _bootstrap.Shutdown());
        }

        [Test]
        public void MultipleInitialize_Calls_AreValid()
        {
            _bootstrap.Initialize();
            _bootstrap.Initialize();
            _bootstrap.Initialize();
            Assert.AreEqual(BootstrapState.Ready, _bootstrap.CurrentState);
        }

        [Test]
        public void NoGameplayDependencies()
        {
            var bootstrapType = typeof(GameBootstrap);
            var references = bootstrapType.Assembly.GetReferencedAssemblies();

            foreach (var r in references)
            {
                Assert.IsFalse(
                    r.FullName.StartsWith("Football.Players") ||
                    r.FullName.StartsWith("Football.Ball") ||
                    r.FullName.StartsWith("Football.Match") ||
                    r.FullName.StartsWith("Football.AI"),
                    $"GameBootstrap assembly references gameplay assembly: {r.FullName}");
            }
        }

        [Test]
        public void Lifecycle_IsDeterministic()
        {
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject($"Determinism{i}");
                var bootstrap = go.AddComponent<GameBootstrap>();
                bootstrap.Initialize();
                Assert.AreEqual(BootstrapState.Ready, bootstrap.CurrentState);
                Assert.IsTrue(bootstrap.IsReady);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BootstrapType_IsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(GameBootstrap)));
        }

        [Test]
        public void IsReady_IsFalse_InFailedState()
        {
            var go = new GameObject("FailCheck");
            var bs = go.AddComponent<BootstrapWithForcedFail>();
            bs.FailDuringInit();
            Assert.IsFalse(bs.IsReady);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void IsReady_IsFalse_InStoppedState()
        {
            _bootstrap.Initialize();
            _bootstrap.Shutdown();
            Assert.IsFalse(_bootstrap.IsReady);
        }

        [Test]
        public void NoUpdateMethodExists()
        {
            var method = typeof(GameBootstrap).GetMethod("Update",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            Assert.IsNull(method, "GameBootstrap must not have an Update method");
        }

        [Test]
        public void NoFixedUpdateMethodExists()
        {
            var method = typeof(GameBootstrap).GetMethod("FixedUpdate",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            Assert.IsNull(method, "GameBootstrap must not have a FixedUpdate method");
        }

        [Test]
        public void State_IsCentralizedAndDeterministic()
        {
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject($"Cycle{i}");
                var fresh = go.AddComponent<GameBootstrap>();
                fresh.Initialize();
                Assert.AreEqual(BootstrapState.Ready, fresh.CurrentState);
                fresh.Shutdown();
                Assert.AreEqual(BootstrapState.Stopped, fresh.CurrentState);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private class BootstrapWithForcedFail : GameBootstrap
        {
            public void FailDuringInit()
            {
                Initialize();
                MarkFailed();
            }
        }
    }
}
