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
    /// Task 131 — Remapping input contract.
    ///
    /// Task 131 concerns the INPUT BINDING / CONTROL CONFIGURATION layer:
    ///
    ///     Mapping   = default physical control → logical action
    ///     Remapping = changing the physical control assigned to an existing
    ///                 logical action (logical meaning unchanged).
    ///
    /// The audit proved that this project ships DEFAULT/STATIC bindings only via
    /// the Legacy Input Manager (InputManager.asset positiveButton /
    /// altPositiveButton / axis), consumed by HumanPlayerInput through named
    /// axes and GetButton/GetButtonDown. NO runtime/user-configurable remapping
    /// system exists: no PlayerPrefs, no JSON binding store, no ScriptableObject
    /// binding model, no InputActionRebinding, no remapping UI, no persistence,
    /// no reset-to-default, no conflict handling.
    ///
    /// Conclusion: PASS WITH DEFERRED — default bindings exist and are valid,
    /// but player-facing runtime remapping is UNDEFINED and NOT implemented.
    /// Task 131 makes NO production change and introduces NO speculative
    /// remapping framework.
    ///
    /// Status assertions:
    ///   - Default/static mappings remain intact.
    ///   - Logical action semantics are unchanged.
    ///   - Binding configuration is NOT gameplay state and is NOT in InputFrame
    ///     or IPlayerInput.
    ///   - No duplicate binding authority exists.
    ///   - No speculative remapping system was added.
    ///   - Gameplay / Core remain device-independent.
    ///   - Runtime remapping is explicitly NOT claimed.
    ///
    /// NOT under test: buffering (132), priority (133), simultaneous input
    /// (134), gameplay, any runtime rebinding behavior (not implemented).
    /// </summary>
    public class RemappingInputContractTests
    {
        private static string ProjectRoot
            => Directory.GetParent(Application.dataPath).FullName;

        private static string InputManagerPath
            => Path.Combine(ProjectRoot, "ProjectSettings", "InputManager.asset");

        private static string ReadProjectFile(string path)
        {
            Assert.IsTrue(File.Exists(path), $"Project file must exist: {path}");
            return File.ReadAllText(path);
        }

        private class AxisInfo
        {
            public string Name;
            public int Type;
            public string PositiveButton;
            public string NegativeButton;
            public string AltPositiveButton;
            public string AltNegativeButton;
        }

        /// <summary>
        /// Line-based parser (robust to Windows \r\n) that captures the axis
        /// bindings from InputManager.asset.
        /// </summary>
        private static List<AxisInfo> ParseAxes(string assetText)
        {
            var lines = assetText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var result = new List<AxisInfo>();
            AxisInfo current = null;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("m_Name:"))
                {
                    if (current != null) result.Add(current);
                    current = new AxisInfo();
                    current.Name = line.Substring("m_Name:".Length).Trim();
                    current.Type = -1;
                }
                else if (current != null)
                {
                    if (line.StartsWith("type:"))
                        int.TryParse(line.Substring("type:".Length).Trim(), out current.Type);
                    else if (line.StartsWith("positiveButton:"))
                        current.PositiveButton = line.Substring("positiveButton:".Length).Trim();
                    else if (line.StartsWith("negativeButton:"))
                        current.NegativeButton = line.Substring("negativeButton:".Length).Trim();
                    else if (line.StartsWith("altPositiveButton:"))
                        current.AltPositiveButton = line.Substring("altPositiveButton:".Length).Trim();
                    else if (line.StartsWith("altNegativeButton:"))
                        current.AltNegativeButton = line.Substring("altNegativeButton:".Length).Trim();
                }
            }
            if (current != null) result.Add(current);
            return result;
        }

        private static IEnumerable<Assembly> ProviderAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;
            yield return typeof(HumanPlayerInput).Assembly;
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

        // ---- 1. Mapping vs Remapping: default/static bindings exist ----

        [Test]
        public void KeyboardAndControllerDefaultBindings_ExistAndAreStable()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            // Keyboard movement (type 0) axes.
            var kbH = axes.First(a => a.Name == "Horizontal" && a.Type == 0);
            var kbV = axes.First(a => a.Name == "Vertical" && a.Type == 0);
            Assert.IsNotEmpty(kbH.PositiveButton, "Keyboard Horizontal must have a default positive binding.");
            Assert.IsNotEmpty(kbH.NegativeButton, "Keyboard Horizontal must have a default negative binding.");
            Assert.IsNotEmpty(kbV.PositiveButton, "Keyboard Vertical must have a default positive binding.");
            Assert.IsNotEmpty(kbV.NegativeButton, "Keyboard Vertical must have a default negative binding.");
            // Controller movement (type 2) axes exist (default analog bindings).
            var joyH = axes.First(a => a.Name == "Horizontal" && a.Type == 2);
            var joyV = axes.First(a => a.Name == "Vertical" && a.Type == 2);
            Assert.AreEqual(2, joyH.Type, "Joystick Horizontal must be type 2 (analog).");
            Assert.AreEqual(2, joyV.Type, "Joystick Vertical must be type 2 (analog).");
        }

        [Test]
        public void LogicalActions_KeepTheirDefaultMappings()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            assertAxisPositiveButton(axes, "Sprint", "left shift");
            assertAxisPositiveButton(axes, "Tackle", "e");
            assertAxisPositiveButton(axes, "Interact", "space");
            assertAxisPositiveButton(axes, "Pause", "escape");
            assertAxisPositiveButton(axes, "Fire1", "left ctrl");   // Shoot
            assertAxisPositiveButton(axes, "Fire2", "left alt");    // Pass
            assertAxisPositiveButton(axes, "Cancel", "escape");
        }

        private static void assertAxisPositiveButton(List<AxisInfo> axes, string name, string expected)
        {
            var match = axes.Where(a => a.Name == name && a.Type == 0).ToList();
            Assert.IsNotEmpty(match, $"Axis '{name}' (type 0) must exist.");
            var axis = match[0];
            Assert.AreEqual(expected, axis.PositiveButton,
                $"Default mapping for '{name}' must be '{expected}' (stable default binding).");
        }

        [Test]
        public void ControllerDefaultBindings_UseJoystickButtons()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            var sprint = axes.First(a => a.Name == "Sprint" && a.Type == 0);
            Assert.AreEqual("joystick button 4", sprint.AltPositiveButton,
                "Sprint default controller binding must be joystick button 4.");
            var interrupt = axes.First(a => a.Name == "Interact" && a.Type == 0);
            Assert.AreEqual("joystick button 3", interrupt.AltPositiveButton,
                "Interact default controller binding must be joystick button 3.");
        }

        // ---- 2. No runtime remapping system exists / was claimed ----

        [Test]
        public void NoRuntimeRemappingSystem_Exists()
        {
            var names = GetAllTypeNames();
            var forbidden = new[]
            {
                "RemappingManager", "KeyBindingManager", "InputRebindingManager",
                "ControlMapper", "BindingSystem", "KeyConfigSystem", "RemapManager",
                "BindingMap", "BindingConfiguration", "RemappingController"
            };
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.Contains(f)),
                    $"'{f}' must not exist — no speculative remapping framework is allowed.");
            }
        }

        [Test]
        public void ProviderDoesNotUseHardcodedKeyCodes_NoDeviceBypass()
        {
            var text = File.ReadAllText(HumanPlayerInputSourcePath());
            Assert.IsFalse(text.Contains("KeyCode"),
                "HumanPlayerInput must not reference KeyCode directly.");
            Assert.IsTrue(text.Contains("GetButton") || text.Contains("GetAxisRaw"),
                "HumanPlayerInput must read via named axes/buttons (legacy Input Manager).");
        }

        private static string HumanPlayerInputSourcePath()
            => Path.Combine(ProjectRoot, "Assets", "Football", "Runtime", "Input", "HumanPlayerInput.cs");

        // ---- 3. Binding config stays outside logical contract / gameplay ----

        [Test]
        public void InputFrameAndIPlayerInput_ExposeNoBindingConfiguration()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static;
            var forbidden = new[] { "binding", "keymap", "controlmap", "rebind" };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var f in t.GetFields(flags).Where(f => !f.IsLiteral))
                {
                    foreach (var kw in forbidden)
                        Assert.IsFalse(f.Name.ToLowerInvariant().Contains(kw),
                            $"Contract '{t.Name}' must not expose binding config field '{f.Name}'.");
                }
                foreach (var p in t.GetProperties(flags))
                {
                    foreach (var kw in forbidden)
                        Assert.IsFalse(p.Name.ToLowerInvariant().Contains(kw),
                            $"Contract '{t.Name}' must not expose binding config property '{p.Name}'.");
                }
            }
        }

        [Test]
        public void InputFrameCarriesInputValues_NotPhysicalControlConfiguration()
        {
            // InputFrame has no device-types fields (device-neutral).
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static;
            var deviceTypes = new[] { "KeyCode", "InputAction", "InputControl", "Keyboard", "Gamepad" };
            foreach (var f in typeof(InputFrame).GetFields(flags).Where(f => !f.IsLiteral))
            {
                foreach (var dt in deviceTypes)
                    Assert.IsFalse(f.FieldType.FullName != null && f.FieldType.FullName.Contains(dt),
                        $"InputFrame must be device-neutral; found '{f.Name}' of type '{f.FieldType}'.");
            }
        }

        // ---- 4. Device independence of Core / gameplay ----

        [Test]
        public void Core_HasNoDeviceDependencies_NoBypassIntroduced()
        {
            // Football.Core structurally cannot read UnityEngine.Input (empty asmdef refs).
            var allCoreTypes = SafeGetTypes(typeof(InputFrame).Assembly).ToList();
            Assert.IsNotEmpty(allCoreTypes, "Core assembly must load types.");
            // No UnityEngine.Input referenced directly in Core is enforced by the
            // Core asmdef having no Unity refs; assert the contract types are
            // device-neutral via reflection (no device fields/properties/params).
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static;
            var forbidden = new[] { "UnityEngine.Input", "InputAction", "InputControl", "KeyCode", "Keyboard", "Gamepad" };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var f in t.GetFields(flags))
                    Assert.IsFalse(f.FieldType.FullName != null && forbidden.Contains(f.FieldType.FullName),
                        $"'{t.Name}' field '{f.Name}' must not be a device type.");
                foreach (var p in t.GetProperties(flags))
                    Assert.IsFalse(p.PropertyType.FullName != null && forbidden.Contains(p.PropertyType.FullName),
                        $"'{t.Name}' property '{p.Name}' must not be a device type.");
            }
        }

        // ---- 5. No duplicate binding authority ----

        [Test]
        public void SingleBindingAuthority_InputManagerOnly()
        {
            var names = GetAllTypeNames();
            // Only the Legacy Input Manager (not code) holds bindings.
            Assert.IsFalse(names.Any(n => n.Contains("Binding") && !n.Equals("RemappingInputContractTests")),
                "No code-side binding authority type may exist.");
        }

        // ---- 6. activeInputHandler keeps legacy bindings functional ----

        [Test]
        public void ActiveInputHandler_KeepsLegacyBindingsFunctional()
        {
            var ps = File.ReadAllText(
                Path.Combine(ProjectRoot, "ProjectSettings", "ProjectSettings.asset"));
            var lines = ps.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("activeInputHandler:"))
                {
                    Assert.AreEqual("2", line.Substring("activeInputHandler:".Length).Trim(),
                        "activeInputHandler must be 2 (Both) for legacy bindings to function.");
                    return;
                }
            }
            Assert.Fail("activeInputHandler not found in ProjectSettings.asset.");
        }
    }
}
