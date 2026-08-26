using NUnit.Framework;
using Football.Core;

namespace Football.Tests.EditMode
{
    public class GenericIStateMachineContractTests
    {
        private enum AlphaStateId { Idle, Run, Sprint }
        private enum BetaStateId { Boot, Playing, Pause }

        private class FakeGenericStateMachine<TStateId> : IStateMachine<TStateId> where TStateId : System.Enum
        {
            public TStateId CurrentStateId { get; private set; }
            public IState CurrentState { get; private set; }
            public int ChangeStateCount;
            public int RegisterStateCount;
            public int TickCount;
            public float LastDeltaTime;

            public void ChangeState(TStateId stateId)
            {
                CurrentStateId = stateId;
                ChangeStateCount++;
            }

            public void RegisterState(TStateId stateId, IState state)
            {
                RegisterStateCount++;
            }

            public void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }
        }

        [Test]
        public void GenericImplementation_CanBeCreated()
        {
            IStateMachine<AlphaStateId> machine = new FakeGenericStateMachine<AlphaStateId>();
            Assert.IsNotNull(machine);
        }

        [Test]
        public void CurrentStateId_IsReadable()
        {
            var machine = new FakeGenericStateMachine<AlphaStateId>();
            machine.ChangeState(AlphaStateId.Run);
            Assert.AreEqual(AlphaStateId.Run, machine.CurrentStateId);
        }

        [Test]
        public void ChangeState_CanBeCalled()
        {
            var machine = new FakeGenericStateMachine<AlphaStateId>();
            machine.ChangeState(AlphaStateId.Sprint);
            Assert.AreEqual(1, machine.ChangeStateCount);
            Assert.AreEqual(AlphaStateId.Sprint, machine.CurrentStateId);
        }

        [Test]
        public void Tick_CanBeCalled()
        {
            var machine = new FakeGenericStateMachine<AlphaStateId>();
            machine.Tick(0.016f);
            Assert.AreEqual(1, machine.TickCount);
        }

        [Test]
        public void Tick_ReceivesExactDeltaTime()
        {
            var machine = new FakeGenericStateMachine<AlphaStateId>();
            machine.Tick(0.05f);
            Assert.AreEqual(0.05f, machine.LastDeltaTime, 0.0001f);
        }

        [Test]
        public void RegisterState_CanBeCalled()
        {
            var machine = new FakeGenericStateMachine<AlphaStateId>();
            var state = new TestStateForGeneric();
            machine.RegisterState(AlphaStateId.Idle, state);
            Assert.AreEqual(1, machine.RegisterStateCount);
        }

        [Test]
        public void DifferentStateIdTypes_AreIndependent()
        {
            var alpha = new FakeGenericStateMachine<AlphaStateId>();
            var beta = new FakeGenericStateMachine<BetaStateId>();

            alpha.ChangeState(AlphaStateId.Run);
            beta.ChangeState(BetaStateId.Playing);

            Assert.AreEqual(AlphaStateId.Run, alpha.CurrentStateId);
            Assert.AreEqual(BetaStateId.Playing, beta.CurrentStateId);
        }

        private class TestStateForGeneric : StateBase { }
    }
}
