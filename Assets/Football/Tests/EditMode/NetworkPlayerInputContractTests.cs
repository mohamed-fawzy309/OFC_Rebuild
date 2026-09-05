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
    /// Task 137 — Future Network Input Contract.
    ///
    /// This project uses ONE device/source-neutral logical input contract:
    /// `IPlayerInput` (consumer-facing) + `InputFrame` (immutable pure-data
    /// snapshot transport). Every provider implements `IPlayerInput`.
    /// `NetworkPlayerInput` is the (future-)Network provider.
    ///
    /// AUDIT RESULT (137.1/137.2): `NetworkPlayerInput` EXISTS as a PASSIVE
    /// IPlayerInput implementation — a MonoBehaviour exposing the 14 contract
    /// members as settable auto-properties. It is an INPUT SOURCE / source
    /// adapter only ("Network inject"), NOT a networking runtime. It has NO
    /// transport/socket/packet/authority/tick/prediction/reconciliation logic.
    /// NO networking package is installed (no NGO/Mirror/FishNet/Photon/
    /// UnityTransport); `com.unity.multiplayer.center` is a wizard/UI hub, not a
    /// runtime. There is NO serialization framework, NO deterministic-networking
    /// framework, and NO IPlayerInput consumer.
    ///
    /// Future Network input delivers the SAME logical contract as Human/AI/Replay:
    /// a passive source adapter feeding IPlayerInput/InputFrame. Network metadata
    /// (sequence/tick/timestamp/authority/prediction/ack/etc.) belongs OUTSIDE the
    /// logical input contract (to future, currently-absent network infrastructure).
    ///
    /// These tests prove the verified Future Network INPUT CONTRACT: passive
    /// source adapter, all logical members present, device/source-neutral, no
    /// networking runtime in the provider, no network metadata on IPlayerInput/
    /// InputFrame, no serialization/determinism claims, and no speculative
    /// network infrastructure. They do NOT invent any networking behavior.
    /// </summary>
    public class NetworkPlayerInputContractTests
    {
        private static IEnumerable<Assembly> NetworkRelevantAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;          // Football.Core
            yield return typeof(NetworkPlayerInput).Assembly;    // Football.Input
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static string[] GetAllTypeNames()
            => NetworkRelevantAssemblies()
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

        // ---- 1. NetworkPlayerInput implements the single IPlayerInput contract ----

        [Test]
        public void NetworkPlayerInput_ImplementsIPlayerInput_AndIsMonoBehaviour()
        {
            Assert.IsTrue(typeof(IPlayerInput).IsAssignableFrom(typeof(NetworkPlayerInput)),
                "NetworkPlayerInput must implement the single consumer-facing IPlayerInput contract.");
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(NetworkPlayerInput)),
                "NetworkPlayerInput is a MonoBehaviour provider (consistent with Human/AI/Replay).");
        }

        [Test]
        public void NetworkPlayerInput_ExposesAllContractMembers()
        {
            foreach (var name in new[]
                     { "MoveDirection", "LookDirection", "Sprint", "Pass", "Shoot",
                       "Tackle", "SwitchPlayer", "SwitchDirection", "Skill",
                       "SkillDirection", "Interact", "Cancel", "Pause", "IsEnabled" })
            {
                var p = typeof(NetworkPlayerInput).GetProperty(name);
                Assert.IsNotNull(p, $"NetworkPlayerInput must expose '{name}' from IPlayerInput.");
                Assert.IsTrue(p.CanRead && p.CanWrite,
                    $"'{name}' on NetworkPlayerInput must be settable (passive externally-driven source adapter).");
            }
        }

        [Test]
        public void NetworkPlayerInput_MemberTypes_MatchContract()
        {
            Assert.AreEqual(typeof(Vector2), typeof(NetworkPlayerInput).GetProperty(nameof(NetworkPlayerInput.MoveDirection)).PropertyType);
            Assert.AreEqual(typeof(Vector2), typeof(NetworkPlayerInput).GetProperty(nameof(NetworkPlayerInput.LookDirection)).PropertyType);
            foreach (var b in new[] { nameof(NetworkPlayerInput.Sprint), nameof(NetworkPlayerInput.Pass),
                                      nameof(NetworkPlayerInput.Shoot), nameof(NetworkPlayerInput.Tackle),
                                      nameof(NetworkPlayerInput.SwitchPlayer), nameof(NetworkPlayerInput.Skill),
                                      nameof(NetworkPlayerInput.Interact), nameof(NetworkPlayerInput.Cancel),
                                      nameof(NetworkPlayerInput.Pause), nameof(NetworkPlayerInput.IsEnabled) })
            {
                Assert.AreEqual(typeof(bool), typeof(NetworkPlayerInput).GetProperty(b).PropertyType,
                    $"'{b}' must be bool on NetworkPlayerInput.");
            }
            Assert.AreEqual(typeof(Vector2), typeof(NetworkPlayerInput).GetProperty(nameof(NetworkPlayerInput.SwitchDirection)).PropertyType);
            Assert.AreEqual(typeof(Vector2), typeof(NetworkPlayerInput).GetProperty(nameof(NetworkPlayerInput.SkillDirection)).PropertyType);
        }

        // ---- 2. Network provider is source-neutral (logical input, not transport) ----

        [Test]
        public void NetworkPlayerInput_IsSourceNeutral_NoTransportOrDeviceApis()
        {
            var src = File.ReadAllText(Path.Combine(Application.dataPath,
                "Football/Runtime/Input/NetworkPlayerInput.cs"));
            Assert.IsFalse(src.Contains("UnityEngine.Input."),
                "NetworkPlayerInput must not sample physical device input.");
            Assert.IsFalse(src.Contains("NetworkTransport"), "NetworkPlayerInput must not reference a network transport.");
            Assert.IsFalse(src.Contains("using Unity.Netcode"), "NetworkPlayerInput must not depend on a netcode package.");
            foreach (var kw in new[] { "Socket", "Udp", "Tcp", "Packet", "Serialize", "Deserialize" })
            {
                Assert.IsFalse(src.Contains(kw),
                    $"NetworkPlayerInput must not reference transport/postdata token '{kw}'.");
            }
        }

        // ---- 3. Passive source adapter (no sampling, no lifecycle, no network callbacks) ----

        [Test]
        public void NetworkPlayerInput_HasNoUpdateOrLifecycleMethods()
        {
            var methods = typeof(NetworkPlayerInput).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var name in new[] { "Update", "FixedUpdate", "LateUpdate", "Awake", "Start",
                                         "OnEnable", "OnDisable", "OnDestroy", "SampleInput", "ProduceInput",
                                         "OnSerialize", "OnDeserialize", "OnNetworkUpdate", "OnReceive" })
            {
                Assert.IsFalse(methods.Any(m => m.Name == name),
                    $"NetworkPlayerInput must not have a '{name}' method (passive source adapter; no sampling/network callbacks).");
            }
        }

        [Test]
        public void NetworkPlayerInput_DefaultIsEnabled_IsTrue()
        {
            var input = new NetworkPlayerInput();
            Assert.IsTrue(input.IsEnabled,
                "NetworkPlayerInput.IsEnabled must default to true (lifecycle gate), consistent with sibling providers.");
        }

        // ---- 4. No network runtime/tick/authority/prediction members on the provider ----

        [Test]
        public void NetworkPlayerInput_HasNoNetworkMetadataOrRunTimeMembers()
        {
            var members = SnapshotFieldsAndProperties(typeof(NetworkPlayerInput)).ToList();
            foreach (var kw in new[] { "NetworkTick", "Sequence", "InputCommandId", "ClientId", "PlayerId",
                                       "Timestamp", "ServerTime", "Authority", "Prediction", "Rollback",
                                       "Reconciliation", "Ack", "Reliable", "PacketId", "Checksum",
                                       "Compression", "SnapshotId", "Replication", "EndPoint", "Channel" })
            {
                Assert.IsFalse(members.Any(m => m.Contains(kw)),
                    $"NetworkPlayerInput must not expose network metadata/runtime member '{kw}' (source adapter only).");
            }
        }

        // ---- 5. No speculative network infrastructure exists ----

        [Test]
        public void NoNetworkRuntime_OrTransport_OrNetcode_Exists()
        {
            var names = GetAllTypeNames().Where(n => !IsKnownProvider(n)).ToArray();
            foreach (var f in new[] { "NetworkManager", "NetworkTransport", "NetworkConnection",
                                      "NetworkClient", "NetworkServer", "NetworkPlayerRuntime",
                                      "NetworkSpawner", "ReplicationManager", "NetcodeManager",
                                      "UnityTransport", "PacketProcessor", "NetworkClock",
                                      "NetworkSerializer", "PredictionManager", "RollbackManager" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — no speculative network/transport/netcode infrastructure is allowed.");
            }
        }

        [Test]
        public void NoDeterministicNetworkingFramework_Exists()
        {
            // No fixed network tick, no command sequence, no prediction/reconciliation/
            // rollback/resend/loss/compression/hash framework. Determinism is NOT implemented.
            var names = GetAllTypeNames().Where(n => !IsKnownProvider(n)).ToArray();
            foreach (var kw in new[] { "NetworkTick", "CommandSequence", "ClientPrediction",
                                       "Reconciliation", "Rollback", "InputResend", "PacketOrdering",
                                       "PacketLoss", "InputCompression", "InputHash", "InputChecksum",
                                       "ServerAuthoritativeInput", "NetworkFrame" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(kw)),
                    $"'{kw}' must not exist — no deterministic-networking framework is implemented (deferred).");
            }
        }

        private static bool IsKnownProvider(string fullName)
        {
            var legit = new[]
            {
                "Football.Input.NetworkPlayerInput", "Football.Input.AIPlayerInput",
                "Football.Input.HumanPlayerInput", "Football.Input.ReplayPlayerInput",
                "Football.Core.IPlayerInput", "Football.Core.InputFrame", "Football.Core.Module",
            };
            return legit.Contains(fullName);
        }

        // ---- 6. No network metadata on IPlayerInput / InputFrame ----

        [Test]
        public void NoNetworkMetadata_InInputFrame_OrIPlayerInput()
        {
            // Case-sensitive (Ordinal) matching: the legit contract member
            // 'Tackle' contains lowercase "ack", which must not collide with the
            // network acronym 'Ack'. None of these keywords appear in the actual
            // (capitalized) logical input members.
            foreach (var t in new[] { typeof(InputFrame), typeof(IPlayerInput) })
            {
                var members = SnapshotFieldsAndProperties(t).ToList();
                foreach (var kw in new[] { "Network", "Sequence", "Tick", "Timestamp", "ServerTime",
                                           "Authority", "Prediction", "ClientId", "PlayerId",
                                           "Packet", "Ack", "Snapshot", "Rollback", "Replication" })
                {
                    Assert.IsFalse(members.Any(m => m.Contains(kw, StringComparison.Ordinal)),
                        $"'{t.Name}' must not carry network metadata member '{kw}' — network control stays outside the logical input contract.");
                }
            }
        }

        // ---- 7. InputFrame remains pure data ----

        [Test]
        public void InputFrame_RemainsPureDataSnapshot()
        {
            var members = SnapshotFieldsAndProperties(typeof(InputFrame)).ToList();
            Assert.IsFalse(members.Any(m => m.ToLowerInvariant().Contains("network")
                                         || m.ToLowerInvariant().Contains("sequence")
                                         || m.ToLowerInvariant().Contains("tick")
                                         || m.ToLowerInvariant().Contains("timestamp")
                                         || m.ToLowerInvariant().Contains("authority")
                                         || m.ToLowerInvariant().Contains("packet")
                                         || m.ToLowerInvariant().Contains("snapshotid")),
                "InputFrame (pure-data snapshot) must not carry network/transport metadata.");
        }

        // ---- 8. No parallel network-specific input contract ----

        [Test]
        public void NoParallelNetworkInputContract_Exists()
        {
            var names = GetAllTypeNames();
            foreach (var f in new[] { "INetworkInput", "INetworkPlayerInputContract", "NetworkInputFrame",
                                      "NetworkInputContract", "NetworkPacketInput", "INetworkContract" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — this project consolidates on the single IPlayerInput contract.");
            }
        }

        // ---- 9. Contract surface consistent with sibling providers (DeclaredOnly) ----

        [Test]
        public void NetworkPlayerInput_MatchesContractAndSiblingProviders_NoExtraLogicalInputMembers()
        {
            var declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var ifaceProps = typeof(IPlayerInput)
                .GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var netProps = typeof(NetworkPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var humanProps = typeof(HumanPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var aiProps = typeof(AIPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var replayProps = typeof(ReplayPlayerInput)
                .GetProperties(declared).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

            Assert.IsTrue(netProps.SequenceEqual(ifaceProps),
                $"NetworkPlayerInput must declare exactly the IPlayerInput members. Net={string.Join(",", netProps)} iface={string.Join(",", ifaceProps)}");
            Assert.IsTrue(netProps.SequenceEqual(humanProps),
                "NetworkPlayerInput must match HumanPlayerInput's declared IPlayerInput members (contract consistency).");
            Assert.IsTrue(netProps.SequenceEqual(aiProps),
                "NetworkPlayerInput must match AIPlayerInput's declared IPlayerInput members (contract consistency).");
            Assert.IsTrue(netProps.SequenceEqual(replayProps),
                "NetworkPlayerInput must match ReplayPlayerInput's declared IPlayerInput members (contract consistency).");
        }
    }
}
