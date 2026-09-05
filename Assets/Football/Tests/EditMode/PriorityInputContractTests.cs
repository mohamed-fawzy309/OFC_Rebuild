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
    /// Task 133 — Input Priority input contract.
    ///
    ///     Input Priority
    ///         = precedence between competing input intents (which logical input
    ///           command takes precedence when multiple intents conflict or
    ///           cannot be processed together).
    ///     Input Priority
    ///         ≠ Button Buffering (Task 132).
    ///     Input Priority
    ///         ≠ Simultaneous Input Handling (Task 134).
    ///     Input Priority
    ///         ≠ Gameplay Execution.
    ///
    /// The audit proved that this project defines NO input-priority /
    /// precedence semantics anywhere:
    ///   - No priority/precedence/arbitration/conflict-resolution code, enum,
    ///     or numeric priority constant exists (Runtime).
    ///   - `IPlayerInput` exposes raw logical intents with no priority metadata.
    ///   - `InputFrame` is a documented PURE-DATA SNAPSHOT that must NOT contain
    ///     priority/simultaneous-input policy (InputFrame.cs:9-12).
    ///   - No input consumer exists anywhere (InputFrame is never read by
    ///     production code), so no precedence is ever applied.
    ///   - No authoritative precedence pair (e.g. "Pass takes precedence over
    ///     Shoot") is defined; none is invented here.
    ///   - Compatible inputs (e.g. Move + Sprint, one-shot + continuous) are
    ///     generated together and are NOT suppressed.
    ///   - Which simultaneously-present action is "processed" is the Task 134
    ///     (Simultaneous Input Handling) concern; action legality / player
    ///     selection / Pause/Cancel/Interact context are gameplay-owned.
    ///
    /// Conclusion: PASS WITH DEFERRED — no global input-priority ordering is
    /// defined; precedence policy is product-owned and DEFERRED. Task 133 makes
    /// NO production change and creates NO speculative priority system.
    ///
    /// These tests prove the DOCUMENTED EXISTING BEHAVIOR (raw logical intents
    /// with no priority/precedence machinery) and the ABSENCE of any speculative
    /// priority system.
    /// </summary>
    public class PriorityInputContractTests
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

        // ---- 1. No speculative priority / precedence system exists ----

        [Test]
        public void NoPrioritySystem_Exists()
        {
            var names = GetAllTypeNames();
            var forbidden = new[]
            {
                "PriorityManager", "InputPriorityManager", "CommandPrioritySystem",
                "InputArbitrator", "ActionArbitrator", "InputPriority",
                "PrioritySource", "Prioritizer", "PrioritySystem",
                "InputConflictResolver", "ConflictResolver", "InputResolver",
                "PrecedenceResolver", "PrecedenceRule", "PriorityTable",
                "CommandPriority", "ActionPriority", "InputPrioritySystem"
            };
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — no speculative priority framework is allowed (Task 133).");
            }
        }

        // ---- 2. No arbitrary numeric priority attached to inputs ----

        [Test]
        public void NoNumericPriority_OnContract()
        {
            var numeric = new[] { "Priority", "PriorityValue", "PriorityRank", "Precedence",
                                  "PrecedenceLevel", "PrecedenceRank", "CommandPriority" };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                var members = SnapshotFieldsAndProperties(t).ToList();
                foreach (var kw in numeric)
                {
                    Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains(kw.ToLowerInvariant())),
                        $"'{t.Name}' must not carry priority/precedence metadata member '{kw}'.");
                }
            }
        }

        [Test]
        public void NoInputPriorityEnum_OrConstant_Exists()
        {
            // No province-wide priority enum or numeric priority constant may exist.
            var names = GetAllTypeNames();
            foreach (var kw in new[] { "Priority", "Precedence" })
            {
                Assert.IsFalse(names.Any(n => n.ToLowerInvariant().Contains(kw.ToLowerInvariant())
                                            && !n.Contains("Tests")),
                    $"No type name may contain '{kw}' — no priority enum/constant is defined.");
            }
        }

        // ---- 3. Providers do not suppress or resolve inputs ----

        [Test]
        public void Providers_DoNotResolveOrSuppressInputs()
        {
            // Each provider exposes raw logical intents; none arbitrates any input.
            foreach (var t in new[] { typeof(HumanPlayerInput), typeof(AIPlayerInput),
                                      typeof(ReplayPlayerInput), typeof(NetworkPlayerInput) })
            {
                var members = SnapshotFieldsAndProperties(t).ToList();
                foreach (var kw in new[] { "Priority", "Precedence", "Arbitrat", "Resolve",
                                           "Suppress", "Conflict", "Exclusive" })
                {
                    Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains(kw.ToLowerInvariant())),
                        $"'{t.Name}' must not resolve/suppress inputs (no priority member '{kw}').");
                }
            }
        }

        // ---- 4. InputFrame remains a pure-data snapshot (no priority state) ----

        [Test]
        public void InputFrame_IsPureDataSnapshot_NoPriorityState()
        {
            var members = SnapshotFieldsAndProperties(typeof(InputFrame)).ToList();
            Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains("priority")
                                         || m.ToLowerInvariant().Contains("precedence")
                                         || m.ToLowerInvariant().Contains("resolution")
                                         || m.ToLowerInvariant().Contains("selected")),
                "InputFrame (pure-data snapshot) must not expose priority/resolution state.");
        }

        // ---- 5. Compatible inputs remain present / not suppressed ----

        [Test]
        public void CompatibleInputs_CoexistOnInputFrame()
        {
            // Continuous + one-shot co-exist as raw intents; no suppression removes
            // any field from the snapshot contract.
            var members = SnapshotFieldsAndProperties(typeof(InputFrame)).ToList();
            foreach (var required in new[]
                     { "MoveDirection", "Sprint", "Pass", "Shoot", "Tackle",
                       "SwitchPlayer", "Skill", "Interact", "Cancel", "Pause" })
            {
                Assert.IsTrue(members.Contains(required),
                    $"InputFrame (and IPlayerInput) must expose '{required}' — compatible inputs coexist; none is dropped.");
                Assert.IsNotNull(typeof(IPlayerInput).GetProperty(required),
                    $"IPlayerInput must expose '{required}'.");
            }
        }

        // ---- 6. Task 134 / buffering / gameplay remain separate ----

        [Test]
        public void NoSimultaneousHandling_OrBuffering_TypeExists()
        {
            // Task 133 must not leak Task 134 (simultaneous) or Task 132 (buffering) logic.
            var names = GetAllTypeNames();
            foreach (var f in new[] { "SimultaneousResolution", "SimultaneousHandler",
                                      "SimultaneousInputResolver", "BufferManager",
                                      "InputBufferManager", "CommandQueue", "ActionQueue" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — simultaneous (134) and buffering (132) stay separate from priority.");
            }
        }

        [Test]
        public void Priority_DoesNotExecuteGameplay()
        {
            // No processing/coordination layer exists; no gameplay-execution type
            // was created to consume priority.
            var names = GetAllTypeNames();
            foreach (var f in new[] { "PriorityProcessing", "PriorityCoordinator", "CommandProcessor" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — priority must not execute gameplay (no consumer, no execution).");
            }
        }

        // ---- 7. No invented precedence ordering policy exists ----

        [Test]
        public void NoInventedPrecedencePolicy_Exists()
        {
            // No last-wins / first-wins / drop / override policy type or constant
            // is defined — precedence policy is UNDEFINED/DEFERRED.
            var names = GetAllTypeNames();
            foreach (var kw in new[] { "LastWins", "FirstWins", "PriorityOrder", "OverridePolicy",
                                       "SuppressInput", "InputSuppression", "DropPolicy" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(kw)),
                    $"'{kw}' must not exist — no precedence/override policy is defined.");
            }
        }

        // ---- 8. Existing logical actions unchanged / raw intents preserved ----

        [Test]
        public void LogicalActionSemantics_Unchanged()
        {
            // All IPlayerInput members retained with their intended types;
            // priority must not have changed meaning or added fields.
            var props = typeof(IPlayerInput).GetProperties().Select(p => p.Name).ToArray();
            foreach (var expected in new[] { "MoveDirection", "LookDirection", "Sprint", "Pass", "Shoot",
                                             "Tackle", "SwitchPlayer", "SwitchDirection", "Skill",
                                             "SkillDirection", "Interact", "Cancel", "Pause", "IsEnabled" })
            {
                Assert.IsTrue(props.Contains(expected), $"IPlayerInput must still expose '{expected}'.");
            }
            Assert.AreEqual(typeof(Vector2), typeof(IPlayerInput).GetProperty(nameof(IPlayerInput.MoveDirection)).PropertyType);
            Assert.AreEqual(typeof(bool), typeof(IPlayerInput).GetProperty(nameof(IPlayerInput.Pass)).PropertyType);
            Assert.AreEqual(typeof(bool), typeof(IPlayerInput).GetProperty(nameof(IPlayerInput.Pause)).PropertyType);
        }
    }
}
