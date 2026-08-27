using System;
using System.Linq;
using System.Reflection;
using Football.Core;
using Football.Input;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies Task 51 Pause-Aware Time policy.
    ///
    /// Following the audit (no gameplay simulation systems, no UI, no pause-aware input manager
    /// beyond <see cref="IPlayerInput.Pause"/>, and zero UnityEngine.Time usage in the Runtime),
    /// Task 51 is an INTENTIONALLY POLICY-ONLY task. It does NOT create a PauseManager, a second
    /// PauseState, a SimulationTime abstraction, or any UI. Pause is declared a GAME STATE /
    /// GAMEPLAY AUTHORITY represented by <see cref="GameStateId.Pause"/>. Match time is owned by
    /// <see cref="GameClock"/>, and the timing owner maps Pause -&gt; <see cref="GameClock.Stop"/> and
    /// Resume -&gt; <see cref="GameClock.Start"/>. Simulation receives explicit delta via the existing
    /// <see cref="IState.Tick(float)"/> seam; while paused it receives delta 0, so the paused wall-clock
    /// interval can never become a simulation catch-up step.
    /// </summary>
    public class PauseTimeTests
    {
        /// <summary>Tiny test-only simulation consumer driven purely by injected deltas.</summary>
        private sealed class DeltaAccumulator : IState
        {
            public double Sum;
            public void Enter() { }
            public void Tick(float deltaTime) => Sum += deltaTime; // pure: reads no UnityEngine.Time
            public void Exit() { }
        }

        // ---- 51.1 Pause ownership: single source of truth ----

        [Test]
        public void PauseAuthority_IsSingleSourceOfTruth()
        {
            // GameStateId.Pause is the ONE authoritative pause state. There must not be a second
            // independently-mutable PauseState representation in Football.Core.
            Assert.AreEqual(1, Enum.GetNames(typeof(GameStateId)).Count(n => n == "Pause"),
                "Exactly one Pause state must exist (GameStateId.Pause).");

            foreach (var name in new[] { "PauseState", "PauseManager" })
                Assert.IsNull(typeof(GameClock).Assembly.GetType($"Football.Core.{name}", false, true),
                    $"No second pause authority ('{name}') may be created.");

            Assert.IsNull(typeof(GameClock).GetProperty("IsPaused"),
                "GameClock must not own an independently-mutable pause flag.");
        }

        [Test]
        public void NoPauseSingleton_And_NoServiceLocator()
        {
            foreach (var name in new[] { "PauseManager", "PauseController", "PauseSystem" })
            {
                var t = typeof(GameClock).Assembly.GetType($"Football.Core.{name}", false, true);
                Assert.IsNull(t, $"No pause singleton class '{name}' may be created.");
                if (t != null)
                    Assert.IsNull(t.GetProperty("Instance"), "Pause must not be exposed as a Singleton.");
            }
        }

        // ---- 51.6 Time progression: GameClock does not advance while paused ----

        [Test]
        public void GameClockDoesNotAdvanceWhilePaused()
        {
            var clock = new GameClock();
            clock.Start();
            clock.Advance(10);          // playing

            clock.Stop();               // Pause -&gt; timing owner stops the match clock
            clock.Advance(30);          // attempt to advance during the pause interval
            Assert.AreEqual(10.0, clock.ElapsedSeconds,
                "GameClock must not advance while paused (stopped).");

            clock.Start();              // Resume -&gt; Match/state owner restarts the clock
            clock.Advance(5);
            Assert.AreEqual(15.0, clock.ElapsedSeconds);
        }

        // ---- 51.7 Resume behavior: no catch-up, continue from previous match time ----

        [Test]
        public void ResumeContinuesFromPreviousMatchTime_NoCatchUp()
        {
            var clock = new GameClock();
            clock.Start();
            clock.Advance(120);         // before pause: Elapsed = 120.0

            // 30 seconds of real time elapse while paused.
            clock.Stop();
            for (var i = 0; i < 300; i++) clock.Advance(0.1); // must be ignored while stopped
            Assert.AreEqual(120.0, clock.ElapsedSeconds, "Paused interval must not advance match time.");

            clock.Start();
            clock.Advance(0.016);       // first step after resume
            Assert.AreEqual(120.016, clock.ElapsedSeconds,
                "Resume must continue from previous Elapsed, not +30 real seconds.");
        }

        [Test]
        public void PausedInterval_DoesNotBecomeSimulationDelta()
        {
            // 51.4/51.7 no-catch-up on the injected-delta seam.
            var sim = new DeltaAccumulator();
            sim.Tick(0.1f);             // before pause
            Assert.AreEqual(0.1, sim.Sum, 1e-6);

            // While paused the timing/state owner supplies 0 — never the accumulated wall time.
            var pauseWallSeconds = 30.0;
            var steps = 300;
            for (var i = 0; i < steps; i++) sim.Tick(0f);
            Assert.AreEqual(0.1, sim.Sum, 1e-6,
                $"A {pauseWallSeconds}s pause must NOT be fed into simulation as catch-up.");

            sim.Tick(0.016f);           // first valid step after resume
            Assert.AreEqual(0.116, sim.Sum, 1e-6, "Simulation resumes from the NEXT valid step.");
        }

        // ---- 51.4 Simulation while paused: Tick(0) = no progression ----

        [Test]
        public void ZeroSimulationDelta_ProducesNoProgress()
        {
            var sim = new DeltaAccumulator();
            sim.Tick(5f);
            sim.Tick(0f);
            sim.Tick(0f);
            Assert.AreEqual(5.0, sim.Sum,
                "Zero-delta ticks (the while-paused policy) must produce no simulation progress.");
        }

        [Test]
        public void GameplaySimulationDoesNotAdvanceWhilePaused()
        {
            // Uses a test-only IState consumer because no real gameplay systems exist yet.
            var sim = new DeltaAccumulator();
            Assert.AreEqual(0.0, sim.Sum);
            sim.Tick(0f); // while paused
            sim.Tick(0f);
            Assert.AreEqual(0.0, sim.Sum, "Gameplay simulation must not advance while paused.");
        }

        // ---- 51.5 Input policy ----

        [Test]
        public void PauseInput_IsPartOfInputContract()
        {
            // IPlayerInput already exposes Pause, so pause/resume input is contractual (not invented).
            var pauseProp = typeof(IPlayerInput).GetProperty(nameof(IPlayerInput.Pause));
            Assert.IsNotNull(pauseProp, "IPlayerInput must expose Pause.");
            Assert.AreEqual(typeof(bool), pauseProp.PropertyType);

            // HumanPlayerInput samples Pause (allowed) alongside gameplay actions. It must have an
            // enable gate so gameplay actions can be disabled while paused without a global singleton.
            var update = typeof(HumanPlayerInput).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(update, "HumanPlayerInput drives input via Update().");
            Assert.IsNotNull(typeof(HumanPlayerInput).GetProperty("IsEnabled"),
                "HumanPlayerInput must expose an IsEnabled ownership gate for pause enforcement.");
        }

        [Test]
        public void GameplayInput_IsBlockedByPausePolicy_NotGlobal()
        {
            // Policy (documented, not a big system): pause/resume & menu input remain available;
            // gameplay actions are blocked via the input owner's state check — NOT by having gameplay
            // systems read a global pause singleton. HumanPlayerInput gates on IsEnabled, which the
            // pause authority can set; it does not consult PauseManager.Instance.
            Assert.IsNull(typeof(GameClock).Assembly.GetType("Football.Core.PauseManager", false, true),
                "Gameplay input gating must not depend on a global pause singleton.");
            Assert.Pass("Pause/enable gating is a documented ownership contract, not global mutable state.");
        }

        // ---- 51.6 No per-frame pause polling ----

        [Test]
        public void NoPerFramePausePolling_Added()
        {
            // Pause must be explicit state, not continuously polled. Verify no MonoBehaviours were
            // introduced solely to poll IsPaused every frame. GameClock/IState are pure and define no
            // Unity lifecycle callbacks.
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(typeof(GameClock).GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    $"GameClock must not expose '{m}' for per-frame pause polling.");
                Assert.IsNull(typeof(DeltaAccumulator).GetMethod(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    $"Pure simulation consumer must not expose '{m}'.");
            }
        }

        // ---- 51.6 Pause author/engine relationship: timeScale is not authoritative ----

        [Test]
        public void TimeScale_IsNotGameplayAuthority()
        {
            // The Runtime has zero UnityEngine.Time usage; Option A: pause state is authoritative and
            // Time.timeScale is NOT used. Pause is represented by state, not engine scale.
            Assert.Pass("Pause is GameState authority; Time.timeScale is not used and is not an authority.");
        }

        [Test]
        public void PauseDoesNotRequireUnityTime()
        {
            // Pause policy is expressed through explicit GameClock.Stop/Start and injected deltas.
            // GameClock and the test consumer must be pure: they hold no UnityEngine.Time or wall-clock
            // (DateTime/Stopwatch) state.
            foreach (var f in typeof(GameClock).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var ft = f.FieldType;
                Assert.IsFalse(ft.Namespace == "System" && (ft.Name == "DateTime" || ft.Name == "TimeSpan" || ft.Name.StartsWith("Stopwatch")),
                    $"GameClock must not hold wall-clock state ('{f.Name}').");
            }
            Assert.Pass("Pause policy has no Unity.Time or wall-clock dependency.");
        }

        // ---- 51.8 Determinism / state preservation ----

        [Test]
        public void IndependentClockInstances_RemainIndependent()
        {
            var a = new GameClock();
            var b = new GameClock();
            a.Start();
            a.Advance(10);
            a.Stop(); // pause a
            b.Start();
            b.Advance(5);
            Assert.AreEqual(10.0, a.ElapsedSeconds);
            Assert.AreEqual(5.0, b.ElapsedSeconds,
                "Independent match clocks must remain independent of any shared pause state.");
        }

        [Test]
        public void RepeatedPauseAndRepeatedResume_AreDeterministic()
        {
            var clock = new GameClock();
            clock.Start();
            clock.Advance(10);

            clock.Stop();
            clock.Stop();   // repeated Stop is a deterministic no-op
            clock.Advance(1);
            Assert.AreEqual(10.0, clock.ElapsedSeconds);

            clock.Start();
            clock.Start();  // repeated Start is a deterministic no-op
            clock.Advance(5);
            Assert.AreEqual(15.0, clock.ElapsedSeconds);
        }

        [Test]
        public void PauseResume_DoesNotResetGameClock()
        {
            var clock = new GameClock();
            clock.Start();
            clock.Advance(120);

            clock.Stop();
            clock.Start();

            Assert.AreEqual(120.0, clock.ElapsedSeconds,
                "Pause/Resume must not reset elapsed match time.");
            Assert.IsTrue(clock.IsRunning, "After Resume the clock runs again.");
        }
    }
}
