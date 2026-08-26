using NUnit.Framework;
using Football.Core;

namespace Football.Tests.EditMode
{
    public class IStateMachineContractTests
    {
        private class FakeState : StateBase { }

        private class FakeStateMachine : IStateMachine
        {
            public IState CurrentState { get; private set; }
            public int ChangeStateCount;
            public int TickCount;
            public float LastDeltaTime;

            public void ChangeState(IState newState)
            {
                CurrentState = newState;
                ChangeStateCount++;
            }

            public void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }
        }

        [Test]
        public void IStateMachine_CanBeImplemented()
        {
            IStateMachine machine = new FakeStateMachine();
            Assert.IsNotNull(machine);
        }

        [Test]
        public void CurrentState_IsReadable()
        {
            var machine = new FakeStateMachine();
            Assert.IsNull(machine.CurrentState);

            var state = new FakeState();
            machine.ChangeState(state);
            Assert.AreSame(state, machine.CurrentState);
        }

        [Test]
        public void ChangeState_CanBeCalled()
        {
            var machine = new FakeStateMachine();
            var state = new FakeState();
            machine.ChangeState(state);
            Assert.AreEqual(1, machine.ChangeStateCount);
            Assert.AreSame(state, machine.CurrentState);
        }

        [Test]
        public void Tick_CanBeCalled()
        {
            var machine = new FakeStateMachine();
            machine.Tick(0.016f);
            Assert.AreEqual(1, machine.TickCount);
        }

        [Test]
        public void Tick_ReceivesExactDeltaTime()
        {
            var machine = new FakeStateMachine();
            machine.Tick(0.05f);
            Assert.AreEqual(0.05f, machine.LastDeltaTime, 0.0001f);
        }
    }
}
