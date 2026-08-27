using System;
using Football.Core;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    public class ServiceRegistryTests
    {
        private ServiceRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new ServiceRegistry();
        }

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
        public void RegistryCanBeCreated()
        {
            Assert.IsNotNull(_registry);
        }

        [Test]
        public void RegisterService()
        {
            var service = new FakeService();
            _registry.Register<FakeService>(service);
            Assert.IsNotNull(_registry.Get<FakeService>());
        }

        [Test]
        public void LookupRegisteredService()
        {
            var service = new FakeService();
            _registry.Register<FakeService>(service);
            Assert.IsNotNull(_registry.Get<FakeService>());
        }

        [Test]
        public void LookupReturnsExactInstance()
        {
            var service = new FakeService();
            _registry.Register<FakeService>(service);
            Assert.AreSame(service, _registry.Get<FakeService>());
        }

        [Test]
        public void NullRegistrationFails()
        {
            Assert.Throws<ArgumentNullException>(() => _registry.Register<FakeService>(null));
        }

        [Test]
        public void DuplicateRegistrationFails()
        {
            _registry.Register<FakeService>(new FakeService());
            Assert.Throws<DuplicateServiceException>(() => _registry.Register<FakeService>(new FakeService()));
        }

        [Test]
        public void MissingServiceFails()
        {
            Assert.Throws<ServiceNotFoundException>(() => _registry.Get<FakeService>());
        }

        [Test]
        public void TryGetMissingService_ReturnsFalse()
        {
            Assert.IsFalse(_registry.TryGet<FakeService>(out _));
        }

        [Test]
        public void TryGetRegisteredService_ReturnsTrueAndInstance()
        {
            var service = new FakeService();
            _registry.Register<FakeService>(service);
            Assert.IsTrue(_registry.TryGet<FakeService>(out var result));
            Assert.AreSame(service, result);
        }

        [Test]
        public void MultipleServiceTypesCanCoexist()
        {
            var serviceA = new FakeService();
            var serviceB = new AnotherFakeService();
            _registry.Register<FakeService>(serviceA);
            _registry.Register<AnotherFakeService>(serviceB);

            Assert.AreSame(serviceA, _registry.Get<FakeService>());
            Assert.AreSame(serviceB, _registry.Get<AnotherFakeService>());
        }

        [Test]
        public void RegistryInstancesAreIndependent()
        {
            var registryA = new ServiceRegistry();
            var registryB = new ServiceRegistry();

            registryA.Register<FakeService>(new FakeService());

            Assert.IsTrue(registryA.TryGet<FakeService>(out _));
            Assert.IsFalse(registryB.TryGet<FakeService>(out _));
        }

        [Test]
        public void RegistryDoesNotRequireSingleton()
        {
            _registry.Register<FakeService>(new FakeService());
            Assert.IsNull(typeof(ServiceRegistry).GetProperty("Instance"));
        }

        [Test]
        public void RegistrationDoesNotImplicitlyInitializeService()
        {
            var service = new FakeService();
            _registry.Register<FakeService>(service);
            Assert.AreEqual(0, service.InitializeCalls);
            Assert.IsFalse(service.IsInitialized);
        }

        [Test]
        public void LookupDoesNotInitializeService()
        {
            var service = new FakeService();
            _registry.Register<FakeService>(service);

            _registry.Get<FakeService>();
            _registry.TryGet<FakeService>(out _);

            Assert.AreEqual(0, service.InitializeCalls);
            Assert.IsFalse(service.IsInitialized);
        }

        [Test]
        public void ServiceLifetimeIsExternalToRegistry()
        {
            var service = new FakeService();
            _registry.Register<FakeService>(service);

            service.Initialize();
            Assert.IsTrue(service.IsInitialized);

            _registry.Get<FakeService>();
            Assert.IsTrue(service.IsInitialized);

            service.Shutdown();
            Assert.IsFalse(service.IsInitialized);
        }

        [Test]
        public void RegistryIsPureCSharpNotMonoBehaviour()
        {
            Assert.IsFalse(typeof(ServiceRegistry).IsSubclassOf(typeof(UnityEngine.Object)));
            Assert.AreEqual(typeof(object), typeof(ServiceRegistry).BaseType);
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
    }
}
