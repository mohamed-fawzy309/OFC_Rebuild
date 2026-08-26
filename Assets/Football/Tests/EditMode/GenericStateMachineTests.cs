using System;
using System.Collections.Generic;
using NUnit.Framework;
using Football.Core;

namespace Football.Tests.EditMode
{
    public class GenericStateMachineTests
    {
        private enum TestStateId { Idle, Run, Sprint }

        private class TrackingState : StateBase
        {
            public int EnterCount;
            public int TickCount;
            public int ExitCount;
            public float LastDeltaTime;
            public List<string> Log;

            public TrackingState(List<string> log)
            {
                Log = log;
            }

            public override void Enter()
            {
                EnterCount++;
                Log?.Add($"Enter:{ GetHashCode() }");
            }

            public override void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }

            public override void Exit()
            {
                ExitCount++;
                Log?.Add($"Exit:{ GetHashCode() }");
            }
        }

        private class EmptyState : StateBase { }

        [Test]
        public void CanCreateGenericStateMachine()
        {
            var machine = new GenericStateMachine<TestStateId>();
            Assert.IsNotNull(machine);
        }

        [Test]
        public void CurrentStateInitiallyNull()
        {
            var machine = new GenericStateMachine<TestStateId>();
            Assert.IsNull(machine.CurrentState);
        }

        [Test]
        public void CurrentStateIdInitiallyDefault()
        {
            var machine = new GenericStateMachine<TestStateId>();
            Assert.AreEqual(default(TestStateId), machine.CurrentStateId);
        }

