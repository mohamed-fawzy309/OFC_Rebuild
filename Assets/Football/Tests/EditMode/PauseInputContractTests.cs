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
    /// Task 125 — Pause Input contract.
    ///
    /// Task 125 verifies (it does NOT newly build) that PAUSE INPUT is a
    /// device-neutral, TRANSIENT one-shot request pulse ("the player REQUESTED
    /// Pause") that flows from the input providers through
    /// <see cref="IPlayerInput.Pause"/> (authoritative provider contract) to
    /// <see cref="InputFrame.Pause"/> (authoritative device-neutral snapshot
    /// field), with exactly one semantic authority, no buffering/queueing/
    /// priority machinery, and NO paused-game-state metadata in the input
    /// contract.
    ///
    /// The audit (Task 125.1) confirmed Pause was already established by earlier
    /// Phase 3 / Task 51 work. The existing contract is KEPT. No production code
    /// changed for Task 125 — this fixture proves the contract.
    ///
    /// CRITICAL DISTINCTION (proven by these tests and by PauseTimeTests):
    ///   PAUSE INPUT  = "the player requested Pause"  (this contract)
    ///   PAUSED STATE = a separate gameplay / game-flow authority
    ///                  (<see cref="GameStateId.Pause"/>), outside Input.
    ///
    /// What is OWNED BY GAMEFLOW/GAMEPLAY (NOT input, NOT added here):
    ///   IsPaused, GameStateId.Pause, PauseMenuOpen, Time.timeScale,
    ///   MatchClockStopped, PhysicsPaused, AnimationPaused.
    ///
    /// These tests inspect actual assembled types (reflection, not source-text
    /// matching).
    ///
    /// NOT under test (Task 125 is INPUT ONLY): actual game pausing,
    /// Time.timeScale behavior, pause menu / UI, game-state transitions, match
    /// clock pausing (GameClock, Task 51), physics pausing, animation pausing,
    /// network pause, replay pause, controller/keyboard/mobile bindings
    /// (deferred), dead zones, sensitivity, remapping, buffering (Task 132),
    /// priority (Task 133), or simultaneous-input resolution (Task 134).
    /// </summary>
    public class PauseInputContractTests
    {
        // Pause-input abstractions could only exist in these two assemblies
        // (contract/snapshot + providers). Football.Core is the contract host;
        // Football.Input is the provider host. GameClock / GameStateId.Pause
        // (Task 51) is the separate paused-state authority, not input.
        private static IEnumerable<Assembly> PauseInputAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input
        }

        private static string[] GetAllTypeNames()
            => PauseInputAssemblies()
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
        public void IPlayerInput_Pause_IsReadOnlyBool()
        {
            var p = typeof(IPlayerInput).GetProperty("Pause");
            Assert.IsNotNull(p, "IPlayerInput must expose Pause.");
            Assert.AreEqual(typeof(bool), p.PropertyType,
                "Pause must be a bool (device-neutral one-shot request pulse).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.Pause is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void InputFrame_Pause_MatchesContractType_IsReadOnly()
        {
            var contract = typeof(IPlayerInput).GetProperty("Pause");
            var snapshot = typeof(InputFrame).GetProperty("Pause");
            Assert.IsNotNull(snapshot, "InputFrame must expose Pause.");
            Assert.AreEqual(contract.PropertyType, snapshot.PropertyType,
                "InputFrame.Pause must be the same bool type as IPlayerInput.Pause.");
            Assert.IsNull(snapshot.SetMethod,
                "InputFrame.Pause must be read-only (immutable snapshot).");
        }

        [Test]
        public void InputFrame_Pause_DefaultIsFalse_AndSnapshotable()
        {
            var f = default(InputFrame);
            Assert.IsFalse(f.Pause, "A default InputFrame must carry NO pause request.");
            Assert.AreEqual(false, typeof(InputFrame).GetProperty("Pause").GetValue(f),
                "InputFrame.Pause must default to false (no request by default).");
        }

        [Test]
        public void AllFourProviders_ExposePause_AsBool_OnTheSameContract()
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
                var p = t.GetProperty("Pause");
                Assert.IsNotNull(p, $"'{name}' must expose Pause.");
                Assert.AreEqual(typeof(bool), p.PropertyType,
                    $"'{name}' Pause must be bool (same semantics as the contract).");
                Assert.AreEqual(false, p.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' Pause must default to false (no request by default).");
            }
        }

        // ---- Runtime carries no pause REQUEST consumer yet: consumption is deferred ----

        [Test]
        public void Pause_HasNoRuntimeConsumer_ConsumptionDeferred()
        {
            // The audit found no gameplay/game-flow/UI system consumes IPlayerInput
            // in the Runtime. Pause input has no current runtime consumer; how the
            // pause REQUEST is interpreted (what pausing means in a given state) is
            // a gameplay/game-flow responsibility deferred to future systems.
            var consumers = SafeGetTypes(typeof(HumanPlayerInput).Assembly)
                .Where(t => t.IsClass
                            && !typeof(IPlayerInput).IsAssignableFrom(t)
                            && t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 .Any(f => f.FieldType == typeof(IPlayerInput)))
                .Select(t => t.Name)
                .ToArray();
            Assert.IsEmpty(consumers,
                "No runtime consumer of IPlayerInput should exist yet; game-flow/UI consumption is deferred. "
                + "Found: " + string.Join(", ", consumers));
        }

        // ---- single authority & no duplicate abstraction ----

        [Test]
        public void Pause_HasSingleAuthority_NoDuplicatePauseAbstraction()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "PauseInput", "PauseInputProvider", "PauseInputReader", "PauseReader",
                "PauseCommand", "PauseSource", "PauseRequest", "PauseRequestEvent", "PauseButton",
                "PauseFlag", "PauseInputData", "PauseInputState", "PauseService", "PauseSystem",
                "PauseManager", "PauseController"
            }, "Pause input has ONE authority (IPlayerInput / InputFrame). No duplicate abstraction.");
        }

        // ---- transient one-shot request: no buffering/queue/priority machinery ----

        [Test]
        public void PauseInput_IsTransientRequest_NoBufferingQueueOrPriorityMachinery()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "PauseBuffer", "PauseQueuedInput", "PauseQueue", "PauseEdgeDetector", "PauseEdge",
                "PausePrioritizer", "PausePrioritySource", "PauseHistory", "PauseRetention",
                "PauseStatePersister", "PauseDebouncer", "PauseCooldown", "PauseTimer", "ResumeInput",
                "ResumeCommand", "ResumeRequest", "TogglePause", "TogglePauseInput"
            }, "Pause is a TRANSIENT one-shot request with no buffering (Task 132), priority "
               + "(Task 133), edge machinery, cooldown, or separate Resume/Toggle input. "
               + "'Toggle/Resume' interpretation is a game-flow concern, not a second input.");
        }

        // ---- Pause is a REQUEST, distinct from the PAUSED game state ----

        [Test]
        public void PauseContract_CarriesNoPausedGameState()
        {
            var forbidden = new[]
            {
                "IsPaused", "GamePaused", "PauseState", "Paused", "IsGamePaused",
                "MatchPaused", "PauseMenuOpen", "IsPauseMenuOpen", "TimeScale",
                "MatchClockStopped", "PhysicsPaused", "AnimationPaused", "PausedAt"
            };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var name in forbidden)
                {
                    Assert.IsNull(t.GetProperty(name),
                        $"'{t.Name}' must NOT carry paused-game-state '{name}'. "
                        + "Pause INPUT is a request; the PAUSED STATE is a separate "
                        + "gameplay/game-flow authority (GameStateId.Pause).");
                }
            }
        }

        [Test]
        public void PausedStateAuthority_IsGameplay_NotInput()
        {
            // The ONE authoritative paused-state marker is GameStateId.Pause
            // (owned by Football.Core). It is an enum of game states, not an input
            // field, and it is NOT exposed on IPlayerInput/InputFrame.
            var ids = Enum.GetNames(typeof(GameStateId));
            Assert.Contains("Pause", ids,
                "GameStateId.Pause is the single authoritative paused-state marker (gameplay authority).");
            Assert.IsNull(typeof(IPlayerInput).GetProperty("IsPaused"),
                "Paused-state ownership must not leak into the input contract.");
            Assert.IsNull(typeof(InputFrame).GetProperty("IsPaused"),
                "Paused-state ownership must not leak into the input snapshot.");
        }

        // ---- Pause vs Cancel are distinct intents ----

        [Test]
        public void Pause_And_Cancel_AreDistinctInputIntents()
        {
            var pause = typeof(IPlayerInput).GetProperty("Pause");
            var cancel = typeof(IPlayerInput).GetProperty("Cancel");
            Assert.IsNotNull(pause, "Pause contract member is required.");
            Assert.IsNotNull(cancel, "Cancel contract member is required.");
            Assert.AreNotSame(pause, cancel,
                "Pause and Cancel must be distinct input members (distinct intents), not merged.");

            var ifPause = typeof(InputFrame).GetProperty("Pause");
            var ifCancel = typeof(InputFrame).GetProperty("Cancel");
            Assert.AreNotSame(ifPause, ifCancel,
                "InputFrame must keep Pause and Cancel as distinct snapshot fields.");
        }

        // ---- input layer must not own pause implementation semantics ----

        [Test]
        public void InputContract_AddsNoPauseGameplayOrTimeScaleRuntime()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "PauseMenu", "PauseUIController", "PauseScreen", "PauseMenuController",
                "PauseGameplay", "PauseResolver", "PauseStateMachine", "PauseTransition",
                "PauseSimulation", "TimeScaleController", "TimeScaleManager", "PauseEnforcer"
            }, "Task 125 is INPUT ONLY. No pause menu/UI, pause gameplay, state machine, or "
               + "Time.timeScale controller may be introduced into the input/contract assemblies.");
        }
    }
}
