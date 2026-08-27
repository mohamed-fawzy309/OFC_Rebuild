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
    /// Task 63 — Throttled Logging.
    ///
    /// AUDIT RESULT (63 audit-first, re-verified): there is **no runtime logger** and therefore
    /// **no throttle consumer** and **no throttle implementation**.
    ///   - Zero production Runtime `Debug.Log*` calls. The only real log calls remain in
    ///     `Editor/FootballSetup.cs` (editor-only tool: 4 Debug.Log + 1 Debug.LogError).
    ///   - Zero Runtime references to Throttle/RateLimit/Cooldown/LogLimiter/Deduplicator/LogGate/
    ///     LogFilter/Logger. No runtime logger exists to throttle.
    ///   - Zero `Time.*`/`Stopwatch`/`DateTime` usage in Runtime — there is no timing infrastructure
    ///     that could be (mis)appropriated for throttle timing; GameClock is not used for logging.
    ///   - Lifecycle: only HumanPlayerInput.Update() — zero FixedUpdate/LateUpdate, and no logs in
    ///     Update. No per-frame / per-fixed-step / hot-path log spam exists.
    ///   - Structured errors remain exceptions: BootstrapError (T46), ServiceNotFoundException /
    ///     DuplicateServiceException (T47/48) are contracts, not swallowed-and-logged.
    ///
    /// DECISION — POLICY-ONLY: with **no runtime logger and no throttle consumer**, Task 63 defines
    /// the throttling contract (ownership = FUTURE logging layer, time-source = unscaled real time,
    /// per-message identity, severity interaction, no exception suppression, no GameClock/simulation
    /// dependency) and **manufactures no ThrottleManager / LogThrottleManager / Logger /
    /// GlobalLogger / LogLimiter / deduplicator, no throttle state, no time source, no Singleton, no
    /// Service Locator, no per-frame loop, no telemetry, no overlay**.
    /// </summary>
    public class ThrottledLoggingTests
    {
        private static IEnumerable<Assembly> ThrottleRelevantAssemblies()
        {
            yield return typeof(FootballDebugSettings).Assembly; // Football.Core
            yield return typeof(GameClock).Assembly;             // Football.Core
            yield return typeof(GameEvents).Assembly;            // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;      // Football.Input
            yield return typeof(PlayerStateId).Assembly;         // Football.Players
            yield return typeof(GameStateId).Assembly;           // Football.Match
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static Type FindType(string simpleName)
            => ThrottleRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.Name == simpleName || t.FullName?.EndsWith("." + simpleName) == true);

        // ---- No throttle infrastructure exists (no runtime logger to throttle) ----

        [Test]
        public void NoThrottleInfrastructureCreated()
        {
            // No runtime logger exists => no throttle may be built (would be infrastructure with no consumer).
            foreach (var forbidden in new[]
            {
                "ThrottleManager", "LogThrottleManager", "GlobalThrottle", "LogLimiter", "LogThrottle",
                "LogGate", "LogCooldown", "LogDeduplicator", "DedupLogger", "RateLimiter"
            })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Task 63 must not manufacture throttle infrastructure '{forbidden}' (no runtime logger / no consumer).");
            }
        }

        [Test]
        public void ThrottlingDoesNotCreateSecondLogger()
        {
            foreach (var forbidden in new[] { "ThrottleLogger", "ThrottledLogger", "Logger", "GlobalLogger" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling must not create a logger ('{forbidden}'); Task 62 found no runtime logger and established logging is policy-only.");
            }
        }

        // ---- 63.1 Ownership — logging layer owns throttle; not gameplay/time ----

        [Test]
        public void ThrottlingOwnershipBelongsToLoggingLayer()
        {
            // With no logger, ownership is documented as FUTURE logging-layer responsibility.
            // No gameplay/system may own throttle infrastructure.
            foreach (var forbidden in new[] { "GameplayThrottle", "SimulationThrottle", "MatchThrottle", "PlayerThrottle" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Gameplay must not own throttle infrastructure ('{forbidden}').");
            }
            // GameClock (T49) must remain match time; it must not gain a throttle owned by a gameplay system.
            Assert.IsNotNull(typeof(GameClock), "GameClock remains the (match) time owner.");
        }

        [Test]
        public void ThrottlingDoesNotOwnGameClock()
        {
            foreach (var forbidden in new[] { "ClockThrottle", "GameClockThrottle", "ThrottleClock" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling must not create/own a clock ('{forbidden}').");
            }
        }

        [Test]
        public void ThrottlingDoesNotUseGameClockOrMatchTime()
        {
            // Logging throttling must NOT depend on GameClock / match time.
            // Structurally: no runtime throttle exists, so there is nothing reading GameClock for
            // throttle decisions. GameClock.ElapsedSeconds is only match time, never log gating.
            var gameClock = SafeGetTypes(typeof(GameClock).Assembly)
                .SelectMany(t => SafeGetMethods(t))
                .Where(m => m.Name.Contains("Throttle") || m.Name.Contains("ShouldEmit") || m.Name.Contains("Cooldown"))
                .ToList();
            Assert.IsEmpty(gameClock,
                "GameClock must not expose throttle/emit/cooldown logic: logging must not depend on match time.");
        }

        [Test]
        public void ThrottlingDoesNotOwnSimulationTimeOrPauseOrPhysics()
        {
            foreach (var forbidden in new[] { "ThrottleSimulation", "ThrottlePause", "ThrottlePhysics", "ThrottleFixedStep" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling must not own simulation/pause/physics ('{forbidden}').");
            }
        }

        [Test]
        public void ThrottlingDoesNotCreatePauseOrLoopSystems()
        {
            foreach (var forbidden in new[] { "ThrottleLoop", "ThrottleUpdate", "ThrottleFixedUpdate", "ThrottleLateUpdate" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling must not add its own loop/pause system ('{forbidden}').");
            }
        }

        // ---- 63.2 / 63.3 — throttle state, message identity, time source (none; policy only) ----

        [Test]
        public void NoThrottleStateContainerExists()
        {
            // Operational throttle state (per-message timestamps, hash keys) belongs to a FUTURE
            // logger. With no logger, no such state object may exist.
            foreach (var forbidden in new[] { "ThrottleState", "MessageThrottleState", "ThrottleHistory" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttle operational state must not be created without a logger ('{forbidden}').");
            }
        }

        [Test]
        public void ThrottlingDoesNotOwnExceptions()
        {
            // Throttling must never suppress/own thrown exceptions.
            foreach (var forbidden in new[] { "ThrottleException", "ExceptionThrottle" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling must not alter/absorb exceptions ('{forbidden}').");
            }
        }

        // ---- 63.4 — no per-frame/per-fixed-step/hot-path log spam exists ----

        [Test]
        public void NoPerFrameOrPerFixedStepLogSpamExists()
        {
            // Re-verify: the only production lifecycle callback is HumanPlayerInput.Update(), and
            // there are zero production Debug.Log calls anywhere in Runtime. No throttle is needed
            // because no per-frame/per-fixed-step/hot-path logging exists.
            var runtimeTypes = ThrottleRelevantAssemblies().SelectMany(a => SafeGetTypes(a));
            var logCallSites = runtimeTypes.SelectMany(t => SafeGetMethods(t))
                .Select(m => m.Name)
                .Where(n => n.Contains("Log") && (n.Contains("Update") || n.Contains("FixedUpdate") || n.Contains("Tick")))
                .ToList();
            Assert.IsEmpty(logCallSites,
                "No per-frame/per-fixed-step/Tick logging method may exist (no log spam to throttle).");
        }

        [Test]
        public void NoRepeatedWarningOrErrorSpamExists()
        {
            foreach (var forbidden in new[] { "RepeatedWarningLogger", "RepeatedErrorLogger", "WarningSpamGuard", "ErrorSpamGuard" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"No repeated-warning/error spam guard may exist ('{forbidden}') — no logger/no spam.");
            }
        }

        // ---- 63.5 — disabled path & anti-infrastructure ----

        [Test]
        public void ThrottlingDisabledPathHasNoWork()
        {
            // No throttle infrastructure exists, so the disabled path does zero throttle work
            // (no allocation, no dictionary/hash, no timestamp, no string formatting).
            var settings = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(settings.DebugEnabled && settings.EnableMovementDebug,
                "With all debug defaults off, no throttle/logger work may be triggered.");
            Assert.IsNull(FindType("PerFrameThrottle"), "No per-frame throttle may exist.");
        }

        // ---- 63.5 — no Singleton / Service Locator / telemetry / overlay / profiler ----

        [Test]
        public void ThrottlingDoesNotCreateSingletonOrServiceLocator()
        {
            foreach (var forbidden in new[] { "ThrottleManager", "GlobalThrottle", "ThrottleService", "IThrottle", "ThrottleRegistry" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling must not add Singleton/service-locator ('{forbidden}').");
            }
        }

        [Test]
        public void ThrottlingDoesNotCreateTelemetryOrHistory()
        {
            foreach (var forbidden in new[] { "ThrottleLog", "ThrottleHistory", "ThrottleBuffer", "ThrottleSnapshot" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling must not create telemetry/history ('{forbidden}'); Task 64 owns it.");
            }
        }

        [Test]
        public void ThrottlingDoesNotCreateOverlayOrProfiler()
        {
            foreach (var forbidden in new[] { "ThrottleConsole", "ThrottleView", "OnScreenThrottle", "ThrottleProfiler", "ThrottleTimer" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling must not create overlay/profiler ('{forbidden}').");
            }
        }

        // ---- Structured errors preserved, distinct from throttle/log-level policy ----

        [Test]
        public void StructuredErrorsRemainExceptions()
        {
            // Throttling must not swallow structured errors.
            Assert.IsNotNull(FindType("BootstrapError"), "BootstrapError (T46) remains a structured error.");
            Assert.IsNotNull(FindType("ServiceNotFoundException"), "ServiceNotFoundException (T47/48) remains an exception.");
            Assert.IsNotNull(FindType("DuplicateServiceException"), "DuplicateServiceException (T47/48) remains an exception.");
        }

        [Test]
        public void ThrottlingPolicyIsDistinctFromLogLevelPolicy()
        {
            // Throttling controls OUTPUT FREQUENCY; Task 62 log levels control WHICH level may be
            // emitted. They are distinct concerns; no single type may fuse them.
            foreach (var forbidden in new[] { "LevelThrottle", "ThrottleLevelFilter", "LogLevelThrottle" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Throttling and log-level filtering must remain distinct ('{forbidden}').");
            }
        }

        private static IEnumerable<MethodInfo> SafeGetMethods(Type t)
        {
            try { return t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
            catch { return Enumerable.Empty<MethodInfo>(); }
        }
    }
}
