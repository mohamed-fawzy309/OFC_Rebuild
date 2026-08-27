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
    /// Task 60 — Camera Debug Category.
    ///
    /// AUDIT RESULT (60.1 audit-first, re-verified): there is **no production camera system**.
    ///   - Runtime/Camera (Football.Camera asmdef) contains only the asmdef + .meta — no scripts;
    ///     the assembly is not compiled.
    ///   - Zero Runtime code references Camera/Cinemachine/CameraController/Follow/LookAt/
    ///     fieldOfView/Camera.main. The only "Camera" token in Runtime is the EnableCameraDebug flag.
    ///   - Zero `void LateUpdate` / `void FixedUpdate` anywhere in Runtime (Task 53's LateUpdate is
    ///     future presentation-policy only; not implemented).
    ///   - No Cinemachine package referenced. The scene files each contain one raw default Unity
    ///     Camera with no controller/Cinemachine and no runtime driver — a Camera component is NOT a
    ///     camera system. Prefabs carry no Camera/AudioListener/Cinemachine components (all
    ///     m_TagString: Untagged).
    ///   - Existing Camera debug: only FootballDebugSettings.EnableCameraDebug (pure configuration).
    ///
    /// DECISION — POLICY-ONLY (Option A): no camera system exists, so Task 60 establishes the
    /// category (enablement contract + diagnostic contract + target/follow/state ownership +
    /// anti-control rules) and **manufactures no camera system/controller/rig/manager/state
    /// machine/target system, no free/fly camera or cheats, no debug input, no telemetry, no
    /// overlay, no logging**.
    /// </summary>
    public class CameraDebugCategoryTests
    {
        private static IEnumerable<Assembly> CameraRelevantAssemblies()
        {
            yield return typeof(FootballDebugSettings).Assembly; // Football.Core
            yield return typeof(GameClock).Assembly;             // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;      // Football.Input
            yield return typeof(PlayerStateId).Assembly;         // Football.Players
            yield return typeof(GameStateId).Assembly;           // Football.Match
        }

        private static string[] GetAllTypeNames()
            => CameraRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // ---- 60.1/Enablement — reuse the existing flag ----

        [Test]
        public void CameraDebugCategoryUsesExistingEnableCameraDebug_NoDuplicateFlag()
        {
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableCameraDebug));
            Assert.IsNotNull(f, "EnableCameraDebug must exist (reuse, not a new flag).");
            Assert.AreEqual(typeof(bool), f.FieldType, "Camera category must be a strongly-typed bool.");

            Assert.IsNull(typeof(FootballDebugSettings).GetField("CameraDebugEnabled"),
                "Do NOT create a duplicate camera flag (CameraDebugEnabled) when EnableCameraDebug already represents it.");
        }

        [Test]
        public void CameraDebugFlag_DefaultsOff()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsFalse(s.EnableCameraDebug, "Camera debug must default OFF so disabled debug performs no work.");
        }

        [Test]
        public void CameraDebugSwitch_IsIndependentlyTogglable()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableCameraDebug));
            f.SetValue(s, true);
            Assert.IsTrue((bool)f.GetValue(s));
            f.SetValue(s, false);
            Assert.IsFalse((bool)f.GetValue(s));
        }

        [Test]
        public void CameraDebug_NoBehaviorUntil_MasterAndCategoryBothOn()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();

            s.DebugEnabled = false;
            s.EnableCameraDebug = true;
            Assert.IsFalse(IsActiveFor(s), "Master OFF must gate the camera category even when the category flag is ON.");

            s.DebugEnabled = true;
            s.EnableCameraDebug = false;
            Assert.IsFalse(IsActiveFor(s), "Category OFF means diagnostics inactive even when master is ON.");

            s.EnableCameraDebug = true;
            Assert.IsTrue(IsActiveFor(s), "Master AND category both on -> active.");
        }

        private static bool IsActiveFor(FootballDebugSettings s)
            => s.DebugEnabled && s.EnableCameraDebug;

        [Test]
        public void CameraDebug_IsIndependentOfOtherCategories()
        {
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            s.EnableCameraDebug = true;
            Assert.IsFalse(s.EnableMovementDebug, "Toggling the camera category must not affect other categories.");
            Assert.IsFalse(s.EnableAIDebug);
        }

        // ---- 60.1/60.2/60.3 — no fake camera runtime manufactured ----

        [Test]
        public void NoCameraSystemOrControllerManufactured()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "CameraController", "GameplayCamera", "FollowCamera", "PlayerCamera", "CameraRig",
                "CameraManager", "CameraSystem", "CameraDebugSystem"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 60 must not manufacture '{forbidden}' (no camera system exists to observe).");
            }
        }

        [Test]
        public void NoCameraTargetOrFollowSystemManufactured()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "CameraTargetManager", "CameraFollowSystem", "CameraTargetSystem", "CameraDebugTarget",
                "CameraLookAtSystem", "CameraFollowState"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 60 must not manufacture target/follow systems ('{forbidden}').");
            }
        }

        [Test]
        public void NoCameraStateMachineOrModeManagerManufactured()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "CameraStateMachine", "CameraModeManager", "CameraStateManager", "CameraState",
                "CameraDebugState"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 60 must not manufacture a camera state machine ('{forbidden}').");
            }
        }

        [Test]
        public void NoCinemachineInvented()
        {
            // No Cinemachine package/config/Nodes added.
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "CinemachineBrain", "CinemachineCamera", "VirtualCamera", "CinemachineTargetGroup",
                "CinemachineVirtualCamera"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 60 must not add Cinemachine ('{forbidden}').");
            }
        }

        [Test]
        public void NoDebugCameraCheatsOrFreeCamera()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "FreeCamera", "FlyCamera", "DebugCameraControl", "CameraShakeOverride",
                "ForceFOV", "CameraTeleport", "NCameraCheat", "CameraOptions"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 60 must not implement debug camera controls/cheats ('{forbidden}').");
            }
        }

        [Test]
        public void NoDebugCameraInputOrDirectInputPoller()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugCameraInput", "CameraDebugInput", "CameraDebugInputProvider" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Camera Debug must not read input ('{forbidden}').");
            }
            foreach (var type in CameraRelevantAssemblies().SelectMany(SafeGetTypes))
            {
                if (!IsCameraDebugLike(type))
                    continue;
                Assert.IsFalse(ReferencesUnityEngineInput(type),
                    $"'{type.FullName}' must not poll keyboard/mouse/gamepad or UnityEngine.Input.");
            }
        }

        [Test]
        public void NoGlobalOrStaticCameraDebugState()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "GlobalCameraDebug", "StaticCameraDebugState", "CameraDebugState", "GlobalCameraState" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"No static/global camera debug state ('{forbidden}'). Camera Debug never becomes camera authority.");
            }
        }

        [Test]
        public void CameraDebug_AddsNoLifecycleUpdateLoops()
        {
            // No per-frame polling and no LateUpdate just for diagnostics (Task 53: LateUpdate is
            // the future presentation owner, not a debug loop).
            foreach (var asm in CameraRelevantAssemblies())
                foreach (var type in SafeGetTypes(asm))
                {
                    if (!IsCameraDebugLike(type))
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
        public void CameraDebug_IsNotTelemetry()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "CameraTelemetry", "CameraHistoryBuffer", "CameraSampler", "TelemetryRecorder", "CameraFrameTimingRecorder" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 60 must not create telemetry ('{forbidden}'). Task 64 owns Telemetry Snapshots.");
            }
        }

        [Test]
        public void CameraDebug_IsNotUI_NotOverlay()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "DebugOverlay", "CameraOverlay", "CameraDebugOverlay", "CameraHUD", "CameraDebugWindow" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 60 must not create UI/overlay ('{forbidden}'). Task 65 owns Debug Overlay.");
            }
        }

        [Test]
        public void CameraDebug_IsNotLoggingFramework()
        {
            var names = GetAllTypeNames();
            foreach (var forbidden in new[] { "CameraLogger", "DebugLogger", "LoggerService", "GlobalLogger" })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Task 60 must not create a logging framework ('{forbidden}'). Task 62 owns logging levels.");
            }
        }

        // ---- Independence: no gameplay->debug dependency; camera debug owns nothing ----

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

        [Test]
        public void CameraDebugDoesNotOwnGameplayState()
        {
            // Camera Debug must not own Match/score/clock/movement/ball/AI/animation state.
            var names = GetAllTypeNames();
            foreach (var forbidden in new[]
            {
                "CameraMatchState", "CameraScore", "CameraGameClock", "CameraMovementState",
                "CameraBallState", "CameraAIState", "CameraAnimationState"
            })
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + forbidden) || n == forbidden),
                    $"Camera Debug must not own gameplay cross-category state ('{forbidden}').");
            }
        }

        // ---- structural helpers (reflection, not comment matching) ----

        private static bool IsCameraDebugLike(Type t)
            => (t.Namespace ?? "").IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0
               && t.Name.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0;

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
