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
    /// Task 122 — Tackle Input contract.
    ///
    /// Task 122 establishes (by verification, NOT new infrastructure) that TACKLE INPUT is a
    /// device-neutral, TRANSIENT one-shot command pulse ("the player requested a tackle") that flows
    /// from the input providers through <see cref="IPlayerInput.Tackle"/> (authoritative provider
    /// contract) to <see cref="InputFrame.Tackle"/> (authoritative device-neutral snapshot field),
    /// with exactly one semantic authority, no buffering/queueing/priority machinery, and NO
    /// tackle-gameplay metadata in the input contract. Tackle gameplay (movement, collision, range,
    /// success/failure, ball recovery, possession changes, foul/card logic, animation, physics,
    /// cooldown, stamina, state) does not exist and is not implemented. PlayerStats.Defending
    /// (incl. StandingTackle/SlidingTackle) is PLAYER DATA; AnimationConfig.TackleBlendTime is
    /// GAMEPLAY TUNING; neither belongs to Tackle Input.
    ///
    /// These tests inspect actual assembled types (reflection, not source-text matching).
    ///
    /// NOT under test: tackle range/force/power/direction/timing, tackle success/failure, collision,
    /// ball recovery, possession changes, foul/card behavior, tackle targeting, tackle animation,
    /// tackle physics, tackle cooldown/stamina/state, controller/keyboard support, dead zones,
    /// sensitivity, remapping, buffering (Task 132), priority (Task 133), or simultaneous-input
    /// resolution (Task 134).
    /// </summary>
    public class TackleInputContractTests
    {
        // Tackle-input abstractions/mechanics could only exist in these two assemblies
        // (contract/snapshot + providers). Football.Data (DefendingStats ratings, Task 72) is a
        // separate player-data concern and already covered by DefendingTests.
        private static IEnumerable<Assembly> TackleInputAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input
        }

        private static string[] GetAllTypeNames()
            => TackleInputAssemblies()
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
        public void IPlayerInput_Tackle_IsReadOnlyBool()
        {
            var p = typeof(IPlayerInput).GetProperty("Tackle");
            Assert.IsNotNull(p, "IPlayerInput must expose Tackle.");
            Assert.AreEqual(typeof(bool), p.PropertyType,
                "Tackle must be a bool (device-neutral one-shot command pulse).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.Tackle is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void InputFrame_Tackle_MatchesContractType_IsReadOnly()
        {
            var contract = typeof(IPlayerInput).GetProperty("Tackle");
            var snapshot = typeof(InputFrame).GetProperty("Tackle");
            Assert.IsNotNull(snapshot, "InputFrame must expose Tackle.");
            Assert.AreEqual(contract.PropertyType, snapshot.PropertyType,
                "InputFrame.Tackle must be the same bool type as IPlayerInput.Tackle.");
            Assert.IsNull(snapshot.SetMethod,
                "InputFrame.Tackle must be read-only (immutable snapshot).");
        }

        [Test]
        public void AllFourProviders_ExposeTackle_AsBool_OnTheSameContract()
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
                var p = t.GetProperty("Tackle");
                Assert.IsNotNull(p, $"'{name}' must expose Tackle.");
                Assert.AreEqual(typeof(bool), p.PropertyType,
                    $"'{name}' Tackle must be bool (same semantics as the contract).");
                Assert.AreEqual(false, p.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' Tackle must default to false (no request by default).");
            }
        }

        // ---- single authority & no duplicate abstraction ----

        [Test]
        public void Tackle_HasSingleAuthority_NoDuplicateTackleAbstraction()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "TackleInput", "TackleInputProvider", "TackleInputReader", "TackleReader",
                "TackleCommand", "TackleSource", "TackleRequest", "TackleRequestEvent", "TackleButton",
                "TackleFlag", "TackleInputData", "TackleInputState", "SlideInput", "SlideCommand"
            }, "Tackle input has ONE authority (IPlayerInput / InputFrame). No duplicate abstraction.");
        }

        // ---- transient one-shot pulse: no buffering/queue/priority machinery ----

        [Test]
        public void TackleInput_IsTransientPulse_NoBufferingQueueOrPriorityMachinery()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "TackleBuffer", "TackleQueuedInput", "TackleQueue", "TackleEdgeDetector", "TackleEdge",
                "TacklePrioritizer", "TacklePrioritySource", "TackleHistory", "TackleRetention", "TackleStatePersister"
            }, "Tackle is a TRANSIENT one-shot pulse with no buffering (Task 132), priority (Task 133), "
               + "edge machinery, or history.");
        }

        // ---- intent, not tackle gameplay ----

        [Test]
        public void TackleContract_AddsNoTackleGameplayMetadata()
        {
            var forbidden = new[]
            {
                "TackleRange", "TackleForce", "TacklePower", "TackleDirection", "TackleSpeed",
                "TackleDuration", "TackleCooldown", "TackleStamina", "TackleSuccess", "TackleState",
                "FoulFlag", "IsFoul", "PossessionResult", "BallRecovered", "IsTackling", "TackleAnimationTabIndex"
            };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var name in forbidden)
                {
                    Assert.IsNull(t.GetProperty(name),
                        $"'{t.Name}' must not expose tackle-gameplay metadata '{name}' (mechanics are not input).");
                }
            }
        }

        [Test]
        public void Tackle_IsPlayerRequest_NoTackleGameplayRuntimeImplemented()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "TackleGameplay", "TackleExecution", "TackleResolver", "TackleTargetResolver",
                "TackleMechanics", "TacklingController", "TackleComposer", "TackleSystem",
                "TackleAnimator", "SlideTackleSystem", "BallRecoverySystem", "FoulDetector"
            }, "Tackle INPUT is established; tackle gameplay/runtime (collision, success, ball "
               + "recovery, foul detection) does not exist.");
        }
    }
}