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
    /// Task 135 — AI Input Contract.
    ///
    /// This project uses ONE device-neutral logical input contract: `IPlayerInput`
    /// (consumer-facing) + `InputFrame` (immutable pure-data snapshot transport).
    /// Each provider implements `IPlayerInput`. `AIPlayerInput` is the AI provider.
    ///
    /// AUDIT RESULT (135.1, re-verified): there is NO separate AI input contract
    /// and NO reason for one — `IPlayerInput` already carries every logical input
    /// an AI would express (MoveDirection, LookDirection, Sprint, Pass, Shoot,
    /// Tackle, SwitchPlayer, SwitchDirection, Skill, SkillDirection, Interact,
    /// Cancel, Pause, IsEnabled).
    ///
    ///   - `AIPlayerInput` (Football.Input) is a PASSIVE IPlayerInput
    ///     implementation: a MonoBehaviour exposing the 14 contract members as
    ///     settable auto-properties. It has NO Update(), NO lifecycle logic, NO
    ///     decision logic, and does NOT sample/produce input itself — external
    ///     code sets the logical intents.
    ///   - The `Football.AI` assembly (Runtime/AI) is EMPTY (no scripts) — no AI
    ///     runtime / decision system exists, so AI decision-making is UNKNOWN/
    ///     DEFERRED and is NOT part of the input provider.
    ///   - No production consumer of IPlayerInput exists; gameplay independence
    ///     is satisfied at the contract level.
    ///
    /// These tests prove the verified AI INPUT CONTRACT only. They assert that
    /// AIPlayerInput conforms to IPlayerInput, is device-neutral, exposes no
    /// AI-runtime/gameplay responsibilities, keeps InputFrame pure data, and its
    /// default IsEnabled state. They do NOT invent or test any AI behavior.
    /// </summary>
    public class AIPlayerInputContractTests
    {
        private static IEnumerable<Assembly> AIRelevantAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;          // Football.Core
            yield return typeof(AIPlayerInput).Assembly;         // Football.Input
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static string[] GetAllTypeNames()
            => AIRelevantAssemblies()
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

        // ---- 1. AIPlayerInput implements the single IPlayerInput contract ----

        [Test]
        public void AIPlayerInput_ImplementsIPlayerInput_AndIsMonoBehaviour()
        {
            Assert.IsTrue(typeof(IPlayerInput).IsAssignableFrom(typeof(AIPlayerInput)),
                "AIPlayerInput must implement the single consumer-facing IPlayerInput contract.");
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(AIPlayerInput)),
                "AIPlayerInput is a MonoBehaviour provider (consistent with Human/Replay/Network).");
        }

        [Test]
        public void AIPlayerInput_ExposesAllContractMembers()
        {
            foreach (var name in new[]
                     { "MoveDirection", "LookDirection", "Sprint", "Pass", "Shoot",
                       "Tackle", "SwitchPlayer", "SwitchDirection", "Skill",
                       "SkillDirection", "Interact", "Cancel", "Pause", "IsEnabled" })
            {
                var p = typeof(AIPlayerInput).GetProperty(name);
                Assert.IsNotNull(p, $"AIPlayerInput must expose '{name}' from IPlayerInput.");
                Assert.IsTrue(p.CanRead && p.CanWrite,
                    $"'{name}' on AIPlayerInput must be settable (passive externally-driven provider).");
            }
        }

        [Test]
        public void AIPlayerInput_MemberTypes_MatchContract()
        {
            Assert.AreEqual(typeof(Vector2), typeof(AIPlayerInput).GetProperty(nameof(AIPlayerInput.MoveDirection)).PropertyType);
            Assert.AreEqual(typeof(Vector2), typeof(AIPlayerInput).GetProperty(nameof(AIPlayerInput.LookDirection)).PropertyType);
            foreach (var b in new[] { nameof(AIPlayerInput.Sprint), nameof(AIPlayerInput.Pass),
                                      nameof(AIPlayerInput.Shoot), nameof(AIPlayerInput.Tackle),
                                      nameof(AIPlayerInput.SwitchPlayer), nameof(AIPlayerInput.Skill),
                                      nameof(AIPlayerInput.Interact), nameof(AIPlayerInput.Cancel),
                                      nameof(AIPlayerInput.Pause), nameof(AIPlayerInput.IsEnabled) })
            {
                Assert.AreEqual(typeof(bool), typeof(AIPlayerInput).GetProperty(b).PropertyType,
                    $"'{b}' must be bool on AIPlayerInput.");
            }
            Assert.AreEqual(typeof(Vector2), typeof(AIPlayerInput).GetProperty(nameof(AIPlayerInput.SwitchDirection)).PropertyType);
            Assert.AreEqual(typeof(Vector2), typeof(AIPlayerInput).GetProperty(nameof(AIPlayerInput.SkillDirection)).PropertyType);
        }

        // ---- 2. AI provider is device-neutral ----

        [Test]
        public void AIPlayerInput_IsDeviceNeutral_NoDeviceApis()
        {
            // No UnityEngine.Input.* reference (sampling would couple to devices);
            // no device type leaks into the provider.
            var src = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath,
                    "Football/Runtime/Input/AIPlayerInput.cs"));
            Assert.IsFalse(src.Contains("UnityEngine.Input."),
                "AIPlayerInput must not sample device input (device-neutral passive provider).");
            Assert.IsFalse(src.Contains("GetAxis"), "AIPlayerInput must not read axes.");
            Assert.IsFalse(src.Contains("GetButton"), "AIPlayerInput must not read buttons.");
        }

        [Test]
        public void NoAIDeviceType_LeaksIntoCore()
        {
            // Core (IPlayerInput/InputFrame assembly) must stay device-neutral.
            var names = GetAllTypeNames();
            foreach (var kw in new[] { "Keyboard", "Mouse", "Joystick", "Gamepad", "Controller", "Touch", "Mobile" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(kw)),
                    $"No AI-related device type '{kw}' may leak into Core/Input provider assemblies.");
            }
        }

        // ---- 3. AIPlayerInput introduces no AI runtime / gameplay responsibilities ----

        [Test]
        public void AIPlayerInput_HasNoDecisionOrRuntimeMembers()
        {
            var members = SnapshotFieldsAndProperties(typeof(AIPlayerInput)).ToList();
            foreach (var kw in new[] { "Decision", "Target", "Tactic", "Pathfind", "Navigate",
                                       "Possess", "Percept", "StateMachine", "Action", "Brain",
                                       "Evaluate", "Think", "Plan", "Priority", "Buffer", "Queue" })
            {
                Assert.IsFalse(members.Any(m => m.Contains(kw) && !m.Contains("Skill") && !m.Contains(".")),
                    $"AIPlayerInput must not expose AI-runtime/gameplay member '{kw}' (provider-only, no decision engine).");
            }
        }

        [Test]
        public void AIPlayerInput_HasNoGameplaySelectionOrIntentMetadata()
        {
            var members = SnapshotFieldsAndProperties(typeof(AIPlayerInput)).ToList();
            foreach (var kw in new[] { "TargetPlayerId", "TargetPosition", "TacticalIntent",
                                       "PassTarget", "ShotType", "SkillId", "ActionId",
                                       "DecisionState", "AttackIntent", "DefenseIntent" })
            {
                Assert.IsFalse(members.Any(m => m.Contains(kw)),
                    $"AIPlayerInput must not expose gameplay/decision metadata '{kw}' — it is an input provider only.");
            }
        }

        [Test]
        public void NoSpeculativeAIBrain_OrRuntime_SystemExists()
        {
            // No AI decision/runtime system may be manufactured by this task.
            var names = GetAllTypeNames();
            foreach (var f in new[] { "AIController", "AIBrain", "AIDecisionSystem",
                                      "AITargetSelector", "AIPathfinding", "AINavigation",
                                      "AIManager", "AIThinkingSystem", "AIStrategy", "AITactics" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — no speculative AI runtime/decision system is allowed.");
            }
        }

        // ---- 4. Passive provider (no Update path, no sampling) ----

        [Test]
        public void AIPlayerInput_HasNoUpdateOrLifecycleMethods()
        {
            var methods = typeof(AIPlayerInput).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate", "Awake", "Start",
                                         "OnEnable", "OnDisable", "OnDestroy", "SampleInput", "ProduceInput" })
            {
                Assert.IsFalse(methods.Any(m => m.Name == name),
                    $"AIPlayerInput must not have a '{name}' method (passive provider; does not produce/sample input).");
            }
        }

        [Test]
        public void AIPlayerInput_DefaultIsEnabled_IsTrue()
        {
            var input = new AIPlayerInput();
            Assert.IsTrue(input.IsEnabled,
                "AIPlayerInput.IsEnabled must default to true (lifecycle gate), consistent with the other providers.");
        }

        // ---- 5. InputFrame remains pure data (not a behavior/decision object) ----

        [Test]
        public void InputFrame_RemainsPureDataSnapshot_NoAIState()
        {
            var members = SnapshotFieldsAndProperties(typeof(InputFrame)).ToList();
            foreach (var kw in new[] { "Decision", "Target", "Tactic", "ActionId", "Intent",
                                       "Priority", "Buffer", "Queue", "State", "Legal", "Executed" })
            {
                Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains(kw.ToLowerInvariant())),
                    $"InputFrame must not carry AI decision/behavior state member '{kw}' — it is pure data.");
            }
        }

        // ---- 6. Single source of truth: no parallel AI-specific contract ----

        [Test]
        public void NoParallelAIInputContract_Exists()
        {
            var names = GetAllTypeNames();
            // Only the device-neutral IPlayerInput + providers exist; no separate
            // AI-specific input interface/contract was created.
            foreach (var f in new[] { "IAIInput", "IAIPlayerInputContract", "AIFrame",
                                      "AIInputContract", "AIPlayerInputFrame", "IAIContract" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — this project consolidates on the single IPlayerInput contract.");
            }
        }

        // ---- 7. Contract surface remains unchanged / consistent across providers ----

        [Test]
        public void AIPlayerInput_Surface_MatchesContract_NoExtraLogicalInputMembers()
        {
            // Compare only DIRECTLY-DECLARED properties (exclude inherited Unity
            // engine surface such as name/enabled/transform). The AI provider must
            // declare exactly the IPlayerInput logical-input members — no added and
            // no dropped members — and must match the sibling providers.
            var declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var ifaceProps = typeof(IPlayerInput)
                .GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var aiProps = typeof(AIPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var humanProps = typeof(HumanPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var replayProps = typeof(ReplayPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var networkProps = typeof(NetworkPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

            Assert.IsTrue(aiProps.SequenceEqual(ifaceProps),
                $"AIPlayerInput must declare exactly the IPlayerInput logical-input members. AI={string.Join(",", aiProps)} iface={string.Join(",", ifaceProps)}");
            Assert.IsTrue(humanProps.SequenceEqual(ifaceProps),
                "HumanPlayerInput must declare exactly the IPlayerInput members (contract consistency).");
            Assert.IsTrue(replayProps.SequenceEqual(ifaceProps),
                "ReplayPlayerInput must declare exactly the IPlayerInput members (contract consistency).");
            Assert.IsTrue(networkProps.SequenceEqual(ifaceProps),
                "NetworkPlayerInput must declare exactly the IPlayerInput members (contract consistency).");
        }
    }
}
