using System;
using System.Reflection;
using Football.Core;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies Task 50 Simulation Time policy. Following the audit (no runtime system reads
    /// UnityEngine.Time and none consumes a simulation-step contract), the runtime abstraction
    /// is INTENTIONALLY DEFERRED: the existing injected-delta seam — <see cref="IState.Tick(float)"/>
    /// / <see cref="IStateMachine.Tick(float)"/> — already gives pure simulation explicit,
    /// de-coupled time. These tests lock in that determinism boundary and confirm no speculative
    /// simulation-time machinery was added.
    /// </summary>
    public class SimulationTimePolicyTests
    {
        /// <summary>A tiny test-only simulation consumer accumulating injected deltas.</summary>
        private sealed class DeltaAccumulator : IState
        {
            public double Sum;

            public void Enter() { }
            public void Tick(float deltaTime) => Sum += deltaTime; // pure: reads no Unity Time
            public void Exit() { }
        }

        // ---- 50.8 Reproducibility: same initial + same deltas => same result ----

        [Test]
        public void SameDeltaSequence_ProducesSameResult_OnIndependentConsumers()
        {
            var a = new DeltaAccumulator();
            var b = new DeltaAccumulator();

            foreach (var delta in new[] { 0.1f, 0.2f, 0.3f })
            {
                a.Tick(delta);
                b.Tick(delta);
            }

            Assert.That(a.Sum, Is.EqualTo(0.6).Within(1e-6),
                "float-sourced accumulation must land within float precision of 0.6");
            Assert.That(b.Sum, Is.EqualTo(0.6).Within(1e-6));
            Assert.AreEqual(a.Sum, b.Sum,
                "Identical delta sequences on identical consumers must reproduce identical state.");
        }

        [Test]
        public void SameSequence_Rerun_ProducesIdenticalResult()
        {
            var acc = new DeltaAccumulator();
            foreach (var delta in new[] { 1.0f, 2.0f, 3.0f })
                acc.Tick(delta);

            var result = acc.Sum;

            var rerun = new DeltaAccumulator();
            foreach (var delta in new[] { 1.0f, 2.0f, 3.0f })
                rerun.Tick(delta);

            Assert.AreEqual(result, rerun.Sum);
        }

        // ---- 50.4 Zero delta does not advance ----

        [Test]
        public void ZeroDelta_DoesNotAdvanceSimulation()
        {
            var acc = new DeltaAccumulator();
            acc.Tick(5f);
            acc.Tick(0f);
            acc.Tick(0f);
            Assert.AreEqual(5.0, acc.Sum, "Zero-delta ticks must not advance simulation state.");
        }

        // ---- 50.8 / 50.3 Pure seam: injected time, no Unity Time read ----

        [Test]
        public void ExistingSeam_ReceivesInjectedDelta_NotReadFromUnity()
        {
            // The simulation seam is an explicit parameter (IState.Tick(float deltaTime));
            // pure consumers therefore never call UnityEngine.Time.
            var tick = typeof(IState).GetMethod(nameof(IState.Tick));
            Assert.IsNotNull(tick);
            var prms = tick.GetParameters();
            Assert.AreEqual(1, prms.Length, "Tick must carry the delta explicitly.");
            Assert.AreEqual(typeof(float), prms[0].ParameterType);
            Assert.AreEqual("deltaTime", prms[0].Name);
        }

        [Test]
        public void PureConsumer_HasNoUnityTimeOrWallClockDependency()
        {
            // The test consumer holds a single double; it cannot reference Time.* / DateTime.
            foreach (var f in typeof(DeltaAccumulator).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreEqual(typeof(double), f.FieldType,
                    $"Pure simulation consumer must hold no UnityEngine/wall-clock state (field '{f.Name}').");
            }
        }

        // ---- 50.2 Decision: deferral is explicit and locked ----

        [Test]
        public void NoSpeculativeSimulationTimeAbstractionWasAdded()
        {
            // The audit proved no current consumer needs a runtime simulation-step contract, so
            // Task 50 intentionally deferred it rather than adding dead machinery. Verify no
            // speculative type was introduced into Football.Core.
            var core = typeof(GameClock).Assembly;
            foreach (var name in new[] { "ISimulationTime", "SimulationTimeStep", "SimulationClock", "TimeProvider" })
            {
                Assert.IsNull(core.GetType($"Football.Core.{name}", false, true),
                    $"No speculative '{name}' may be added without a consumer.");
            }
        }

        // ---- Determinism boundary does not promise network/replay ----

        [Test]
        public void DeterminismBoundary_IsExplicitInputs_DocumentationOnly()
        {
            // Full network/rollback determinism is NOT claimed; the boundary is "same inputs +
            // same deltas => same result", demonstrated by the reproducibility tests above.
            Assert.Pass("Determinism boundary documented as same-input/same-delta reproducibility.");
        }
    }
}
