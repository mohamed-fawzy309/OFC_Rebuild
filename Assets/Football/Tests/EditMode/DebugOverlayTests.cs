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
    /// Task 65 — Debug Overlay Foundation.
    ///
    /// AUDIT RESULT (65 audit-first, re-verified): there is **no Debug Overlay** and **no production
    /// presentation runtime** to host one.
    ///   - Zero Runtime references to Overlay/DebugUI/DebugPanel/DebugWindow/DebugHUD/DebugCanvas/
    ///     DebugMenu/DeveloperConsole/Canvas/CanvasGroup/OnGUI/GUILayout/TextMeshPro/TMP_/UIDocument/
    ///     VisualElement. No UI technology is used.
    ///   - Runtime/UI contains only Football.UI.asmdef (+ meta) — no scripts; the assembly is NOT
    ///     compiled. Runtime/Camera contains only Football.Camera.asmdef — likewise empty/not compiled.
    ///   - No debug overlay toggle: existing input is gameplay-only (HumanPlayerInput: LeftShift/E/
    ///     Space/Escape for sprint/tackle/interact/pause); no F1–F5/Tab/BackQuote debug toggle exists.
    ///   - All debug categories (55–64) are POLICY-ONLY — there are NO debug data providers to present,
    ///     so an overlay would have no real diagnostic data and no gameplay to observe.
    ///   - Assembly boundary verified: Football.UI references Football.Core; Football.Core references
    ///     NOTHING (Core has zero UI/Camera dependency). Correct one-way dependency.
    ///   - No overlay prefab; no scene-bound/persistent overlay.
    ///
    /// DECISION — POLICY-ONLY (Option C): no production presentation/UI/Camera runtime and no debug
    /// data providers exist, so Task 65 defines the overlay foundation contract (ownership, data
    /// presentation boundary, toggle ownership, category visibility, Core independence, no gameplay
    /// UI) and **manufactures no DebugOverlay/DebugCanvas/DebugConsole component, no prefab, no
    /// toggle, no input action, no debug data provider framework, no GameObject/Canvas, no
    /// OnGUI/Update loop, no fake gameplay values**.
    /// </summary>
    public class DebugOverlayTests
    {
        private static IEnumerable<Assembly> OverlayRelevantAssemblies()
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
            => OverlayRelevantAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.Name == simpleName || t.FullName?.EndsWith("." + simpleName) == true);

        // ---- 65.1 Ownership — no overlay manufactured; ownership documented ----

        [Test]
        public void NoDebugOverlaySystemManufactured()
        {
            // No overlay exists; none may be built while there is no production presentation runtime
            // and no debug data providers.
            foreach (var forbidden in new[]
            {
                "DebugOverlay", "DebugCanvas", "DebugConsole", "DebugHUD", "DebugPanel", "DebugWindow",
                "DebugMenu", "DeveloperConsole", "OverlayController", "DebugOverlayView"
            })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Task 65 must not manufacture a Debug Overlay type '{forbidden}' (no presentation runtime / no data providers).");
            }
        }

        [Test]
        public void OverlayDoesNotOwnGameplayState()
        {
            // Overlay must never own/control gameplay domain state.
            foreach (var forbidden in new[] { "OverlayGameState", "OverlayScore", "OverlayClock", "OverlayPlayerState", "OverlayBallState", "OverlayAIState" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Overlay must not own gameplay state ('{forbidden}').");
            }
        }

        // ---- 65.1 / 65.3 — Toggle / input ownership (no overlay toggle exists) ----

        [Test]
        public void NoOverlayToggleOrInputActionCreated()
        {
            // Existing input (HumanPlayerInput) is gameplay-only; there is no overlay toggle and none
            // may be added without a runtime overlay. No F-key/Tab/Backquote debug hotkey exists.
            foreach (var forbidden in new[] { "DebugOverlayToggle", "OverlayToggleInput", "DebugToggle", "OverlayHotkey" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"No overlay toggle/input type may exist ('{forbidden}') without a runtime overlay.");
            }
        }

        [Test]
        public void GameplayInputDoesNotOwnDebugOverlay()
        {
            // The gameplay input is authoritative for gameplay; it must not be re-purposed to own the
            // debug overlay. Structurally: HumanPlayerInput exposes only gameplay actions.
            var inputTypes = SafeGetTypes(typeof(HumanPlayerInput).Assembly);
            var overlayInInput = inputTypes.SelectMany(SafeGetMethods)
                .Where(m => m.Name.Contains("Overlay") || m.Name.Contains("Debug"))
                .ToList();
            Assert.IsEmpty(overlayInInput,
                "HumanPlayerInput must not expose overlay/debug controls; gameplay input does not own the debug overlay.");
        }

        // ---- 65.2 Data presentation boundary — no debug data providers exist ----

        [Test]
        public void NoDebugDataProviderFrameworkCreated()
        {
            // All categories are policy-only; no provider framework may be built for the overlay.
            foreach (var forbidden in new[]
            {
                "UniversalDebugDataProvider", "IDebugDataProvider", "DebugDataRegistry",
                "IDebugDataProvider`1", "DebugDataProvider", "IDebugMetric", "DebugDataSource"
            })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Overlay must not create a data-provider framework ('{forbidden}') with no real providers/consumers.");
            }
        }

        [Test]
        public void NoFakeGameplayValuesPresented()
        {
            // Overlay must not invent runtime values (speed/velocity/AI target/score). With no
            // providers, no value-producing overlay may exist.
            foreach (var forbidden in new[] { "OverlayPlayerStats", "OverlayBallVelocity", "OverlayScoreView", "OverlayMetrics" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Overlay must not produce fake gameplay values ('{forbidden}').");
            }
        }

        // ---- 65.4 Category visibility — flags remain the config source ----

        [Test]
        public void CategoryVisibilityUsesExistingDebugFlags()
        {
            // Overlay visibility would be driven by the existing 7 category flags + master switch.
            var fields = typeof(FootballDebugSettings)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(f => f.Name)
                .ToArray();
            foreach (var expected in new[]
            {
                nameof(FootballDebugSettings.EnableMovementDebug), nameof(FootballDebugSettings.EnableBallDebug),
                nameof(FootballDebugSettings.EnableAnimationDebug), nameof(FootballDebugSettings.EnableMatchDebug),
                nameof(FootballDebugSettings.EnableAIDebug), nameof(FootballDebugSettings.EnableCameraDebug),
                nameof(FootballDebugSettings.EnablePerformanceDebug),
            })
            {
                Assert.Contains(expected, fields, $"'{expected}' must remain the category-visibility config source.");
            }
            Assert.Contains(nameof(FootballDebugSettings.DebugEnabled), fields,
                "Master DebugEnabled must gate overlay visibility (when an overlay exists).");
        }

        [Test]
        public void NoDuplicateOverlayVisibilityFlags()
        {
            var fields = typeof(FootballDebugSettings)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(f => f.Name)
                .ToArray();
            Assert.IsFalse(fields.Any(n => n.Contains("Overlay") || n.Contains("DebugCanvas") || n.Contains("ShowOverlay")),
                "Do NOT add duplicate overlay-visibility flags; reuse DebugEnabled + Enable*Debug.");
        }

        // ---- 65.5 Overlay must not become gameplay UI ----

        [Test]
        public void OverlayIsNotGameplayUI()
        {
            // Overlay is developer presentation, never a HUD/scoreboard/pause menu/inventory.
            foreach (var forbidden in new[] { "GameplayHUD", "MatchScoreboard", "PauseMenu", "PlayerControlUI", "InventoryUI" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Overlay must not become a gameplay UI ('{forbidden}').");
            }
        }

        [Test]
        public void OverlayDoesNotModifyGameplay()
        {
            foreach (var forbidden in new[] { "OverlayPause", "OverlayControlPlayer", "OverlayChangeScore", "OverlaySetMatchState", "OverlayControlCamera" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Overlay must not modify gameplay ('{forbidden}').");
            }
        }

        // ---- 65.6 Core independence ----

        [Test]
        public void CoreDoesNotDependOnUIOrOverlay()
        {
            // Football.Core references NOTHING (verified in Football.Core.asmdef). Core must never
            // gain a UI/overlay/Camera dependency. Any overlay lives in a presentation assembly that
            // references Core, never the reverse.
            var assembly = typeof(FootballDebugSettings).Assembly;
            var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.IsFalse(referenced.Contains("Football.UI"),
                "Football.Core must not reference Football.UI: Core must stay free of overlay/UI dependency.");
        }

        [Test]
        public void OverlayWouldLiveInPresentationAssemblyNotCore()
        {
            // Assert the one-way boundary: the (absent) overlay would be presentation-owned. Core
            // hosts no overlay types today.
            var coreTypes = SafeGetTypes(typeof(FootballDebugSettings).Assembly).Select(t => t.Name);
            Assert.IsFalse(coreTypes.Any(n => n.Contains("Overlay") || n.Contains("DebugCanvas") || n.Contains("DebugUI")),
                "Core must not host overlay/UI types.");
        }

        // ---- 65.7 no runtime loop / singleton / service locator / prefab ----

        [Test]
        public void NoOverlayLifecycleLoopAdded()
        {
            var runtimeTypes = OverlayRelevantAssemblies().SelectMany(a => SafeGetTypes(a));
            var loops = runtimeTypes.SelectMany(t => SafeGetMethods(t))
                .Select(m => m.Name)
                .Where(n => n.Contains("Overlay") && (n.Contains("Update") || n.Contains("LateUpdate") || n.Contains("OnGUI") || n.Contains("Start") || n.Contains("Awake")))
                .ToList();
            Assert.IsEmpty(loops,
                "Overlay must not add Update/LateUpdate/OnGUI/Awake/Start runtime loops yet (no overlay exists).");
        }

        [Test]
        public void NoOverlaySingletonOrServiceLocator()
        {
            foreach (var forbidden in new[] { "OverlayManager", "GlobalOverlay", "OverlayService", "IOverlay", "OverlayLocator" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Overlay must not create Singleton/service-locator ('{forbidden}').");
            }
        }

        [Test]
        public void NoOverlayPrefabOrSceneRequirements()
        {
            // No overlay prefab asset should exist in a running project without a runtime overlay.
            string[] needles = { "DebugOverlay", "DebugCanvas" };
            string prefabRoot = "D:\\Projects\\unity\\OFC_Rebuild\\Assets\\Football\\Prefabs";
            if (System.IO.Directory.Exists(prefabRoot))
            {
                foreach (var f in System.IO.Directory.GetFiles(prefabRoot, "*.prefab", System.IO.SearchOption.AllDirectories))
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(f);
                    Assert.IsFalse(needles.Any(n => name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0),
                        $"Unexpected overlay prefab '{name}' must not exist while there is no runtime overlay.");
                }
            }
        }

        // ---- 65.9 indicators: overlay independent of other debug boundaries ----

        [Test]
        public void NoOverlayDependencyOnLoggingThrottleTelemetry()
        {
            // Overlay is presentation; it must not fuse with logging (T62), throttling (T63), or
            // telemetry (T64).
            foreach (var forbidden in new[] { "OverlayConsole", "OverlayLogView", "OverlayThrottle", "OverlayTelemetry" })
            {
                Assert.IsNull(FindType(forbidden),
                    $"Overlay must remain independent of logging/throttle/telemetry ('{forbidden}').");
            }
        }

        private static IEnumerable<MethodInfo> SafeGetMethods(Type t)
        {
            try { return t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
            catch { return Enumerable.Empty<MethodInfo>(); }
        }
    }
}
