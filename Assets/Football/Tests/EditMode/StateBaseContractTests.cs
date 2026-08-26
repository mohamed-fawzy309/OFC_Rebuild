using NUnit.Framework;
using Football.Core;

namespace Football.Tests.EditMode
{
    public class StateBaseContractTests
    {
        private class DefaultState : StateBase { }

        private class OverrideEnterState : StateBase
        {
            public int EnterCount;
            public override void Enter() => EnterCount++;
        }

        private class OverrideTickState : StateBase
        {
            public int TickCount;
            public float LastDeltaTime;
            public override void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }
        }

        private class OverrideExitState : StateBase
        {
            public int ExitCount;
            public override void Exit() => ExitCount++;
        }

        private class FullOverrideState : StateBase
        {
            public int EnterCount;
            public int TickCount;
            public int ExitCount;
            public float LastDeltaTime;

            public override void Enter() => EnterCount++;
            public override void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }
            public override void Exit() => ExitCount++;
        }

        [Test]
        public void StateBase_Implements_IState()
        {
            IState state = new DefaultState();
            Assert.IsNotNull(state);
        }

        [Test]
        public void DefaultEnter_DoesNotThrow()
        {
            var state = new DefaultState();
            Assert.DoesNotThrow(() => state.Enter());
        }

        [Test]
        public void DefaultTick_DoesNotThrow()
        {
            var state = new DefaultState();
            Assert.DoesNotThrow(() => state.Tick(0.016f));
        }

        [Test]
        public void DefaultExit_DoesNotThrow()
        {
            var state = new DefaultState();
            Assert.DoesNotThrow(() => state.Exit());
        }

        [Test]
        public void DerivedState_CanOverrideEnter()
        {
            var state = new OverrideEnterState();
            state.Enter();
            Assert.AreEqual(1, state.EnterCount);
        }

        [Test]
        public void DerivedState_CanOverrideTick()
        {
            var state = new OverrideTickState();
            state.Tick(0.016f);
            Assert.AreEqual(1, state.TickCount);
        }

        [Test]
        public void DerivedState_CanOverrideExit()
        {
            var state = new OverrideExitState();
            state.Exit();
            Assert.AreEqual(1, state.ExitCount);
        }

        [Test]
        public void DerivedState_CanOverrideFullLifecycle()
        {
            var state = new FullOverrideState();

            state.Enter();
            Assert.AreEqual(1, state.EnterCount);
            Assert.AreEqual(0, state.TickCount);
            Assert.AreEqual(0, state.ExitCount);

            state.Tick(0.032f);
            Assert.AreEqual(1, state.EnterCount);
            Assert.AreEqual(1, state.TickCount);
            Assert.AreEqual(0, state.ExitCount);
            Assert.AreEqual(0.032f, state.LastDeltaTime, 0.0001f);

            state.Exit();
            Assert.AreEqual(1, state.EnterCount);
            Assert.AreEqual(1, state.TickCount);
            Assert.AreEqual(1, state.ExitCount);
        }

        [Test]
        public void OverrideTick_ReceivesExactDeltaTime()
        {
            var state = new OverrideTickState();
            state.Tick(0.05f);
            Assert.AreEqual(0.05f, state.LastDeltaTime, 0.0001f);
        }
    }
}
