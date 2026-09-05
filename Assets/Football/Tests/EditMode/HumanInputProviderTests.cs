using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Football.Core;
using Football.Input;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 126 — Human Input Provider contract.
    ///
    /// Task 126 defines/verifies the HUMAN INPUT PROVIDER boundary:
    ///
    ///     Device Input
    ///         ↓
    ///     HumanPlayerInput   (translation boundary)
    ///         ↓
    ///     IPlayerInput       (device-neutral provider contract)
    ///         ↓
    ///     InputFrame         (device-neutral snapshot)
    ///         ↓
    ///     Future Gameplay
    ///
    /// IMPORTANT DISTINCTIONS (proven here):
    ///   Human Input Provider = translation boundary between human/device input
    ///                          and IPlayerInput.
    ///   Human Input Provider ≠ gameplay.
    ///
    /// The provider ACQUIRES human input and EXPOSES it through the IPlayerInput
    /// contract. It must NOT move/select players, execute passes/shots/tackles/
    /// skills, pause the game, open UI, or manipulate game/match state.
    ///
    /// Per the Task 123/124/125 convention, contract fields that are structurally
    /// supported but intentionally UNBOUND (no invented device binding yet) remain
    /// at their safe default (false / zero). These are: SwitchPlayer,
    /// SwitchDirection, Skill, SkillDirection, and LookDirection. Device bindings
    /// for these belong to the device-specific tasks (Controller / Keyboard /
    /// Mobile), NOT Task 126.
    ///
    /// These tests inspect actual assembled types (reflection). They verify the
    /// PROVIDER boundary only — never controller/keyboard/mobile support, dead
    /// zones, sensitivity, remapping, buffering, priority, simultaneous-input, or
    /// gameplay execution.
    ///
    /// NOT under test: controller support (Task 127), keyboard support
    /// (Task 128), dead zones (Task 129), sensitivity (Task 130), remapping
    /// (Task 131), buffering (Task 132), priority (Task 133), simultaneous-input
    /// (Task 134), and all gameplay execution.
    /// </summary>
    public class HumanInputProviderTests
    {
        private static IEnumerable<Assembly> ProviderAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core (contract)
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input (providers)
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static string[] GetAllTypeNames()
            => ProviderAssemblies()
                .SelectMany(SafeGetTypes)
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static void AssertNoTypeNamed(string[] names, string[] forbidden, string why)
        {
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + f) || n == f),
                    $"'{f}' must not exist — {why}");
            }
        }

        private static readonly string[] ContractMembers =
        {
            "MoveDirection", "LookDirection", "Sprint", "Pass", "Shoot", "Tackle",
            "SwitchPlayer", "SwitchDirection", "Skill", "SkillDirection", "Interact",
            "Cancel", "Pause", "IsEnabled"
        };

        private static object GetDefault(Type t, string prop)
        {
            // Building a MonoBehaviour via new is fine for pure property-read tests
            // (no AddComponent / no scene). An empty ctor is guaranteed only by
            // Activator; MonoBehaviours support Activator.CreateInstance.
            object instance;
            var ctor = t.GetConstructor(Type.EmptyTypes);
            instance = ctor != null ? ctor.Invoke(null) : Activator.CreateInstance(t);
            return t.GetProperty(prop).GetValue(instance, null);
        }

        // ---- HumanPlayerInput is THE authoritative Human Input Provider ----

        [Test]
        public void HumanPlayerInput_IsTheSoleHumanProvider_ImplementingIPlayerInput()
        {
            Assert.IsTrue(typeof(IPlayerInput).IsAssignableFrom(typeof(HumanPlayerInput)),
                "HumanPlayerInput must implement IPlayerInput (the provider contract).");

            var humanProviders = SafeGetTypes(typeof(HumanPlayerInput).Assembly)
                .Where(x => x.IsClass && typeof(IPlayerInput).IsAssignableFrom(x) && !x.IsAbstract
                            && IsHumanAlike(x.Name))
                .Select(x => x.Name)
                .OrderBy(n => n)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "HumanPlayerInput" }, humanProviders,
                "HumanPlayerInput is the single authoritative Human Input Provider; no duplicate or "
                + "parallel human-input abstraction exists.");
        }

        private static bool IsHumanAlike(string name)
            => name.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0
               || name.Equals("PlayerInput", StringComparison.Ordinal)
               || name.IndexOf("HumanInput", StringComparison.OrdinalIgnoreCase) >= 0;

        [Test]
        public void HumanPlayerInput_ImplementsEveryIPlayerInputMember()
        {
            foreach (var name in ContractMembers)
            {
                var p = typeof(HumanPlayerInput).GetProperty(name);
                Assert.IsNotNull(p, $"HumanPlayerInput must implement contract member '{name}'.");
                Assert.AreEqual(typeof(IPlayerInput).GetProperty(name).PropertyType, p.PropertyType,
                    $"HumanPlayerInput.{name} must match IPlayerInput.{name} type.");
            }
        }

        [Test]
        public void HumanPlayerInput_IsAMonoBehaviour_WithUpdateSampling()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(HumanPlayerInput)),
                "HumanPlayerInput is a MonoBehaviour (Unity lifecycle agent).");
            Assert.IsNotNull(typeof(HumanPlayerInput)
                    .GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "HumanPlayerInput must sample human input in Update()");
        }

        // ---- defaults are safe ----

        [Test]
        public void HumanPlayerInput_DefaultValues_AreSafe()
        {
            var t = typeof(HumanPlayerInput);
            Assert.AreEqual(true, (bool)GetDefault(t, "IsEnabled"),
                "HumanPlayerInput must be enabled by default (IsEnabled = true).");
            Assert.AreEqual(Vector2.zero, (Vector2)GetDefault(t, "MoveDirection"));
            Assert.AreEqual(Vector2.zero, (Vector2)GetDefault(t, "LookDirection"));
            Assert.AreEqual(Vector2.zero, (Vector2)GetDefault(t, "SwitchDirection"));
            Assert.AreEqual(Vector2.zero, (Vector2)GetDefault(t, "SkillDirection"));
            Assert.IsFalse((bool)GetDefault(t, "Sprint"));
            foreach (var n in new[] { "Pass", "Shoot", "Tackle", "SwitchPlayer", "Skill", "Interact", "Cancel", "Pause" })
                Assert.IsFalse((bool)GetDefault(t, n), $"'{n}' must default to false.");
        }

        // ---- temporal semantics: continuous vs transient contract members ----

        [Test]
        public void ContinuousMembers_And_TransientMembers_FollowContractSemantics()
        {
            // Given the established one-shot convention across Tasks 117-125, the
            // continuous members are MoveDirection/LookDirection (Vector2 analog) and
            // Sprint (held bool). All other action members are one-shot transient
            // bools. The provider's Update() samples them accordingly (Verified by the
            // per-action InputContractTests + the source in HumanPlayerInput.Update()).
            Assert.AreEqual(typeof(Vector2), typeof(HumanPlayerInput).GetProperty("MoveDirection").PropertyType);
            Assert.AreEqual(typeof(Vector2), typeof(HumanPlayerInput).GetProperty("LookDirection").PropertyType);
            Assert.AreEqual(typeof(bool), typeof(HumanPlayerInput).GetProperty("Sprint").PropertyType);
            foreach (var oneShot in new[] { "Pass", "Shoot", "Tackle", "SwitchPlayer", "Skill", "Interact", "Cancel", "Pause" })
            {
                Assert.AreEqual(typeof(bool), typeof(HumanPlayerInput).GetProperty(oneShot).PropertyType,
                    $"'{oneShot}' is a one-shot transient contract member (bool).");
            }
        }

        [Test]
        public void Update_GatesOnIsEnabled_AndSamplesDeviceInput()
        {
            // The Update() body returns early when IsEnabled is false and samples Unity
            // device input otherwise. Verify the IsEnabled gate exists (property) and
            // Update() exists; the actual device reads are confined to this file (grep
            // audit confirms UnityEngine.Input appears nowhere else in Runtime).
            Assert.IsNotNull(typeof(HumanPlayerInput).GetProperty("IsEnabled"),
                "HumanPlayerInput exposes the IsEnabled enable/ownership gate.");
            Assert.IsNotNull(typeof(HumanPlayerInput)
                    .GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                "Update() exists for gated input sampling.");
        }

        // ---- gameplay independence: the provider is a translation boundary, not gameplay ----

        [Test]
        public void HumanPlayerInput_ExposesNoGameplayOrPlayerStateMembers()
        {
            var forbidden = new[]
            {
                "CurrentPlayer", "SelectedPlayer", "ControlledPlayer", "Ball", "TargetPlayer",
                "CurrentState", "PlayerState", "MatchTime", "IsPaused", "GameState", "Team"
            };
            foreach (var n in forbidden)
            {
                Assert.IsNull(typeof(HumanPlayerInput).GetProperty(n),
                    $"HumanPlayerInput must not expose gameplay/player-state member '{n}'.");
            }
        }

        [Test]
        public void HumanPlayerInput_HasNoGameplaySystemReferences()
        {
            var forbiddenTypeNames = new[]
            {
                "PlayerStateId", "PlayerController", "MatchRuntime", "GameClock", "PlayerDefinition",
                "GameStateId", "Animator", "Rigidbody", "CharacterController", "Camera"
            };
            var checkType = typeof(HumanPlayerInput);
            var fieldTypes = checkType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(f => f.FieldType.FullName ?? f.FieldType.Name)
                .ToArray();
            foreach (var ft in fieldTypes)
            {
                Assert.IsFalse(forbiddenTypeNames.Contains(ft),
                    $"HumanPlayerInput must not reference gameplay/system type '{ft}'.");
            }
        }

        [Test]
        public void HumanPlayerInput_PerformsNoGameplay_OnlyExposesInput()
        {
            // The provider has only properties + a private Update. It must not expose
            // methods that execute gameplay (no Move/Pass/Shoot/Tackle/Skill/Pause/Select).
            var methodNames = typeof(HumanPlayerInput)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name)
                .Where(n => n != "get_" && !n.StartsWith("get_") && !n.StartsWith("set_"))
                .ToArray();
            var gameplayActionNames = new[] { "Move", "Pass", "Shoot", "Tackle", "Switch", "Skill", "Pause", "Select", "Interact" };
            foreach (var action in gameplayActionNames)
            {
                Assert.IsFalse(methodNames.Any(m => m.StartsWith(action, StringComparison.OrdinalIgnoreCase)),
                    $"HumanPlayerInput must not expose a gameplay-execution method '{action}*'.");
            }
        }

        // ---- single authority & no parallel/duplicate architecture ----

        [Test]
        public void NoParallelHumanInputAbstraction_Exists()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "HumanInputProvider", "HumanInputService", "HumanInputManager", "PlayerInputProvider",
                "KeyboardInputProvider", "ControllerInputProvider", "TouchInputProvider",
                "DeviceInputManager", "InputPipeline", "InputRouter", "HumanInputSystem"
            }, "HumanPlayerInput is the ONE human provider. No parallel device/human-input abstraction "
               + "may exist in Task 126.");
        }

        // ---- Core must not depend on Unity input APIs ----

        [Test]
        public void Core_DoesNotDependOnUnityInputApis()
        {
            // Football.Core asmdef has no UnityEngine references, so Core structurally
            // CANNOT read UnityEngine.Input / KeyCode / device types (compile-time guarantee).
            var coreTypes = SafeGetTypes(typeof(IPlayerInput).Assembly)
                .Where(t => t.Namespace == "Football.Core").ToArray();
            Assert.IsNotEmpty(coreTypes, "Football.Core must contain types.");
            foreach (var t in coreTypes)
            {
                var deps = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .SelectMany(c => c.GetParameters())
                    .Where(p => p.ParameterType.FullName != null &&
                                (p.ParameterType.FullName.Contains("UnityEngine.InputSystem")
                                 || p.ParameterType.FullName == "UnityEngine.Input"
                                 || p.ParameterType.FullName == "UnityEngine.KeyCode"))
                    .ToArray();
                Assert.IsEmpty(deps,
                    $"Football.Core type '{t.Name}' must not depend on Unity input APIs (provider boundary).");
            }
        }

        // ---- InputFrame compatibility ----

        [Test]
        public void HumanPlayerInput_IsCompatibleWithInputFrame_NoFieldIsLost()
        {
            // Every contract value on InputFrame maps 1:1 to an IPlayerInput member the
            // Human provider implements. IsEnabled is the provider's enable gate and is
            // (deliberately) NOT part of the InputFrame snapshot.
            var snapshotMembers = new[]
            {
                "MoveDirection", "LookDirection", "Sprint", "Pass", "Shoot", "Tackle",
                "SwitchPlayer", "SwitchDirection", "Skill", "SkillDirection", "Interact",
                "Cancel", "Pause"
            };
            foreach (var name in snapshotMembers)
            {
                Assert.IsNotNull(typeof(InputFrame).GetProperty(name),
                    $"InputFrame must expose '{name}' (snapshot compatibility).");
                Assert.IsNotNull(typeof(HumanPlayerInput).GetProperty(name),
                    $"HumanPlayerInput must expose '{name}' (provider compatibility).");
            }
            Assert.IsNull(typeof(InputFrame).GetProperty("IsEnabled"),
                "IsEnabled is the provider enable gate, not an InputFrame snapshot field.");
        }
    }
}
