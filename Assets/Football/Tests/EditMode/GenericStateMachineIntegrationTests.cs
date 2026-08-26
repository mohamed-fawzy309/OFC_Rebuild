using System.Collections.Generic;
using NUnit.Framework;
using Football.Core;

namespace Football.Tests.EditMode
{
    public class GenericStateMachineIntegrationTests
    {
        private enum PlayerTestState { Idle, Run, Sprint }
        private enum GameTestState { Loading, Playing, Paused }

        private class LifecycleRecorder : StateBase
        {
            public List<string> Events;
            public string Label;
            public int EnterCount;
            public int TickCount;
            public int ExitCount;

            public LifecycleRecorder(string label, List<string> events)
            {
                Label = label;
                Events = events;
            }

            public override void Enter()
            {
                EnterCount++;
                Events.Add($"{Label}:Enter");
            }
            public override void Tick(float deltaTime)
            {
                TickCount++;
                Events.Add($"{Label}:Tick:{deltaTime}");
            }
            public override void Exit()
            {
                ExitCount++;
                Events.Add($"{Label}:Exit");
            }
        }

        [Test]
        public void FullWorkflow_RegisterChangeTickChange()
        {
            var machine = new GenericStateMachine<PlayerTestState>();
            var events = new List<string>();

            var idle = new LifecycleRecorder("Idle", events);
            var run = new LifecycleRecorder("Run", events);
            var sprint = new LifecycleRecorder("Sprint", events);

            machine.RegisterState(PlayerTestState.Idle, idle);
            machine.RegisterState(PlayerTestState.Run, run);
            machine.RegisterState(PlayerTestState.Sprint, sprint);

            machine.ChangeState(PlayerTestState.Idle);
            machine.Tick(0.016f);
            machine.Tick(0.032f);
            machine.ChangeState(PlayerTestState.Run);
            machine.Tick(0.016f);
            machine.ChangeState(PlayerTestState.Sprint);

            Assert.AreEqual(
                new List<string>
                {
                    "Idle:Enter",
                    "Idle:Tick:0.016",
                    "Idle:Tick:0.032",
                    "Idle:Exit",
                    "Run:Enter",
                    "Run:Tick:0.016",
                    "Run:Exit",
                    "Sprint:Enter"
                },
                events);
        }

        [Test]
        public void StateBaseDerivedStates_WorkWithGenericMachine()
        {
            var machine = new GenericStateMachine<PlayerTestState>();
            var events = new List<string>();

            var idle = new LifecycleRecorder("Idle", events);
            var run = new LifecycleRecorder("Run", events);

            machine.RegisterState(PlayerTestState.Idle, idle);
            machine.RegisterState(PlayerTestState.Run, run);

            machine.ChangeState(PlayerTestState.Idle);
            Assert.AreEqual(1, idle.EnterCount);

            machine.ChangeState(PlayerTestState.Run);
            Assert.AreEqual(1, idle.ExitCount);
            Assert.AreEqual(1, run.EnterCount);

            machine.Tick(0.016f);
            Assert.AreEqual(0, idle.TickCount);
            Assert.AreEqual(1, run.TickCount);
        }

        [Test]
        public void TwoIndependentMachines_RemainIsolated()
        {
            var playerMachine = new GenericStateMachine<PlayerTestState>();
            var gameMachine = new GenericStateMachine<GameTestState>();
            var events = new List<string>();

            var playerIdle = new LifecycleRecorder("PlayerIdle", events);
            var playerRun = new LifecycleRecorder("PlayerRun", events);
            var gameLoading = new LifecycleRecorder("GameLoading", events);
            var gamePlaying = new LifecycleRecorder("GamePlaying", events);

            playerMachine.RegisterState(PlayerTestState.Idle, playerIdle);
            playerMachine.RegisterState(PlayerTestState.Run, playerRun);
            gameMachine.RegisterState(GameTestState.Loading, gameLoading);
            gameMachine.RegisterState(GameTestState.Playing, gamePlaying);

            playerMachine.ChangeState(PlayerTestState.Idle);
            gameMachine.ChangeState(GameTestState.Loading);

            playerMachine.Tick(0.016f);
            gameMachine.Tick(0.032f);

            playerMachine.ChangeState(PlayerTestState.Run);
            gameMachine.ChangeState(GameTestState.Playing);

            Assert.AreEqual(PlayerTestState.Run, playerMachine.CurrentStateId);
            Assert.AreSame(playerRun, playerMachine.CurrentState);

            Assert.AreEqual(GameTestState.Playing, gameMachine.CurrentStateId);
            Assert.AreSame(gamePlaying, gameMachine.CurrentState);

            Assert.AreEqual(1, playerIdle.EnterCount);
            Assert.AreEqual(1, playerIdle.ExitCount);
            Assert.AreEqual(1, playerRun.EnterCount);
            Assert.AreEqual(0, playerRun.ExitCount);

            Assert.AreEqual(1, gameLoading.EnterCount);
            Assert.AreEqual(1, gameLoading.ExitCount);
            Assert.AreEqual(1, gamePlaying.EnterCount);
            Assert.AreEqual(0, gamePlaying.ExitCount);
        }

