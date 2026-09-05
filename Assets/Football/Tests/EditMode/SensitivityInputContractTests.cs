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
    /// Task 130 — Sensitivity (input responsiveness) input contract.
    ///
    /// Task 130 verifies that INPUT SENSITIVITY is handled by a single
    /// authoritative mechanism: the Legacy Input Manager's `sensitivity:` field
    /// in ProjectSettings/InputManager.asset.
    ///
    /// The audit proved that:
    ///   1. The ONLY sensitivity mechanism in the repository is the Input Manager
    ///      `sensitivity:` field. No custom sensitivity code exists in Runtime.
    ///   2. `HumanPlayerInput` reads MoveDirection via `GetAxisRaw`, which per
    ///      legacy Input Manager semantics applies only the axis dead zone and
    ///      does NOT apply sensitivity/gravity smoothing. This is the deliberate
    ///      Task 118 "raw passthrough" contract (see MoveInputContractTests).
    ///   3. No product tuning requirement (intended sensitivity value / range) is
    ///      defined anywhere in the repository, docs, or tests. The configured
    ///      values are Unity stock defaults, not project-specific.
    ///
    /// Conclusion: NO PRODUCTION CHANGE REQUIRED. The Legacy Input Manager
    /// `sensitivity:` field IS the sensitivity authority. Product tuning is
    /// UNDEFINED and DEFERRED.
    ///
    /// Sensitivity = input responsiveness / processing responsibility (this task).
    /// Dead Zone   = separate input filtering responsibility (Task 129).
    /// Gravity     = separate smoothing responsibility (Input Manager, not applied
    ///               via GetAxisRaw).
    /// Normalization = separate concern (NOT applied; MoveDirection is raw).
    ///
    /// Sensitivity ≠ Gameplay State. Sensitivity must NOT appear as gameplay data
    /// in InputFrame or Core.
    ///
    /// NOT under test: remapping (131), buffering (132), priority (133),
    /// simultaneous input (134), gameplay, camera, mouse-look, switch/skill/look
    /// direction tuning while those fields are unbound.
    /// </summary>
    public class SensitivityInputContractTests
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

        private class AxisInfo
        {
            public string Name;
            public int Type;
            public float Dead;
            public float Sensitivity;
            public float Gravity;
            public string PositiveButton;
            public string NegativeButton;
            public string AltPositiveButton;
            public int JoyNum;
        }

        /// <summary>
        /// Parse the InputManager.asset into a list of axis blocks (line-based to
        /// stay robust against Windows \r\n line endings).
        /// </summary>
        private static List<AxisInfo> ParseAxes(string assetText)
        {
            var lines = assetText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var result = new List<AxisInfo>();
            AxisInfo current = null;
            bool inBlock = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("m_Name:"))
                {
                    if (inBlock && current != null)
                    {
                        result.Add(current);
                    }
                    current = new AxisInfo();
                    current.Name = line.Substring("m_Name:".Length).Trim();
                    current.Type = -1;
                    current.Dead = -1f;
                    current.Sensitivity = -1f;
                    current.Gravity = -1f;
                    current.JoyNum = -1;
                    inBlock = true;
                }
                else if (inBlock && current != null)
                {
                    if (line.StartsWith("type:"))
                        int.TryParse(line.Substring("type:".Length).Trim(), out current.Type);
                    else if (line.StartsWith("dead:"))
                        float.TryParse(line.Substring("dead:".Length).Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out current.Dead);
                    else if (line.StartsWith("sensitivity:"))
                        float.TryParse(line.Substring("sensitivity:".Length).Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out current.Sensitivity);
                    else if (line.StartsWith("gravity:"))
                        float.TryParse(line.Substring("gravity:".Length).Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out current.Gravity);
                    else if (line.StartsWith("positiveButton:"))
                        current.PositiveButton = line.Substring("positiveButton:".Length).Trim();
                    else if (line.StartsWith("negativeButton:"))
                        current.NegativeButton = line.Substring("negativeButton:".Length).Trim();
                    else if (line.StartsWith("altPositiveButton:"))
                        current.AltPositiveButton = line.Substring("altPositiveButton:".Length).Trim();
                    else if (line.StartsWith("joyNum:"))
                        int.TryParse(line.Substring("joyNum:".Length).Trim(), out current.JoyNum);
                }
            }
            if (inBlock && current != null)
            {
                result.Add(current);
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

        private static string HumanPlayerInputSourcePath
            => Path.Combine(ProjectRoot, "Assets", "Football", "Runtime", "Input", "HumanPlayerInput.cs");

        private static string ReadHumanPlayerInputSource()
        {
            Assert.IsTrue(File.Exists(HumanPlayerInputSourcePath),
                $"HumanPlayerInput.cs must exist: {HumanPlayerInputSourcePath}");
            return File.ReadAllText(HumanPlayerInputSourcePath);
        }

        // ---- 1. The legacy Input Manager IS the sensitivity authority ----

        [Test]
        public void InputManager_HasSensitivityField_ForDirectionalAxes()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            var kbH = axes.Where(a => a.Name == "Horizontal" && a.Type == 0).ToList();
            var kbV = axes.Where(a => a.Name == "Vertical" && a.Type == 0).ToList();
            var joyH = axes.Where(a => a.Name == "Horizontal" && a.Type == 2).ToList();
            var joyV = axes.Where(a => a.Name == "Vertical" && a.Type == 2).ToList();
            Assert.IsNotEmpty(kbH, "Keyboard Horizontal axis (type 0) must exist.");
            Assert.IsNotEmpty(kbV, "Keyboard Vertical axis (type 0) must exist.");
            Assert.IsNotEmpty(joyH, "Joystick Horizontal axis (type 2) must exist.");
            Assert.IsNotEmpty(joyV, "Joystick Vertical axis (type 2) must exist.");
            Assert.GreaterOrEqual(kbH[0].Sensitivity, 0f);
            Assert.GreaterOrEqual(joyH[0].Sensitivity, 0f);
            Assert.GreaterOrEqual(joyV[0].Sensitivity, 0f);
        }

        [Test]
        public void SensitivityAndDeadZone_AreSeparateFields()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            var joyH = axes.First(a => a.Name == "Horizontal" && a.Type == 2);
            // Sensitivity and dead zone are distinct InputManager per-axis concepts.
            Assert.AreNotEqual(joyH.Sensitivity, joyH.Dead,
                "Sensitivity and dead zone are distinct configured responsibilities.");
        }

        [Test]
        public void SensitivityAndGravity_AreSeparateFields()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            var joyH = axes.First(a => a.Name == "Horizontal" && a.Type == 2);
            // Sensitivity and gravity are distinct InputManager per-axis concepts.
            // Joystick Horizontal has sensitivity 1 and gravity 0.
            Assert.AreNotEqual(joyH.Sensitivity, joyH.Gravity,
                "Sensitivity and gravity are distinct configured responsibilities.");
            Assert.AreEqual(0f, joyH.Gravity,
                "Joystick horizontal axis gravity is 0 (analog stick has no return smoothing).");
        }

        // ---- 2. No duplicate / custom sensitivity processing ----

        [Test]
        public void NoCustomSensitivityCode_ExistsInRuntime()
        {
            var names = GetAllTypeNames();
            Assert.IsFalse(names.Any(n =>
                    n.Contains("Sensitivity") || n.Contains("SensitivityManager")
                    || n.Contains("ResponseSystem") || n.Contains("ResponseCurve")),
                "No custom sensitivity/response types may exist — Input Manager is the sole authority.");
        }

        [Test]
        public void MoveDirection_IsReadViaGetAxisRaw_NotGetAxis()
        {
            // GetAxisRaw applies only the dead zone and NOT sensitivity/gravity
            // smoothing. This is the established Task 118 raw-passthrough contract.
            var body = ReadHumanPlayerInputSource();
            Assert.IsTrue(body.Contains("GetAxisRaw(\"Horizontal\")"),
                "HumanPlayerInput must read Horizontal via GetAxisRaw (raw passthrough).");
            Assert.IsTrue(body.Contains("GetAxisRaw(\"Vertical\")"),
                "HumanPlayerInput must read Vertical via GetAxisRaw (raw passthrough).");
            // Ensure no smoothing (GetAxis) call exists. GetAxisRaw contains the
            // substring "GetAxis", so check for a bare GetAxis( call that is not
            // preceded by "Raw". A regex-free scan: every occurrence of "GetAxis("
            // must be part of "GetAxisRaw(".
            int idx = 0;
            while ((idx = body.IndexOf("GetAxis(", idx)) >= 0)
            {
                int before = idx - 3;
                bool precededByRaw = before >= 0
                    && body.Substring(before, 3) == "Raw";
                Assert.IsTrue(precededByRaw,
                    "Found a GetAxis( (smoothed/sensitivity-applied) call; only GetAxisRaw is allowed.");
                idx += 9;
            }
        }

        // ---- 3. No Sensitivity field leaks into Core / InputFrame ----

        [Test]
        public void CoreAndInputFrame_ExposeNoSensitivityState()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static;
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
                foreach (var f in t.GetFields(flags).Where(f => !f.IsLiteral))
                {
                    Assert.IsFalse(f.Name.ToLowerInvariant().Contains("sensitivity"),
                        $"Contract type '{t.Name}' must not expose sensitivity field '{f.Name}'.");
                }
                foreach (var p in t.GetProperties(flags))
                {
                    Assert.IsFalse(p.Name.ToLowerInvariant().Contains("sensitivity"),
                        $"Contract type '{t.Name}' must not expose sensitivity property '{p.Name}'.");
                }
            }
            // InputFrame constructor must not accept sensitivity.
            var ctor = typeof(InputFrame).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
            foreach (var param in ctor.GetParameters())
            {
                Assert.IsFalse(param.Name.ToLowerInvariant().Contains("sensitivity"),
                    $"InputFrame constructor must not take sensitivity parameter '{param.Name}'.");
            }
        }

        // ---- 4. Keyboard / controller distinction ----

        [Test]
        public void KeyboardAndJoystickMovementAxes_AreDistinct()
        {
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            var kbHold = axes.Count(a => a.Name == "Horizontal" && a.Type == 0);
            var joyHold = axes.Count(a => a.Name == "Horizontal" && a.Type == 2);
            Assert.AreEqual(1, kbHold, "Exactly one keyboard Horizontal axis (type 0) for MoveDirection.");
            Assert.AreEqual(1, joyHold, "Exactly one joystick Horizontal axis (type 2) for MoveDirection.");
        }

        [Test]
        public void ButtonAxes_UseDigitalSensitivity()
        {
            // Button/trigger axes (type 0) for actions use a large sensitivity so a
            // held digital key registers immediately (Unity stock default 1000).
            var axes = ParseAxes(ReadProjectFile(InputManagerPath));
            var sprint = axes.First(a => a.Name == "Sprint" && a.Type == 0);
            var tackle = axes.First(a => a.Name == "Tackle" && a.Type == 0);
            Assert.GreaterOrEqual(sprint.Sensitivity, 1000f,
                "Sprint button axis sensitivity must be the digital stock default.");
            Assert.GreaterOrEqual(tackle.Sensitivity, 1000f,
                "Tackle button axis sensitivity must be the digital stock default.");
        }

        // ---- 5. activeInputHandler (legacy enabled) ----

        [Test]
        public void ActiveInputHandler_AllowsLegacySensitivity()
        {
            var ps = ReadProjectFile(ProjectSettingsPath);
            var lines = ps.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("activeInputHandler:"))
                {
                    Assert.AreEqual("2", line.Substring("activeInputHandler:".Length).Trim(),
                        "activeInputHandler must be 2 (Both) for the legacy sensitivity mechanism.");
                    return;
                }
            }
            Assert.Fail("activeInputHandler not found in ProjectSettings.asset.");
        }
    }
}
