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
    /// Task 118 — Move Input contract.
    ///
    /// Task 118 establishes (by verification, not by new infrastructure) that MOVEMENT INPUT is
    /// represented device-neutrally as a single <see cref="Vector2"/> MoveDirection that flows from
    /// the input providers through <see cref="IPlayerInput"/> (authoritative provider contract) to
    /// <see cref="InputFrame.MoveDirection"/> (authoritative device-neutral snapshot value), with no
    /// duplicate movement-input authority, no movement-processing abstractions, and no movement
    /// gameplay. Sprint is explicitly deferred to Task 119; movement gameplay is future work.
    ///
    /// These tests inspect the actual assembled types (reflection, not source-text matching).
    ///
    /// NOT under test: movement speed, acceleration, turning, sprint, stamina, physics, animation,
    /// controller/keyboard mapping, dead zones, sensitivity, remapping, buffering, priority, or
    /// simultaneous-input resolution — none of these exist or belong to Task 118.
    /// </summary>
    public class MoveInputContractTests
    {
        // The assemblies where a (possibly accidental) movement-input abstraction or processing
        // layer would necessarily live: Football.Core (contract/snapshot) and Football.Input
        // (providers). Football.Players (gameplay) is out of scope for input contract checks.
        private static IEnumerable<Assembly> MoveInputAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input
        }

        private static string[] GetAllTypeNames()
            => MoveInputAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static IEnumerable<Type> AllMoveInputTypes()
            => MoveInputAssemblies().SelectMany(SafeGetTypes);

        private static void AssertNoTypeNamed(IEnumerable<string> names, string[] forbidden, string why)
        {
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + f) || n == f),
                    $"'{f}' must not exist — {why}");
            }
        }

        // ---- single authority & contract coherence ----

        [Test]
        public void IPlayerInput_MoveDirection_IsReadOnlyVector2()
        {
            var p = typeof(IPlayerInput).GetProperty("MoveDirection");
            Assert.IsNotNull(p, "IPlayerInput must expose MoveDirection.");
            Assert.AreEqual(typeof(Vector2), p.PropertyType,
                "MoveDirection must be a Vector2 (device-neutral direction vector).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.MoveDirection is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void InputFrame_MoveDirection_MatchesContractType_IsReadOnly()
        {
            var contract = typeof(IPlayerInput).GetProperty("MoveDirection");
            var snapshot = typeof(InputFrame).GetProperty("MoveDirection");
            Assert.IsNotNull(snapshot, "InputFrame must expose MoveDirection.");
            Assert.AreEqual(contract.PropertyType, snapshot.PropertyType,
                "InputFrame.MoveDirection must be the same Vector2 type as IPlayerInput.MoveDirection.");
            Assert.IsNull(snapshot.SetMethod,
                "InputFrame.MoveDirection must be read-only (immutable snapshot).");
        }

        [Test]
        public void AllFourProviders_ExposeMoveDirection_AsVector2_OnTheSameContract()
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
                var p = t.GetProperty("MoveDirection");
                Assert.IsNotNull(p, $"'{name}' must expose MoveDirection.");
                Assert.AreEqual(typeof(Vector2), p.PropertyType,
                    $"'{name}' MoveDirection must be Vector2 (same semantics as the contract).");
            }
        }

        [Test]
        public void MoveDirection_HasSingleAuthority_NoDuplicateMovementInputAbstraction()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "MoveInput", "MoveInputProvider", "MoveInputReader", "MovementInputProvider",
                "MovementInputReader", "MoveDirectionSource", "MoveDirectionProvider",
                "MovementVector", "MovementInput", "MoveAxis", "MoveAxisPair", "MoveCommand",
                "MovementInputData", "MovementInputContract", "MoveInputSystem", "MovementInputSystem"
            }, "Movement input has ONE authority (IPlayerInput / InputFrame). No duplicate abstraction.");
        }

        // ---- device-neutrality ----

        [Test]
        public void MoveInputContract_ContainsNoDeviceDependencies()
        {
            var forbidden = new[]
            {
                "UnityEngine.Input", "UnityEngine.InputSystem", "UnityEngine.KeyCode",
                "InputAction", "InputControl", "Keyboard", "Gamepad", "Mouse"
            };
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static;

            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var f in t.GetFields(flags))
                {
                    Assert.IsFalse(forbidden.Contains(f.FieldType.FullName),
                        $"Contract type '{t.Name}' must not declare device field '{f.Name}' of type '{f.FieldType}'.");
                }
                foreach (var p in t.GetProperties(flags))
                {
                    Assert.IsFalse(forbidden.Contains(p.PropertyType.FullName),
                        $"Contract type '{t.Name}' must not declare device property '{p.Name}' of type '{p.PropertyType}'.");
                }
            }

            var ctor = typeof(InputFrame).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
            foreach (var param in ctor.GetParameters())
            {
                Assert.IsFalse(forbidden.Contains(param.ParameterType.FullName),
                    $"InputFrame constructor must not take device parameter '{param.Name}' of type '{param.ParameterType}'.");
            }
        }

        // ---- no processing abstractions manufactured ----

        [Test]
        public void MoveInputContract_AddsNoProcessingAbstractions()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "MovementNormalizer", "MoveNormalizer", "MoveDirectionNormalizer",
                "MovementSmoother", "MoveSmoother", "MovementFilter", "MoveFilter",
                "MoveDeadZone", "MoveDeadzone", "MovementDeadZone", "MovementDeadzone",
                "MoveSensitivity", "MovementSensitivity", "MovementRemapper", "MoveRemapper",
                "AnalogMagnitude", "MoveMagnitudeSource"
            }, "The provider path is RAW passthrough; Task 118 adds no normalization/smoothing/"
               + "dead-zone/sensitivity/remapping layer.");
        }

        // ---- scope boundaries ----

        [Test]
        public void MoveInput_SprintDeferredToTask119_NotImplementedHere()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SprintInput", "SprintHandler", "SprintCommand", "SprintSystem",
                "SprintProvider", "SprintModifier", "SprintState"
            }, "Sprint is Task 119 scope and must not be implemented in the input layer.");
        }

        [Test]
        public void MoveInput_NoMovementGameplayOrLocomotionImplemented()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "MovementSystem", "MovementController", "LocomotionSystem", "MovementDriver",
                "PlayerMovementController", "MovementEngine", "MovementStateMachine", "MovementAnimator"
            }, "Task 118 establishes movement INPUT only; movement gameplay/locomotion is future work.");
        }
    }
}