using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Football.Core;
using Football.Input;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 132 — Button Buffering input contract.
    ///
    ///     Button Buffering
    ///         = temporary retention of transient input intent (a short bounded
    ///           window after the press so the request can be consumed when the
    ///           gameplay action becomes available).
    ///     Button Buffering
    ///         ≠ gameplay action execution.
    ///     Button Buffering
    ///         ≠ Input Priority (Task 133).
    ///     Button Buffering
    ///         ≠ Simultaneous Input Handling (Task 134).
    ///
    /// The audit proved that this project has NO input buffering and the
    /// established architecture intentionally treats every one-shot transient
    /// action (Pass / Shoot / Tackle / SwitchPlayer / Skill / Interact / Cancel
    /// / Pause) as an UNBUFFERED one-frame pulse:
    ///   - `InputFrame` is a documented PURE-DATA SNAPSHOT that must NOT contain
    ///     buffering/history/priority/simultaneous-input policy (InputFrame.cs:9-12).
    ///   - Existing action contract tests enforce
    ///     "transient one-shot pulse: no buffering/queue/priority machinery"
    ///     (e.g. PassInputContractTests forbids PassBuffer/PassQueue/etc.).
    ///   - No buffer duration, queue depth, ordering, duplicate, expiration, or
    ///     consumption policy is defined anywhere (repository or product).
    ///   - No gameplay consumer of buffered input exists.
    ///
    /// Conclusion: PASS WITH DEFERRED — buffering is intentionally NOT part of
    /// the current input contract; its semantics/tuning are UNDEFINED and
    /// DEFERRED. Task 132 makes NO production change and creates NO speculative
    /// buffering infrastructure.
    ///
    /// These tests prove the DOCUMENTED EXISTING BEHAVIOR (unbuffered one-shot
    /// transient pulses) and the ABSENCE of any speculative buffer system.
    /// </summary>
    public class BufferingInputContractTests
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

        // ---- 1. No speculative input-buffer system exists ----

        [Test]
        public void NoBufferingSystem_Exists()
        {
            var names = GetAllTypeNames();
            var forbidden = new[]
            {
                "BufferManager", "InputBufferManager", "CommandQueue", "ActionQueue",
                "GameplayInputQueue", "BufferedInputSystem", "InputBuffer",
                "BufferedAction", "InputBufferEntry", "ButtonBuffer", "CommandBuffer"
            };
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — no speculative buffering framework is allowed.");
            }
        }

        // ---- 2. Transient one-shot actions remain unbuffered pulses ----

        private static readonly string[] OneShotActions =
        {
            "Pass", "Shoot", "Tackle", "SwitchPlayer", "Skill", "Interact", "Cancel", "Pause"
        };

        [Test]
        public void OneShotActions_HaveNoBufferingRetentionTypes()
        {
            var names = GetAllTypeNames();
            foreach (var action in OneShotActions)
            {
                foreach (var suffix in new[] { "Buffer", "Queue", "QueuedInput", "Retention", "History", "StatePersister" })
                {
                    Assert.IsFalse(names.Any(n => n.Contains(action + suffix)),
                        $"'{action}{suffix}' must not exist — '{action}' is an UNBUFFERED transient one-shot pulse.");
                }
            }
        }

        [Test]
        public void OneShotActions_AreBooleanOneShot_NoBufferedFieldsOnInputFrame()
        {
            foreach (var action in OneShotActions)
            {
                var prop = typeof(InputFrame).GetProperty(action);
                Assert.IsNotNull(prop, $"InputFrame must expose '{action}'.");
                Assert.AreEqual(typeof(bool), prop.PropertyType,
                    $"'{action}' must be a transient boolean one-shot signal in InputFrame.");
                var ifaceProp = typeof(IPlayerInput).GetProperty(action);
                Assert.IsNotNull(ifaceProp, $"IPlayerInput must expose '{action}'.");
                Assert.AreEqual(typeof(bool), ifaceProp.PropertyType,
                    $"'{action}' must be a boolean in IPlayerInput (device-neutral logical pulse).");
            }
        }

        [Test]
        public void OneShotActions_DoNotCarryTimestampsOrLifetimeInContract()
        {
            var forbidden = new[] { "Timestamp", "Time", "Expiry", "Expires", "Lifetime",
                                    "FrameNumber", "Frame", "Age", "BufferDuration", "InsertionTime" };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                var members = SnapshotFieldsAndProperties(t).ToList();
                foreach (var kw in forbidden)
                {
                    Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains(kw.ToLowerInvariant())),
                        $"'{t.Name}' must not carry buffering time/lifetime member '{kw}'.");
                }
            }
        }

        // ---- 3. InputFrame remains a pure-data snapshot (no buffering) ----

        [Test]
        public void InputFrame_IsPureDataSnapshot_NoBufferState()
        {
            var members = SnapshotFieldsAndProperties(typeof(InputFrame)).ToList();
            Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains("buffer")
                                         || m.ToLowerInvariant().Contains("queue")
                                         || m.ToLowerInvariant().Contains("history")),
                "InputFrame (pure-data snapshot) must not expose buffering/queue/history state.");
        }

        [Test]
        public void BufferingIsNotOwnedByCore_OrProvider()
        {
            var names = GetAllTypeNames();
            // Buffering must not be implemented in Core or the provider assemblies.
            // (Note: doc-comment mention in InputFrame.cs is not a type.)
            Assert.IsFalse(names.Any(n =>
                n.Contains("Buffer") || n.Contains("Buffered") || n.Contains("Buffering")
                || n.Contains("Queue") || n.Contains("CommandBuffer")),
                "No buffering/queue type may be exposed by Core or the Input provider assemblies.");
        }

        // ---- 4. No priority or simultaneous-input logic leaked in (Tasks 133/134) ----

        [Test]
        public void NoInputPriority_OrSimultaneous_LogicExists()
        {
            var names = GetAllTypeNames();
            var forbidden = new[]
            {
                "InputPriority", "PrioritySource", "Prioritizer", "SimultaneousResolution",
                "InputConflictResolver", "ConflictResolver", "InputPrioritySystem"
            };
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — priority is Task 133, simultaneous is Task 134.");
            }
        }

        // ---- 5. No buffer duration / queue policy invented ----

        [Test]
        public void NoBufferTuningConstants_Exist()
        {
            var names = GetAllTypeNames();
            foreach (var kw in new[] { "BufferDuration", "BufferWindow", "BufferedWindow", "BufferSize", "MaxBufferDepth", "BufferTimeout" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(kw)),
                    $"'{kw}' must not exist — no buffer duration/queue policy is defined (tuning undefined).");
            }
        }

        // ---- 6. Providers do not retain transient requests ----

        [Test]
        public void HumanProvider_ReadsTransientActionsAsOneShot_NoRetention()
        {
            // HumanPlayerInput uses GetButtonDown (one-frame edge) for one-shot
            // actions and exposes them as plain properties — no buffering member.
            var t = typeof(HumanPlayerInput);
            var members = SnapshotFieldsAndProperties(t).ToList();
            Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains("buffer")
                                         || m.ToLowerInvariant().Contains("queue")
                                         || m.ToLowerInvariant().Contains("history")
                                         || m.ToLowerInvariant().Contains("timestamp")),
                "HumanPlayerInput must not retain transient requests (no buffering member).");
        }
    }
}
