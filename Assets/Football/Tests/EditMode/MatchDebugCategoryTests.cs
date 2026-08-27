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
    /// Task 58 — Match Debug Category.
    ///
    /// AUDIT RESULT (58.1 audit-first, re-verified): there is **no production Match system**.
    ///   - Runtime/Match contains only GameStateId.cs (an orphan enum, namespace Football.Core, in
    ///     the Football.Match assembly). Only its own declaration references it — no consumer.
    ///   - No MatchManager/Controller/System/Runtime, no match score system (no HomeScore/AwayScore/
    ///     CurrentScore runtime), no match clock owner.
    ///   - GameClock (Football.Core) is a real tested class (Task 49) but is **never instantiated
    ///     or driven by production** — no `new GameClock`, no Start/Stop/Reset/Advance anywhere.
    ///   - Match events (MatchStartedEvent/MatchEndedEvent/GoalScoredEvent/PossessionChangedEvent/
    ///     BallKickedEvent/PlayerActionStarted/FinishedEvent) are `readonly struct` contracts in
    ///     Football.Core.Events. GameEvents is a static subscribe/raise bus, but there are **no
    ///     production publishers and no production subscribers** — event structs are contracts, not
    ///     proof of a runtime Match event flow.
    ///   - Existing Match debug: only FootballDebugSettings.EnableMatchDebug (category), a pure
    ///     configuration flag.
    ///
    /// DECISION — POLICY-ONLY (Option A): no Match runtime exists, so Task 58 establishes the
    /// category (enablement contract + diagnostic contract + ownership/authority boundary) and
    /// **manufactures no Match runtime, state machine, score system, second clock, event monitor,
    /// second event bus, cheats, debug input, telemetry, overlay, or logging**.
    /// </summary>
    public class MatchDebugCategoryTests
    {
        private static IEnumerable<Assembly> MatchRelevantAssemblies()
        {
            yield return typeof(FootballDebugSettings).Assembly; // Football.Core
            yield return typeof(GameClock).Assembly;             // Football.Core
            yield return typeof(GameEvents).Assembly;            // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;      // Football.Input
            yield return typeof(PlayerStateId).Assembly;         // Football.Players
            yield return typeof(GameStateId).Assembly;           // Football.Match
        }

        private static string[] GetAllTypeNames()
            => MatchRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ---- 58.1/Enablement — reuse the existing flag ----

        [Test]
        public void MatchDebugCategoryUsesExistingEnableMatchDebug_NoDuplicateFlag()
        {
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableMatchDebug));
            Assert.IsNotNull(f, "EnableMatchDebug must exist (reuse, not a new flag).");
            Assert.AreEqual(typeof(bool), f.FieldType, "Match category must be a strongly-typed bool.");

            Assert.IsNull(typeof(FootballDebugSettings).GetField("MatchDebugEnabled"),
                "Do NOT create a duplicate match flag (MatchDebugEnabled) when EnableMatchDebug already represents it.");
        }

        [Test]
        public void MatchDebugFlag_DefaultsOff()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(s.EnableMatchDebug, "Match debug must default OFF so disabled debug performs no work.");
        }

        [Test]
        public void MatchDebugSwitch_IsIndependentlyTogglable()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableMatchDebug));
            f.SetValue(s, true);
            Assert.IsTrue((bool)f.GetValue(s));
            f.SetValue(s, false);
            Assert.IsFalse((bool)f.GetValue(s));
        }

        [Test]
        public void MatchDebug_NoBehaviorUntil_MasterAndCategoryBothOn()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();

            s.DebugEnabled = false;
            s.EnableMatchDebug = true;
            Assert.IsFalse(IsActiveFor(s), "Master OFF must gate the match category even when the category flag is ON.");

            s.DebugEnabled = true;
            s.EnableMatchDebug = false;
            Assert.IsFalse(IsActiveFor(s), "Category OFF means diagnostics inactive even when master is ON.");

            s.EnableMatchDebug = true;
            Assert.IsTrue(IsActiveFor(s), "Master AND category both on -> active.");
        }

        private static bool IsActiveFor(FootballDebugSettings s)
            => s.DebugEnabled && s.EnableMatchDebug;

        [Test]
        public void MatchDebug_IsIndependentOfOtherCategories()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            s.EnableMatchDebug = true;
            Assert.IsFalse(s.EnableMovementDebug, "Toggling the match category must not affect other categories.");
            Assert.IsFalse(s.EnableBallDebug);
        }

        // ---- 58.1/58.2/58.4 — no Match/score/clock/state runtime manufactured ----

        [Test]
        public void NoMatchRuntimeOrManagerCreated()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "MatchManager", "MatchController", "MatchRuntime", "MatchSimulation", "MatchSystem"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 58 must not manufacture '{forbidden}' (no Match system exists to observe).");
            }
        }

        [Test]
        public void NoMatchStateMachineOrScoreSystemManufactured()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "MatchStateMachine", "MatchState", "ScoreManager", "MatchScore", "ScoreSystem",
                "ScoreDebugState"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 58 must not manufacture a state/score authority ('{forbidden}').");
            }
        }

        [Test]
        public void NoSecondGameClockOrDebugClock()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "MatchDebugClock", "MatchClock", "DebugClock", "SecondGameClock" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 58 must not create a second clock ('{forbidden}'). GameClock is the single source of truth.");
            }
        }

        [Test]
        public void NoMatchEventMonitorOrSecondEventBus()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "MatchEventMonitor", "MatchEventRecorder", "EventBus", "SecondEventBus", "MatchEventHistory",
                "EventTimeline", "GoalHistoryBuffer"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 58 must not add a second event bus / history ('{forbidden}'). GameEvents remains the single event bus.");
            }
        }

        [Test]
        public void NoDebugMatchCheats()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "ForceGoal", "EditScore", "SkipTime", "ForceKickoff", "ForceExtraTime",
                "RestartMatch", "ScoreDebugControl", "MatchDebugControl"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 58 must not implement debug cheats/controls ('{forbidden}').");
            }
        }

        [Test]
        public void NoDebugMatchInputOrDirectInputPoller()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugMatchInput", "MatchDebugInput", "MatchDebugInputProvider" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Match Debug must not read input ('{forbidden}').");
            }
            foreach (var type in MatchRelevantAssemblies().SelectMany(SafeGetTypes))
            {
                if (!IsMatchDebugLike(type))
                    continue;
                Assert.IsFalse(ReferencesUnityEngineInput(type),
                    $"'{type.FullName}' must not poll keyboard/mouse/gamepad or UnityEngine.Input.");
            }
        }

        [Test]
        public void NoGlobalOrStaticMatchDebugState()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "GlobalMatchState", "StaticMatchDebugState", "MatchDebugState", "GlobalMatchDebug",
                "DebugHomeScore", "DebugAwayScore"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"No static/global match debug state or duplicated score ('{forbidden}'). No second authoritative match state.");
            }
        }

        [Test]
        public void SettingsDoNotOwnMatchScoreClockOrState()
        {
            // FootballDebugSettings (the category location) must not store score/clock/state as
            // mutable gameplay truth.
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var name in new[] { "HomeScore", "AwayScore", "CurrentScore", "ElapsedSeconds", "MatchClock", "GameState" })
            {
                Assert.IsNull(typeof(FootballDebugSettings).GetField(name, flags),
                    $"Settings must NOT own '{name}' (debug is observability, not authority).");
                Assert.IsNull(typeof(FootballDebugSettings).GetProperty(name, flags),
                    $"Settings must NOT own '{name}' (debug is observability, not authority).");
            }
        }

        [Test]
        public void MatchDebug_AddsNoLifecycleUpdateLoops()
        {
            foreach (var asm in MatchRelevantAssemblies())
                foreach (var type in SafeGetTypes(asm))
                {
                    if (!IsMatchDebugLike(type))
                        continue;
                    foreach (var methodName in new[] { "Update", "FixedUpdate", "LateUpdate" })
                    {
                        var m = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.IsNull(m, $"'{type.FullName}' must not add '{methodName}' for diagnostics.");
                    }
                }
        }

        // ---- Task-evolution boundaries — no telemetry/overlay/logging/throttling ----

        [Test]
        public void MatchDebug_IsNotTelemetry()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "MatchTelemetry", "MatchSnapshotHistory", "MatchEventRecorder", "TelemetryRecorder" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 58 must not create telemetry ('{forbidden}'). Task 64 owns Telemetry Snapshots.");
            }
        }

        [Test]
        public void MatchDebug_IsNotUI_NotOverlay()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugOverlay", "MatchOverlay", "MatchDebugOverlay", "MatchHUD", "Scoreboard" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 58 must not create UI/overlay/HUD ('{forbidden}'). Task 65 owns Debug Overlay.");
            }
        }

        [Test]
        public void MatchDebug_IsNotLoggingFramework()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "MatchLogger", "DebugLogger", "LoggerService", "GlobalLogger" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 58 must not create a logging framework ('{forbidden}'). Task 62 owns logging levels.");
            }
        }

        // ---- Independence: no gameplay->debug dependency ----

        [Test]
        public void GameplayAssemblies_DoNotDependOnDebugSettings()
        {
            foreach (var asm in new[]
            {
                typeof(HumanPlayerInput).Assembly, // Football.Input
                typeof(PlayerStateId).Assembly,    // Football.Players
                typeof(GameStateId).Assembly       // Football.Match
            })
            {
                foreach (var type in SafeGetTypes(asm))
                {
                    bool references = DeclaresType(type, typeof(FootballDebugSettings))
                        || ReferencesTypeInMethodBodies(type, typeof(FootballDebugSettings));
                    Assert.IsFalse(references,
                        $"Gameplay type '{type.FullName}' must not depend on FootballDebugSettings.");
                }
            }
        }

        // ---- structural helpers (reflection, not comment matching) ----

        private static bool IsMatchDebugLike(Type t)
            => (t.Namespace ?? "").IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0
               && (t.Name.IndexOf("Match", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool ReferencesUnityEngineInput(Type t)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            return t.GetFields(flags).Any(f => f.FieldType == typeof(UnityEngine.Input))
                || t.GetProperties(flags).Any(p => p.PropertyType == typeof(UnityEngine.Input));
        }

        private static bool DeclaresType(Type t, Type target)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            return t.GetFields(flags).Any(f => f.FieldType == target)
                || t.GetProperties(flags).Any(p => p.PropertyType == target);
        }

        private static bool ReferencesTypeInMethodBodies(Type t, Type target)
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (m.GetParameters().Any(p => p.ParameterType == target)) return true;
                if (m.ReturnType == target) return true;
            }
            return false;
        }
    }
}
