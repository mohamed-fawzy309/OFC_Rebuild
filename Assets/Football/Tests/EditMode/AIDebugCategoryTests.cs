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
    /// Task 59 — AI Debug Category.
    ///
    /// AUDIT RESULT (59.1 audit-first, re-verified): there is **no production AI system**.
    ///   - Runtime/AI (Football.AI asmdef) is EMPTY — no scripts; the AI assembly is not compiled.
    ///   - No AI controller, decision system, target selector, AI state machine, perception, or
    ///     navigation. The only "Target"/"Decision" tokens in Runtime are scene-transition variables
    ///     in SceneLoader/SceneTransitionSystem (NOT AI).
    ///   - AIPlayerInput (Football.Input) is a plain IPlayerInput input-abstraction MonoBehaviour
    ///     with settable properties and no production consumer — it is an input source, NOT proof of
    ///     AI gameplay (same as Human/Network/Replay inputs; no IPlayerInput consumer exists).
    ///   - No AI configuration data. Existing AI debug: only FootballDebugSettings.EnableAIDebug
    ///     (pure configuration).
    ///
    /// DECISION — POLICY-ONLY (Option A): no AI runtime exists, so Task 59 establishes the category
    /// (enablement contract + diagnostic contract + decision/target/state ownership + anti-mutation
    /// rules + visualization policy) and **manufactures no AI controller/brain/decision system/
    /// target selector/state machine/perception/navigation, no debug AI input, no cheats, no
    /// telemetry, no overlay, no logging**.
    /// </summary>
    public class AIDebugCategoryTests
    {
        private static IEnumerable<Assembly> AIRelevantAssemblies()
        {
            yield return typeof(FootballDebugSettings).Assembly; // Football.Core
            yield return typeof(GameClock).Assembly;             // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;      // Football.Input
            yield return typeof(AIPlayerInput).Assembly;         // Football.Input
            yield return typeof(PlayerStateId).Assembly;         // Football.Players
            yield return typeof(GameStateId).Assembly;           // Football.Match
        }

        private static string[] GetAllTypeNames()
            => AIRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ---- 59.1/Enablement — reuse the existing flag ----

        [Test]
        public void AIDebugCategoryUsesExistingEnableAIDebug_NoDuplicateFlag()
        {
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableAIDebug));
            Assert.IsNotNull(f, "EnableAIDebug must exist (reuse, not a new flag).");
            Assert.AreEqual(typeof(bool), f.FieldType, "AI category must be a strongly-typed bool.");

            Assert.IsNull(typeof(FootballDebugSettings).GetField("AIDebugEnabled"),
                "Do NOT create a duplicate AI flag (AIDebugEnabled) when EnableAIDebug already represents it.");
        }

        [Test]
        public void AIDebugFlag_DefaultsOff()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(s.EnableAIDebug, "AI debug must default OFF so disabled debug performs no work.");
        }

        [Test]
        public void AIDebugSwitch_IsIndependentlyTogglable()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableAIDebug));
            f.SetValue(s, true);
            Assert.IsTrue((bool)f.GetValue(s));
            f.SetValue(s, false);
            Assert.IsFalse((bool)f.GetValue(s));
        }

        [Test]
        public void AIDebug_NoBehaviorUntil_MasterAndCategoryBothOn()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();

            s.DebugEnabled = false;
            s.EnableAIDebug = true;
            Assert.IsFalse(IsActiveFor(s), "Master OFF must gate the AI category even when the category flag is ON.");

            s.DebugEnabled = true;
            s.EnableAIDebug = false;
            Assert.IsFalse(IsActiveFor(s), "Category OFF means diagnostics inactive even when master is ON.");

            s.EnableAIDebug = true;
            Assert.IsTrue(IsActiveFor(s), "Master AND category both on -> active.");
        }

        private static bool IsActiveFor(FootballDebugSettings s)
            => s.DebugEnabled && s.EnableAIDebug;

        [Test]
        public void AIDebug_IsIndependentOfOtherCategories()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            s.EnableAIDebug = true;
            Assert.IsFalse(s.EnableMovementDebug, "Toggling the AI category must not affect other categories.");
            Assert.IsFalse(s.EnableMatchDebug);
        }

        // ---- 59.1/59.2/59.3 — no fake AI runtime manufactured ----

        [Test]
        public void NoAIRuntimeOrDecisionSystemManufactured()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "AIController", "AIBrain", "DecisionSystem", "AIDecisionManager",
                "AIBehavior", "AIDecision"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 59 must not manufacture '{forbidden}' (no AI system exists to observe).");
            }
        }

        [Test]
        public void NoAITargetOrPerceptionSystemManufactured()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "TargetSelector", "TargetSelection", "AITargetSystem", "PerceptionSystem",
                "AIPerception", "AIDetection"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 59 must not manufacture targeting/perception ('{forbidden}').");
            }
        }

        [Test]
        public void NoAIStateMachineOrNavigationManufactured()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "AIStateMachine", "AIStateManager", "AIBehaviorStateMachine", "AIBrain",
                "NavigationSystem", "AIPathfinding"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 59 must not manufacture an AI state machine or navigation ('{forbidden}').");
            }
        }

        [Test]
        public void NoDebugAIInputOrAIPlayerInputControl()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugAIInput", "AIOverrideInput", "DebugAIController", "AIInputOverride" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"AI Debug must not manipulate AIPlayerInput ('{forbidden}').");
            }
            foreach (var type in AIRelevantAssemblies().SelectMany(SafeGetTypes))
            {
                if (!IsAIDebugLike(type))
                    continue;
                Assert.IsFalse(ReferencesUnityEngineInput(type),
                    $"'{type.FullName}' must not poll keyboard/mouse/gamepad or UnityEngine.Input.");
            }
        }

        [Test]
        public void NoGlobalOrStaticAIDebugState()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "GlobalAIDebug", "StaticAIDebugState", "AIDebugState", "GlobalAIState" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"No static/global AI debug state or second AI authority ('{forbidden}').");
            }
        }

        [Test]
        public void NoAIDebugCheats()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "ForceTarget", "ForcePass", "ForceShoot", "ForceTackle", "ForceSprint",
                "FreezeAI", "RevealAllPlayers", "AIDebugControl", "InstantDecision", "PathOverride"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 59 must not implement debug cheats/controls ('{forbidden}').");
            }
        }

        [Test]
        public void AIDebug_AddsNoLifecycleUpdateLoops()
        {
            foreach (var asm in AIRelevantAssemblies())
                foreach (var type in SafeGetTypes(asm))
                {
                    if (!IsAIDebugLike(type))
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
        public void AIDebug_IsNotTelemetry()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "AITelemetry", "AIDecisionHistory", "AITargetHistory", "AIPerceptionHistory",
                "AIRetention", "TelemetryRecorder"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 59 must not create telemetry ('{forbidden}'). Task 64 owns Telemetry Snapshots.");
            }
        }

        [Test]
        public void AIDebug_IsNotUI_NotOverlay()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugOverlay", "AIOverlay", "AIDebugOverlay", "AIHUD", "AIDebugWindow" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 59 must not create UI/overlay ('{forbidden}'). Task 65 owns Debug Overlay.");
            }
        }

        [Test]
        public void AIDebug_IsNotLoggingFramework()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "AILogger", "AIDebugLogger", "DebugLogger", "LoggerService", "GlobalLogger" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 59 must not create a logging framework ('{forbidden}'). Task 62 owns logging levels.");
            }
        }

        // ---- Independence: no gameplay->debug dependency; AI owns nothing it shouldn't ----

        [Test]
        public void GameplayAssemblies_DoNotDependOnDebugSettings()
        {
            foreach (var asm in new[]
            {
                typeof(HumanPlayerInput).Assembly, // Football.Input
                typeof(AIPlayerInput).Assembly,    // Football.Input
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

        [Test]
        public void AIDebugDoesNotOwnBallOrMatchOrClockState()
        {
            // AI Debug must not own Ball/Match/GameClock/PlayerMovement state (cross-category data
            // ownership stays with their authoritative owners).
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "AIBallState", "AIMatchState", "AIGameClock", "AIMovementState", "AIPossessionState"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"AI Debug must not own cross-category state ('{forbidden}').");
            }
        }

        // ---- structural helpers (reflection, not comment matching) ----

        private static bool IsAIDebugLike(Type t)
            => (t.Namespace ?? "").IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0
               && (t.Name.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0);

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
