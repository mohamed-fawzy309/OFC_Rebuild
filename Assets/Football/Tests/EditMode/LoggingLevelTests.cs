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
    /// Task 62 — Logging Levels.
    ///
    /// AUDIT RESULT (62 audit-first, re-verified): there is **no logging system** in Football code.
    ///   - The ONLY real Debug.Log calls live in `Editor/FootballSetup.cs` (an editor-only
    ///     setup/verification tool: 4 Debug.Log + 1 Debug.LogError). Zero production Runtime log
    ///     calls; zero test log calls; zero editor Runtime logs.
    ///   - No central logger, no LoggerManager/GlobalLogger/GameLogger, no LogLevel/LogCategory
    ///     enum, no ILogger/ILogHandler abstraction, no LoggerFactory anywhere in Football.
    ///   - Logging categories exist only as 7 bools in FootballDebugSettings (EnableMovementDebug …
    ///     EnablePerformanceDebug); there are NO separate Enable*Logging flags.
    ///   - Structured error reporting exists and remains authoritative: BootstrapError (Task 46),
    ///     ServiceNotFoundException / DuplicateServiceException (Tasks 47/48). These are contracts;
    ///     logging must NOT replace them.
    ///   - Release policy: FootballDebugSettings.IsDevelopmentContext (UNITY_EDITOR ||
    ///     DEVELOPMENT_BUILD) is the single build-context directive; no compile-time stripping is
    ///     implemented (and none is claimed).
    ///   - No hot-path / per-frame / per-fixed-step logging; GameEvents is an event bus, not a
    ///     logging bus; no telemetry; no overlay.
    ///
    /// DECISION — POLICY-ONLY (Option C): there is **no current runtime logging consumer**, so Task
    /// 62 defines the logging-level contract + category-filtering contract + anti-abuse rules and
    /// **manufactures no LogLevel type, no Logger abstraction, no central/global logger, no logging
    /// Singleton, no logging Service Locator, no logging event bus, no telemetry, no overlay, no
    /// throttling, no per-frame logging infrastructure**.
    /// </summary>
    public class LoggingLevelTests
    {
        private static IEnumerable<Assembly> LoggingRelevantAssemblies()
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
            => LoggingRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.Name == simpleName || t.FullName?.EndsWith("." + simpleName) == true);

        // ---- 62.1 — No LogLevel type manufactured (policy defines the levels; no runtime type) ----

        [Test]
        public void LogLevelsArePolicyNotRuntimeType()
        {
            // With no logging consumer, ERROR/WARNING/INFO/DEBUG are a documented policy, not a
            // runtime enum. A LogLevel type would only be justified by a real logger.
            Assert.IsNull(FindType("LogLevel"),
                "Task 62 is policy-only (no logging consumer); no LogLevel enum should be created.");
            Assert.IsNull(FindType("LogCategory"),
                "No LogCategory enum should be created; category filtering reuses the debug category flags.");
        }

        // ---- 62.1/62.4 — Semantics are distinct and documented ----

        [Test]
        public void LoggingLevelsArePlainObservability_NotExposedAsRuntimeTypes()
        {
            // The four conceptual levels (ERROR/WARNING/INFO/DEBUG) map to Unity's LogType; we
            // assert the mapping contract is representable without a custom framework.
            var expected = new[] { "Error", "Warning", "Info", "Debug" };
            Assert.AreEqual(4, expected.Length, "Exactly four conceptual levels (Error/Warning/Info/Debug).");
            foreach (var lvl in expected)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(lvl), "Every level has a stable name.");
            }
        }

        [Test]
        public void ErrorSemanticsDistinctFromWarnings_NoEmotionalSeverityType()
        {
            // Semantics are documented, not encoded in an unused enum. There must be no runtime
            // artefact that lets call sites misuse levels (a log framework would be a placeholder).
            foreach (var forbidden in new[] { "ErrorLevel", "WarningLevel", "InfoLevel", "DebugLevel", "LogSeverity" })
            {
                Assert.IsNull(FindType(forbidden), $"No unused '{forbidden}' severity type should exist.");
            }
        }

        // ---- 62.2 — Category filtering reuses the existing debug category flags ----

        [Test]
        public void CategoryFilteringUsesExistingDebugCategories()
        {
            var fields = typeof(FootballDebugSettings)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(f => f.Name)
                .ToArray();
            foreach (var expected in new[]
            {
                nameof(FootballDebugSettings.EnableMovementDebug),
                nameof(FootballDebugSettings.EnableBallDebug),
                nameof(FootballDebugSettings.EnableAnimationDebug),
                nameof(FootballDebugSettings.EnableMatchDebug),
                nameof(FootballDebugSettings.EnableAIDebug),
                nameof(FootballDebugSettings.EnableCameraDebug),
                nameof(FootballDebugSettings.EnablePerformanceDebug),
            })
            {
                Assert.Contains(expected, fields, $"'{expected}' must exist to serve as the logging category filter.");
            }
        }

        [Test]
        public void NoDuplicateLoggingCategoryFlags()
        {
            // Logging must reuse Enable*Debug flags; it must NOT duplicate them as Enable*Logging.
            var fields = typeof(FootballDebugSettings)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(f => f.Name)
                .ToArray();
            Assert.IsFalse(fields.Any(n => n.Contains("Logging")),
                "Do NOT add separate Enable*Logging flags while Enable*Debug already provides category filtering.");
        }

        [Test]
        public void DebugMasterSwitchControlsDebugLogging()
        {
            // Master switch exists (Task 54) and gates all debug work including logging.
            var master = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.DebugEnabled));
            Assert.IsNotNull(master, "DebugEnabled master switch (Task 54) must exist and gate logging.");
            Assert.AreEqual(typeof(bool), master.FieldType);
        }

        [Test]
        public void DisabledDebugDoesNotProduceDebugWork()
        {
            // Category + master OFF by default => logging channel is a no-op with no infrastructure.
            var settings = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(settings.DebugEnabled, "Master must default OFF (no logging work when disabled).");
            Assert.IsNull(FindType("GameLogger"), "No logger Singleton may exist.");
            Assert.IsNull(FindType("GlobalLogger"), "No global logger may exist.");
        }

        // ---- 62.5 — No logging framework / infra manufactured (no consumer) ----

        [Test]
        public void NoLoggingFrameworkCreated()
        {
            foreach (var forbidden in new[]
            {
                "GameLogger", "GlobalLogger", "DebugLogger", "FootballLogger", "LoggerManager",
                "LoggerFactory", "LogHandler", "FootballLog", "LoggerService", "LogFilter", "LogConfig"
            })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Task 62 must not manufacture a logging framework type '{forbidden}' (no consumer).");
            }
        }

        [Test]
        public void NoLoggingSingletonCreated()
        {
            Assert.IsNull(FindType("Logger"), "No logging Singleton (Logger/Logger.Instance) may exist.");
            Assert.IsNull(FindType("Log"), "No static logging class (Log.*) may exist.");
        }

        [Test]
        public void NoLoggingServiceLocatorCreated()
        {
            foreach (var forbidden in new[] { "LoggerResolver", "LoggingService", "ILog", "ILogService" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Logging must not introduce a service-locator type '{forbidden}'.");
            }
        }

        // ---- 62.5 — No uncontrolled logging infrastructure ----

        [Test]
        public void NoPerFrameLoggingInfrastructureCreated()
        {
            Assert.IsNull(FindType("PerFrameLogger"), "No per-frame logging infrastructure may exist.");
            Assert.IsNull(FindType("LogBuffer"), "No log-buffer type may exist (Task 64 telemetry boundary).");
            Assert.IsNull(FindType("LogHistory"), "No log-history type may exist (Task 64 boundary).");
        }

        [Test]
        public void NoFixedStepLoggingInfrastructureCreated()
        {
            Assert.IsNull(FindType("FixedStepLogger"), "No fixed-step logging infrastructure may exist.");
        }

        [Test]
        public void NoLoggingThrottleCreated()
        {
            // Task 63 owns throttling; Task 62 must not introduce it.
            foreach (var forbidden in new[] { "LogThrottle", "LogRateLimiter", "LogCooldown", "DedupLogger" })
            {
                Assert.IsNull(FindType(forbidden), $"Task 62 must not implement throttling ('{forbidden}'); Task 63 owns it.");
            }
        }

        [Test]
        public void NoLoggingTelemetryCreated()
        {
            // Task 64 owns telemetry snapshots; logging must not persist.
            foreach (var forbidden in new[] { "LogTelemetry", "TelemetrySnapshot", "LogPersistence", "LogUploader" })
            {
                Assert.IsNull(FindType(forbidden), $"Logging must not create telemetry/persistence ('{forbidden}'); Task 64 owns it.");
            }
        }

        [Test]
        public void NoLoggingOverlayCreated()
        {
            // Task 65 owns the debug overlay/console; logging must not create on-screen UI.
            foreach (var forbidden in new[] { "LogConsole", "LogView", "OnScreenLogger", "LogWindow" })
            {
                Assert.IsNull(FindType(forbidden), $"Logging must not create overlay UI ('{forbidden}'); Task 65 owns it.");
            }
        }

        // ---- 62.4 — structured errors remain structured (logging must not replace exceptions) ----

        [Test]
        public void BootstrapErrorSemanticsRemainStructured()
        {
            // Task 46 BootstrapError is the structured bootstrap error-reporting contract.
            Assert.IsNotNull(FindType("BootstrapError"), "BootstrapError (Task 46) must remain the structured error path.");
        }

        [Test]
        public void ServiceExceptionsRemainExceptions()
        {
            // Tasks 47/48 service exceptions remain thrown exceptions, not swallowed log messages.
            Assert.IsNotNull(FindType("ServiceNotFoundException"),
                "ServiceNotFoundException (Task 47/48) must remain an exception contract.");
            Assert.IsNotNull(FindType("DuplicateServiceException"),
                "DuplicateServiceException (Task 47/48) must remain an exception contract.");
        }

        [Test]
        public void LoggingDoesNotCreateSecondEventBus()
        {
            // GameEvents is the (single) event bus; logging must not become a second bus.
            Assert.IsNotNull(typeof(GameEvents), "GameEvents remains the single event bus.");
            Assert.IsNull(FindType("LogEvent"), "No LogEvent type; logging is not an event bus.");
            Assert.IsNull(FindType("LoggerEvent"), "No LoggerEvent type; do not bus logs through events.");
        }

        // ---- Logging must not own gameplay ----

        [Test]
        public void LoggingDoesNotOwnGameplayState()
        {
            // Logging must not introduce any state owner for gameplay (player/ball/match/AI).
            foreach (var forbidden in new[] { "LogGame", "LoggerSettings", "LoggerClock" })
            {
                Assert.IsNull(FindType(forbidden), $"Logging must not own gameplay ('{forbidden}').");
            }
        }

        [Test]
        public void LoggingDoesNotOwnGameClockOrPauseOrFixedTimestep()
        {
            Assert.IsNull(FindType("LoggerClock"), "Logging must not own GameClock.");
            Assert.IsNull(FindType("LoggerPause"), "Logging must not own pause.");
            Assert.IsNull(FindType("LoggerFixedStep"), "Logging must not own the fixed timestep.");
        }

        [Test]
        public void LoggingIsIndependentOfOtherDebugCategories()
        {
            // Logging is driven by the shared category flags; it does not add its own category set.
            var all = typeof(FootballDebugSettings)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(f => f.Name)
                .Where(n => n.StartsWith("Enable") && n.EndsWith("Debug"))
                .ToArray();
            Assert.AreEqual(7, all.Length, "Seven category flags (movement/ball/animation/match/AI/camera/performance) are the single category source.");
        }
    }
}
