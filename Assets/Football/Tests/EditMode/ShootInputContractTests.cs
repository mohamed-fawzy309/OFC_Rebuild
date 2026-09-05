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
    /// Task 121 — Shoot Input contract.
    ///
    /// Task 121 establishes (by verification, not new infrastructure) that SHOOT INPUT is a
    /// device-neutral, TRANSIENT one-shot command pulse ("the player requested a shot") that flows
    /// from the input providers through <see cref="IPlayerInput.Shoot"/> (authoritative provider
    /// contract) to <see cref="InputFrame.Shoot"/> (authoritative device-neutral snapshot field),
    /// with exactly one semantic authority, no buffering/queueing/priority machinery, and NO
    /// shooting-gameplay metadata in the input contract. Shooting gameplay (power, force, direction,
    /// aim, targeting, accuracy, shot type, charge, animation, ball launch, kick physics, goalkeeper
    /// reaction) does not exist and is not implemented. PlayerStats.Shooting is PLAYER DATA and
    /// BallConfig.KickForce is GAMEPLAY TUNING, neither belongs to Shoot Input.
    ///
    /// These tests inspect actual assembled types (reflection, not source-text matching).
    ///
    /// NOT under test: shot power/force, shot targeting, aim, direction, accuracy, shot type,
    /// charge/charge duration, shot animation, kick physics, ball trajectory, goalkeeper response,
    /// shot result, stamina/cooldown, controller/keyboard support, dead zones, sensitivity,
    /// remapping, buffering (Task 132), priority (Task 133), or simultaneous-input resolution
    /// (Task 134).
    /// </summary>
    public class ShootInputContractTests
    {
        // Shoot-input abstractions/mechanics could only exist in these two assemblies
        // (contract/snapshot + providers). Football.Data (ShootingStats ratings, Task 69) is a
        // separate player-data concern and already covered by ShootingTests.
        private static IEnumerable<Assembly> ShootInputAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input
        }

        private static string[] GetAllTypeNames()
            => ShootInputAssemblies()
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
        public void IPlayerInput_Shoot_IsReadOnlyBool()
        {
            var p = typeof(IPlayerInput).GetProperty("Shoot");
            Assert.IsNotNull(p, "IPlayerInput must expose Shoot.");
            Assert.AreEqual(typeof(bool), p.PropertyType,
                "Shoot must be a bool (device-neutral one-shot command pulse).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.Shoot is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void InputFrame_Shoot_MatchesContractType_IsReadOnly()
        {
            var contract = typeof(IPlayerInput).GetProperty("Shoot");
            var snapshot = typeof(InputFrame).GetProperty("Shoot");
            Assert.IsNotNull(snapshot, "InputFrame must expose Shoot.");
            Assert.AreEqual(contract.PropertyType, snapshot.PropertyType,
                "InputFrame.Shoot must be the same bool type as IPlayerInput.Shoot.");
            Assert.IsNull(snapshot.SetMethod,
                "InputFrame.Shoot must be read-only (immutable snapshot).");
        }

        [Test]
        public void AllFourProviders_ExposeShoot_AsBool_OnTheSameContract()
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
                var p = t.GetProperty("Shoot");
                Assert.IsNotNull(p, $"'{name}' must expose Shoot.");
                Assert.AreEqual(typeof(bool), p.PropertyType,
                    $"'{name}' Shoot must be bool (same semantics as the contract).");
                Assert.AreEqual(false, p.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' Shoot must default to false (no request by default).");
            }
        }

        // ---- single authority & no duplicate abstraction ----

        [Test]
        public void Shoot_HasSingleAuthority_NoDuplicateShootAbstraction()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "ShootInput", "ShootInputProvider", "ShootInputReader", "ShootReader",
                "ShootCommand", "ShootSource", "ShootRequest", "ShootRequestEvent", "ShootButton",
                "ShootFlag", "ShootInputData", "ShootInputState", "ShotCommand", "ShotInput",
                "ShotRequest", "KickInput", "KickCommand"
            }, "Shoot input has ONE authority (IPlayerInput / InputFrame). No duplicate abstraction.");
        }

        // ---- transient one-shot pulse: no buffering/queue/priority machinery ----

        [Test]
        public void ShootInput_IsTransientPulse_NoBufferingQueueOrPriorityMachinery()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "ShootBuffer", "ShootQueuedInput", "ShootQueue", "ShootEdgeDetector", "ShootEdge",
                "ShootPrioritizer", "ShootPrioritySource", "ShootHistory", "ShootRetention", "ShootStatePersister"
            }, "Shoot is a TRANSIENT one-shot pulse with no buffering (Task 132), priority (Task 133), "
               + "edge machinery, or history.");
        }

        // ---- intent, not shooting gameplay ----

        [Test]
        public void ShootContract_AddsNoShootingGameplayMetadata()
        {
            var forbidden = new[]
            {
                "ShotPower", "ShotForce", "ShotStrength", "ShotTarget", "ShotDirection",
                "ShotAccuracy", "ShotType", "ShotState", "ShotCooldown", "ShotAnimation",
                "ShotPlacement", "IsShooting", "BallVelocity", "ShotSpeed", "Charge", "ChargeLevel"
            };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var name in forbidden)
                {
                    Assert.IsNull(t.GetProperty(name),
                        $"'{t.Name}' must not expose shooting-gameplay metadata '{name}' (mechanics are not input).");
                }
            }
        }

        [Test]
        public void Shoot_IsPlayerRequest_NoShootingGameplayRuntimeImplemented()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "ShootGameplay", "ShotExecution", "ShotResolver", "ShotTargetResolver",
                "ShootMechanics", "ShootingController", "ShotComposer", "ShotSystem",
                "ShootingSystem", "ShotController", "KickPhysics", "GoalResolver"
            }, "Shoot INPUT is established; shooting gameplay/runtime (power, targeting, ball launch, "
               + "kick physics) does not exist.");
        }
    }
}