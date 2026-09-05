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
    /// Task 119 — Sprint Input contract.
    ///
    /// Task 119 establishes (by verification, not by new infrastructure) that SPRINT INPUT is a
    /// device-neutral, continuous, HELD player-intent signal (`bool`) that flows from the input
    /// providers through <see cref="IPlayerInput.Sprint"/> (authoritative provider contract) to
    /// <see cref="InputFrame.Sprint"/> (authoritative device-neutral snapshot field), with exactly
    /// one semantic authority, no buffering/edge/history machinery, and sprint gameplay metadata
    /// excluded from the input contract. Sprint MECHANICS (speed, stamina, acceleration, state)
    /// are not input and are not implemented.
    ///
    /// These tests inspect actual assembled types (reflection, not source-text matching).
    ///
    /// NOT under test: sprint speed, sprint acceleration, stamina (consumption/regen), movement
    /// state changes, locomotion, animation, physics, controller support, keyboard remapping,
    /// dead zones, sensitivity, buffering (Task 132), priority (Task 133), or simultaneous-input
    /// resolution (Task 134).
    /// </summary>
    public class SprintInputContractTests
    {
        // Sprint input abstractions/mechanics could only exist in these two assemblies
        // (contract/snapshot + providers). Football.Players is excluded: PlayerStateId.Sprint
        // is an (unused) gameplay-state enum, not sprint input.
        private static IEnumerable<Assembly> SprintInputAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input
        }

        private static string[] GetAllTypeNames()
            => SprintInputAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static IEnumerable<Type> SafeGetTypesFull()
            => SprintInputAssemblies().SelectMany(a => SafeGetTypes(a));

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
        public void IPlayerInput_Sprint_IsReadOnlyBool()
        {
            var p = typeof(IPlayerInput).GetProperty("Sprint");
            Assert.IsNotNull(p, "IPlayerInput must expose Sprint.");
            Assert.AreEqual(typeof(bool), p.PropertyType,
                "Sprint must be a bool (device-neutral held intent signal).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.Sprint is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void InputFrame_Sprint_MatchesContractType_IsReadOnly()
        {
            var contract = typeof(IPlayerInput).GetProperty("Sprint");
            var snapshot = typeof(InputFrame).GetProperty("Sprint");
            Assert.IsNotNull(snapshot, "InputFrame must expose Sprint.");
            Assert.AreEqual(contract.PropertyType, snapshot.PropertyType,
                "InputFrame.Sprint must be the same bool type as IPlayerInput.Sprint.");
            Assert.IsNull(snapshot.SetMethod,
                "InputFrame.Sprint must be read-only (immutable snapshot).");
        }

        [Test]
        public void AllFourProviders_ExposeSprint_AsBool_OnTheSameContract()
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
                var p = t.GetProperty("Sprint");
                Assert.IsNotNull(p, $"'{name}' must expose Sprint.");
                Assert.AreEqual(typeof(bool), p.PropertyType,
                    $"'{name}' Sprint must be bool (same semantics as the contract).");
                Assert.AreEqual(false, p.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' Sprint must default to false (held input absent by default).");
            }
        }

        // ---- single authority & no duplicate abstraction ----

        [Test]
        public void Sprint_HasSingleAuthority_NoDuplicateSprintAbstraction()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SprintInput", "SprintInputProvider", "SprintInputReader", "SprintReader",
                "SprintSource", "SprintRequest", "SprintFlag", "SprintCommand",
                "SprintInputData", "SprintInputContract", "SprintEntity", "SprintInputState"
            }, "Sprint input has ONE authority (IPlayerInput / InputFrame). No duplicate abstraction.");
        }

        // ---- held intent signal: no edge/buffer/history machinery ----

        [Test]
        public void SprintInput_AddsNoBufferingEdgeOrHistoryMachinery()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SprintBuffer", "SprintQueuedInput", "SprintEdgeDetector", "SprintEdge",
                "SprintHistoryBuffer", "SprintHistory", "SprintRetention", "SprintStatePersister",
                "SprintPrioritySource", "SprintPrecedence"
            }, "Sprint is a continuous HELD signal with no edge detection, buffering (Task 132), "
               + "priority (Task 133), or history.");
        }

        // ---- intent, not gameplay state / metadata ----

        [Test]
        public void SprintContract_AddsNoSprintGameplayMetadata()
        {
            var forbidden = new[]
            {
                "SprintSpeed", "SprintAcceleration", "SprintStamina", "Stamina",
                "SprintDuration", "SprintCooldown", "SprintState", "IsSprinting", "CurrentStamina"
            };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var name in forbidden)
                {
                    Assert.IsNull(t.GetProperty(name),
                        $"'{t.Name}' must not expose sprint-gameplay metadata '{name}' (mechanics are not input).");
                }
            }
        }

        [Test]
        public void Sprint_IsPlayerIntent_NoSprintGameplaySystemImplemented()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SprintSystem", "SprintController", "SprintStateMachine", "SprintMechanic",
                "SprintMovement", "StaminaSystem", "StaminaController", "SprintManager"
            }, "Sprint INPUT is established; sprint/mechanics gameplay (speed, stamina, state) does not exist.");
        }
    }
}