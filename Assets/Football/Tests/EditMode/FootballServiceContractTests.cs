using System.Collections.Generic;
using NUnit.Framework;
using Football.Core;

namespace Football.Tests.EditMode
{
    public class FootballServiceContractTests
    {
        private class FakeService : IFootballService
        {
            public bool IsInitialized { get; private set; }
            public int InitializeCount;
            public int ShutdownCount;
            public List<string> LifecycleLog;

            public FakeService()
            {
                LifecycleLog = new List<string>();
            }

            public void Initialize()
            {
                InitializeCount++;
                IsInitialized = true;
                LifecycleLog.Add("Initialize");
            }

            public void Shutdown()
            {
                ShutdownCount++;
                IsInitialized = false;
                LifecycleLog.Add("Shutdown");
            }
        }

        [Test]
        public void Service_CanBeImplemented()
        {
            IFootballService service = new FakeService();
            Assert.IsNotNull(service);
        }

        [Test]
        public void Initialize_CanBeCalled()
        {
            var service = new FakeService();
            service.Initialize();
            Assert.AreEqual(1, service.InitializeCount);
            Assert.IsTrue(service.IsInitialized);
        }

        [Test]
        public void Shutdown_CanBeCalled()
        {
            var service = new FakeService();
            service.Initialize();
            service.Shutdown();
            Assert.AreEqual(1, service.ShutdownCount);
            Assert.IsFalse(service.IsInitialized);
        }

        [Test]
        public void InitializeAndShutdown_CanBeCalledInOrder()
        {
            var service = new FakeService();
            Assert.IsFalse(service.IsInitialized);

            service.Initialize();
            Assert.IsTrue(service.IsInitialized);
            Assert.AreEqual(0, service.ShutdownCount);

            service.Shutdown();
            Assert.IsFalse(service.IsInitialized);
            Assert.AreEqual(1, service.InitializeCount);
            Assert.AreEqual(1, service.ShutdownCount);

            Assert.AreEqual(
                new List<string> { "Initialize", "Shutdown" },
                service.LifecycleLog);
        }

        [Test]
        public void Service_DoesNotRequireSingletonBehavior()
        {
            var a = new FakeService();
            var b = new FakeService();

            a.Initialize();
            Assert.IsTrue(a.IsInitialized);
            Assert.IsFalse(b.IsInitialized);
        }

        [Test]
        public void MultipleIndependentServiceInstances_CanExist()
        {
            var serviceA = new FakeService();
            var serviceB = new FakeService();

            serviceA.Initialize();
            serviceB.Initialize();
            serviceB.Shutdown();

            Assert.IsTrue(serviceA.IsInitialized);
            Assert.IsFalse(serviceB.IsInitialized);
            Assert.AreEqual(1, serviceA.InitializeCount);
            Assert.AreEqual(1, serviceB.InitializeCount);
            Assert.AreEqual(1, serviceB.ShutdownCount);
        }

        [Test]
        public void InterfaceOnlyContainsLifecycleMembers()
        {
            var methods = typeof(IFootballService).GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly);
            var properties = typeof(IFootballService).GetProperties(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly);

            Assert.AreEqual(3, methods.Length); // Initialize, Shutdown, get_IsInitialized
            Assert.AreEqual(1, properties.Length);

            Assert.IsNotNull(typeof(IFootballService).GetMethod("Initialize"));
            Assert.IsNotNull(typeof(IFootballService).GetMethod("Shutdown"));
            Assert.IsNotNull(typeof(IFootballService).GetProperty("IsInitialized"));
        }

        [Test]
        public void InterfaceHasNoStaticMembers()
        {
            var members = typeof(IFootballService).GetMembers();
            foreach (var member in members)
            {
                Assert.IsFalse(
                    member.ToString().Contains("Static"),
                    $"Interface should not have static members: {member}");
            }
        }
    }
}
