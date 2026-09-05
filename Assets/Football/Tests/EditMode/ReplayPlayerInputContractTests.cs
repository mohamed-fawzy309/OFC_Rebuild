using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Football.Core;
using Football.Input;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 136 — Replay Input Contract.
    ///
    /// This project uses ONE device-neutral logical input contract: `IPlayerInput`
    /// (consumer-facing) + `InputFrame` (immutable pure-data snapshot transport).
    /// Every provider implements `IPlayerInput`. `ReplayPlayerInput` is the Replay
    /// provider.
    ///
    /// AUDIT RESULT (136.1): `ReplayPlayerInput` EXISTS as a PASSIVE IPlayerInput
    /// implementation — a MonoBehaviour exposing the 14 contract members as
    /// settable auto-properties. It is an INPUT SOURCE only ("Replay inject"), NOT
    /// a replay recorder/player. There is NO replay recording / playback /
    /// serialization / save-load / timeline infrastructure anywhere: no recorder,
    /// no playback controller, no serializable input stream, no replay assets, and
    /// no deterministic-replay framework. (GameClock notes it is "replay-compatible"
    /// as design intent only; no such system is implemented.)
    ///
    /// Replay input acts as a provider of previously-captured LOGICAL input on the
    /// same IPlayerInput/InputFrame model as Human/AI/Network. Replay metadata /
    /// playback control / storage belong OUTSIDE the logical input contract (to
    /// future, currently-absent replay infrastructure).
    ///
    /// These tests prove the verified Replay INPUT CONTRACT: passive provider, all
    /// logical members present, device-neutral, no recorder/player/serialization
    /// responsibilities, no replay metadata on IPlayerInput/InputFrame, IsEnabled
    /// default, and no speculative replay infrastructure. They do NOT invent or
    /// test any recording/playback behavior.
    /// </summary>
    public class ReplayPlayerInputContractTests
    {
        private static IEnumerable<Assembly> ReplayRelevantAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;          // Football.Core
            yield return typeof(ReplayPlayerInput).Assembly;     // Football.Input
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static string[] GetAllTypeNames()
            => ReplayRelevantAssemblies()
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

        // ---- 1. ReplayPlayerInput implements the single IPlayerInput contract ----

        [Test]
        public void ReplayPlayerInput_ImplementsIPlayerInput_AndIsMonoBehaviour()
        {
            Assert.IsTrue(typeof(IPlayerInput).IsAssignableFrom(typeof(ReplayPlayerInput)),
                "ReplayPlayerInput must implement the single consumer-facing IPlayerInput contract.");
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(ReplayPlayerInput)),
                "ReplayPlayerInput is a MonoBehaviour provider (consistent with Human/AI/Network).");
        }

        [Test]
        public void ReplayPlayerInput_ExposesAllContractMembers()
        {
            foreach (var name in new[]
                     { "MoveDirection", "LookDirection", "Sprint", "Pass", "Shoot",
                       "Tackle", "SwitchPlayer", "SwitchDirection", "Skill",
                       "SkillDirection", "Interact", "Cancel", "Pause", "IsEnabled" })
            {
                var p = typeof(ReplayPlayerInput).GetProperty(name);
                Assert.IsNotNull(p, $"ReplayPlayerInput must expose '{name}' from IPlayerInput.");
                Assert.IsTrue(p.CanRead && p.CanWrite,
                    $"'{name}' on ReplayPlayerInput must be settable (passive externally-driven provider).");
            }
        }

        [Test]
        public void ReplayPlayerInput_MemberTypes_MatchContract()
        {
            Assert.AreEqual(typeof(Vector2), typeof(ReplayPlayerInput).GetProperty(nameof(ReplayPlayerInput.MoveDirection)).PropertyType);
            Assert.AreEqual(typeof(Vector2), typeof(ReplayPlayerInput).GetProperty(nameof(ReplayPlayerInput.LookDirection)).PropertyType);
            foreach (var b in new[] { nameof(ReplayPlayerInput.Sprint), nameof(ReplayPlayerInput.Pass),
                                      nameof(ReplayPlayerInput.Shoot), nameof(ReplayPlayerInput.Tackle),
                                      nameof(ReplayPlayerInput.SwitchPlayer), nameof(ReplayPlayerInput.Skill),
                                      nameof(ReplayPlayerInput.Interact), nameof(ReplayPlayerInput.Cancel),
                                      nameof(ReplayPlayerInput.Pause), nameof(ReplayPlayerInput.IsEnabled) })
            {
                Assert.AreEqual(typeof(bool), typeof(ReplayPlayerInput).GetProperty(b).PropertyType,
                    $"'{b}' must be bool on ReplayPlayerInput.");
            }
            Assert.AreEqual(typeof(Vector2), typeof(ReplayPlayerInput).GetProperty(nameof(ReplayPlayerInput.SwitchDirection)).PropertyType);
            Assert.AreEqual(typeof(Vector2), typeof(ReplayPlayerInput).GetProperty(nameof(ReplayPlayerInput.SkillDirection)).PropertyType);
        }

        // ---- 2. Replay provider is device-neutral (logical input, not device input) ----

        [Test]
        public void ReplayPlayerInput_IsDeviceNeutral_NoDeviceApis()
        {
            var src = File.ReadAllText(Path.Combine(Application.dataPath,
                "Football/Runtime/Input/ReplayPlayerInput.cs"));
            Assert.IsFalse(src.Contains("UnityEngine.Input."),
                "ReplayPlayerInput must not sample physical device input.");
            Assert.IsFalse(src.Contains("GetAxis"), "ReplayPlayerInput must not read axes.");
            Assert.IsFalse(src.Contains("GetButton"), "ReplayPlayerInput must not read buttons.");
            Assert.IsFalse(src.Contains("GetKey"), "ReplayPlayerInput must not read keys.");
        }

        // ---- 3. Passive provider (no update path, no sampling, no lifecycle) ----

        [Test]
        public void ReplayPlayerInput_HasNoUpdateOrLifecycleMethods()
        {
            var methods = typeof(ReplayPlayerInput).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate", "Awake", "Start",
                                         "OnEnable", "OnDisable", "OnDestroy", "SampleInput", "ProduceInput" })
            {
                Assert.IsFalse(methods.Any(m => m.Name == name),
                    $"ReplayPlayerInput must not have a '{name}' method (passive provider; does not sample/produce input).");
            }
        }

        [Test]
        public void ReplayPlayerInput_DefaultIsEnabled_IsTrue()
        {
            var input = new ReplayPlayerInput();
            Assert.IsTrue(input.IsEnabled,
                "ReplayPlayerInput.IsEnabled must default to true (lifecycle gate), consistent with the other providers.");
        }

        // ---- 4. No recorder / player / serializer / playback responsibilities ----

        [Test]
        public void ReplayPlayerInput_HasNoRecorderOrPlaybackMembers()
        {
            // NOTE: 'Player' is intentionally NOT checked here, because the legit
            // contract member 'SwitchPlayer' contains it (false positive).
            var members = SnapshotFieldsAndProperties(typeof(ReplayPlayerInput)).ToList();
            foreach (var kw in new[] { "Record", "Recorder", "Playback", "Tick", "Frame",
                                       "Timestamp", "Timeline", "Seek", "Speed", "Serialize",
                                       "Deserialize", "Save", "Load", "Buffer", "Queue", "Capture" })
            {
                Assert.IsFalse(members.Any(m => m.Contains(kw)),
                    $"ReplayPlayerInput must not expose recorder/player/serialization member '{kw}'.");
            }
        }

        [Test]
        public void ReplayPlayerInput_HasNoReplayMetadata_OrGameplayIntent()
        {
            var members = SnapshotFieldsAndProperties(typeof(ReplayPlayerInput)).ToList();
            foreach (var kw in new[] { "ReplayTick", "ReplayFrame", "ReplayTimestamp", "TargetPlayerId",
                                       "ReplayActionId", "DecisionState", "TacticalIntent",
                                       "SerializedGameplayState", "Checksum", "RandomSeed",
                                       "AuthoritativeWorldState", "PlaybackSpeed", "SeekState" })
            {
                Assert.IsFalse(members.Any(m => m.Contains(kw)),
                    $"ReplayPlayerInput must not expose replay-metadata/gameplay member '{kw}' (input provider only).");
            }
        }

        // ---- 5. No speculative replay infrastructure exists ----

        [Test]
        public void NoReplayRuntime_OrRecorder_OrSerializer_Exists()
        {
            // Exclude the legit provider/contract types so their names (e.g.
            // 'ReplayPlayerInput') do not cause false substring matches.
            var legit = new[]
            {
                "Football.Input.ReplayPlayerInput", "Football.Input.AIPlayerInput",
                "Football.Input.HumanPlayerInput", "Football.Input.NetworkPlayerInput",
                "Football.Core.IPlayerInput", "Football.Core.InputFrame",
            };
            var names = GetAllTypeNames().Where(n => !legit.Contains(n)).ToArray();
            foreach (var f in new[] { "ReplayManager", "ReplayController", "ReplayRecorder",
                                      "ReplayPlayer", "ReplayPlayback", "ReplaySerializer",
                                      "ReplayRecording", "ReplayTimeline", "InputSerializer",
                                      "SerializedInput", "ReplayClock", "PlaybackController",
                                      "InputCapture", "ReplaySystem" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — no speculative replay/playback/serialization infrastructure is allowed.");
            }
        }

        [Test]
        public void NoDeterministicReplayFramework_Exists()
        {
            // No fixed-tick replay, no rollback, no checksum/CRC/hash, no fixed
            // authoritative replay state — determinism guarantees are NOT implemented.
            var names = GetAllTypeNames();
            foreach (var kw in new[] { "Checksum", "FrameHash", "Rollback", "FixedTickReplay",
                                       "ReplayState", "ReplaySeed", "AuthoritativeReplay",
                                       "ReplaySnapshot", "ReplayArchive" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(kw)),
                    $"'{kw}' must not exist — no deterministic-replay framework is implemented (deferred).");
            }
        }

        // ---- 6. No replay metadata on IPlayerInput / InputFrame ----

        [Test]
        public void NoReplayMetadata_InInputFrame_OrIPlayerInput()
        {
            foreach (var t in new[] { typeof(InputFrame), typeof(IPlayerInput) })
            {
                var members = SnapshotFieldsAndProperties(t).ToList();
                foreach (var kw in new[] { "ReplayTick", "ReplayFrame", "ReplayTimestamp", "Playback",
                                           "ReplayIndex", "Recorded", "SerializedState", "Tick" })
                {
                    Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains(kw.ToLowerInvariant())),
                        $"'{t.Name}' must not carry replay metadata member '{kw}' — replay control stays outside the logical input contract.");
                }
            }
        }

        // ---- 7. InputFrame remains pure data ----

        [Test]
        public void InputFrame_RemainsPureDataSnapshot()
        {
            var members = SnapshotFieldsAndProperties(typeof(InputFrame)).ToList();
            Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains("replay")
                                         || m.ToLowerInvariant().Contains("playback")
                                         || m.ToLowerInvariant().Contains("record")
                                         || m.ToLowerInvariant().Contains("timeline")),
                "InputFrame (pure-data snapshot) must not carry replay/playback/recording state.");
        }

        // ---- 8. No parallel replay-specific input contract ----

        [Test]
        public void NoParallelReplayInputContract_Exists()
        {
            var names = GetAllTypeNames();
            foreach (var f in new[] { "IReplayInput", "IReplayPlayerInputContract", "ReplayFrame",
                                      "ReplayInputContract", "ReplayPlayerInputFrame", "IReplayContract" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — this project consolidates on the single IPlayerInput contract.");
            }
        }

        // ---- 9. Contract surface consistent with sibling providers (DeclaredOnly) ----

        [Test]
        public void ReplayPlayerInput_Surface_MatchesContract_NoExtraLogicalInputMembers()
        {
            var declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var ifaceProps = typeof(IPlayerInput)
                .GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var replayProps = typeof(ReplayPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var humanProps = typeof(HumanPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var aiProps = typeof(AIPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var networkProps = typeof(NetworkPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

            Assert.IsTrue(replayProps.SequenceEqual(ifaceProps),
                $"ReplayPlayerInput must declare exactly the IPlayerInput members. Replay={string.Join(",", replayProps)} iface={string.Join(",", ifaceProps)}");
            Assert.IsTrue(replayProps.SequenceEqual(humanProps),
                "ReplayPlayerInput must match HumanPlayerInput's declared IPlayerInput members (contract consistency).");
            Assert.IsTrue(replayProps.SequenceEqual(aiProps),
                "ReplayPlayerInput must match AIPlayerInput's declared IPlayerInput members (contract consistency).");
            Assert.IsTrue(replayProps.SequenceEqual(networkProps),
                "ReplayPlayerInput must match NetworkPlayerInput's declared IPlayerInput members (contract consistency).");
        }
    }
}
