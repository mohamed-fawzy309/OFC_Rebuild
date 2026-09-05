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
    /// Task 120 — Pass Input contract.
    ///
    /// Task 120 establishes (by verification, not by new infrastructure) that PASS INPUT is a
    /// device-neutral, TRANSIENT one-shot command pulse ("the player requested a pass") that flows
    /// from the input providers through <see cref="IPlayerInput.Pass"/> (authoritative provider
    /// contract) to <see cref="InputFrame.Pass"/> (authoritative device-neutral snapshot field),
    /// with exactly one semantic authority, no buffering/queueing/priority machinery, and NO
    /// passing-gameplay metadata in the input contract. Pass gameplay (force, target, accuracy,
    /// animation, state, ball interaction) does not exist and is not implemented.
    ///
    /// These tests inspect actual assembled types (reflection, not source-text matching).
    ///
    /// NOT under test: pass force/power, pass targeting, pass direction, pass accuracy, pass type,
    /// pass animation, ball physics, pass cooldown, receiver selection, controller/keyboard support,
    /// dead zones, sensitivity, remapping, buffering (Task 132), priority (Task 133), or
    /// simultaneous-input resolution (Task 134).
    /// </summary>
    public class PassInputContractTests
    {
        // Pass-input abstractions/mechanics could only exist in these two assemblies
        // (contract/snapshot + providers). Football.Data (PassingStats ratings, Task 70) is a
        // separate player-data concern and already covered by PassingTests.
        private static IEnumerable<Assembly> PassInputAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input
        }

        private static string[] GetAllTypeNames()
            => PassInputAssemblies()
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
        public void IPlayerInput_Pass_IsReadOnlyBool()
        {
            var p = typeof(IPlayerInput).GetProperty("Pass");
            Assert.IsNotNull(p, "IPlayerInput must expose Pass.");
            Assert.AreEqual(typeof(bool), p.PropertyType,
                "Pass must be a bool (device-neutral one-shot command pulse).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.Pass is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void InputFrame_Pass_MatchesContractType_IsReadOnly()
        {
            var contract = typeof(IPlayerInput).GetProperty("Pass");
            var snapshot = typeof(InputFrame).GetProperty("Pass");
            Assert.IsNotNull(snapshot, "InputFrame must expose Pass.");
            Assert.AreEqual(contract.PropertyType, snapshot.PropertyType,
                "InputFrame.Pass must be the same bool type as IPlayerInput.Pass.");
            Assert.IsNull(snapshot.SetMethod,
                "InputFrame.Pass must be read-only (immutable snapshot).");
        }

        [Test]
        public void AllFourProviders_ExposePass_AsBool_OnTheSameContract()
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
                var p = t.GetProperty("Pass");
                Assert.IsNotNull(p, $"'{name}' must expose Pass.");
                Assert.AreEqual(typeof(bool), p.PropertyType,
                    $"'{name}' Pass must be bool (same semantics as the contract).");
                Assert.AreEqual(false, p.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' Pass must default to false (no request by default).");
            }
        }

        // ---- single authority & no duplicate abstraction ----

        [Test]
        public void Pass_HasSingleAuthority_NoDuplicatePassAbstraction()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "PassInput", "PassInputProvider", "PassInputReader", "PassReader",
                "PassCommand", "PassSource", "PassRequest", "PassRequestEvent", "PassButton",
                "PassFlag", "PassInputData", "PassInputState", "PassAbstraction"
            }, "Pass input has ONE authority (IPlayerInput / InputFrame). No duplicate abstraction.");
        }

        // ---- transient one-shot pulse: no buffering/queue/priority machinery ----

        [Test]
        public void PassInput_IsTransientPulse_NoBufferingQueueOrPriorityMachinery()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "PassBuffer", "PassQueuedInput", "PassQueue", "PassEdgeDetector", "PassEdge",
                "PassPrioritizer", "PassPrioritySource", "PassHistory", "PassRetention", "PassStatePersister"
            }, "Pass is a TRANSIENT one-shot pulse with no buffering (Task 132), priority (Task 133), "
               + "edge machinery, or history.");
        }

        // ---- intent, not passing gameplay ----

        [Test]
        public void PassContract_AddsNoPassingGameplayMetadata()
        {
            var forbidden = new[]
            {
                "PassPower", "PassForce", "PassStrength", "PassTarget", "PassDirection",
                "PassAccuracy", "PassType", "PassState", "PassCooldown", "PassAnimation",
                "Receiver", "IsPassing", "BallVelocity", "PassSpeed"
            };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var name in forbidden)
                {
                    Assert.IsNull(t.GetProperty(name),
                        $"'{t.Name}' must not expose passing-gameplay metadata '{name}' (mechanics are not input).");
                }
            }
        }

        [Test]
        public void Pass_IsPlayerRequest_NoPassingGameplayRuntimeImplemented()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "PassGameplay", "PassExecution", "PassResolver", "PassTargetResolver",
                "PassMechanics", "PassingController", "PassComposer", "BallLaunch"
            }, "Pass INPUT is established; passing gameplay/runtime (force, targeting, ball launch) "
               + "does not exist.");
        }
    }
}