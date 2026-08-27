using System;
using System.Reflection;
using Football.Core;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies Task 49 Game Clock: a deterministic, pure-C# source of FOOTBALL MATCH TIME that
    /// advances only when explicitly told to, with a single authoritative elapsed-seconds value,
    /// and no dependency on Unity Time, real time, Singleton, or game systems.
    /// </summary>
    public class GameClockTests
    {
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = new GameClock();
        }

        // ---- 49.3 / 49.4 initial state ----

        [Test]
        public void GameClockCanBeCreated()
        {
            var clock = new GameClock();
            Assert.IsNotNull(clock);
        }

        [Test]
        public void InitialStateIsStopped()
        {
            Assert.AreEqual(GameClockState.Stopped, _clock.State);
            Assert.IsFalse(_clock.IsRunning);
        }

        [Test]
        public void InitialTimeIsZero()
        {
            Assert.AreEqual(0.0, _clock.ElapsedSeconds);
        }

        [Test]
        public void InitialRegulationDuration_FromConfig()
        {
            var clock = new GameClock(5400);
            Assert.AreEqual(5400.0, clock.RegulationDurationSeconds);
        }

        [Test]
        public void NegativeRegulationDuration_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameClock(-1));
        }

        // ---- 49.5 Start / Stop / Reset ----

        [Test]
        public void StartBeginsRunning()
        {
            _clock.Start();
            Assert.AreEqual(GameClockState.Running, _clock.State);
            Assert.IsTrue(_clock.IsRunning);
        }

        [Test]
        public void StopStopsRunning()
        {
            _clock.Start();
            _clock.Stop();
            Assert.AreEqual(GameClockState.Stopped, _clock.State);
            Assert.IsFalse(_clock.IsRunning);
        }

        [Test]
        public void StartWhileRunning_IsDeterministic_NoOp()
        {
            _clock.Start();
            _clock.Start();
            Assert.AreEqual(GameClockState.Running, _clock.State);
        }

        [Test]
        public void StopWhileStopped_IsDeterministic_NoOp()
        {
            _clock.Stop();
            _clock.Stop();
            Assert.AreEqual(GameClockState.Stopped, _clock.State);
        }

        [Test]
        public void ResetReturnsTimeToZero()
        {
            _clock.Start();
            _clock.Advance(10);
            _clock.Reset();
            Assert.AreEqual(0.0, _clock.ElapsedSeconds);
        }

        [Test]
        public void ResetStopsClock_AccordingToPolicy()
        {
            _clock.Start();
            _clock.Advance(5);
            _clock.Reset();
            Assert.AreEqual(GameClockState.Stopped, _clock.State, "Reset returns the clock to Stopped.");
            Assert.IsFalse(_clock.IsRunning);
            Assert.AreEqual(0.0, _clock.ElapsedSeconds);
        }

        [Test]
        public void ResetProducesDeterministicState_AndClockIsRestartable()
        {
            _clock.Start();
            _clock.Advance(25);
            _clock.Reset();
            _clock.Start(); // must still be restartable after reset
            Assert.AreEqual(GameClockState.Running, _clock.State);
        }

        [Test]
        public void RepeatedStartStop_IsDeterministic()
        {
            _clock.Start();
            _clock.Stop();
            _clock.Start();
            _clock.Stop();
            Assert.AreEqual(GameClockState.Stopped, _clock.State);
            Assert.AreEqual(0.0, _clock.ElapsedSeconds);
        }

        // ---- 49.6 Match-time progression ----

        [Test]
        public void RunningClockAdvances()
        {
            _clock.Start();
            _clock.Advance(0.5);
            Assert.That(_clock.ElapsedSeconds, Is.EqualTo(0.5));
        }

        [Test]
        public void StoppedClockDoesNotAdvance()
        {
            _clock.Advance(10);
            Assert.AreEqual(0.0, _clock.ElapsedSeconds, "A stopped clock ignores advancement.");
        }

        [Test]
        public void ZeroDeltaDoesNotAdvance()
        {
            _clock.Start();
            _clock.Advance(0);
            Assert.AreEqual(0.0, _clock.ElapsedSeconds);
        }

        [Test]
        public void NegativeDelta_IsRejected()
        {
            _clock.Start();
            Assert.Throws<ArgumentOutOfRangeException>(() => _clock.Advance(-1));
        }

        [Test]
        public void NegativeDelta_DoesNotAlterTime()
        {
            _clock.Start();
            _clock.Advance(5);
            Assert.Throws<ArgumentOutOfRangeException>(() => _clock.Advance(-2));
            Assert.AreEqual(5.0, _clock.ElapsedSeconds, "A rejected delta must not mutate time.");
        }

        [Test]
        public void FractionalDelta_IsPreserved()
        {
            _clock.Start();
            _clock.Advance(0.1);
            _clock.Advance(0.2);
            Assert.That(_clock.ElapsedSeconds, Is.EqualTo(0.3).Within(1e-9));
        }

        [Test]
        public void LargeDelta_BehavesDeterministically()
        {
            _clock.Start();
            _clock.Advance(1_000_000);
            Assert.AreEqual(1_000_000.0, _clock.ElapsedSeconds);
        }

        [Test]
        public void MultipleAdvances_Accumulate()
        {
            _clock.Start();
            _clock.Advance(1.5);
            _clock.Advance(2.25);
            Assert.That(_clock.ElapsedSeconds, Is.EqualTo(3.75).Within(1e-9));
        }

        [Test]
        public void StopFreezesProgression()
        {
            _clock.Start();
            _clock.Advance(1.5);
            _clock.Advance(2.25);
            _clock.Stop();
            _clock.Advance(10);
            Assert.That(_clock.ElapsedSeconds, Is.EqualTo(3.75).Within(1e-9),
                "Stopped clock must not advance, even with a large delta.");
        }

        [Test]
        public void ClockDoesNotAutoStop_AtRegulationBoundary()
        {
            // The clock carries regulation duration as config but does not enforce/decide
            // match-end. It simply keeps counting elapsed time.
            var clock = new GameClock(2700);
            clock.Start();
            clock.Advance(3000);
            Assert.AreEqual(3000.0, clock.ElapsedSeconds);
            Assert.AreEqual(GameClockState.Running, clock.State);
        }

        // ---- Single source of truth / immutability ----

        [Test]
        public void TimeCannotBeExternallyMutated()
        {
            var prop = typeof(GameClock).GetProperty(nameof(GameClock.ElapsedSeconds));
            Assert.IsNotNull(prop);
            Assert.IsTrue(prop.CanRead);
            Assert.IsTrue(prop.GetSetMethod(true) != null
                          && !prop.GetSetMethod(true).IsPublic,
                "ElapsedSeconds setter must be non-public; time is owned by the clock.");
        }

        [Test]
        public void ElapsedTimeIsSingleSourceOfTruth()
        {
            // No separately stored minutes/seconds/display state — only ElapsedSeconds exists.
            Assert.IsNull(typeof(GameClock).GetProperty("Minutes"));
            Assert.IsNull(typeof(GameClock).GetProperty("Seconds"));
            Assert.IsNull(typeof(GameClock).GetProperty("DisplayText"));
        }

        // ---- Lifecycle independence (no MonoBehaviour frame methods) ----

        [Test]
        public void NoUnityLifecycleDependency()
        {
            Assert.IsFalse(typeof(GameClock).IsSubclassOf(typeof(UnityEngine.Object)));
            foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(typeof(GameClock).GetMethod(name,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                    $"GameClock advances explicitly and must not have a {name}().");
            }
        }

        // ---- No Singleton / no Service Locator ----

        [Test]
        public void NoSingletonRequirement()
        {
            Assert.IsNull(typeof(GameClock).GetProperty("Instance"));
        }

        // ---- No Unity Time authority ----

        [Test]
        public void NoUnityTimeAuthority()
        {
            // Football.Core references no UnityCode assemblies, so GameClock structurally cannot
            // call UnityEngine.Time. Confirm the clock holds no UnityEngine fields at all.
            foreach (var f in typeof(GameClock).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var ns = f.FieldType.Namespace;
                Assert.IsFalse(
                    ns == "UnityEngine" || (ns != null && ns.StartsWith("UnityEngine.")),
                    $"GameClock must hold no UnityEngine field, so it cannot use Time.* as authority (field '{f.Name}').");
            }
        }

        [Test]
        public void NoTimeScaleAuthority()
        {
            // GameClock state is driven by explicit Start/Stop, never Time.timeScale.
            Assert.IsNull(typeof(GameClock).GetProperty("TimeScale"));
            Assert.IsNull(typeof(GameClock).GetProperty("Paused"));
        }

        [Test]
        public void NoGameStateMachineDependency()
        {
            var fields = typeof(GameClock).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                Assert.IsFalse(
                    typeof(GameStateId).IsAssignableFrom(f.FieldType)
                    || f.FieldType.FullName?.Contains("GameState") == true,
                    $"GameClock must not reference a game state machine (field '{f.Name}').");
            }
        }

        [Test]
        public void NoSceneManagerDependency()
        {
            // No scene types and no MeshRenderer/Transform references on the clock.
            Assert.IsFalse(typeof(GameClock).BaseType == typeof(UnityEngine.Object));
            foreach (var f in typeof(GameClock).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(f.FieldType.FullName?.StartsWith("UnityEngine.SceneManagement") == true
                               || f.FieldType == typeof(UnityEngine.Transform)
                               || f.FieldType == typeof(UnityEngine.GameObject),
                    $"GameClock must hold no scene/object dependency (field '{f.Name}').");
            }
        }

        [Test]
        public void NoInfrastructureDependency()
        {
            // GameClock must not reference bootstrap/registry/loader/transition types as fields.
            foreach (var f in typeof(GameClock).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var name = f.FieldType.Name;
                Assert.IsFalse(
                    name == nameof(GameBootstrap) ||
                    name == nameof(ServiceRegistry) ||
                    name == nameof(SceneLoader) ||
                    name == nameof(SceneTransitionSystem),
                    $"GameClock must not hold an infrastructure field ('{name}').");
            }
        }

        // ---- 49.7 Half-time / stoppage compatibility ----

        [Test]
        public void HalfTimeDoesNotBecomeClockResponsibility()
        {
            // A single GameClock can start, advance to a boundary, stop, and resume — no second
            // "half" clock is required. This is an architecture-compatibility test, not a match test.
            _clock.Start();
            _clock.Advance(2700); // first half elapses
            _clock.Stop();        // half-time boundary: clock stops
            _clock.Start();       // second half resumes from the same ElapsedSeconds
            _clock.Advance(2700);
            Assert.That(_clock.ElapsedSeconds, Is.EqualTo(5400).Within(1e-9));
        }

        [Test]
        public void StoppageTimeDoesNotRequireSecondClock()
        {
            // Future stoppage-time policy can be layered over the single authoritative
            // ElapsedSeconds without a separate stoppage/display clock.
            Assert.IsNull(typeof(GameClock).GetProperty("StoppageElapsedSeconds"));
            Assert.IsNull(typeof(GameClock).GetProperty("DisplayClock"));
            Assert.IsNull(typeof(GameClock).GetProperty("SecondClock"));
        }

        [Test]
        public void RegulationDurationIsConfigurationNotClockState()
        {
            var clock = new GameClock(5400);
            var timeProp = typeof(GameClock).GetProperty(nameof(GameClock.ElapsedSeconds));
            var regProp = typeof(GameClock).GetProperty(nameof(GameClock.RegulationDurationSeconds));
            Assert.IsTrue(regProp.CanRead);
            Assert.IsTrue(timeProp.GetSetMethod(true) != null
                          && !timeProp.GetSetMethod(true).IsPublic,
                "ElapsedSeconds is owned state; RegulationDuration is read-only config.");
            Assert.AreEqual(5400.0, clock.RegulationDurationSeconds);
        }

        // ---- Independence ----

        [Test]
        public void IndependentClocksAreIndependent()
        {
            var a = new GameClock();
            var b = new GameClock();
            a.Start();
            a.Advance(5);
            Assert.AreEqual(5.0, a.ElapsedSeconds);
            Assert.AreEqual(0.0, b.ElapsedSeconds);
            b.Reset();
            Assert.AreEqual(5.0, a.ElapsedSeconds, "Resetting one clock must not affect another.");
        }
    }
}
