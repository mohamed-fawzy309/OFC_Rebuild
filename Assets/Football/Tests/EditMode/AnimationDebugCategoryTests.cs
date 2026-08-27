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
    /// Task 57 — Animation Debug Category.
    ///
    /// AUDIT RESULT (57.1 audit-first, re-verified): there is **no production animation system**.
    ///   - No Runtime/Animation folder; **zero** Runtime code references Animator / animator APIs
    ///     (no Play/CrossFade/SetBool/SetFloat/SetTrigger/GetCurrentAnimatorStateInfo/Playables).
    ///   - Animation asset folders (Controllers/Processed/Source) are EMPTY (only .meta files);
    ///     no .controller/.anim/.fbx anywhere in the project.
    ///   - Player.prefab carries only CharacterController — **no Animator component**.
    ///   - No runtime animation driver; no Animator parameters driven by code; no
    ///     gameplay-to-animation bridge; no Animation Events.
    ///   - Only authored configuration exists: AnimationConfig (blend times), a ScriptableObject in
    ///     Football.Data.
    ///   - Existing Animation debug: only FootballDebugSettings.EnableAnimationDebug (category) +
    ///     ShowPlayerStateLabels (visualization), both pure configuration.
    ///
    /// DECISION — POLICY-ONLY (Option A): no real Animator/animation system exists, so Task 57
    /// establishes the category (enablement contract + read-only diagnostic contract + the hard
    /// "Animator is presentation, gameplay is authority" boundary) and **manufactures no animation
    /// runtime, controller, bridge, snapshot, monitor, debug input, telemetry, overlay, logging,
    /// or throttling**. The invariants below prevent accidental scope creep via reflection /
    /// assembly inspection (not comment matching).
    /// </summary>
    public class AnimationDebugCategoryTests
    {
        // The compiled assemblies in which an accidental animation-debug/fake implementation could
        // appear.
        private static IEnumerable<Assembly> AnimationRelevantAssemblies()
        {
            yield return typeof(FootballDebugSettings).Assembly;           // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;                // Football.Input
            yield return typeof(PlayerStateId).Assembly;                   // Football.Players
        }

        private static string[] GetAllTypeNames()
            => AnimationRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ---- 57.1/Enablement — reuse the existing flag ----

        [Test]
        public void AnimationDebugCategoryUsesExistingEnableAnimationDebug_NoDuplicateFlag()
        {
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableAnimationDebug));
            Assert.IsNotNull(f, "EnableAnimationDebug must exist (reuse, not a new flag).");
            Assert.AreEqual(typeof(bool), f.FieldType, "Animation category must be a strongly-typed bool.");

            Assert.IsNull(typeof(FootballDebugSettings).GetField("AnimationDebugEnabled"),
                "Do NOT create a duplicate animation flag (AnimationDebugEnabled) when EnableAnimationDebug already represents it.");
        }

        [Test]
        public void AnimationDebugFlag_DefaultsOff()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(s.EnableAnimationDebug,
                "Animation debug must default OFF so disabled debug performs no work.");
        }

        [Test]
        public void AnimationDebugSwitch_IsIndependentlyTogglable()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableAnimationDebug));
            f.SetValue(s, true);
            Assert.IsTrue((bool)f.GetValue(s));
            f.SetValue(s, false);
            Assert.IsFalse((bool)f.GetValue(s));
        }

        [Test]
        public void AnimationDebug_NoBehaviorUntil_MasterAndCategoryBothOn()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();

            s.DebugEnabled = false;
            s.EnableAnimationDebug = true;
            Assert.IsFalse(IsActiveFor(s), "Master OFF must gate the animation category even when the category flag is ON.");

            s.DebugEnabled = true;
            s.EnableAnimationDebug = false;
            Assert.IsFalse(IsActiveFor(s), "Category OFF means diagnostics inactive even when master is ON.");

            s.EnableAnimationDebug = true;
            Assert.IsTrue(IsActiveFor(s), "Master AND category both on -> active.");
        }

        private static bool IsActiveFor(FootballDebugSettings s)
            => s.DebugEnabled && s.EnableAnimationDebug;

        [Test]
        public void AnimationDebug_IsIndependentOfOtherCategories()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            s.EnableAnimationDebug = true;
            Assert.IsFalse(s.EnableMovementDebug, "Toggling the animation category must not affect other categories.");
            Assert.IsFalse(s.EnableBallDebug);
        }

        [Test]
        public void NoShowStateLabelDuplication()
        {
            // ShowPlayerStateLabels already covers state-label visualization; do not introduce a
            // duplicate animation-labelling flag.
            var existing = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.ShowPlayerStateLabels));
            Assert.IsNotNull(existing, "ShowPlayerStateLabels remains the state-label visualization flag.");
            Assert.IsNull(typeof(FootballDebugSettings).GetField("ShowAnimationState"),
                "Do NOT create ShowAnimationState when an existing label/visualization flag already represents it.");
            Assert.IsNull(typeof(FootballDebugSettings).GetField("AnimationLabels"));
        }

        // ---- 57.1/57.3 — no fake animation runtime manufactured ----

        [Test]
        public void NoFakeAnimationRuntimeCreated()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "AnimationDebugSystem", "AnimationController", "PlayerAnimationController",
                "AnimatorController", "AnimationBridge", "AnimationSnapshot", "AnimationMonitor",
                "AnimatorDriver", "AnimationStateMachine", "AnimationStateManager"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 57 must not manufacture '{forbidden}' (no Animator/animation system exists to observe).");
            }
        }

        [Test]
        public void NoSecondStateMachineOrDuplicateState()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "AnimationDebugStateMachine", "AnimationDebugState" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 57 must not create a second state machine '{forbidden}' or duplicate gameplay state.");
            }
        }

        [Test]
        public void NoDebugAnimationControlOrCheats()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "AnimationDebugControl", "ForceAnimation", "AnimationSpeedOverride", "AnimationFreeze"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 57 must not implement debug animation control/cheats ('{forbidden}').");
            }
        }

        // ---- 57.2/57.4 — no Animator mutation / no authority inversion ----

        [Test]
        public void NoDebugAnimatorParameterWriter()
        {
            // Animator parameters are presentation, not gameplay truth. Debug must never write them.
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "AnimatorParameterWriter", "AnimationDebugParameter", "AnimationParameterWriter",
                "AnimationDebugTransition", "AnimationTransitionManager"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Debug must not mutate Animator parameters or transitions ('{forbidden}').");
            }
        }

        [Test]
        public void NoDebugAnimationInputOrDirectInputPoller()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugAnimationInput", "AnimationDebugInput", "AnimationDebugInputProvider" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Animation Debug must not read input ('{forbidden}').");
            }
            foreach (var type in AnimationRelevantAssemblies().SelectMany(SafeGetTypes))
            {
                if (!IsAnimationDebugLike(type))
                    continue;
                Assert.IsFalse(ReferencesUnityEngineInput(type),
                    $"'{type.FullName}' must not poll keyboard/mouse/gamepad or UnityEngine.Input.");
            }
        }

        [Test]
        public void NoGlobalOrStaticAnimationDebugState()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "GlobalAnimationState", "StaticAnimationDebugState", "AnimationDebugState", "GlobalAnimationDebug"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"No static/global animation debug state ('{forbidden}'). Ownership stays explicit.");
            }
        }

        [Test]
        public void AnimationDebug_AddsNoLifecycleUpdateLoops()
        {
            foreach (var asm in AnimationRelevantAssemblies())
                foreach (var type in SafeGetTypes(asm))
                {
                    if (!IsAnimationDebugLike(type))
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
        public void AnimationDebug_IsNotTelemetry()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "AnimationTelemetry", "AnimationHistoryBuffer", "AnimationSampler",
                "AnimationRetention", "AnimationReplay", "TelemetryRecorder"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 57 must not create telemetry machinery ('{forbidden}'). Task 64 owns Telemetry Snapshots.");
            }
        }

        [Test]
        public void AnimationDebug_IsNotUI_NotOverlay()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugOverlay", "AnimationOverlay", "AnimationDebugOverlay", "AnimationHUD" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 57 must not create UI/overlay ('{forbidden}'). Task 65 owns Debug Overlay.");
            }
        }

        [Test]
        public void AnimationDebug_IsNotLoggingFramework()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "AnimationLogger", "DebugLogger", "LoggerService", "GlobalLogger" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 57 must not create a logging framework ('{forbidden}'). Task 62 owns logging levels.");
            }
        }

        // ---- Independence: no gameplay->debug dependency, animation never authority ----

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

        private static bool IsAnimationDebugLike(Type t)
            => (t.Namespace ?? "").IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0
               && t.Name.IndexOf("Animation", StringComparison.OrdinalIgnoreCase) >= 0;

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
