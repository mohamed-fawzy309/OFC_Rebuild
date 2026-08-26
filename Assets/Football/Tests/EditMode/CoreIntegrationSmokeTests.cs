using System.Linq;
using Football.Core;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    public class CoreIntegrationSmokeTests
    {
        private enum TestPhase { Idle, Active, Done }

        private class ConcreteTestState : IState
        {
            public bool HasEntered { get; private set; }
            public bool HasExited { get; private set; }
            public int EnterCount { get; private set; }

            public void Enter()
            {
                HasEntered = true;
                EnterCount++;
            }

            public void Exit()
            {
                HasExited = true;
            }

            public void Tick(float deltaTime) { }
        }

        private class TickTrackingState : IState
        {
            public int TickCount { get; private set; }
            public float LastDeltaTime { get; private set; }

            public void Enter() { }
            public void Exit() { }
            public void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }
        }

        private class FakeFootballService : IFootballService
        {
            public bool IsInitialized { get; private set; }
            public void Initialize() { IsInitialized = true; }
            public void Shutdown() { IsInitialized = false; }
        }

        [Test]
        public void IStateMachineCanDriveStateBase()
        {
            var machine = new GenericStateMachine<TestPhase>();
            var state = new ConcreteTestState();
            machine.RegisterState(TestPhase.Idle, state);
            machine.ChangeState(TestPhase.Idle);

            Assert.AreEqual(TestPhase.Idle, machine.CurrentStateId);
            Assert.IsTrue(state.HasEntered);
        }

        [Test]
        public void StateTransition_FiresEnterAndExit()
        {
            var machine = new GenericStateMachine<TestPhase>();
            var idle = new ConcreteTestState();
            var active = new ConcreteTestState();
            machine.RegisterState(TestPhase.Idle, idle);
            machine.RegisterState(TestPhase.Active, active);

            machine.ChangeState(TestPhase.Idle);
            Assert.IsTrue(idle.HasEntered);

            machine.ChangeState(TestPhase.Active);
            Assert.IsTrue(idle.HasExited);
            Assert.IsTrue(active.HasEntered);
            Assert.AreEqual(TestPhase.Active, machine.CurrentStateId);
        }

        [Test]
        public void StateTick_ReceivesDeltaTime()
        {
            var machine = new GenericStateMachine<TestPhase>();
            var state = new TickTrackingState();
            machine.RegisterState(TestPhase.Active, state);
            machine.ChangeState(TestPhase.Active);

            machine.Tick(0.016f);
            machine.Tick(0.033f);

            Assert.AreEqual(2, state.TickCount);
            Assert.AreEqual(0.033f, state.LastDeltaTime, 0.001f);
        }

        [Test]
        public void GenericStateMachine_SetSameStateTwice_IsIdempotent()
        {
            var machine = new GenericStateMachine<TestPhase>();
            var state = new ConcreteTestState();
            machine.RegisterState(TestPhase.Idle, state);
            machine.ChangeState(TestPhase.Idle);
            machine.ChangeState(TestPhase.Idle);

            Assert.AreEqual(1, state.EnterCount);
        }

        [Test]
        public void GameEvents_SubscribeAndRaise()
        {
            bool received = false;
            GameEvents.Subscribe<MatchStartedEvent>(e => received = true);

            var evt = new MatchStartedEvent(10.0f, 1, 2);
            GameEvents.Raise(evt);

            Assert.IsTrue(received);
            GameEvents.Clear();
        }

        [Test]
        public void GameEvents_UnsubscribeStopsDelivery()
        {
            int count = 0;
            System.Action<MatchStartedEvent> handler = e => count++;
            GameEvents.Subscribe(handler);

            GameEvents.Raise(new MatchStartedEvent(1.0f, 1, 2));
            Assert.AreEqual(1, count);

            GameEvents.Unsubscribe(handler);
            GameEvents.Raise(new MatchStartedEvent(2.0f, 1, 2));
            Assert.AreEqual(1, count);
        }

        [Test]
        public void GameEvents_ClearRemovesAll()
        {
            int count = 0;
            GameEvents.Subscribe<MatchStartedEvent>(e => count++);
            GameEvents.Subscribe<GoalScoredEvent>(e => count++);
            GameEvents.Clear();

            GameEvents.Raise(new MatchStartedEvent(1.0f, 1, 2));
            GameEvents.Raise(new GoalScoredEvent(3.0f, 1, 10));
            Assert.AreEqual(0, count);
        }

        [Test]
        public void AllEventStructs_HaveTimestamp()
        {
            var events = new IGameEvent[]
            {
                new MatchStartedEvent(1.0f, 1, 2),
                new MatchEndedEvent(2.0f, 3, 1),
                new GoalScoredEvent(3.0f, 1, 10),
                new PossessionChangedEvent(4.0f, 1, 2, 11),
                new BallKickedEvent(5.0f, 10, 0f, 1f, 0f, 15f),
                new PlayerActionStartedEvent(6.0f, 10, "kick"),
                new PlayerActionFinishedEvent(7.0f, 10, "kick", true),
            };

            foreach (var evt in events)
            {
                Assert.GreaterOrEqual(evt.Timestamp, 0f, $"{evt.GetType().Name} should have valid Timestamp");
            }
        }

        [Test]
        public void IFootballService_CanBeImplemented()
        {
            var service = new FakeFootballService();
            Assert.IsFalse(service.IsInitialized);

            service.Initialize();
            Assert.IsTrue(service.IsInitialized);

            service.Shutdown();
            Assert.IsFalse(service.IsInitialized);
        }

        [Test]
        public void IState_EnterExitLifecycle_Works()
        {
            var state = new ConcreteTestState();
            Assert.IsFalse(state.HasEntered);
            Assert.IsFalse(state.HasExited);

            state.Enter();
            Assert.IsTrue(state.HasEntered);
            Assert.IsFalse(state.HasExited);

            state.Exit();
            Assert.IsTrue(state.HasExited);
        }
    }
}
