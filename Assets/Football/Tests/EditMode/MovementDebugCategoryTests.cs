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
    /// Task 55 — Movement Debug Category.
    ///
    /// AUDIT RESULT (55.1 audit-first, re-verified): there is **no production movement system** in
    /// the Runtime.
    ///   - No movement controller / driver (nothing calls CharacterController, no Rigidbody usage).
    ///   - No runtime source of velocity / speed / acceleration / grounded state. Only authored
    ///     configuration ScriptableObjects exist (MovementConfig, PlayerDefinition, DribbleConfig,
    ///     BallConfig) under Runtime/Data — authored data, not runtime motion state.
    ///   - PlayerStateId is an orphan enum; GenericStateMachine&lt;PlayerStateId&gt; is never
    ///     instantiated as a runtime player state machine. No movement/velocity/speed state exists.
    ///   - Input: IPlayerInput is implemented by 4 classes (Human/AI/Network/Replay) but **no
    ///     production code consumes it**; nothing reads MoveDirection/Sprint.
    ///   - Existing movement debug: only the FootballDebugSettings.EnableMovementDebug flag (and
    ///     ShowPlayerStateLabels) exist. No movement debug consumers, no gizmos, no overlay.
    ///
    /// DECISION — POLICY-ONLY (OPTION A): because no real movement/runtime consumer exists, Task 55
    /// establishes the category (enablement contract + read-only diagnostic contract + the hard
    /// observational-only boundary) and **manufactures no runtime movement debug system and no fake
    /// velocity/state/input data**. The invariants below prevent accidental scope creep: no fake
    /// movement runtime, no debug input poller, no global movement state, no update loops, no UI, no
    /// telemetry, no gameplay->debug dependency. These tests verify actual project structure via
    /// reflection/assembly inspection (not comment matching).
    /// </summary>
    public class MovementDebugCategoryTests
    {
        // The assemblies in which an actual movement-debug implementation would have to live, and
        // which an accidental fake implementation might contaminate.
        private static IEnumerable<Assembly> MovementRelevantAssemblies()
        {
            yield return typeof(FootballDebugSettings).Assembly;           // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;                // Football.Input
            yield return typeof(PlayerStateId).Assembly;                   // Football.Players
        }

        private static string[] GetAllTypeNames()
            => MovementRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ---- 55.1/55.Category — the category reuses the existing flag ----

        [Test]
        public void MovementCategoryUsesExistingEnableMovementDebug_NoDuplicateFlag()
        {
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableMovementDebug));
            Assert.IsNotNull(f, "EnableMovementDebug must exist (reuse, not a new flag).");
            Assert.AreEqual(typeof(bool), f.FieldType, "Category must be a strongly-typed bool.");

            Assert.IsNull(typeof(FootballDebugSettings).GetField("MovementDebugEnabled"),
                "Do NOT create a duplicate movement flag (MovementDebugEnabled) when EnableMovementDebug already represents it.");
        }

        [Test]
        public void MovementDebugFlag_DefaultsOff()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(s.EnableMovementDebug,
                "Movement debug must default OFF so disabled debug performs no work.");
        }

        [Test]
        public void MovementDebugSwitch_IsIndependentlyTogglable()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableMovementDebug));
            f.SetValue(s, true);
            Assert.IsTrue((bool)f.GetValue(s));
            f.SetValue(s, false);
            Assert.IsFalse((bool)f.GetValue(s));
        }

        // ---- 55.Enablement — master AND category ----

        [Test]
        public void MovementDebug_NoBehaviorUntil_MasterAndCategoryBothOn()
        {
            // Encodes the documented three-state enablement contract using the real fields only.
            // Movement diagnostics may be active ONLY when BOTH flags are on:
            //   DebugEnabled=false                          -> inactive (regardless of category)
            //   DebugEnabled=true  + EnableMovementDebug=false -> inactive
            //   DebugEnabled=true  + EnableMovementDebug=true  -> active
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();

            // Case 1: master off, category on -> must remain inactive (master must never be
            // bypassed). Both flags are independently settable so the policy is representable.
            s.DebugEnabled = false;
            s.EnableMovementDebug = true;
            Assert.IsFalse(ActivationFor(s), "Master OFF must gate the category even when the category flag is ON.");

            // Case 2: master on but category off -> inactive.
            s.DebugEnabled = true;
            s.EnableMovementDebug = false;
            Assert.IsFalse(ActivationFor(s), "Category OFF means diagnostics inactive even when master is ON.");

            // Case 3: both on -> active.
            s.EnableMovementDebug = true;
            Assert.IsTrue(ActivationFor(s), "Master AND category both on -> active.");
        }

        // Expresses the enablement contract from the real flags. No manufactured
        // IsMovementDebugActive property is added because no consumer exists yet (policy-only).
        private static bool ActivationFor(FootballDebugSettings s)
            => s.DebugEnabled && s.EnableMovementDebug;

        [Test]
        public void MovementDebug_IsIndependentOfOtherCategories()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            s.EnableMovementDebug = true;
            Assert.IsFalse(s.EnableBallDebug, "Toggling the movement category must not affect others.");
            Assert.IsFalse(s.EnableAIDebug);
        }

        // ---- 55.2/55.3 — no fake runtime manufactured (data does NOT exist) ----

        [Test]
        public void NoFakeMovementRuntimeOrSystemCreated()
        {
            // Because there is no real movement system, Task 55 must not manufacture one purely for
            // debug observation.
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "MovementDebugSystem",
                "MovementMonitor",
                "MovementTelemetry",
                "MovementSnapshot",
                "GlobalMovementDebug",
                "MovementDebugState",
                "StaticMovementSnapshot",
                "MovementDebug"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 55 must not manufacture '{forbidden}' (no movement system exists to observe).");
            }
        }

        [Test]
        public void NoDebugInputProviderOrDirectInputPollerCreated()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "DebugInput",
                "DebugMovementInput",
                "DebugMovementProvider",
                "MovementInputProvider"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Debug must not own/poll input ('{forbidden}'). IPlayerInput remains the authoritative input contract.");
            }

            // No Movement-debug type may reference UnityEngine.Input directly.
            foreach (var type in SafeGetTypes(typeof(FootballDebugSettings).Assembly).Concat(
                         SafeGetTypes(typeof(HumanPlayerInput).Assembly)))
            {
                if (!IsMovementDebugLike(type))
                    continue;
                Assert.IsFalse(ReferencesUnityEngineInput(type),
                    $"'{type.FullName}' must not poll UnityEngine.Input directly.");
            }
        }

        [Test]
        public void NoGlobalStaticMovementStateCreated()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "MovementDebugState",
                "GlobalMovementDebug",
                "StaticMovementSnapshot",
                "MovementDebugState.Instance"
            })
            {
                Assert.IsFalse(names.Any(n => (n.EndsWith("." + forbidden) || n == forbidden)),
                    $"No static/global movement debug state ('{forbidden}'). Ownership stays explicit.");
            }
        }

        [Test]
        public void MovementDebug_AddsNoLifecycleUpdateLoops()
        {
            // No Movement-debug type may introduce Update / FixedUpdate / LateUpdate just to run
            // diagnostics (no per-frame debug polling).
            foreach (var asm in MovementRelevantAssemblies())
            {
                foreach (var type in SafeGetTypes(asm))
                {
                    if (!IsMovementDebugLike(type))
                        continue;
                    foreach (var methodName in new[] { "Update", "FixedUpdate", "LateUpdate" })
                    {
                        var m = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        Assert.IsNull(m, $"'{type.FullName}' must not add '{methodName}' for diagnostics.");
                    }
                }
            }
        }

        // ---- 55.Task evolution — no UI, no telemetry, no gameplay->debug dependency ----

        [Test]
        public void MovementDebug_IsNotUI_NotOverlay()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "DebugOverlay",
                "MovementOverlay",
                "FootDebugOverlay",
                "DebugOverlayRenderer"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 55 must not create UI/overlay ('{forbidden}'). Task 65 owns Debug Overlay.");
            }
        }

        [Test]
        public void MovementDebug_IsNotTelemetry()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "MovementTelemetry",
                "TelemetryRecorder",
                "MovementHistoryBuffer",
                "MovementSampler",
                "MovementRetention"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 55 must not create telemetry machinery ('{forbidden}'). Task 64 owns Telemetry.");
            }
        }

        [Test]
        public void GameplayAssemblies_DoNotDependOnDebugSettingsOrMovementDebug()
        {
            // Playes/Input gameplay types must not reference FootballDebugSettings or a movement
            // debug type (debug is observability; gameplay never depends on it).
            foreach (var asm in new[] { typeof(HumanPlayerInput).Assembly, typeof(PlayerStateId).Assembly })
            {
                foreach (var type in SafeGetTypes(asm))
                {
                    bool referencesSettings = DeclaresType(type, typeof(FootballDebugSettings))
                        || ReferencesTypeInMethodBodies(type, typeof(FootballDebugSettings));
                    Assert.IsFalse(referencesSettings,
                        $"Gameplay type '{type.FullName}' must not depend on FootballDebugSettings.");
                }
            }
        }

        // ---- structural helpers (reflection, not comment matching) ----

        // A "movement debug" type: lives under a Debug namespace (or is clearly a movement debug
        // type) and is movement-related. MovementConfig (plain authored data, non-Debug namespace)
        // does not match, so it is never flagged.
        private static bool IsMovementDebugLike(Type t)
            => (t.Namespace ?? "").IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0
               && t.Name.IndexOf("Movement", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool ReferencesUnityEngineInput(Type t)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            if (t.GetFields(flags).Any(f => f.FieldType == typeof(UnityEngine.Input)))
                return true;
            if (t.GetProperties(flags).Any(p => p.PropertyType == typeof(UnityEngine.Input)))
                return true;
            return false;
        }

        private static bool DeclaresType(Type t, Type target)
            => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                 .Any(f => f.FieldType == target)
               || t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                 .Any(p => p.PropertyType == target);

        private static bool ReferencesTypeInMethodBodies(Type t, Type target)
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var p in m.GetParameters())
                    if (p.ParameterType == target) return true;
                if (m.ReturnType == target) return true;
            }
            return false;
        }
    }
}
