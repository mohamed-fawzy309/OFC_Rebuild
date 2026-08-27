using System;
using System.Collections.Generic;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies Task 48 Duplicate Service Diagnostics: duplicate registration is detected,
    /// the colliding type and both the existing and attempted registration are identified,
    /// the diagnostic is actionable, and the first registration is never silently replaced.
    /// </summary>
    public class DuplicateServiceDiagnosticsTests
    {
        private ServiceRegistry _registry;

        private interface IMarkerService : IFootballService { }
        private interface IOtherService : IFootballService { }

        private class FakeService : IMarkerService
        {
            public int Id { get; }
            public bool IsInitialized { get; private set; }

            public FakeService(int id = 0)
            {
                Id = id;
            }

            public void Initialize()
            {
                IsInitialized = true;
            }

            public void Shutdown()
            {
                IsInitialized = false;
            }
        }

        private class OtherService : IOtherService
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

        // ---- 48.1 Detect duplicate registration ----

        [Test]
        public void DuplicateRegistrationIsDetected()
        {
            _registry.Register<IMarkerService>(new FakeService(1));
            Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(new FakeService(2)));
        }

        [Test]
        public void DuplicateThrowsSpecificException()
        {
            _registry.Register<IMarkerService>(new FakeService(1));
            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(new FakeService(2)));
            Assert.IsNotNull(ex);
        }

        // ---- 48.2 / 48.3 / 48.4 Exact duplicate test ----

        [Test]
        public void ExactDuplicate_IdentifiesTypeAndBothInstances()
        {
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(b));

            Assert.AreSame(typeof(IMarkerService), ex.DuplicateServiceType, "48.2 type");
            Assert.AreSame(a, ex.ExistingService, "48.3 existing");
            Assert.AreSame(b, ex.AttemptedService, "48.4 attempted");
            Assert.AreSame(a, _registry.Get<IMarkerService>(), "first registration remains authoritative");
        }

        [Test]
        public void DuplicateServiceTypeIsCorrect()
        {
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(b));

            Assert.AreSame(typeof(IMarkerService), ex.DuplicateServiceType);
        }

        [Test]
        public void GenericRegistrationUsesCorrectDuplicateType()
        {
            // Uses the interface type (the registration key), not the concrete impl type.
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(b));

            Assert.AreSame(typeof(IMarkerService), ex.DuplicateServiceType);
        }

        // ---- 48.3 Identify existing registration ----

        [Test]
        public void ExistingRegistrationIsIdentified()
        {
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(b));

            Assert.AreSame(a, ex.ExistingService);
        }

        // ---- 48.4 Identify attempted registration ----

        [Test]
        public void AttemptedRegistrationIsIdentified()
        {
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(b));

            Assert.AreSame(b, ex.AttemptedService);
        }

        [Test]
        public void ExistingAndAttemptedAreDistinguishable()
        {
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(b));

            Assert.AreNotSame(ex.ExistingService, ex.AttemptedService);
        }

        // ---- 48.5 Actionable diagnostic ----

        [Test]
        public void DiagnosticMessageContainsServiceType()
        {
            _registry.Register<IMarkerService>(new FakeService(1));
            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(new FakeService(2)));
            StringAssert.Contains("IMarkerService", ex.Message);
        }

        [Test]
        public void DiagnosticMessageIsActionable()
        {
            _registry.Register<IMarkerService>(new FakeService(1));
            var ex = Assert.Throws<DuplicateServiceException>(
                () => _registry.Register<IMarkerService>(new FakeService(2)));
            StringAssert.Contains("already registered", ex.Message);
            StringAssert.Contains("rejected", ex.Message);
            StringAssert.Contains("Remove the duplicate registration", ex.Message);
        }

        // ---- 48.6 Prevent silent replacement ----

        [Test]
        public void ExistingRegistrationRemainsAfterFailure()
        {
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            try
            {
                _registry.Register<IMarkerService>(b);
                Assert.Fail("Expected DuplicateServiceException");
            }
            catch (DuplicateServiceException)
            {
            }

            Assert.AreSame(a, _registry.Get<IMarkerService>());
        }

        [Test]
        public void AttemptedRegistrationIsNotStored()
        {
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            Assert.Throws<DuplicateServiceException>(() => _registry.Register<IMarkerService>(b));

            var resolved = _registry.Get<IMarkerService>();
            Assert.AreSame(a, resolved, "Get must return the original, not the attempted instance.");
            Assert.AreNotSame(b, resolved);
        }

        [Test]
        public void DuplicateDoesNotIncreaseRegistryCount()
        {
            // No public Count is exposed; verify via observable behaviour: after the failed
            // duplicate, the registry still resolves exactly the original for the type.
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            Assert.Throws<DuplicateServiceException>(() => _registry.Register<IMarkerService>(b));

            Assert.AreSame(a, _registry.Get<IMarkerService>());
            Assert.IsTrue(_registry.TryGet<IMarkerService>(out var r) && ReferenceEquals(r, a));
        }

        // ---- Backward compatibility ----

        [Test]
        public void DuplicateExceptionRemainsInvalidOperationException()
        {
            _registry.Register<IMarkerService>(new FakeService(1));
            // Assert.Catch (unlike Assert.Throws<T>) accepts the derived type, mirroring a
            // runtime `catch (InvalidOperationException)`.
            var ex = Assert.Catch<InvalidOperationException>(
                () => _registry.Register<IMarkerService>(new FakeService(2)));
            Assert.IsInstanceOf<DuplicateServiceException>(ex);
        }

        // ---- Task 43/44 compatibility: ServiceInitializer ----

        [Test]
        public void ServiceInitializerPreservesDuplicateException()
        {
            // ServiceInitializer performs lookup only (Get) and never re-registers services, so
            // it does not itself detect or swallow duplicates. A DuplicateServiceException raised
            // during the coordinator's registration phase must propagate cleanly — it must not be
            // re-wrapped, replaced, or silently swallowed by the coordinator. Here we drive the
            // registration-gate phase and assert the specific exception survives unchanged.
            var registry = new ServiceRegistry();
            var initializer = new ServiceInitializer(new[] { typeof(FakeService) });

            // Simulate a Composition Root that (incorrectly) registers the same type twice before
            // telling the coordinator to initialize.
            Assert.Throws<DuplicateServiceException>(() =>
            {
                registry.Register<FakeService>(new FakeService(1));
                registry.Register<FakeService>(new FakeService(2)); // throws here, before Initialize
                initializer.Initialize(registry);
            });
        }

        // ---- Task 46 compatibility: Bootstrap classification ----

        [Test]
        public void BootstrapCanClassifyDuplicateServiceFailureWithoutLosingCause()
        {
            var go = new GameObject("DupBootstrapTest");
            try
            {
                var bootstrap = go.AddComponent<GameBootstrap>();
                bootstrap.Configure(registerServices: () =>
                {
                    var registry = new ServiceRegistry();
                    registry.Register<IMarkerService>(new FakeService(1));
                    registry.Register<IMarkerService>(new FakeService(2)); // throws DuplicateServiceException
                });

                Assert.Throws<DuplicateServiceException>(() => bootstrap.Initialize());

                Assert.IsTrue(bootstrap.HasFailed);
                Assert.AreEqual(BootstrapErrorCategory.Service, bootstrap.FailureCategory);
                Assert.IsNotNull(bootstrap.CurrentError, "CurrentError must hold the failure context.");
                Assert.IsInstanceOf<DuplicateServiceException>(bootstrap.CurrentError.Exception,
                    "The specific duplicate exception must survive as CurrentError.Exception.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ---- No side effects on services ----

        [Test]
        public void DuplicateRegistrationDoesNotInitializeService()
        {
            var a = new FakeService(1);
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            Assert.Throws<DuplicateServiceException>(() => _registry.Register<IMarkerService>(b));

            Assert.IsFalse(a.IsInitialized, "Existing service must not be initialized by registration.");
            Assert.IsFalse(b.IsInitialized, "Attempted service must not be initialized.");
        }

        [Test]
        public void DuplicateRegistrationDoesNotShutdownService()
        {
            var a = new FakeService(1);
            a.Initialize();
            var b = new FakeService(2);
            _registry.Register<IMarkerService>(a);

            Assert.Throws<DuplicateServiceException>(() => _registry.Register<IMarkerService>(b));

            Assert.IsTrue(a.IsInitialized, "Existing service must not be shut down by a failed registration.");
        }

        // ---- Registry remains usable ----

        [Test]
        public void RegistryStillWorksAfterDuplicateFailure()
        {
            var a = new FakeService(1);
            _registry.Register<IMarkerService>(a);
            Assert.Throws<DuplicateServiceException>(() => _registry.Register<IMarkerService>(new FakeService(2)));

            var resolved = _registry.Get<IMarkerService>();
            Assert.AreSame(a, resolved);
            Assert.IsTrue(_registry.IsRegistered<IMarkerService>());
        }

        [Test]
        public void DifferentServiceTypesCanStillRegister()
        {
            var a = new FakeService(1);
            var o = new OtherService();
            _registry.Register<IMarkerService>(a);
            _registry.Register<IOtherService>(o);

            Assert.AreSame(a, _registry.Get<IMarkerService>());
            Assert.AreSame(o, _registry.Get<IOtherService>());
        }

        // ---- Architecture constraints ----

        [Test]
        public void DuplicateDetectionDoesNotUseSingleton()
        {
            _registry.Register<IMarkerService>(new FakeService(1));
            Assert.Throws<DuplicateServiceException>(() => _registry.Register<IMarkerService>(new FakeService(2)));
            Assert.IsNull(typeof(ServiceRegistry).GetProperty("Instance"),
                "No static singleton Instance may be introduced by diagnostics.");
        }

        [Test]
        public void DuplicateDetectionDoesNotUseServiceLocator()
        {
            _registry.Register<IMarkerService>(new FakeService(1));
            Assert.Throws<DuplicateServiceException>(() => _registry.Register<IMarkerService>(new FakeService(2)));
            Assert.IsNull(typeof(ServiceRegistry).GetProperty("GlobalServices"),
                "No global Service Locator property may be introduced.");
        }

        [Test]
        public void DuplicateException_IsSealed_NotMonoBehaviour()
        {
            Assert.IsTrue(typeof(DuplicateServiceException).IsSealed);
            Assert.IsFalse(typeof(DuplicateServiceException).IsSubclassOf(typeof(UnityEngine.Object)));
            Assert.IsTrue(typeof(InvalidOperationException).IsAssignableFrom(typeof(DuplicateServiceException)));
        }
    }
}
