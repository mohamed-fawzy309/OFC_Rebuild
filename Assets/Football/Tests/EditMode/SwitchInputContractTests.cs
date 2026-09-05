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
    /// Task 123 — Switch-Player Input contract.
    ///
    /// Task 123 introduces, per the authoritative product requirement, a device-neutral
    /// PLAYER-SWITCH INPUT: a one-shot switch REQUEST plus an OPTIONAL directional intent.
    ///
    ///   - Switch without a direction  = "request smart defensive player selection".
    ///   - Switch with a direction     = "request directional player selection".
    ///
    /// The input contract carries only the REQUEST and the directional INTENT. It must NOT carry
    /// which player is finally selected — that is a separate gameplay (player-selection)
    /// responsibility and must not exist in the input layer.
    ///
    /// Representation: <see cref="IPlayerInput.SwitchPlayer"/> (one-shot bool pulse) and
    /// <see cref="IPlayerInput.SwitchDirection"/> (device-neutral <see cref="Vector2"/>,
    /// <see cref="Vector2.zero"/> = no direction). The same two fields live on
    /// <see cref="InputFrame"/> (immutable snapshot). Mobile swipe/drag and Controller
    /// D-Pad/analog are device-side sources translated by the provider into this neutral value;
    /// the exact device bindings are future device tasks, not implemented here.
    ///
    /// These tests inspect actual assembled types (reflection). They verify the INPUT CONTRACT
    /// only, never a player-selection algorithm.
    ///
    /// NOT under test: which player is selected, best-defender scoring, nearest-player selection,
    /// ball-aware selection, target ranking, controlled-player assignment, camera/UI/animation,
    /// definesse handling, dead zones, sensitivity, remapping, buffering (Task 132),
    /// priority (Task 133), simultaneous-input resolution (Task 134).
    /// </summary>
    public class SwitchInputContractTests
    {
        // Switch-input abstractions could only exist in these two assemblies
        // (contract/snapshot + providers).
        private static IEnumerable<Assembly> SwitchInputAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input
        }

        private static string[] GetAllTypeNames()
            => SwitchInputAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static void AssertNoTypeNamed(string[] names, string[] forbidden, string why)
        {
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + f) || n == f),
                    $"'{f}' must not exist — {why}");
            }
        }

        // ---- contract shape & coherence ----

        [Test]
        public void IPlayerInput_HasSwitchRequest_AsReadOnlyBool()
        {
            var p = typeof(IPlayerInput).GetProperty("SwitchPlayer");
            Assert.IsNotNull(p, "IPlayerInput must expose the switch request (SwitchPlayer).");
            Assert.AreEqual(typeof(bool), p.PropertyType,
                "SwitchPlayer must be a bool (one-shot switch request pulse).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.SwitchPlayer is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void IPlayerInput_HasSwitchDirection_AsReadOnlyVector2()
        {
            var p = typeof(IPlayerInput).GetProperty("SwitchDirection");
            Assert.IsNotNull(p, "IPlayerInput must expose the optional switch direction (SwitchDirection).");
            Assert.AreEqual(typeof(Vector2), p.PropertyType,
                "SwitchDirection must be a device-neutral Vector2 (zero = no direction).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.SwitchDirection is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void InputFrame_MatchesContract_ForRequestAndDirection()
        {
            var reqContract = typeof(IPlayerInput).GetProperty("SwitchPlayer");
            var reqSnap = typeof(InputFrame).GetProperty("SwitchPlayer");
            Assert.IsNotNull(reqSnap, "InputFrame must expose SwitchPlayer.");
            Assert.AreEqual(reqContract.PropertyType, reqSnap.PropertyType,
                "InputFrame.SwitchPlayer must be the same bool type as IPlayerInput.SwitchPlayer.");
            Assert.IsNull(reqSnap.SetMethod, "InputFrame.SwitchPlayer must be read-only.");

            var dirContract = typeof(IPlayerInput).GetProperty("SwitchDirection");
            var dirSnap = typeof(InputFrame).GetProperty("SwitchDirection");
            Assert.IsNotNull(dirSnap, "InputFrame must expose SwitchDirection.");
            Assert.AreEqual(dirContract.PropertyType, dirSnap.PropertyType,
                "InputFrame.SwitchDirection must be the same Vector2 type as IPlayerInput.SwitchDirection.");
            Assert.IsNull(dirSnap.SetMethod, "InputFrame.SwitchDirection must be read-only.");
        }

        [Test]
        public void AllFourProviders_ExposeSwitchRequest_AndDirection_OnTheSameContract()
        {
            var providers = SafeGetTypes(typeof(HumanPlayerInput).Assembly)
                .Where(t => t.IsClass && typeof(IPlayerInput).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "AIPlayerInput", "HumanPlayerInput", "NetworkPlayerInput", "ReplayPlayerInput" },
                providers,
                "Exactly the four known providers must implement IPlayerInput.");

            foreach (var name in providers)
            {
                var t = typeof(HumanPlayerInput).Assembly.GetType("Football.Input." + name);
                var req = t.GetProperty("SwitchPlayer");
                Assert.IsNotNull(req, $"'{name}' must expose SwitchPlayer.");
                Assert.AreEqual(typeof(bool), req.PropertyType, $"'{name}' SwitchPlayer must be bool.");
                Assert.AreEqual(false, req.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' SwitchPlayer must default to false (no request by default).");

                var dir = t.GetProperty("SwitchDirection");
                Assert.IsNotNull(dir, $"'{name}' must expose SwitchDirection.");
                Assert.AreEqual(typeof(Vector2), dir.PropertyType, $"'{name}' SwitchDirection must be Vector2.");
                Assert.AreEqual(Vector2.zero, dir.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' SwitchDirection must default to zero (no direction by default).");
            }
        }

        // ---- no-direction / direction semantics ----

        [Test]
        public void SwitchDirection_Zero_MeansNoDirection()
        {
            var dir = typeof(InputFrame).GetProperty("SwitchDirection");
            Assert.AreEqual(Vector2.zero, dir.GetValue(default(InputFrame)),
                "A default/zero SwitchDirection must represent 'no direction' (smart defensive selection request).");
            Assert.IsFalse((bool)typeof(InputFrame).GetProperty("SwitchPlayer").GetValue(default(InputFrame)),
                "A default InputFrame must carry NO switch request.");
        }

        [Test]
        public void SwitchDirection_CanRepresentNonZero_DeviceNeutralDirections()
        {
            var ctor = typeof(InputFrame).GetConstructors().Single();
            var f = (InputFrame)ctor.Invoke(new object[]
            {
                Vector2.zero, Vector2.zero,             // move, look
                false, false, false, false,               // sprint, pass, shoot, tackle
                true, new Vector2(0f, -1f),               // switchPlayer (true), switchDirection (down)
                false, Vector2.zero,                      // skill (false), skillDirection (none)
                false, false, false                        // interact, cancel, pause
            });
            Assert.IsTrue(f.SwitchPlayer);
            Assert.AreEqual(new Vector2(0f, -1f), f.SwitchDirection,
                "A non-zero SwitchDirection must be representable and device-neutral.");
            Assert.IsTrue(f.SwitchPlayer && f.SwitchDirection != Vector2.zero,
                "Switch-with-direction must be representable (request true + non-zero direction).");
        }

        // ---- single authority & no duplicate abstraction ----

        [Test]
        public void Switch_HasSingleAuthority_NoDuplicateSwitchAbstraction()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SwitchInput", "SwitchInputProvider", "SwitchInputReader", "SwitchReader",
                "SwitchCommand", "SwitchSource", "SwitchRequest", "SwitchRequestEvent", "SwitchButton",
                "SwitchFlag", "SwitchInputData", "SwitchInputState", "PlayerSwitchInput",
                "PlayerSwitchCommand", "SwitchPlayerInput"
            }, "Switch-Player input has ONE authority (IPlayerInput / InputFrame). No duplicate abstraction.");
        }

        // ---- transient one-shot pulse: no buffering/queue/priority machinery ----

        [Test]
        public void SwitchPlayer_IsTransient_NoBufferingQueueOrPriorityMachinery()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SwitchBuffer", "SwitchQueuedInput", "SwitchQueue", "SwitchEdgeDetector", "SwitchEdge",
                "SwitchPrioritizer", "SwitchPrioritySource", "SwitchHistory", "SwitchRetention",
                "SwitchStatePersister", "SwitchDebouncer", "SwitchCooldown"
            }, "Switch-Player is a TRANSIENT one-shot request with no buffering (Task 132), priority "
               + "(Task 133), edge machinery, cooldown, or history.");
        }

        // ---- player-selection gameplay must NOT be input ----

        [Test]
        public void SwitchContract_MustNotCarryPlayerSelectionState()
        {
            var forbidden = new[]
            {
                "SelectedPlayer", "ControlledPlayer", "TargetPlayer", "NearestPlayer",
                "BestDefender", "SelectionResult", "SelectionScore", "PlayerIndex",
                "SwitchCandidate", "SwitchTarget", "SelectedTeammate"
            };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var name in forbidden)
                {
                    Assert.IsNull(t.GetProperty(name),
                        $"'{t.Name}' must NOT carry player-selection state '{name}' — final player selection is gameplay, not input.");
                }
            }
        }

        // ---- no selection algorithm / no device leakage in the contract ----

        [Test]
        public void NoPlayerSelectionAlgorithm_OrDeviceTypes_InTheContract()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "PlayerSelectionSystem", "PlayerSelectionAlgorithm", "NearestPlayerResolver",
                "BestDefenderScorer", "DefensiveSuitabilityScorer", "TargetRanker", "BallAwareSelection",
                "ControlledPlayerAssigner", "SmartSwitchResolver", "DirectionalSwitchResolver"
            }, "Player-selection scoring/targeting is game PLAY responsibility; it must not live in Task 123 input.");

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var forbiddenDevices = new[]
            {
                "UnityEngine.InputSystem.Keyboard", "UnityEngine.InputSystem.Gamepad",
                "UnityEngine.InputSystem.Mouse", "UnityEngine.InputSystem.Touchscreen",
                "UnityEngine.InputSystem.InputAction", "UnityEngine.InputSystem.InputControl",
                "UnityEngine.KeyCode", "UnityEngine.Touch"
            };
            foreach (var p in typeof(InputFrame).GetProperties(flags))
            {
                Assert.IsFalse(forbiddenDevices.Contains(p.PropertyType.FullName),
                    $"InputFrame must not contain device type '{p.PropertyType}' on '{p.Name}'.");
            }
        }
    }
}