        [Test]
        public void RegisterState_StoresState()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var state = new EmptyState();
            machine.RegisterState(TestStateId.Idle, state);
            machine.ChangeState(TestStateId.Idle);
            Assert.AreSame(state, machine.CurrentState);
        }

        [Test]
        public void RegisterState_DoesNotActivate()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var state = new EmptyState();
            machine.RegisterState(TestStateId.Idle, state);
            Assert.IsNull(machine.CurrentState);
        }

        [Test]
        public void RegisterMultipleStates()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var idle = new EmptyState();
            var run = new EmptyState();
            var sprint = new EmptyState();

            machine.RegisterState(TestStateId.Idle, idle);
            machine.RegisterState(TestStateId.Run, run);
            machine.RegisterState(TestStateId.Sprint, sprint);

            machine.ChangeState(TestStateId.Run);
            Assert.AreSame(run, machine.CurrentState);
        }

        [Test]
        public void RegisterState_NullState_Throws()
        {
            var machine = new GenericStateMachine<TestStateId>();
            Assert.Throws<ArgumentNullException>(() =>
                machine.RegisterState(TestStateId.Idle, null));
        }

        [Test]
        public void RegisterState_DuplicateId_Throws()
        {
            var machine = new GenericStateMachine<TestStateId>();
            machine.RegisterState(TestStateId.Idle, new EmptyState());
            Assert.Throws<InvalidOperationException>(() =>
                machine.RegisterState(TestStateId.Idle, new EmptyState()));
        }

        [Test]
        public void ChangeState_SelectsRegisteredState()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var run = new EmptyState();
            machine.RegisterState(TestStateId.Run, run);

            machine.ChangeState(TestStateId.Run);

            Assert.AreSame(run, machine.CurrentState);
            Assert.AreEqual(TestStateId.Run, machine.CurrentStateId);
        }

        [Test]
        public void CurrentStateAndIdStaySynchronized()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var idle = new EmptyState();
            var run = new EmptyState();
            machine.RegisterState(TestStateId.Idle, idle);
            machine.RegisterState(TestStateId.Run, run);

            machine.ChangeState(TestStateId.Idle);
            Assert.AreSame(idle, machine.CurrentState);
            Assert.AreEqual(TestStateId.Idle, machine.CurrentStateId);

            machine.ChangeState(TestStateId.Run);
            Assert.AreSame(run, machine.CurrentState);
            Assert.AreEqual(TestStateId.Run, machine.CurrentStateId);
        }

        [Test]
        public void ChangeState_UnregisteredId_Throws()
        {
            var machine = new GenericStateMachine<TestStateId>();
            Assert.Throws<InvalidOperationException>(() =>
                machine.ChangeState(TestStateId.Run));
        }

        [Test]
        public void SameStateChange_IsIdempotent()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var run = new TrackingState(log);
            machine.RegisterState(TestStateId.Run, run);

            machine.ChangeState(TestStateId.Run);
            Assert.AreEqual(1, run.EnterCount);
            Assert.AreEqual(0, run.ExitCount);

            machine.ChangeState(TestStateId.Run);
            Assert.AreEqual(1, run.EnterCount);
            Assert.AreEqual(0, run.ExitCount);
        }

        [Test]
        public void ExitCalledBeforeEnter()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var idle = new TrackingState(log);
            var run = new TrackingState(log);
            machine.RegisterState(TestStateId.Idle, idle);
            machine.RegisterState(TestStateId.Run, run);

            machine.ChangeState(TestStateId.Idle);
            machine.ChangeState(TestStateId.Run);

            Assert.AreEqual(3, log.Count);
            Assert.IsTrue(log[0].StartsWith("Enter:"));
            Assert.IsTrue(log[1].StartsWith("Exit:"));
            Assert.IsTrue(log[2].StartsWith("Enter:"));
        }

        [Test]
        public void LifecycleSequence_IsCorrect()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var idle = new TrackingState(log);
            var run = new TrackingState(log);
            var sprint = new TrackingState(log);

            machine.RegisterState(TestStateId.Idle, idle);
            machine.RegisterState(TestStateId.Run, run);
            machine.RegisterState(TestStateId.Sprint, sprint);

            machine.ChangeState(TestStateId.Idle);
            machine.ChangeState(TestStateId.Run);
            machine.ChangeState(TestStateId.Sprint);

            Assert.AreEqual(5, log.Count);
            Assert.IsTrue(log[0].StartsWith("Enter:"));
            Assert.IsTrue(log[1].StartsWith("Exit:"));
            Assert.IsTrue(log[2].StartsWith("Enter:"));
            Assert.IsTrue(log[3].StartsWith("Exit:"));
            Assert.IsTrue(log[4].StartsWith("Enter:"));
        }

        [Test]
        public void Tick_CallsCurrentState()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var run = new TrackingState(log);
            machine.RegisterState(TestStateId.Run, run);

            machine.ChangeState(TestStateId.Run);
            machine.Tick(0.016f);

            Assert.AreEqual(1, run.TickCount);
        }

        [Test]
        public void Tick_DoesNotCallInactiveState()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var idle = new TrackingState(log);
            var run = new TrackingState(log);
            machine.RegisterState(TestStateId.Idle, idle);
            machine.RegisterState(TestStateId.Run, run);

            machine.ChangeState(TestStateId.Idle);
            machine.ChangeState(TestStateId.Run);
            machine.Tick(0.016f);

            Assert.AreEqual(0, idle.TickCount);
            Assert.AreEqual(1, run.TickCount);
        }

        [Test]
        public void Tick_PassesExactDeltaTime()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var run = new TrackingState(log);
            machine.RegisterState(TestStateId.Run, run);

            machine.ChangeState(TestStateId.Run);
            machine.Tick(0.05f);

            Assert.AreEqual(0.05f, run.LastDeltaTime, 0.0001f);
        }

        [Test]
        public void TickBeforeInitialState_DoesNothing()
        {
            var machine = new GenericStateMachine<TestStateId>();
            Assert.DoesNotThrow(() => machine.Tick(0.016f));
        }

        [Test]
        public void ZeroDeltaTime_IsHandled()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var run = new TrackingState(log);
            machine.RegisterState(TestStateId.Run, run);
            machine.ChangeState(TestStateId.Run);

            machine.Tick(0f);

            Assert.AreEqual(1, run.TickCount);
            Assert.AreEqual(0f, run.LastDeltaTime, 0.0001f);
        }

        [Test]
        public void LargeDeltaTime_IsHandled()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var run = new TrackingState(log);
            machine.RegisterState(TestStateId.Run, run);
            machine.ChangeState(TestStateId.Run);

            machine.Tick(10f);

            Assert.AreEqual(1, run.TickCount);
            Assert.AreEqual(10f, run.LastDeltaTime, 0.0001f);
        }

        [Test]
        public void MultipleTransitions_RecordCorrectCounts()
        {
            var machine = new GenericStateMachine<TestStateId>();
            var log = new List<string>();
            var idle = new TrackingState(log);
            var run = new TrackingState(log);
            var sprint = new TrackingState(log);

            machine.RegisterState(TestStateId.Idle, idle);
            machine.RegisterState(TestStateId.Run, run);
            machine.RegisterState(TestStateId.Sprint, sprint);

            machine.ChangeState(TestStateId.Idle);
            machine.Tick(0.016f);
            machine.Tick(0.016f);
            machine.ChangeState(TestStateId.Run);
            machine.Tick(0.016f);
            machine.ChangeState(TestStateId.Sprint);

            Assert.AreEqual(1, idle.EnterCount);
            Assert.AreEqual(1, idle.ExitCount);
            Assert.AreEqual(2, idle.TickCount);

            Assert.AreEqual(1, run.EnterCount);
            Assert.AreEqual(1, run.ExitCount);
            Assert.AreEqual(1, run.TickCount);

            Assert.AreEqual(1, sprint.EnterCount);
            Assert.AreEqual(0, sprint.ExitCount);
            Assert.AreEqual(0, sprint.TickCount);
        }
    }
}
