using System;
using System.Collections.Generic;
using Football.Core;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies Task 47 Missing Service Diagnostics: the registry detects a missing required
    /// service, identifies the requesting system and the missing service type, produces an
    /// actionable diagnostic, distinguishes "missing" from "registered-but-not-initialized",
    /// and does all of this without breaking the pre-existing InvalidOperationException contract.
    /// </summary>
    public class MissingServiceDiagnosticsTests
    {
        private ServiceRegistry _registry;

        private class FakeService : IFootballService
        {
            public bool IsInitialized { get; private set; }

            public void Initialize()
            {
                IsInitialized = true;
            }

            public void Shutdown()
            {
                IsInitialized = false;
            }
        }

        private class AnotherFakeService : IFootballService
        {
            public bool IsInitialized { get; private set; }

            public void Initialize()
            {
                IsInitialized = true;
            }

            public void Shutdown()
            {
                IsInitialized = false;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _registry = new ServiceRegistry();
        }

        // ---- 47.1 Detect a missing required service ----

        [Test]
        public void DetectsMissingService_ThrowsServiceNotFoundException()
        {
            // FakeService is declared required but never registered.
            var ex = Assert.Throws<ServiceNotFoundException>(() => _registry.Get<FakeService>());
            Assert.IsNotNull(ex);
        }

        [Test]
        public void IsRegistered_ReturnsFalse_ForMissingService()
        {
            Assert.IsFalse(_registry.IsRegistered<FakeService>());
            Assert.IsFalse(_registry.IsRegistered(typeof(FakeService)));
        }

        [Test]
        public void IsRegistered_ReturnsTrue_ForRegisteredService()
        {
            _registry.Register<FakeService>(new FakeService());
            Assert.IsTrue(_registry.IsRegistered<FakeService>());
            Assert.IsTrue(_registry.IsRegistered(typeof(FakeService)));
        }

        // ---- 47.3 Identify the missing service type ----

        [Test]
        public void IdentifiesMissingServiceType_Generic()
        {
            var ex = Assert.Throws<ServiceNotFoundException>(() => _registry.Get<FakeService>());
            Assert.AreSame(typeof(FakeService), ex.MissingServiceType);
        }

        [Test]
        public void IdentifiesMissingServiceType_RuntimeType()
        {
            var ex = Assert.Throws<ServiceNotFoundException>(
                () => _registry.Get(typeof(AnotherFakeService)));
            Assert.AreSame(typeof(AnotherFakeService), ex.MissingServiceType);
        }

        // ---- 47.2 Identify the requesting system ----

        [Test]
        public void IdentifiesRequestingSystem_WhenProvided()
        {
            var ex = Assert.Throws<ServiceNotFoundException>(
                () => _registry.Get<FakeService>("MatchSystem"));
            Assert.AreEqual("MatchSystem", ex.RequestingSystem);
        }

        [Test]
        public void RequestingSystem_DefaultsToUnknown_WhenNotProvided()
        {
            var ex = Assert.Throws<ServiceNotFoundException>(() => _registry.Get<FakeService>());
            Assert.AreEqual("Unknown", ex.RequestingSystem);
        }

        [Test]
        public void RequestingSystem_ThroughServiceInitializer_IsCoordinator()
        {
            // ServiceInitializer resolves every declared service up front and names itself as
            // the requesting system for the missing-service diagnostic.
            var registry = new ServiceRegistry();
            registry.Register<FakeService>(new FakeService());
            // AnotherFakeService is declared but NOT registered.

            var initializer = new ServiceInitializer(
                new[] { typeof(FakeService), typeof(AnotherFakeService) });

            var ex = Assert.Throws<ServiceNotFoundException>(
                () => initializer.Initialize(registry));

            Assert.AreSame(typeof(AnotherFakeService), ex.MissingServiceType);
            Assert.AreEqual(nameof(ServiceInitializer), ex.RequestingSystem);
        }

        // ---- 47.4 Produce an actionable diagnostic ----

        [Test]
        public void Message_IsActionable_ContainsMissingTypeAndResolutionSteps()
        {
            var ex = Assert.Throws<ServiceNotFoundException>(() => _registry.Get<FakeService>());
            StringAssert.Contains("FakeService", ex.Message);
            StringAssert.Contains("not registered", ex.Message);
            StringAssert.Contains("Register", ex.Message);
        }

        [Test]
        public void Message_IdentifiesRequestingSystem_WhenProvided()
        {
            var ex = Assert.Throws<ServiceNotFoundException>(
                () => _registry.Get<FakeService>("InputSystem"));
            StringAssert.Contains("InputSystem", ex.Message);
        }

        // ---- 47.5 Distinguish missing vs registered-but-not-initialized ----

        [Test]
        public void RegisteredButNotInitialized_IsNotMissing()
        {
            // A service that is registered but NOT yet initialized must not be reported as
            // missing: lookup succeeds and IsInitialized reports its (false) state.
            _registry.Register<FakeService>(new FakeService());

            var service = _registry.Get<FakeService>();
            Assert.IsTrue(_registry.IsRegistered<FakeService>());
            Assert.IsFalse(service.IsInitialized, "Registered service may be uninitialized, not missing.");
            Assert.DoesNotThrow(() => _registry.Get<FakeService>());
        }

        [Test]
        public void MissingVsNotInitialized_AreDistinctSignals()
        {
            // Missing  == not in registry (IsRegistered false, Get throws ServiceNotFoundException).
            // Uninitialized == registered but IsInitialized false (IsRegistered true, Get returns instance).
            _registry.Register<FakeService>(new FakeService());

            Assert.IsTrue(_registry.IsRegistered<FakeService>());
            Assert.IsFalse(_registry.Get<FakeService>().IsInitialized);
            Assert.IsFalse(_registry.IsRegistered<AnotherFakeService>());
            Assert.Throws<ServiceNotFoundException>(() => _registry.Get<AnotherFakeService>());
        }

        // ---- Backward compatibility: ServiceNotFoundException is an InvalidOperationException ----

        [Test]
        public void MissingService_IsStillAnInvalidOperationException()
        {
            // Assert.Catch<InvalidOperationException> (unlike Assert.Throws<T>) accepts the
            // derived ServiceNotFoundException, mirroring a runtime `catch (InvalidOperationException)`.
            var ex = Assert.Catch<InvalidOperationException>(() => _registry.Get<FakeService>());
            Assert.IsInstanceOf<ServiceNotFoundException>(ex);
        }

        [Test]
        public void InvalidOperationException_Contract_IsPreserved()
        {
            // Pre-existing callers that only catch InvalidOperationException on a missing
            // service continue to work unchanged (a runtime catch of the base type matches).
            Assert.Catch<InvalidOperationException>(() => _registry.Get<FakeService>());
        }

        // ---- Diagnostics do not leak into unrelated behaviour ----

        [Test]
        public void LookupOfRegisteredService_DoesNotThrow()
        {
            var service = new FakeService();
            _registry.Register<FakeService>(service);
            Assert.DoesNotThrow(() => _registry.Get<FakeService>());
        }

        [Test]
        public void IsRegistered_NullType_ReturnsFalse()
        {
            Assert.IsFalse(_registry.IsRegistered((Type)null));
        }

        [Test]
        public void Get_NullType_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _registry.Get((Type)null));
        }

        [Test]
        public void DiagnosticsType_IsSealedException_NotMonoBehaviour()
        {
            Assert.IsTrue(typeof(ServiceNotFoundException).IsSealed);
            Assert.IsFalse(typeof(ServiceNotFoundException).IsSubclassOf(typeof(UnityEngine.Object)));
            Assert.IsTrue(typeof(InvalidOperationException).IsAssignableFrom(typeof(ServiceNotFoundException)));
        }
    }
}