        [Test]
        public void SharedStateObject_AcrossMachines()
        {
            var machineA = new GenericStateMachine<PlayerTestState>();
            var machineB = new GenericStateMachine<GameTestState>();
            var events = new List<string>();

            var shared = new LifecycleRecorder("Shared", events);

            machineA.RegisterState(PlayerTestState.Idle, shared);
            machineB.RegisterState(GameTestState.Loading, shared);

            machineA.ChangeState(PlayerTestState.Idle);
            machineB.ChangeState(GameTestState.Loading);

            Assert.AreSame(shared, machineA.CurrentState);
            Assert.AreSame(shared, machineB.CurrentState);
            Assert.AreEqual(2, shared.EnterCount);
            Assert.AreEqual(0, shared.ExitCount);
        }

        [Test]
        public void InterfaceVariable_UsesGenericMachine()
        {
            IStateMachine<PlayerTestState> machine = new GenericStateMachine<PlayerTestState>();
            var events = new List<string>();

            var idle = new LifecycleRecorder("Idle", events);
            var run = new LifecycleRecorder("Run", events);

            machine.RegisterState(PlayerTestState.Idle, idle);
            machine.RegisterState(PlayerTestState.Run, run);

            machine.ChangeState(PlayerTestState.Idle);
            machine.Tick(0.016f);
            machine.ChangeState(PlayerTestState.Run);

            Assert.AreEqual(PlayerTestState.Run, machine.CurrentStateId);
            Assert.AreSame(run, machine.CurrentState);
        }

        [Test]
        public void MultipleTicks_AccumulateCorrectly()
        {
            var machine = new GenericStateMachine<PlayerTestState>();
            var events = new List<string>();

            var run = new LifecycleRecorder("Run", events);
            machine.RegisterState(PlayerTestState.Run, run);

            machine.ChangeState(PlayerTestState.Run);

            for (int i = 0; i < 100; i++)
                machine.Tick(0.016f);

            Assert.AreEqual(100, run.TickCount);
            Assert.AreEqual(1, run.EnterCount);
            Assert.AreEqual(0, run.ExitCount);
        }

        [Test]
        public void TransitionSequence_VerifiesLifecycleOrder()
        {
            var machine = new GenericStateMachine<PlayerTestState>();
            var events = new List<string>();

            var idle = new LifecycleRecorder("A", events);
            var run = new LifecycleRecorder("B", events);
            var sprint = new LifecycleRecorder("C", events);

            machine.RegisterState(PlayerTestState.Idle, idle);
            machine.RegisterState(PlayerTestState.Run, run);
            machine.RegisterState(PlayerTestState.Sprint, sprint);

            machine.ChangeState(PlayerTestState.Idle);
            machine.ChangeState(PlayerTestState.Run);
            machine.ChangeState(PlayerTestState.Sprint);
            machine.ChangeState(PlayerTestState.Idle);

            Assert.AreEqual(
                new List<string>
                {
                    "A:Enter",
                    "A:Exit",
                    "B:Enter",
                    "B:Exit",
                    "C:Enter",
                    "C:Exit",
                    "A:Enter"
                },
                events);
        }
    }
}
