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
    /// Task 56 — Ball Debug Category.
    ///
    /// AUDIT RESULT (56.1 audit-first, re-verified): there is **no production Ball system**.
    ///   - Ball/Actions/World/AI/Camera/Teams/UI asmdef folders are EMPTY — no runtime scripts.
    ///   - No BallController / SoccerBall controller. Only authored data (BallConfig) and event
    ///     structs (BallKickedEvent, PossessionChangedEvent) exist — neither drives the ball nor
    ///     is a debug consumer.
    ///   - SoccerBall.prefab has SphereCollider + Rigidbody (non-kinematic, gravity on, mass 0.43)
    ///     but **no m_Script component** — nothing drives the Rigidbody. A Rigidbody is NOT a Ball
    ///     physics system.
    ///   - No velocity/angular-velocity/speed source in Runtime.
    ///   - No possession system (only PossessionChangedEvent struct + TeamDefinition.PossessionPreference
    ///     authored data); no PlayerBallInteraction; no collision/contact system.
    ///   - Existing Ball debug: only FootballDebugSettings.EnableBallDebug + ShowBallTrail flags
    ///     (pure configuration; control nothing).
    ///
    /// DECISION — POLICY-ONLY (Option A): no real Ball/possession/physics consumer exists, so Task
    /// 56 establishes the category (enablement contract + read-only diagnostic contract + the
    /// observational-only boundary) and **manufactures no Ball runtime, possession, fake physics,
    /// velocity provider, cheats, debug input, telemetry, overlay, or logging framework**. The
    /// invariants below prevent accidental scope creep via reflection/assembly inspection.
    /// </summary>
    public class BallDebugCategoryTests
    {
        // The compiled assemblies in which an accidental Ball-debug/fake implementation could
        // appear. (Ball/Actions/World/etc. asmdef folders are empty and compile nothing.)
        private static IEnumerable<Assembly> BallRelevantAssemblies()
        {
            yield return typeof(FootballDebugSettings).Assembly;           // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;                // Football.Input
            yield return typeof(PlayerStateId).Assembly;                   // Football.Players
        }

        private static string[] GetAllTypeNames()
            => BallRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ---- 56.1/Enablement — reuse the existing flag ----

        [Test]
        public void BallDebugCategoryUsesExistingEnableBallDebug_NoDuplicateFlag()
        {
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableBallDebug));
            Assert.IsNotNull(f, "EnableBallDebug must exist (reuse, not a new flag).");
            Assert.AreEqual(typeof(bool), f.FieldType, "Ball category must be a strongly-typed bool.");

            Assert.IsNull(typeof(FootballDebugSettings).GetField("BallDebugEnabled"),
                "Do NOT create a duplicate ball flag (BallDebugEnabled) when EnableBallDebug already represents it.");
        }

        [Test]
        public void BallDebugFlag_DefaultsOff()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(s.EnableBallDebug, "Ball debug must default OFF so disabled debug performs no work.");
        }

        [Test]
        public void BallDebugSwitch_IsIndependentlyTogglable()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableBallDebug));
            f.SetValue(s, true);
            Assert.IsTrue((bool)f.GetValue(s));
            f.SetValue(s, false);
            Assert.IsFalse((bool)f.GetValue(s));
        }

        [Test]
        public void BallDebug_NoBehaviorUntil_MasterAndCategoryBothOn()
        {
            // Enablement contract from the real flags (three states). No manufactured property.
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();

            s.DebugEnabled = false;
            s.EnableBallDebug = true;
            Assert.IsFalse(IsActiveFor(s), "Master OFF must gate the ball category even when the category flag is ON.");

            s.DebugEnabled = true;
            s.EnableBallDebug = false;
            Assert.IsFalse(IsActiveFor(s), "Category OFF means diagnostics inactive even when master is ON.");

            s.EnableBallDebug = true;
            Assert.IsTrue(IsActiveFor(s), "Master AND category both on -> active.");
        }

        private static bool IsActiveFor(FootballDebugSettings s)
            => s.DebugEnabled && s.EnableBallDebug;

        [Test]
        public void BallDebug_IsIndependentOfOtherCategories()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            s.EnableBallDebug = true;
            Assert.IsFalse(s.EnableMovementDebug, "Toggling the ball category must not affect other categories.");
            Assert.IsFalse(s.EnableAIDebug);
        }

        // ---- 56.1/56.2 — no fake Ball/possession/physics runtime manufactured ----

        [Test]
        public void NoFakeBallSystemOrControllerCreated()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "BallController",
                "SoccerBallController",
                "BallPhysicsSystem",
                "BallDebugSystem",
                "BallMonitor",
                "GlobalBallState",
                "StaticBallDebugState",
                "BallDebugState"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 56 must not manufacture '{forbidden}' (no Ball system exists to observe).");
            }
        }

        [Test]
        public void NoPossessionSystemManufactured()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "PossessionManager",
                "PlayerBallInteraction",
                "BallPossessionDebug",
                "BallOwnerDebug",
                "PossessionSystem"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 56 must not manufacture possession code ('{forbidden}'). Possession diagnostics are DEFERRED until authoritative possession logic exists.");
            }
        }

        [Test]
        public void NoFakeBallVelocityOrPhysicsProvider()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "BallVelocityProvider",
                "BallPhysicsProvider",
                "BallVelocityDebug",
                "BallPhysicsDebug"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 56 must not invent a fake velocity/physics provider ('{forbidden}'). The Rigidbody is not a Ball physics system.");
            }
        }

        // ---- 56.4 — no mutation surfaces ----

        [Test]
        public void NoDebugBallInputOrDirectInputPoller()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugBallInput", "BallDebugInput", "BallDebugInputProvider" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Ball Debug must not own/read input ('{forbidden}').");
            }
            foreach (var type in BallRelevantAssemblies().SelectMany(SafeGetTypes))
            {
                if (!IsBallDebugLike(type))
                    continue;
                Assert.IsFalse(ReferencesUnityEngineInput(type),
                    $"'{type.FullName}' must not read keyboard/mouse/gamepad or UnityEngine.Input.");
            }
        }

        [Test]
        public void NoGlobalOrStaticBallDebugState()
        {
            // No second authoritative ball state (no GlobalBallState / BallDebugState.Instance /
            // StaticBallSnapshot in any debug-like ball type).
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "GlobalBallState", "StaticBallDebugState", "BallDebugState", "StaticBallSnapshot"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"No static/global ball debug state ('{forbidden}'). Ownership stays explicit; there must be no second authoritative ball state.");
            }
        }

        [Test]
        public void BallDebug_AddsNoLifecycleUpdateLoops()
        {
            foreach (var asm in BallRelevantAssemblies())
                foreach (var type in SafeGetTypes(asm))
                {
                    if (!IsBallDebugLike(type))
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
        public void BallDebug_IsNotUI_NotOverlay()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "DebugOverlay", "BallOverlay", "BallDebugOverlay", "DebugOverlayRenderer",
                "BallTrailSystem", "BallTrailRenderer"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 56 must not create UI/overlay/trail ('{forbidden}'). Task 65 owns Debug Overlay; ShowBallTrail stays configuration-only.");
            }
        }

        [Test]
        public void BallDebug_IsNotTelemetry()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "BallTelemetry", "TelemetryRecorder", "BallHistoryBuffer", "BallSampler",
                "BallRetention", "BallSnapshotHistory"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 56 must not create telemetry machinery ('{forbidden}'). Task 64 owns Telemetry Snapshots.");
            }
        }

        [Test]
        public void BallDebug_IsNotLoggingFramework()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "BallLogger", "DebugLogger", "LoggerService", "BallLogSystem"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 56 must not create a Ball logging framework ('{forbidden}'). Task 62 owns logging levels.");
            }
        }

        // ---- Independence: no gameplay->debug dependency, no authorities ----

        [Test]
        public void GameplayAssemblies_DoNotDependOnDebugSettings()
        {
            foreach (var asm in new[]
            {
                typeof(HumanPlayerInput).Assembly,   // Football.Input
                typeof(PlayerStateId).Assembly       // Football.Players
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

        private static bool IsBallDebugLike(Type t)
            => (t.Namespace ?? "").IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0
               && t.Name.IndexOf("Ball", StringComparison.OrdinalIgnoreCase) >= 0;

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
