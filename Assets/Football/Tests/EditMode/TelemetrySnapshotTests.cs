using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Football.Input;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 64 — Telemetry Snapshots.
    ///
    /// AUDIT RESULT (64 audit-first, re-verified): there is **no telemetry runtime infrastructure**
    /// and **no telemetry consumer**.
    ///   - Zero Runtime references to Telemetry/Snapshot/RingBuffer/CircularBuffer/Recorder/Archive/
    ///     Persistence/Serialize/Deserialize/Analytics/WebRequest/UnityWebRequest/Upload/Replay/
    ///     Capture/History. (ReplayPlayerInput is an *input source* — it is input handling, not a
    ///     replay recorder.)
    ///   - No snapshot model, no storage, no retention, no capture scheduler exists anywhere.
    ///   - FootballDebugSettings has **no** EnableTelemetry/Telemetry/CaptureFrequency/Retention/
    ///     MaxSnapshots fields (only a tooltip string mentioning "telemetry capture").
    ///   - No telemetry/diagnostics asmdef. No per-frame update loop beyond HumanPlayerInput.Update();
    ///     zero FixedUpdate/LateUpdate. No CollectionType list/Dictionary telemetry buffers.
    ///
    /// DECISION — POLICY-ONLY: with **no real telemetry consumer**, Task 64 defines the snapshot
    /// contract (observational-only responsibility, data model, immutability, capture/retention
    /// policy, mutation protections) and **manufactures no TelemetryManager / TelemetrySystem /
    /// TelemetryService / TelemetryRecorder / TelemetryBuffer, no snapshot model, no storage, no
    /// retention, no capture scheduler, no network/upload, no replay, no event bus, no Singleton, no
    /// Service Locator, no per-frame loop**.
    /// </summary>
    public class TelemetrySnapshotTests
    {
        private static IEnumerable<Assembly> TelemetryRelevantAssemblies()
        {
            yield return typeof(FootballDebugSettings).Assembly; // Football.Core
            yield return typeof(GameClock).Assembly;             // Football.Core
            yield return typeof(GameEvents).Assembly;            // Football.Core
            yield return typeof(HumanPlayerInput).Assembly;      // Football.Input
            yield return typeof(PlayerStateId).Assembly;         // Football.Players
            yield return typeof(GameStateId).Assembly;           // Football.Match
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        private static Type FindType(string simpleName)
            => TelemetryRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.Name == simpleName || t.FullName?.EndsWith("." + simpleName) == true);

        private static IEnumerable<MethodInfo> SafeGetMethods(Type t)
        {
            try { return t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
            catch { return Enumerable.Empty<MethodInfo>(); }
        }

        private static string[] GetAllTypeNames()
            => TelemetryRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        // ---- 64.1 Responsibility — no telemetry infra, no consumer manufactured ----

        [Test]
        public void NoTelemetrySystemManufactured()
        {
            foreach (var forbidden in new[]
            {
                "TelemetryManager", "TelemetrySystem", "TelemetryService", "TelemetryRecorder",
                "TelemetryBuffer", "TelemetrySnapshot", "SnapshotStore", "TelemetryBus", "GameTelemetrySnapshot"
            })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Task 64 must not manufacture telemetry infrastructure '{forbidden}' (no real consumer).");
            }
        }

        [Test]
        public void TelemetryObservationalOnly_NoAuthorityTypeExists()
        {
            // Telemetry observes; the audit found no snapshot/observation model at all, so no
            // telemetry type of any kind (authoritative or otherwise) exists.
            var names = GetAllTypeNames();
            Assert.IsFalse(names.Any(n => n.Contains("Telemetry") || n.Contains("Snapshot")),
                "No Telemetry/Snapshot type may exist while there is no consumer.");
        }

        // ---- 64.2 Data model / timestamp — no snapshot model; GameClock not wired to telemetry ----

        [Test]
        public void TelemetryDoesNotOwnGameClock()
        {
            // GameClock (T49) is match time; telemetry may never own/control it, and today it is
            // not wired into any telemetry model.
            var gameClock = SafeGetTypes(typeof(GameClock).Assembly)
                .SelectMany(t => SafeGetMethods(t))
                .Where(m => m.Name.Contains("Telemetry") || m.Name.Contains("Snapshot") || m.Name.Contains("Capture"))
                .ToList();
            Assert.IsEmpty(gameClock,
                "GameClock must not expose telemetry/snapshot/capture behaviour; telemetry never owns match time.");
        }

        [Test]
        public void NoUnusedTelemetryConfigurationFlagCreated()
        {
            // No EnableTelemetry/CaptureFrequency/Retention/MaxSnapshots config may be added while
            // there is no capture implementation to drive.
            var fields = typeof(FootballDebugSettings)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(f => f.Name)
                .ToArray();
            foreach (var name in new[] { "EnableTelemetry", "CaptureFrequency", "Retention", "MaxSnapshots", "SamplingInterval" })
            {
                Assert.IsFalse(fields.Contains(name),
                    $"Do NOT add unused telemetry config field '{name}' without a capture implementation.");
            }
            // The existing config is purely enablement/visualization (no telemetry knobs).
            Assert.IsTrue(fields.Contains(nameof(FootballDebugSettings.DebugEnabled)),
                "Master DebugEnabled switch must remain the sole debug master.");
        }

        // ---- 64.3 Immutability — snapshot semantics are policy (no model to hold refs) ----

        [Test]
        public void NoLiveObjectReferenceSnapshotContainer()
        {
            // If a snapshot ever existed it must not retain live gameplay references; no such
            // container exists, and none may be added without a consumer.
            foreach (var forbidden in new[] { "SnapshotContainer", "SnapshotRecord", "SnapshotValue" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"No snapshot container/record may exist ('{forbidden}').");
            }
        }

        // ---- 64.4 Capture frequency — no scheduler exists; on-demand/policy only ----

        [Test]
        public void NoCaptureSchedulerCreated()
        {
            foreach (var forbidden in new[] { "CaptureScheduler", "SnapshotScheduler", "TelemetrySampler", "CaptureRateController" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not add a capture scheduler ('{forbidden}') — capture policy is on-demand/deferred.");
            }
        }

        [Test]
        public void NoPerFrameCaptureLoopAdded()
        {
            // No telemetry Update/FixedUpdate/LateUpdate loop may exist; capture is on-demand policy.
            foreach (var forbidden in new[] { "TelemetryUpdate", "TelemetryFixedUpdate", "TelemetryLateUpdate", "CaptureEveryFrame" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not add a per-frame/capture loop ('{forbidden}').");
            }
        }

        // ---- 64.5 Retention — no unbounded history / no buffers ----

        [Test]
        public void NoUnboundedHistoryOrBufferCreated()
        {
            foreach (var forbidden in new[] { "SnapshotHistory", "TelemetryHistory", "SnapshotBuffer", "TelemetryRingBuffer", "SnapshotArchive" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not create history/buffer/archive ('{forbidden}'); retention is deferred.");
            }
        }

        [Test]
        public void NoRetentionPolicyOwnerManufactured()
        {
            foreach (var forbidden in new[] { "RetentionPolicy", "SnapshotRetention", "TelemetryRetention" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"No retention-policy type may exist ('{forbidden}') without a capture implementation.");
            }
        }

        // ---- 64.6 Mutation protection — no gameplay control through telemetry ----

        [Test]
        public void TelemetryDoesNotOwnOrMutateGameplay()
        {
            // No telemetry command/authority may exist that touches gameplay.
            foreach (var forbidden in new[] { "TelemetryCommand", "SnapshotCommand", "TelemetryAction", "TelemetryController", "TelemetryPlayerControl" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not create commands/control ('{forbidden}').");
            }
        }

        [Test]
        public void TelemetryDoesNotOwnSimulationTimePausePhysics()
        {
            foreach (var forbidden in new[] { "TelemetrySimulation", "TelemetryPause", "TelemetryPhysics", "TelemetryFixedStep" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not own simulation/pause/physics ('{forbidden}').");
            }
        }

        // ---- anti-boundaries ----

        [Test]
        public void NoTelemetryEventBusCreated()
        {
            // GameEvents remains the single event bus; telemetry must not add a second one.
            Assert.IsNotNull(typeof(GameEvents), "GameEvents remains the single event bus.");
            foreach (var forbidden in new[] { "TelemetryEventBus", "SnapshotBus", "TelemetryEvent", "SnapshotEvent" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not create a second event bus ('{forbidden}').");
            }
        }

        [Test]
        public void NoTelemetryLoggerThrottleProfilerCreated()
        {
            foreach (var forbidden in new[]
            {
                "TelemetryLogger", "TelemetryThrottle", "TelemetryProfiler", "TelemetryMetrics", "TelemetryFrameTimer"
            })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not create logging/throttling/profiling ('{forbidden}').");
            }
        }

        [Test]
        public void NoTelemetryNetworkUploadOrReplayCreated()
        {
            foreach (var forbidden in new[]
            {
                "TelemetryUpload", "TelemetrySender", "TelemetryHTTP", "TelemetryPersistence", "TelemetryReplay", "ReplayRecorder"
            })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not create network/upload/persistence/replay ('{forbidden}').");
            }
        }

        [Test]
        public void NoTelemetrySingletonOrServiceLocatorCreated()
        {
            foreach (var forbidden in new[] { "TelemetryManager", "GlobalTelemetry", "TelemetryService", "ITelemetry", "TelemetryLocator" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Telemetry must not create Singleton/service-locator ('{forbidden}').");
            }
        }

        [Test]
        public void TelemetryDoesNotDependOnLoggingThrottleOrOverlay()
        {
            // Telemetry is independent: no dependency on logging (T62) / throttling (T63) / overlay (T65).
            Assert.IsNull(FindType("TelemetryDependency"),
                "Telemetry must not model dependencies on logging/throttle/overlay.");
        }

        // ---- no per-frame / fixed-step loops (re-affirm architecture) ----

        [Test]
        public void NoTelemetryUpdateFixedUpdateLateUpdateLoops()
        {
            // No telemetry lifecycle loop exists anywhere in Runtime.
            var runtimeTypes = TelemetryRelevantAssemblies().SelectMany(a => SafeGetTypes(a));
            var loops = runtimeTypes.SelectMany(t => SafeGetMethods(t))
                .Select(m => m.Name)
                .Where(n => n.Contains("Telemetry") &&
                            (n.Contains("Update") || n.Contains("FixedUpdate") || n.Contains("LateUpdate")))
                .ToList();
            Assert.IsEmpty(loops,
                "Telemetry must not add Update/FixedUpdate/LateUpdate loops.");
        }

        // ---- structured errors / existing architecture unaffected ----

        [Test]
        public void TelemetryDoesNotDisturbStructuredErrors()
        {
            // Telemetry must not add a second error path or affect structured exceptions.
            Assert.IsNotNull(FindType("BootstrapError"), "BootstrapError (T46) remains the structured error path.");
            Assert.IsNotNull(FindType("ServiceNotFoundException"), "ServiceNotFoundException remains an exception.");
            Assert.IsNotNull(FindType("DuplicateServiceException"), "DuplicateServiceException remains an exception.");
        }
    }
}
