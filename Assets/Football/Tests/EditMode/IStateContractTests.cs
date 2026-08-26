using NUnit.Framework;
using Football.Core;

namespace Football.Tests.EditMode
{
    public class IStateContractTests
    {
        private class TestState : IState
        {
            public int EnterCount;
            public int TickCount;
            public int ExitCount;
            public float LastDeltaTime;

            public void Enter() => EnterCount++;
            public void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }
            public void Exit() => ExitCount++;
        }

        [Test]
        public void ConcreteClass_CanImplement_IState()
        {
            IState state = new TestState();
            Assert.IsNotNull(state);
        }

        [Test]
        public void Enter_CalledOnce_IncrementsCount()
        {
            var state = new TestState();
            state.Enter();
            Assert.AreEqual(1, state.EnterCount);
        }

        [Test]
        public void Tick_CalledOnce_IncrementsCount()
        {
            var state = new TestState();
            state.Tick(0.016f);
            Assert.AreEqual(1, state.TickCount);
        }

        [Test]
        public void Tick_ReceivesCorrect_DeltaTime()
        {
            var state = new TestState();
            state.Tick(0.05f);
            Assert.AreEqual(0.05f, state.LastDeltaTime, 0.0001f);
        }

        [Test]
        public void Exit_CalledOnce_IncrementsCount()
        {
            var state = new TestState();
            state.Exit();
            Assert.AreEqual(1, state.ExitCount);
        }

        [Test]
        public void Lifecycle_EnterTickExit_ExecutesInOrder()
        {
            var state = new TestState();

            state.Enter();
            Assert.AreEqual(1, state.EnterCount);
            Assert.AreEqual(0, state.TickCount);
            Assert.AreEqual(0, state.ExitCount);

            state.Tick(0.016f);
            Assert.AreEqual(1, state.EnterCount);
            Assert.AreEqual(1, state.TickCount);
            Assert.AreEqual(0, state.ExitCount);

            state.Exit();
            Assert.AreEqual(1, state.EnterCount);
            Assert.AreEqual(1, state.TickCount);
            Assert.AreEqual(1, state.ExitCount);
        }
    }
}
