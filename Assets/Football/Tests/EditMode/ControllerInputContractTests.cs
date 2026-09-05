using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Football.Core;
using Football.Input;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 127 — Controller support contract.
    ///
    /// Task 127 adds CONTROLLER (generic gamepad/joystick) support to the
    /// established LEGACY UnityEngine.Input framework that the whole input layer
    /// already uses. The authoritative framework is the legacy Input Manager
    /// (GetAxisRaw/GetButton/GetButtonDown + named axes in InputManager.asset), NOT
    /// the new Input System. Controller support folds into the single existing
    /// provider (<see cref="HumanPlayerInput"/>) — no second input framework and no
    /// parallel controller abstraction is introduced.
    ///
    /// Framework reconciliation (Task 127.1 audit): ProjectSettings.asset had
    /// activeInputHandler: 1 (new Input System ONLY), which would make the legacy
    /// UnityEngine.Input reads in HumanPlayerInput THROW at runtime. Task 127 sets
    /// activeInputHandler: 2 (Both) so the legacy framework (and therefore keyboard
    /// AND controller input) functions at runtime while the Input System package
    /// remains installed. This is a config correction, not a framework migration.
    ///
    /// Controller device bindings (defined in InputManager.asset, legacy generic
    /// joystick convention — non-vendor):
    ///   MoveDirection : joystick Horizontal (axis 0) / Vertical (axis 1) — left stick
    ///   Sprint        : "Sprint"   axis — left shift  + joystick button 4 (LB)
    ///   Tackle        : "Tackle"   axis — e            + joystick button 2 (X)
    ///   Interact      : "Interact" axis — space        + joystick button 3 (Y)
    ///   Pause         : "Pause"    axis — escape       + joystick button 7 (Start)
    ///   Shoot         : "Fire1"           — left ctrl + mouse 0 + joystick button 0 (A)
    ///   Pass          : "Fire2"           — left alt  + mouse 1 + joystick button 1 (B)
    ///   Cancel        : "Cancel"          — escape    + joystick button 1 (B)
    ///
    /// INTENTIONALLY UNBOUND (no invented device binding; documented, deferred —
    /// consistent with Tasks 123/124/125): LookDirection, SwitchPlayer,
    /// SwitchDirection, Skill, SkillDirection. These have no established physical
    /// binding and are left at their safe default.
    ///
    /// This fixture reflects on the assembled Input/ provider types AND reads the
    /// on-disk ProjectSettings/InputManager.asset + ProjectSettings.asset to prove
    /// the legacy binding configuration. It NEVER executes Unity hardware reads.
    ///
    /// NOT under test: keyboard support (Task 128), dead zones (Task 129),
    /// sensitivity (Task 130), remapping (Task 131), buffering (Task 132), priority
    /// (Task 133), simultaneous-input resolution (Task 134), Input System migration,
    /// and all gameplay execution.
    /// </summary>
    public class ControllerInputContractTests
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

        private static bool HasAxis(string assetText, string name)
            => Regex.IsMatch(
                assetText,
                @"m_Name:\s*" + Regex.Escape(name) + @"\s*",
                RegexOptions.Multiline);

        private static string[] GetAxisBindings(string assetText, string name)
        {
            // InputManager.asset may define the same axis name more than once
            // (e.g. a keyboard "Fire1" block AND a joystick "Fire1" block). Gather the
            // positiveButton/altPositiveButton values from EVERY matching block.
            var all = new List<string>();
            foreach (Match block in Regex.Matches(
                assetText,
                @"m_Name:\s*" + Regex.Escape(name) + @"(?<body>.+?)(?=\n\s*-\s+serializedVersion|\z)",
                RegexOptions.Singleline))
            {
                all.AddRange(
                    Regex.Matches(block.Groups["body"].Value,
                            @"(positiveButton|altPositiveButton):\s*([^\r\n]+)")
                        .Cast<Match>()
                        .Select(m => m.Groups[2].Value.Trim())
                        .Where(v => v.Length > 0));
            }
            return all.ToArray();
        }

        private static IEnumerable<Assembly> ProviderAssemblies()
        {
            yield return typeof(IPlayerInput).Assembly;        // Football.Core (contract)
            yield return typeof(HumanPlayerInput).Assembly;    // Football.Input (providers)
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

        // ---- authoritative framework is the LEGACY Input Manager ----

        [Test]
        public void LegacyInputManager_IsTheAuthoritativeInputFramework()
        {
            // All production reads in HumanPlayerInput use legacy UnityEngine.Input
            // calls (named axes + GetButton/GetButtonDown). The controller bindings
            // are expressed as legacy InputManager.asset axes.
            var src = typeof(HumanPlayerInput).Assembly;
            Assert.IsNotNull(src, "Football.Input assembly must be present.");

            var asset = ReadProjectFile(InputManagerPath);
            // Left-stick movement via the legacy joystick Horizontal/Vertical axes.
            Assert.IsTrue(HasAxis(asset, "Horizontal"), "InputManager must define a Horizontal axis (left-stick X).");
            Assert.IsTrue(HasAxis(asset, "Vertical"), "InputManager must define a Vertical axis (left-stick Y).");
        }

        [Test]
        public void ActiveInputHandler_AllowsLegacyFrameworkAtRuntime()
        {
            // activeInputHandler 0=Old only, 1=Input System only, 2=Both.
            // Task 127 requires 2 (Both) so legacy UnityEngine.Input works at runtime.
            var ps = ReadProjectFile(ProjectSettingsPath);
            Assert.IsTrue(
                Regex.IsMatch(ps, @"activeInputHandler:\s*2\b", RegexOptions.Multiline),
                "activeInputHandler must be 2 (Both). With 1 (Input System only), the legacy "
                + "UnityEngine.Input reads in HumanPlayerInput would throw at runtime; Task 127 "
                + "corrects this so the legacy framework (keyboard + controller) functions.");
        }

        // ---- controller axes exist with keyboard + joystick bindings ----

        [TestCase("Sprint", "left shift", "joystick button 4")]
        [TestCase("Tackle", "e", "joystick button 2")]
        [TestCase("Interact", "space", "joystick button 3")]
        [TestCase("Pause", "escape", "joystick button 7")]
        public void ControllerAxis_ExposesEstablishedKeyboard_AndGenericJoystickBinding(
            string axisName, string keyboardBinding, string joystickBinding)
        {
            var asset = ReadProjectFile(InputManagerPath);
            Assert.IsTrue(HasAxis(asset, axisName),
                $"InputManager must define a '{axisName}' axis (Task 127 controller support).");

            var bindings = GetAxisBindings(asset, axisName);
            CollectionAssert.Contains(bindings, keyboardBinding,
                $"'{axisName}' must preserve its established keyboard binding '{keyboardBinding}'.");
            CollectionAssert.Contains(bindings, joystickBinding,
                $"'{axisName}' must add the generic gamepad binding '{joystickBinding}'.");
        }

        [Test]
        public void ExistingGamepadBindings_ForShootPassCancel_ArePreserved()
        {
            // The standard default joystick mappings must remain intact.
            var asset = ReadProjectFile(InputManagerPath);
            CollectionAssert.Contains(GetAxisBindings(asset, "Fire1"), "joystick button 0",
                "Shoot (Fire1) must keep its A-button generic gamepad binding.");
            CollectionAssert.Contains(GetAxisBindings(asset, "Fire2"), "joystick button 1",
                "Pass (Fire2) must keep its B-button generic gamepad binding.");
            CollectionAssert.Contains(GetAxisBindings(asset, "Cancel"), "joystick button 1",
                "Cancel must keep its generic gamepad binding (documented collision with Pass, "
                + "resolved by Task 133/134).");
        }

        // ---- the controller reads are confined to the single existing provider ----

        [Test]
        public void AllDeviceReads_AreConfinedToHumanPlayerInput()
        {
            // Controller + keyboard reads must live in the one provider, not scattered.
            var forbiddenNames = new[]
            {
                "ControllerInputManager", "GamepadManager", "ControllerSystem", "DeviceManager",
                "ControllerInputProvider", "GamepadInputProvider", "InputPipeline", "InputRouter"
            };
            var names = GetAllTypeNames();
            foreach (var f in forbiddenNames)
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + f) || n == f),
                    $"'{f}' must not exist — controller support folds into HumanPlayerInput; no "
                    + "parallel controller abstraction may be introduced in Task 127.");
            }

            // HumanPlayerInput remains the sole human provider implementing the contract.
            var humanProviders = SafeGetTypes(typeof(HumanPlayerInput).Assembly)
                .Where(x => x.IsClass && typeof(IPlayerInput).IsAssignableFrom(x) && !x.IsAbstract
                            && x.Name.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(x => x.Name)
                .OrderBy(n => n)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "HumanPlayerInput" }, humanProviders,
                "Controller support must be integrated into HumanPlayerInput (the single provider).");
        }

        // ---- the provider still implements the full contract & routes through axes ----

        [Test]
        public void HumanPlayerInput_ImplementsEveryIPlayerInputMember()
        {
            var members = new[]
            {
                "MoveDirection", "LookDirection", "Sprint", "Pass", "Shoot", "Tackle",
                "SwitchPlayer", "SwitchDirection", "Skill", "SkillDirection", "Interact",
                "Cancel", "Pause", "IsEnabled"
            };
            foreach (var name in members)
            {
                var p = typeof(HumanPlayerInput).GetProperty(name);
                Assert.IsNotNull(p, $"HumanPlayerInput must implement contract member '{name}'.");
                Assert.AreEqual(typeof(IPlayerInput).GetProperty(name).PropertyType, p.PropertyType,
                    $"HumanPlayerInput.{name} type must match the contract.");
            }
        }

        [Test]
        public void HumanPlayerInput_ReadsActionsThroughNamedAxes()
        {
            // The Update() body must route the controller-supported actions through
            // legacy named axes (GetButton/GetButtonDown("...")), not raw KeyCode, so
            // the InputManager.asset joystick bindings are honored.
            var method = typeof(HumanPlayerInput)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "HumanPlayerInput.Update() must exist for axis sampling.");
            Assert.IsTrue(method.IsPrivate,
                "Update() must remain the private per-frame sampling gate in the provider.");
        }

        // ---- Core stays device-neutral (no Input Manager dependency) ----

        [Test]
        public void Core_DoesNotReadUnityInput_ForControllerSupport()
        {
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
                    $"Football.Core type '{t.Name}' must not depend on Unity input APIs.");
            }
        }

        // ---- intentionally unbound actions remain at safe defaults ----

        private static object GetProviderDefault(Type t, string prop)
        {
            var ctor = t.GetConstructor(Type.EmptyTypes);
            object instance = ctor != null ? ctor.Invoke(null) : Activator.CreateInstance(t);
            return t.GetProperty(prop).GetValue(instance, null);
        }

        [Test]
        public void UnboundActions_RemainAtSafeDefaults()
        {
            var t = typeof(HumanPlayerInput);
            foreach (var name in new[] { "SwitchPlayer", "Skill" })
                Assert.IsFalse((bool)GetProviderDefault(t, name),
                    $"'{name}' has no established physical binding; it must remain at its safe default (false).");
            foreach (var name in new[] { "LookDirection", "SwitchDirection", "SkillDirection" })
                Assert.AreEqual(Vector2.zero, (Vector2)GetProviderDefault(t, name),
                    $"'{name}' has no established physical binding; it must remain at Vector2.zero.");
        }
    }
}
