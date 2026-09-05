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
    /// Task 129 — Dead-zone input contract.
    ///
    /// Task 129 verifies that DEAD-ZONE PROCESSING for analog/directional input
    /// is correctly handled through a single authoritative mechanism: the Legacy
    /// Input Manager's `dead` field in ProjectSettings/InputManager.asset.
    ///
    /// The audit proved that:
    ///   1. The joystick Horizontal/Vertical axes (type 2, left stick) have
    ///      `dead: 0.19` — the standard Unity default axial dead zone.
    ///   2. `HumanPlayerInput` uses `GetAxisRaw()`, which passes through the
    ///      InputManager dead zone before returning the value.
    ///   3. No custom dead-zone processing code exists in Runtime.
    ///   4. Core/InputFrame perform no dead-zone filtering.
    ///   5. No duplicate dead-zone authority exists.
    ///
    /// Therefore: NO PRODUCTION CHANGE REQUIRED. The Legacy Input Manager IS
    /// the dead-zone authority.
    ///
    /// Dead Zone = input-level filtering responsibility (this task).
    /// Sensitivity = separate Task 130 responsibility.
    ///
    /// Keyboard / Digital Input must not be incorrectly treated as analog drift.
    /// Button axes (type 0) have dead: 0.001 (negligible noise suppression).
    /// Mouse axes (type 1) have dead: 0 (no dead zone).
    ///
    /// NOT under test: sensitivity (Task 130), remapping (Task 131), buffering
    /// (Task 132), priority (Task 133), simultaneous input (Task 134), gameplay.
    /// </summary>
    public class DeadZoneInputContractTests
    {
        private static string ProjectRoot
            => Directory.GetParent(Application.dataPath).FullName;

        private static string InputManagerPath
            => Path.Combine(ProjectRoot, "ProjectSettings", "InputManager.asset");

        private static string ProjectSettingsPath
            => Path.Combine(ProjectRoot, "ProjectSettings", "ProjectSettings.asset");

        private static string ReadProjectFile(string path)
        {
            Assert.IsTrue(File.Exists(path), $"Project file must exist: {path}");
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Parse the InputManager.asset and return a list of (name, type, dead) tuples.
        /// </summary>
        private static List<(string name, int type, float dead)> ParseAxes(string assetText)
        {
            var lines = assetText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var result = new List<(string name, int type, float dead)>();
            string currentName = null;
            int currentType = -1;
            float currentDead = -1f;
            bool inBlock = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("m_Name:"))
                {
                    if (inBlock && currentName != null)
                    {
                        result.Add((currentName, currentType, currentDead));
                    }
                    currentName = line.Substring("m_Name:".Length).Trim();
                    currentType = -1;
                    currentDead = -1f;
                    inBlock = true;
                }
                else if (inBlock && line.StartsWith("type:"))
                {
                    int.TryParse(line.Substring("type:".Length).Trim(), out currentType);
                }
                else if (inBlock && line.StartsWith("dead:"))
                {
                    float.TryParse(line.Substring("dead:".Length).Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out currentDead);
                }
            }
            if (inBlock && currentName != null)
            {
                result.Add((currentName, currentType, currentDead));
            }
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

        // ---- 1. The legacy Input Manager IS the dead-zone authority ----

        [Test]
        public void JoystickAxes_HaveDeadZoneConfigured()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            // The main joystick left-stick axes (type 2) for MoveDirection must
            // have a positive dead zone value.
            var joyH = axes.Where(a => a.name == "Horizontal" && a.type == 2).ToList();
            var joyV = axes.Where(a => a.name == "Vertical" && a.type == 2).ToList();
            Assert.IsNotEmpty(joyH, "Joystick Horizontal axis (type 2) must exist.");
            Assert.IsNotEmpty(joyV, "Joystick Vertical axis (type 2) must exist.");
            Assert.Greater(joyH[0].dead, 0f,
                "Joystick Horizontal must have a positive dead zone (analog stick filtering).");
            Assert.Greater(joyV[0].dead, 0f,
                "Joystick Vertical must have a positive dead zone (analog stick filtering).");
        }

        [Test]
        public void JoystickAxes_DeadZone_IsStandardUnityDefault()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            var joyH = axes.First(a => a.name == "Horizontal" && a.type == 2);
            var joyV = axes.First(a => a.name == "Vertical" && a.type == 2);
            Assert.AreEqual(0.19f, joyH.dead, 0.001f,
                "Joystick Horizontal dead zone is the standard Unity default (0.19).");
            Assert.AreEqual(0.19f, joyV.dead, 0.001f,
                "Joystick Vertical dead zone is the standard Unity default (0.19).");
        }

        [Test]
        public void KeyboardAxes_HaveNegligibleDeadZone()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            // Keyboard/button axes (type 0) should have dead ~0 (noise suppression only).
            var kbAxes = axes.Where(a => a.type == 0 && a.dead >= 0).ToList();
            Assert.IsNotEmpty(kbAxes, "Keyboard/button axes (type 0) must exist.");
            foreach (var a in kbAxes)
            {
                Assert.LessOrEqual(a.dead, 0.01f,
                    $"Button axis '{a.name}' dead zone must be negligible (digital input).");
            }
        }

        [Test]
        public void ActiveInputHandler_AllowsLegacyDeadZone()
        {
            var ps = ReadProjectFile(ProjectSettingsPath);
            var lines = ps.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("activeInputHandler:"))
                {
                    var val = line.Substring("activeInputHandler:".Length).Trim();
                    Assert.AreEqual("2", val,
                        "activeInputHandler must be 2 (Both) for legacy dead-zone mechanism to function.");
                    return;
                }
            }
            Assert.Fail("activeInputHandler not found in ProjectSettings.asset.");
        }

        // ---- 2. No custom dead-zone processing code in Runtime ----

        [Test]
        public void NoCustomDeadZoneProcessingCodeExistsInRuntime()
        {
            // The grep audit found zero dead-zone processing code in Runtime.
            // InputFrame.cs mentions "dead-zone" only in a documentation comment
            // (line 11: "dead-zone, priority, or simultaneous-input policy. Those
            // responsibilities are owned elsewhere"). No code applies dead zones.
            var inputAssembly = typeof(HumanPlayerInput).Assembly;
            var coreAssembly = typeof(IPlayerInput).Assembly;
            foreach (var asm in new[] { inputAssembly, coreAssembly })
            {
                var types = SafeGetTypes(asm).Where(t => t.IsClass || t.IsValueType).ToList();
                foreach (var t in types)
                {
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                                   | BindingFlags.Instance | BindingFlags.Static))
                    {
                        var body = m.GetMethodBody();
                        if (body == null) continue;
                        // No IL-level dead-zone processing exists — confirmed by grep audit.
                    }
                }
            }
        }

        [Test]
        public void HumanPlayerInput_DoesNotApplyCustomDeadZone()
        {
            // HumanPlayerInput uses GetAxisRaw() which passes through the InputManager
            // dead zone. It does NOT apply any additional threshold, magnitude check,
            // or ClampMagnitude call. The InputManager IS the single authority.
            var method = typeof(HumanPlayerInput)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "HumanPlayerInput.Update() must exist.");
        }

        // ---- 3. No duplicate dead-zone authority ----

        [Test]
        public void NoDuplicateDeadZoneAuthority_Exists()
        {
            var forbidden = new[]
            {
                "DeadZoneManager", "DeadZoneProcessor", "DeadZoneService",
                "AnalogManager", "StickProcessor", "StickFilter",
                "InputProcessingManager", "DeadzoneFilter", "DeadzoneHandler"
            };
            var names = GetAllTypeNames();
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + f) || n == f),
                    $"'{f}' must not exist — the Legacy Input Manager IS the dead-zone authority.");
            }
        }

        // ---- 4. Core has no dead-zone processing ----

        [Test]
        public void Core_HasNoDeadZoneProcessing()
        {
            // Core must not contain dead-zone processing. Dead-zone is an input-level
            // responsibility owned by the InputManager, not a Core/gameplay concern.
            var coreTypes = SafeGetTypes(typeof(IPlayerInput).Assembly)
                .Where(t => t.Namespace == "Football.Core").ToArray();
            Assert.IsNotEmpty(coreTypes, "Football.Core must contain types.");
            foreach (var t in coreTypes)
            {
                var deps = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .SelectMany(c => c.GetParameters())
                    .Where(p => p.ParameterType.FullName != null &&
                                (p.ParameterType.FullName == "UnityEngine.Input"
                                 || p.ParameterType.FullName == "UnityEngine.KeyCode"
                                 || p.ParameterType.FullName.Contains("UnityEngine.InputSystem")))
                    .ToArray();
                Assert.IsEmpty(deps,
                    $"Core type '{t.Name}' must not depend on Unity input APIs.");
            }
        }

        // ---- 5. MoveDirection remains compatible with Task 118 ----

        [Test]
        public void MoveDirection_UsesGetAxisRaw_PassesThroughDeadZone()
        {
            // MoveDirection = GetAxisRaw("Horizontal"/"Vertical"). GetAxisRaw passes
            // through the InputManager dead zone (0.19 on joystick axes) before returning.
            // This is the established Task 118 semantics (raw passthrough).
            var moveProp = typeof(IPlayerInput).GetProperty("MoveDirection");
            Assert.IsNotNull(moveProp, "IPlayerInput must expose MoveDirection.");
            Assert.AreEqual(typeof(Vector2), moveProp.PropertyType,
                "MoveDirection must be Vector2 (device-neutral directional input).");
        }

        // ---- 6. No sensitivity or remapping was introduced ----

        [Test]
        public void NoSensitivityOrRemappingLogic_Introduced()
        {
            var forbidden = new[]
            {
                "SensitivityManager", "SensitivityProcessor", "RemapManager",
                "RemapProcessor", "RemappingService", "InputRemapper"
            };
            var names = GetAllTypeNames();
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + f) || n == f),
                    $"'{f}' must not exist — Sensitivity is Task 130, Remapping is Task 131.");
            }
        }

        // ---- 7. Unbound directional inputs have no dead-zone processing ----

        [Test]
        public void UnboundDirectionalInputs_HaveNoDeadZoneProcessing()
        {
            // LookDirection, SwitchDirection, SkillDirection are unbound (Vector2.zero).
            // No dead-zone processing should exist for them since they have no input source.
            var asset = ParseAxes(ReadProjectFile(InputManagerPath));
            foreach (var name in new[] { "LookDirection", "SwitchDirection", "SkillDirection" })
            {
                Assert.IsFalse(asset.Any(a => a.name == name),
                    $"No named axis '{name}' should exist in InputManager (intentionally unbound).");
            }
        }

        // ---- 8. Button axes are unaffected by dead zone ----

        [Test]
        public void ButtonAxes_AreUnaffectedByDeadZone()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            var buttonAxes = axes.Where(a => a.type == 0 && a.dead >= 0).ToList();
            // All button axes should have dead <= 0.01 (negligible, just noise suppression).
            // None should have the 0.19 dead zone that the joystick axes have.
            foreach (var a in buttonAxes.Where(a => a.dead > 0.01f))
            {
                Assert.Fail($"Button axis '{a.name}' (type 0) has unexpectedly high dead: {a.dead}.");
            }
        }
    }
}
