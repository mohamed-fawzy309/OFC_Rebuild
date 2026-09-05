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
    /// Task 134 — Simultaneous-Input Handling input contract.
    ///
    ///     Simultaneous Input
    ///         = multiple input intents being present at the same input/sample
    ///           moment.
    ///     Simultaneous Input
    ///         ≠ Button Buffering (Task 132).
    ///     Simultaneous Input
    ///         ≠ Input Priority (Task 133).
    ///     Simultaneous Input
    ///         ≠ Gameplay Execution.
    ///
    ///     InputFrame
    ///         = representation of logical input values (a pure-data snapshot).
    ///
    /// The audit proved that this project has NO simultaneous-input resolution
    /// system and no runtime consumer:
    ///   - `InputFrame` is a documented PURE-DATA SNAPSHOT whose fields are
    ///     independent; every combination (Move+Sprint, multiple one-shots,
    ///     action + direction, Pause/Cancel/Interact + gameplay inputs, etc.) is
    ///     REPRESENTABLE simultaneously with NO suppression, gating, or resolution.
    ///   - No simultaneous resolver / combination policy / conflict system /
    ///     arbitration code exists anywhere.
    ///   - `InputFrame` is never read by production code, so whether multiple
    ///     gameplay actions EXECUTE simultaneously is a GAMEPLAY decision that is
    ///     NOT made here and is DEFERRED.
    ///   - `SwitchPlayer`+`SwitchDirection` and `Skill`+`SkillDirection` are
    ///     associated intent pairs that remain independently coherent.
    ///
    /// Conclusion: PASS WITH DEFERRED — input REPRESENTATION fully supports
    /// simultaneous coexistence; a global simultaneous-input RESOLUTION policy
    /// is NOT defined and is deferred (gameplay-owned). Task 134 makes NO
    /// production change and creates NO speculative resolver.
    ///
    /// These tests prove the DOCUMENTED EXISTING BEHAVIOR (independent
    /// coexistence of logical intents with no global resolver / suppression)
    /// and the ABSENCE of any speculative simultaneous-input system.
    /// </summary>
    public class SimultaneousInputContractTests
    {
        private static IEnumerable<Assembly> ProviderAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Core
            yield return typeof(HumanPlayerInput).Assembly;    // Input
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

        private static IEnumerable<string> SnapshotFieldsAndProperties(Type t)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static;
            return t.GetFields(flags).Where(f => !f.IsLiteral).Select(f => f.Name)
                    .Concat(t.GetProperties(flags).Select(p => p.Name));
        }

        // ---- 1. Multiple independent logical inputs coexist on the contract ----

        [Test]
        public void IndependentInputs_CoexistOnInputFrame_AndInterface()
        {
            // Continuous + one-shot + held must all coexist as separate members on
            // both the interface and the snapshot (NO suppression at representation).
            foreach (var name in new[]
                     { "MoveDirection", "Sprint", "Pass", "Shoot", "Tackle",
                       "SwitchPlayer", "Skill", "Interact", "Cancel", "Pause" })
            {
                var ifaceProp = typeof(IPlayerInput).GetProperty(name);
                var frameProp = typeof(InputFrame).GetProperty(name);
                Assert.IsNotNull(ifaceProp, $"IPlayerInput must expose '{name}'.");
                Assert.IsNotNull(frameProp, $"InputFrame must expose '{name}'.");
            }
        }

        [Test]
        public void InputFrame_CanRepresentSimultaneousOneShots()
        {
            // The snapshot can hold multiple one-shot raws at the same time.
            // E.g. Pass + Shoot + Tackle can all be true in one frame (raw intents).
            var frame = new InputFrame(
                moveDirection: Vector2.zero, lookDirection: Vector2.zero, sprint: false,
                pass: true, shoot: true, tackle: true,
                switchPlayer: false, switchDirection: Vector2.zero,
                skill: false, skillDirection: Vector2.zero,
                interact: false, cancel: false, pause: false);
            Assert.IsTrue(frame.Pass && frame.Shoot && frame.Tackle,
                "InputFrame must represent multiple simultaneous one-shot intents as independent bools.");
        }

        [Test]
        public void InputFrame_CanRepresentMovePlusSprint_AndPlusOneShot()
        {
            var frame = new InputFrame(
                moveDirection: new Vector2(1, 0), lookDirection: Vector2.zero, sprint: true,
                pass: true, shoot: false, tackle: false,
                switchPlayer: false, switchDirection: Vector2.zero,
                skill: false, skillDirection: Vector2.zero,
                interact: false, cancel: false, pause: false);
            Assert.IsTrue(frame.MoveDirection.x > 0 && frame.Sprint && frame.Pass,
                "MoveDirection + Sprint + a one-shot must coexist in InputFrame.");
        }

        // ---- 2. Associated direction pairs remain independently coherent ----

        [Test]
        public void AssociatedDirectionPairs_RemainCoherent()
        {
            // SwitchPlayer+SwitchDirection and Skill+SkillDirection stay
            // independently representable (zero means no direction).
            var frame = new InputFrame(
                moveDirection: Vector2.zero, lookDirection: Vector2.zero, sprint: false,
                pass: false, shoot: false, tackle: false,
                switchPlayer: true, switchDirection: new Vector2(0, 1),
                skill: true, skillDirection: new Vector2(1, 0),
                interact: false, cancel: false, pause: false);
            Assert.IsTrue(frame.SwitchPlayer && frame.SwitchDirection.y > 0,
                "Switch + SwitchDirection must coexist coherently.");
            Assert.IsTrue(frame.Skill && frame.SkillDirection.x > 0,
                "Skill + SkillDirection must coexist coherently.");
        }

        // ---- 3. No global simultaneous resolver / combination policy exists ----

        [Test]
        public void NoSimultaneousResolver_Exists()
        {
            var names = GetAllTypeNames();
            var forbidden = new[]
            {
                "SimultaneousInputManager", "InputConflictManager", "SimultaneousInputResolver",
                "InputCombinationManager", "MultiInputResolver", "InputArbitrator",
                "SimultaneousHandler", "SimultaneousResolution", "CoexistencePolicy",
                "InputCombination", "SimultaneousInputPolicy", "ResolveInputs"
            };
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — no speculative simultaneous-input framework is allowed.");
            }
        }

        // ---- 4. No arbitrary combination / suppression / ordering rule exists ----

        [Test]
        public void NoCombinationOrSuppressionPolicy_Exists()
        {
            var names = GetAllTypeNames();
            foreach (var kw in new[] { "FirstWins", "LastWins", "AllWins", "SuppressAction",
                                       "ActionSuppression", "ExclusiveAction", "MutualExclusion",
                                       "CombinationRule", "InputOrdering", "SimultaneousPolicy" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(kw)),
                    $"'{kw}' must not exist — no arbitrary first-wins/last-wins/suppression/ordering policy is defined.");
            }
        }

        // ---- 5. Task 133 (priority) and Task 132 (buffering) remain separate ----

        [Test]
        public void NoPriorityOrBuffering_LeakedIntoSimultaneous()
        {
            var names = GetAllTypeNames();
            foreach (var f in new[] { "PriorityManager", "InputPriorityManager", "InputArbitrator",
                                      "BufferManager", "InputBufferManager", "CommandQueue", "ActionQueue" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — priority (133) and buffering (132) stay separate from simultaneous (134).");
            }
        }

        // ---- 6. InputFrame remains a pure-data snapshot (no resolution state) ----

        [Test]
        public void InputFrame_IsPureDataSnapshot_NoResolutionState()
        {
            var members = SnapshotFieldsAndProperties(typeof(InputFrame)).ToList();
            Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains("selected")
                                         || m.ToLowerInvariant().Contains("resolve")
                                         || m.ToLowerInvariant().Contains("coexist")
                                         || m.ToLowerInvariant().Contains("combination")
                                         || m.ToLowerInvariant().Contains("priority")
                                         || m.ToLowerInvariant().Contains("handled")),
                "InputFrame (pure-data snapshot) must not carry resolution/selection/gating state.");
        }

        [Test]
        public void NoGameplayState_InInputFrame()
        {
            // No gameplay legality/state (no action executed/legal/blocked flags) in the input contract.
            var members = SnapshotFieldsAndProperties(typeof(InputFrame)).ToList();
            foreach (var kw in new[] { "Legal", "Executable", "Blocked", "Allowed", "Consumed",
                                       "PlayerState", "GameState", "BallPossession" })
            {
                Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains(kw.ToLowerInvariant())),
                    $"InputFrame must not expose gameplay state member '{kw}'.");
            }
        }

        // ---- 7. No device-specific simultaneous behavior in Core ----

        [Test]
        public void Core_IsDeviceNeutral_NoSimultaneousDeviceLogic()
        {
            // Core (IPlayerInput/InputFrame assembly) must not leak keyboard/controller/touch types
            // into any simultaneous handling (and none exists).
            var names = GetAllTypeNames();
            foreach (var kw in new[] { "Keyboard", "Mouse", "Joystick", "Gamepad", "Controller", "Touch", "Mobile" })
            {
                // Allow the neutral IPlayerInput? It does not contain device names; verify none leaks.
                Assert.IsFalse(names.Any(n => n.Contains(kw)),
                    $"Core must not leak device-specific type '{kw}'.");
            }
        }

        // ---- 8. No runtime consumer / gameplay execution in input processing ----

        [Test]
        public void NoGameplayConsumerOrExecution_Exists()
        {
            // No processing/consumption layer turns raw intents into executed actions.
            var names = GetAllTypeNames();
            foreach (var f in new[] { "InputProcessor", "InputConsumer", "ActionExecutor",
                                      "CommandExecutor", "SimultaneousProcessor", "InputController" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — no runtime consumer executes simultaneous intents (gameplay-owned, deferred).");
            }
        }

        // ---- 9. Logical action semantics unchanged / providers do not gate ----

        [Test]
        public void Providers_DoNotSuppressOrGateInputs()
        {
            foreach (var t in new[] { typeof(HumanPlayerInput), typeof(AIPlayerInput),
                                      typeof(ReplayPlayerInput), typeof(NetworkPlayerInput) })
            {
                var members = SnapshotFieldsAndProperties(t).ToList();
                foreach (var kw in new[] { "Suppress", "Gate", "Block", "Resolve", "Combination",
                                           "Exclusive", "Priority", "Handled", "Consumed" })
                {
                    Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains(kw.ToLowerInvariant())),
                        $"'{t.Name}' must not suppress/gate/resolve inputs (no member '{kw}').");
                }
            }
        }

        // ---- 10. Session of logical semantics unchanged (identity of IPlayerInput) ----

        [Test]
        public void LogicalActionSemantics_Unchanged()
        {
            var props = typeof(IPlayerInput).GetProperties().Select(p => p.Name).ToArray();
            foreach (var expected in new[] { "MoveDirection", "LookDirection", "Sprint", "Pass", "Shoot",
                                             "Tackle", "SwitchPlayer", "SwitchDirection", "Skill",
                                             "SkillDirection", "Interact", "Cancel", "Pause", "IsEnabled" })
            {
                Assert.IsTrue(props.Contains(expected), $"IPlayerInput must still expose '{expected}'.");
            }
            Assert.AreEqual(typeof(bool), typeof(IPlayerInput).GetProperty(nameof(IPlayerInput.Pass)).PropertyType);
        }
    }
}
