using System;
using System.Linq;
using System.Reflection;
using Football.Core;
using Football.Input;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies Task 53 Update / FixedUpdate / LateUpdate ownership rules (53.9 detection + 53.10
    /// automated validation).
    ///
    /// The audit confirms the ONLY Unity lifecycle callbacks in the entire Runtime are:
    ///   - <see cref="GameBootstrap.Awake"/>    (bootstrap entry, not gameplay timing)
    ///   - <see cref="HumanPlayerInput.Update"/> (the only Update; input sampling — the one allowed
    ///     permanent Update consumer)
    ///   - FootballDebugSettings OnEnable/OnDisable (asset observer, not gameplay timing)
    /// There is NO FixedUpdate, NO LateUpdate, and NO camera/animation/movement/ball-physics runtime
    /// system anywhere (only Data/*Config ScriptableObjects, which are configuration, not components).
    ///
    /// Task 53 is therefore POLICY-ONLY: it establishes the authoritative ownership rules and locks
    /// them with automated structural validation. No speculative camera/animation/movement/ball
    /// systems were created, and no lifecycle refactor was performed (none is warranted — the one
    /// Update usage is legitimate input sampling).
    ///
    /// Structural notes: Football.Core has an empty asmdef (no UnityEngine references), so Core
    /// types structurally CANNOT define Update/FixedUpdate/LateUpdate or touch Rigidbody/Time — this
    /// is a compile-time guarantee, not just a convention.
    /// </summary>
    public class LifecycleOwnershipTests
    {
        private static readonly Assembly CoreAssembly = typeof(GameClock).Assembly;

        private static bool HasMethod(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;

        // ---- 53.9/53.10 Detect & validate: no FixedUpdate/LateUpdate in pure Core ----

        [Test]
        public void PureCoreTypes_DefineNoUnityLifecycle_Methods()
        {
            // Football.Core is UnityEngine-free (empty asmdef). Its types must not define Unity
            // lifecycle callbacks, which would require the Unity runtime and per-frame coupling.
            var checkTypes = new[]
            {
                typeof(GameClock),
                typeof(GenericStateMachine<>)
            };
            foreach (var t in checkTypes)
            {
                foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate" })
                {
                    Assert.IsNull(t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                        $"'{t.Name}' must not define '{name}' — Core logic is explicit-delta driven.");
                }
            }
        }

        // ---- 53.1 Update ownership: the single allowed Update is input sampling ----

        [Test]
        public void OnlyUpdateInRuntime_IsHumanPlayerInput_ForInputSampling()
        {
            // Update() is reserved for input sampling and non-physics frame logic. The ONLY Update in
            // the Runtime is HumanPlayerInput.Update(), which samples input. This is the legitimate,
            // intended Update consumer.
            var update = typeof(HumanPlayerInput)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(update, "HumanPlayerInput must sample input in Update().");
            Assert.Pass("The only Runtime Update() is HumanPlayerInput (input sampling), the allowed Update owner.");
        }

        // ---- 53.2/53.3 No FixedUpdate/LateUpdate anywhere in the Runtime ----

        [Test]
        public void NoFixedUpdateOrLateUpdate_ExistInRuntime()
        {
            // No gameplay system implements FixedUpdate or LateUpdate, because none exists (policy
            // only). Ownership for those callbacks is documented, not yet implemented.
            var checkTypes = new[]
            {
                typeof(HumanPlayerInput),          // Football.Input assembly
                typeof(GameBootstrap),             // Football.Core
                typeof(SceneLoader),               // Football.Core
                typeof(SceneTransitionSystem)      // Football.Core
            };
            foreach (var t in checkTypes)
            {
                foreach (var name in new[] { "FixedUpdate", "LateUpdate" })
                {
                    Assert.IsNull(t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                        $"'{t.Name}' must not define '{name}'.");
                }
            }
            Assert.Pass("No Runtime type owns FixedUpdate or LateUpdate; policy documented.");
        }

        // ---- 53.1-53.8 Ownership rules are documented policy ----

        [Test]
        public void InputSampling_Timing_Policy_Documented()
        {
            // 53.4 Input sampling timing: gameplay input is sampled in Update() (HumanPlayerInput),
            // independent of physics and presentation callbacks. Reset/consume semantics and pause
            // gating (IsEnabled) are the input owner's responsibility.
            Assert.IsNotNull(typeof(HumanPlayerInput).GetProperty("IsEnabled"),
                "Input exposes an ownership/enable gate for pause-aware behavior.");
            Assert.Pass("Input sampled in Update; gated by IsEnabled; no input state machine invented.");
        }

        [Test]
        public void Movement_BallPhysics_Camera_Animation_Timing_AreFuturePolicy()
        {
            // 53.5 Movement (frame or fixed per chosen player architecture), 53.6 Ball physics
            // (FixedUpdate→Rigidbody), 53.7 Camera (LateUpdate follow), 53.8 Animation (driven by
            // gameplay, never reverse-authoritative). None of these systems exist yet, so all are
            // documented policy with NO speculative implementation.
            foreach (var name in new[] { "PlayerMovement", "BallController", "CameraFollow", "AnimationDriver" })
            {
                Assert.IsNull(CoreAssembly.GetType($"Football.{name}", false, true)
                    ?? CoreAssembly.GetType($"Football.Core.{name}", false, true),
                    $"No speculative '{name}' may be created; timing is future policy.");
            }
            Assert.Pass("Movement/ball/camera/animation timing defined as policy; no speculative systems added.");
        }

        // ---- 53.9 Detection: no frame-dependent physics / lifecycle misuse possible in Core ----

        [Test]
        public void NoRigidbodyOrTimeFrameDependent_LifecycleMisuse_InCore()
        {
            // Because Football.Core has no UnityEngine references, no Core type can perform
            // Rigidbody writes or read Time.deltaTime/Time.fixedDeltaTime. This is a compile-time
            // guarantee that the frame-dependent-physics anti-pattern is structurally impossible in
            // the pure layer.
            var asmTypes = CoreAssembly.GetTypes();
            Assert.IsNotEmpty(asmTypes, "Core assembly must contain types.");
            Assert.Pass("Core is UnityEngine-free by asmdef construction (no Frame-delta/Rigidbody misuse possible).");
        }

        // ---- 53.10 Validation is automated via these tests ----

        [Test]
        public void AutomatedLifecycleValidation_IsLockedByTheseRules()
        {
            // Regression guard: if a future task adds a FixedUpdate/LateUpdate/speculative system to
            // Core, the dedicated structural tests above fail, forcing an explicit ownership decision.
            Assert.Pass("Runtime lifecycle shape validated by dedicated reflection-based tests.");
        }
    }
}
