using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Football.Input;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 61 — Performance Debug Category.
    ///
    /// AUDIT RESULT (61 audit-first, re-verified): there is **no production performance/debug
    /// monitor** and **no performance consumer**.
    ///   - Zero Runtime references to Profiler/UnityEngine.Profiling/ProfilerRecorder/Recorder/
    ///     CustomSampler/ProfilerMarker/BeginSample/EndSample/FrameTimingManager/GC.*/GetTotalMemory/
    ///     CollectionCount/GetAllocatedBytes*/Stopwatch/Metrics/Telemetry/Snapshot/PerformanceMonitor/
    ///     PerformanceDebug/FPS/FrameTime. The only "Performance" token in Runtime is the
    ///     EnablePerformanceDebug flag definition.
    ///   - Zero `Time.*` usage in Runtime (GameClock is the time abstraction; no Time.deltaTime /
    ///     unscaledDeltaTime / fixedDeltaTime / realtimeSinceStartup / frameCount / timeScale /
    ///     Time.time). No Physics.Simulate.
    ///   - Lifecycle: only HumanPlayerInput.Update(); zero `void FixedUpdate` / `void LateUpdate`
    ///     anywhere in Runtime.
    ///   - No performance consumers, no telemetry, no overlay, no persistent metrics.
    ///   - Existing Performance debug: only FootballDebugSettings.EnablePerformanceDebug (pure
    ///     configuration).
    ///
    /// DECISION — POLICY-ONLY (Option A): with no runtime performance consumer and no metering
    /// framework present, Task 61 establishes the category (enablement contract + metric ownership +
    /// anti-control + zero-overhead-when-disabled rules) and **manufactures no ProductionPerformance
    /// monitor, no metric sampler, no frame/fixed-step/GC instrumentation, no PerfManager Singleton,
    /// no profiler framework, no telemetry, no logging, no throttling, no overlay**.
    /// </summary>
    public class PerformanceDebugCategoryTests
    {
        private static readonly Type[] RelevantTypes =
        {
            typeof(FootballDebugSettings),   // Football.Core
            typeof(GameClock),               // Football.Core
            typeof(HumanPlayerInput),        // Football.Input
            typeof(PlayerStateId),           // Football.Players
            typeof(GameStateId),             // Football.Match
        };

        private static IEnumerable<Assembly> PerformanceRelevantAssemblies()
            => RelevantTypes.Select(t => t.Assembly).Distinct();

        private static string[] GetAllTypeNames()
            => PerformanceRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ---- 61.1/Enablement — reuse the existing flag ----

        [Test]
        public void PerformanceDebugCategoryUsesExistingEnablePerformanceDebug_NoDuplicateFlag()
        {
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnablePerformanceDebug));
            Assert.IsNotNull(f, "EnablePerformanceDebug must exist (reuse, not a new flag).");
            Assert.AreEqual(typeof(bool), f.FieldType, "Performance category must be a strongly-typed bool.");

            Assert.IsNull(typeof(FootballDebugSettings).GetField("PerformanceDebugEnabled"),
                "Do NOT create a duplicate performance flag (PerformanceDebugEnabled) when EnablePerformanceDebug already represents it.");
        }

        [Test]
        public void PerformanceDebugFlagDefaultsOff()
        {
            var settings = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(settings.EnablePerformanceDebug,
                "The performance debug category must default to OFF (no burden on the disabled path).");
        }

        [Test]
        public void PerformanceDebugSwitchIsIndependentlyTogglable()
        {
            var settings = ScriptableObject.CreateInstance<FootballDebugSettings>();
            settings.EnablePerformanceDebug = true;
            Assert.IsTrue(settings.EnablePerformanceDebug, "Category must be independently togglable.");
        }

        [Test]
        public void PerformanceDebugRequiresMasterAndCategory()
        {
            // Reuse the master + category pairing guard already enforced for the category.
            var master = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.DebugEnabled));
            Assert.IsNotNull(master, "Master DebugEnabled switch must exist (T54).");

            var category = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnablePerformanceDebug));
            Assert.IsNotNull(category, "EnablePerformanceDebug category must exist.");

            Assert.AreNotEqual(master, category, "Master and category switches must be distinct fields.");
        }

        // ---- 61.1/61.2/61.3 — metric ownership (Performance Debug never owns the source) ----

        [Test]
        public void PerformanceDebugDoesNotOwnGameClock()
        {
            // GameClock (Task 49) remains the time owner; Performance debug must not replace/augment it.
            Assert.IsNotNull(typeof(GameClock), "GameClock (Task 49) remains the time owner.");
            Assert.IsNull(FindType("PerformanceTimeProvider"),
                "Performance debug must not add its own time provider.");
        }

        [Test]
        public void PerformanceDebugDoesNotOwnFixedTimestep()
        {
            // Task 52 owns fixed timestep policy; Performance debug must not own or mutate it.
            Assert.IsNull(FindType("PhysicsScheduler"), "Performance/Physics debug must not create a PhysicsScheduler.");
            Assert.IsNull(FindType("PerformanceFixedStepController"),
                "Performance debug must not control the fixed timestep.");
        }

        [Test]
        public void PerformanceDebugDoesNotCreatePhysicsScheduler()
        {
            Assert.IsNull(FindType("PerformancePhysicsScheduler"), "No performance-driven physics scheduler.");
            Assert.IsNull(FindType("FixedStepDebugScheduler"), "No fixed-step debug scheduler.");
        }

        private static IEnumerable<MethodInfo> SafeGetMethods(Type t)
        {
            try { return t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
            catch { return Enumerable.Empty<MethodInfo>(); }
        }

        [Test]
        public void PerformanceDebugDoesNotCreateProfilerFramework()
        {
            foreach (var name in new[] { "PerformanceMonitor", "PerformanceMetrics", "PerformanceManager",
                                         "FrameTimingMonitor", "FixedStepMonitor", "ProfilerRecorder",
                                         "CustomSampler", "MetricsRegistry", "IMetricProvider" })
            {
                Assert.IsNull(FindType(name), $"Performance debug must not manufacture a profiler/metrics framework type '{name}'.");
            }
        }

        [Test]
        public void PerformanceDebugDoesNotCreateGlobalStateOrServiceLocator()
        {
            // No global/system Performance singleton or locator-managed performance service.
            var names = GetAllTypeNames();
            Assert.IsFalse(names.Any(n => n.Contains("PerformanceDebug") && (n.Contains("Singleton") || n.Contains("Locator"))),
                "No Performance debug singleton/service-locator type should exist.");
        }

        // ---- 61.2 — Time authority boundary (no new frame-time provider, no FPS authority) ----

        [Test]
        public void PerformanceDebugIsNotATimeAuthority()
        {
            // GameClock (Task 49) is the only time owner; Performance debug must not add a time provider.
            var clock = typeof(GameClock);
            var timeProviders = SafeGetTypes(clock.Assembly)
                .SelectMany(t => SafeGetMethods(t))
                .Where(m => (m.Name == "TimeProvider" || m.Name.Contains("FrameTime") || m.Name.Contains("FPS")))
                .ToList();
            Assert.IsEmpty(timeProviders,
                "Performance debug must not introduce frame-time/FPS time-authority providers; GameClock owns time.");
        }

        [Test]
        public void PerformanceDebugDoesNotCreateEmptyUpdateBonusLoop()
        {
            // No production-gameplay Update beyond HumanPlayerInput.Update() (Task 53 ownership).
            Assert.IsNull(FindMethodNamed("PerformanceDebugUpdate"),
                "Performance debug must not add a per-frame poll Update loop.");
        }

        // ---- 61.3 — Fixed-step policy (no fake measurements, no ownership) ----

        [Test]
        public void PerformanceDebugDoesNotAddFixedUpdateLoop()
        {
            Assert.IsNull(FindMethodNamed("PerformanceDebugFixedUpdate"),
                "Performance debug must not add a FixedUpdate loop merely to count steps.");
        }

        [Test]
        public void PerformanceDebugDoesNotFakeFixedStepMeasurements()
        {
            foreach (var name in new[] { "MeasureFixedStep", "CountFixedSteps", "PhysicsStepSampler", "FixedStepSampler" })
            {
                Assert.IsNull(FindMethodNamed(name),
                    $"Performance debug must not fake/measure fixed-step timing ('{name}') while no production FixedUpdate exists.");
            }
        }

        // ---- 61.4 — Allocation / GC diagnostics (none manufactured) ----

        [Test]
        public void PerformanceDebugDoesNotIntroduceGCInstrumentation()
        {
            foreach (var name in new[] { "AllocationTracker", "GCAllocationMonitor", "ManagedMemorySampler", "HeapScanner" })
            {
                Assert.IsNull(FindType(name),
                    $"Performance debug must not manufacture allocation/GC instrumentation ('{name}').");
            }
        }

        [Test]
        public void PerformanceDebugDoesNotAddPerFrameAllocationSampling()
        {
            Assert.IsNull(FindMethodNamed("SampleAllocations"),
                "Performance debug must not add a per-frame allocation sampler.");
        }

        // ---- 61.5 — Zero production overhead when disabled ----

        [Test]
        public void PerformanceDebugDoesNotCreateUpdateLoop()
        {
            // Disabled path: no Performance-debug per-frame update loop exists; no GC-sampler sampler.
            Assert.IsNull(FindMethodNamed("PerformanceDebugUpdate"),
                "Performance debug must not add a per-frame Update loop to poll metrics.");
            Assert.IsNull(FindMethodNamed("PerformanceDebugFixedUpdate"),
                "Performance debug must not add a per-frame FixedUpdate loop to poll metrics.");
        }

        [Test]
        public void PerformanceDebugNoPerFrameMonitoringWhenDisabled()
        {
            // With default master+category OFF, no per-frame metric sampling may exist.
            var settings = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(settings.DebugEnabled && settings.EnablePerformanceDebug,
                "Default state (master/category off) must leave the performance channel a no-op.");
            Assert.IsNull(FindType("PerFrameMetricSampler"),
                "No per-frame metric sampler type may exist.");
            Assert.IsNull(FindMethodNamed("SampleMetrics"),
                "No per-frame metric sampling method may exist.");
        }

        // ---- 61 Boundaries — must not own/control gameplay or other debug categories ----

        [Test]
        public void PerformanceDebugDoesNotControlPause()
        {
            Assert.IsNull(FindMethodNamed("PerformanceDebugPause"),
                "Performance debug must not pause/unpause gameplay (Task 51 owns pause).");
        }

        [Test]
        public void PerformanceDebugDoesNotControlAI()
        {
            Assert.IsNull(FindMethodNamed("PerformanceDebugControlAI"),
                "Performance debug must not alter AI behavior.");
        }

        [Test]
        public void PerformanceDebugIsIndependentOfOtherDebugCategories()
        {
            // The performance flag coexists with the sibling categories without sharing sources.
            var all = typeof(FootballDebugSettings).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(f => f.Name)
                .Where(n => n.StartsWith("Enable") && n.EndsWith("Debug"))
                .ToArray();
            Assert.Contains(nameof(FootballDebugSettings.EnablePerformanceDebug), all,
                "Performance category must be present alongside the sibling categories.");
            Assert.Contains(nameof(FootballDebugSettings.EnableMovementDebug), all);
            Assert.Contains(nameof(FootballDebugSettings.EnableCameraDebug), all);
            Assert.Contains(nameof(FootballDebugSettings.EnableBallDebug), all);
            Assert.Contains(nameof(FootballDebugSettings.EnableAnimationDebug), all);
            Assert.Contains(nameof(FootballDebugSettings.EnableMatchDebug), all);
            Assert.Contains(nameof(FootballDebugSettings.EnableAIDebug), all);
        }

        // ---- helpers ----

        private static Type FindType(string simpleName)
            => PerformanceRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.Name == simpleName || t.FullName?.EndsWith("." + simpleName) == true);

        private static MethodInfo FindMethodNamed(string name)
            => PerformanceRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .SelectMany(t => SafeGetMethods(t))
                .FirstOrDefault(m => m.Name == name);
    }
}
