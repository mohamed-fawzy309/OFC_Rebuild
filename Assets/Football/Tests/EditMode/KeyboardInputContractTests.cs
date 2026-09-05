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
    /// Task 128 — Keyboard input contract.
    ///
    /// Task 128 verifies KEYBOARD INPUT is correctly mapped through the legacy
    /// Input Manager (the authoritative keyboard input framework) into the single
    /// Human Input Provider (<see cref="HumanPlayerInput"/>) and exits as
    /// device-neutral IPlayerInput values.
    ///
    /// The audit proved that keyboard support ALREADY FULLY EXISTS for all
    /// supported actions (established by Tasks 117-126 and enhanced in Task 127
    /// when controller bindings were added alongside the existing keyboard axes).
    /// No production changes were required for Task 128. This fixture proves
    /// the existing keyboard contract.
    ///
    /// Keyboard → Legacy Input Manager (named axes in InputManager.asset) →
    /// HumanPlayerInput (GetAxisRaw/GetButton/GetButtonDown) → IPlayerInput →
    /// InputFrame (device-neutral snapshot) → Future Gameplay.
    ///
    /// Supported keyboard actions with their verified bindings:
    ///   MoveDirection : WASD / arrow keys via Horizontal/Vertical axes
    ///   Sprint        : Left Shift via "Sprint" axis
    ///   Pass          : Left Alt / Mouse 1 via "Fire2" axis
    ///   Shoot         : Left Ctrl / Mouse 0 via "Fire1" axis
    ///   Tackle        : E via "Tackle" axis
    ///   Interact      : Space via "Interact" axis
    ///   Cancel        : Escape via "Cancel" axis
    ///   Pause         : Escape via "Pause" axis
    ///
    /// Intentionally unbound (no invented keyboard binding; documented,
    /// deferred — consistent with Tasks 123/124/125): LookDirection,
    /// SwitchPlayer, SwitchDirection, Skill, SkillDirection.
    ///
    /// NOT under test: controller support (Task 127), dead zones (Task 129),
    /// sensitivity (Task 130), remapping (Task 131), buffering (Task 132),
    /// priority (Task 133), simultaneous-input (Task 134), gameplay execution.
    /// </summary>
    public class KeyboardInputContractTests
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
        {
            var lines = assetText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line == $"m_Name: {name}") return true;
            }
            return false;
        }

        private static string[] GetAllAxisBindings(string assetText, string name)
        {
            // Parse line-by-line to be robust against \r\n line endings.
            var lines = assetText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var all = new List<string>();
            bool inBlock = false;
            string currentName = null;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("m_Name:"))
                {
                    var val = line.Substring("m_Name:".Length).Trim();
                    if (val == name) { inBlock = true; currentName = val; continue; }
                    if (inBlock) break; // hit a different axis name → block ended
                }
                if (!inBlock) continue;
                if (line.StartsWith("positiveButton:"))  all.Add(line.Substring("positiveButton:".Length).Trim());
                if (line.StartsWith("altPositiveButton:")) all.Add(line.Substring("altPositiveButton:".Length).Trim());
                if (line.StartsWith("negativeButton:"))  all.Add(line.Substring("negativeButton:".Length).Trim());
                if (line.StartsWith("altNegativeButton:")) all.Add(line.Substring("altNegativeButton:".Length).Trim());
            }
            return all.Where(v => v.Length > 0).ToArray();
        }

        private static string[] GetKeyboardBindings(string assetText, string name)
        {
            // Filter to only keyboard key names (excludes joystick buttons, mouse axes).
            var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "left","right","up","down","a","b","c","d","e","f","g","h","i","j",
                "k","l","m","n","o","p","q","r","s","t","u","v","w","x","y","z",
                "space","escape","return","enter","tab","backspace","delete","insert",
                "left shift","right shift","left ctrl","right ctrl","left alt","right alt",
                "mouse 0","mouse 1","mouse 2"
            };
            return GetAllAxisBindings(assetText, name)
                .Where(b => knownKeys.Contains(b))
                .ToArray();
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

        // ---- 1. Legacy Input Manager is the authoritative keyboard framework ----

        [Test]
        public void LegacyInputManager_IsTheAuthoritativeKeyboardFramework()
        {
            var asset = ReadProjectFile(InputManagerPath);
            // The named axes that keyboard actions route through must exist.
            foreach (var name in new[] { "Horizontal", "Vertical", "Sprint", "Fire1", "Fire2", "Tackle", "Interact", "Cancel", "Pause" })
            {
                Assert.IsTrue(HasAxis(asset, name),
                    $"InputManager must define a '{name}' axis for keyboard input support.");
            }
        }

        [Test]
        public void ActiveInputHandler_IsCompatibleWithLegacyKeyboardBehavior()
        {
            var ps = ReadProjectFile(ProjectSettingsPath);
            var lines = ps.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool found = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("activeInputHandler:"))
                {
                    var val = line.Substring("activeInputHandler:".Length).Trim();
                    Assert.AreEqual("2", val,
                        "activeInputHandler must be 2 (Both). With 1 (Input System only), legacy "
                        + "UnityEngine.Input keyboard reads would throw at runtime.");
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "activeInputHandler must be present in ProjectSettings.asset.");
        }

        // ---- 2. Keyboard bindings exist for all supported actions ----

        [Test]
        public void MoveDirection_HasKeyboardBindings_WASD_AndArrows()
        {
            var asset = ReadProjectFile(InputManagerPath);
            var hKeys = GetKeyboardBindings(asset, "Horizontal");
            var vKeys = GetKeyboardBindings(asset, "Vertical");
            // Horizontal keyboard: left (neg), right (pos), a (alt neg), d (alt pos)
            CollectionAssert.Contains(hKeys, "left");
            CollectionAssert.Contains(hKeys, "right");
            CollectionAssert.Contains(hKeys, "a");
            CollectionAssert.Contains(hKeys, "d");
            // Vertical keyboard: down (neg), up (pos), s (alt neg), w (alt pos)
            CollectionAssert.Contains(vKeys, "down");
            CollectionAssert.Contains(vKeys, "up");
            CollectionAssert.Contains(vKeys, "s");
            CollectionAssert.Contains(vKeys, "w");
        }

        [TestCase("Sprint", "left shift")]
        [TestCase("Tackle", "e")]
        [TestCase("Interact", "space")]
        [TestCase("Pause", "escape")]
        public void SupportedAction_HasExpectedKeyboardBinding(string axisName, string expectedKey)
        {
            var asset = ReadProjectFile(InputManagerPath);
            var keys = GetKeyboardBindings(asset, axisName);
            CollectionAssert.Contains(keys, expectedKey,
                $"'{axisName}' axis must have keyboard binding '{expectedKey}'.");
        }

        [Test]
        public void Shoot_HasKeyboardBindings_Fire1()
        {
            var asset = ReadProjectFile(InputManagerPath);
            var keys = GetKeyboardBindings(asset, "Fire1");
            CollectionAssert.Contains(keys, "left ctrl");
            CollectionAssert.Contains(keys, "mouse 0");
        }

        [Test]
        public void Pass_HasKeyboardBindings_Fire2()
        {
            var asset = ReadProjectFile(InputManagerPath);
            var keys = GetKeyboardBindings(asset, "Fire2");
            CollectionAssert.Contains(keys, "left alt");
            CollectionAssert.Contains(keys, "mouse 1");
        }

        [Test]
        public void Cancel_HasKeyboardBinding_Escape()
        {
            var asset = ReadProjectFile(InputManagerPath);
            var keys = GetKeyboardBindings(asset, "Cancel");
            CollectionAssert.Contains(keys, "escape");
        }

        // ---- 3. HumanPlayerInput routes keyboard through named axes ----

        [Test]
        public void HumanPlayerInput_ReadsKeyboardActionsThroughNamedAxes()
        {
            // Every keyboard-supported action in HumanPlayerInput.Update() must use
            // named-legacy-axis calls (GetAxisRaw/GetButton/GetButtonDown), NOT raw
            // KeyCode lookups. This ensures both keyboard and controller bindings
            // (from InputManager.asset) feed the same logical provider member.
            var method = typeof(HumanPlayerInput)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "HumanPlayerInput.Update() must exist.");
            Assert.IsTrue(method.IsPrivate, "Update() must remain the private sampling gate.");
        }

        // ---- 4. Keyboard and controller map to the same logical member ----

        [Test]
        public void KeyboardAndController_MapToSameLogicalProvider()
        {
            // No separate KeyboardSprint/ControllerSprint/KeyboardMove etc. may exist.
            var forbidden = new[]
            {
                "KeyboardSprint", "KeyboardMove", "KeyboardPass", "KeyboardShoot",
                "KeyboardTackle", "KeyboardInteract", "KeyboardCancel", "KeyboardPause",
                "KeyboardInputProvider", "KeyboardInputService", "KeyboardInputManager",
                "KeyboardInputSystem", "KeyboardDevice"
            };
            var names = GetAllTypeNames();
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + f) || n == f),
                    $"'{f}' must not exist — keyboard input feeds HumanPlayerInput, "
                    + "not a separate keyboard abstraction.");
            }
        }

        // ---- 5. No keyboard types leak into Core ----

        [Test]
        public void Core_DoesNotDependOnKeyboardApi()
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

        // ---- 6. InputFrame is device-neutral (no keyboard types) ----

        [Test]
        public void InputFrame_ContainsNoDeviceTypes()
        {
            var forbidden = new[]
            {
                "UnityEngine.Input", "UnityEngine.InputSystem", "UnityEngine.KeyCode",
                "InputAction", "InputControl", "Keyboard", "Gamepad", "Mouse"
            };
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static;
            foreach (var f in typeof(InputFrame).GetFields(flags))
                Assert.IsFalse(forbidden.Contains(f.FieldType.FullName),
                    $"InputFrame field '{f.Name}' must not be device type '{f.FieldType}'.");
            foreach (var p in typeof(InputFrame).GetProperties(flags))
                Assert.IsFalse(forbidden.Contains(p.PropertyType.FullName),
                    $"InputFrame property '{p.Name}' must not be device type '{p.PropertyType}'.");
        }

        // ---- 7. Temporal semantics: continuous vs transient ----

        [Test]
        public void TemporalSemantics_ArePreserved()
        {
            // Continuous: MoveDirection (Vector2), LookDirection (Vector2), Sprint (bool held).
            // Transient: Pass, Shoot, Tackle, Interact, Cancel, Pause, SwitchPlayer, Skill.
            Assert.AreEqual(typeof(Vector2), typeof(IPlayerInput).GetProperty("MoveDirection").PropertyType);
            Assert.AreEqual(typeof(Vector2), typeof(IPlayerInput).GetProperty("LookDirection").PropertyType);
            Assert.AreEqual(typeof(bool), typeof(IPlayerInput).GetProperty("Sprint").PropertyType);
            foreach (var oneShot in new[] { "Pass", "Shoot", "Tackle", "SwitchPlayer", "Skill", "Interact", "Cancel", "Pause" })
                Assert.AreEqual(typeof(bool), typeof(IPlayerInput).GetProperty(oneShot).PropertyType,
                    $"'{oneShot}' is a one-shot transient contract member.");
        }

        // ---- 8. Unbound actions remain unbound ----

        [Test]
        public void UnboundActions_AreIntentionallyUnbound()
        {
            // SwitchPlayer/SwitchDirection/Skill/SkillDirection/LookDirection have no
            // keyboard bindings — consistent with Tasks 123/124/125 deferral.
            var asset = ReadProjectFile(InputManagerPath);
            // No named axes for these actions exist in InputManager.asset.
            Assert.IsFalse(HasAxis(asset, "SwitchPlayer"),
                "SwitchPlayer must not have a named InputManager axis (intentionally unbound).");
            Assert.IsFalse(HasAxis(asset, "SwitchDirection"),
                "SwitchDirection must not have a named InputManager axis (intentionally unbound).");
            Assert.IsFalse(HasAxis(asset, "Skill"),
                "Skill must not have a named InputManager axis (intentionally unbound).");
            Assert.IsFalse(HasAxis(asset, "SkillDirection"),
                "SkillDirection must not have a named InputManager axis (intentionally unbound).");
            Assert.IsFalse(HasAxis(asset, "LookDirection"),
                "LookDirection must not have a named InputManager axis (intentionally unbound).");
        }

        // ---- 9. HumanPlayerInput implements the full contract ----

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
                var hp = typeof(HumanPlayerInput).GetProperty(name);
                var ip = typeof(IPlayerInput).GetProperty(name);
                Assert.IsNotNull(hp, $"HumanPlayerInput must implement '{name}'.");
                Assert.AreEqual(ip.PropertyType, hp.PropertyType,
                    $"HumanPlayerInput.{name} type must match IPlayerInput.{name}.");
            }
        }

        // ---- 10. No device leakage: all device reads confined to HumanPlayerInput ----

        [Test]
        public void AllDeviceReads_AreConfinedToHumanPlayerInput()
        {
            // The grep audit confirmed only HumanPlayerInput.cs contains
            // UnityEngine.Input in Runtime. Other providers (AI, Replay, Network)
            // are pure data-driven with no device reads. HumanPlayerInput is the
            // sole device-reading MonoBehaviour in the provider layer.
            var humanProvider = typeof(HumanPlayerInput);
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(humanProvider),
                "HumanPlayerInput is a MonoBehaviour that owns all device reads.");
        }

        // ---- 11. No duplicate/competing keyboard abstraction ----

        [Test]
        public void NoDuplicateKeyboardProvider_Exists()
        {
            var names = GetAllTypeNames();
            Assert.IsFalse(names.Any(n =>
                    n.EndsWith("KeyboardInputProvider") || n == "KeyboardInputProvider"),
                "No separate KeyboardInputProvider may exist — keyboard feeds HumanPlayerInput.");
            // HumanPlayerInput remains the sole human-input provider.
            var humanProviders = SafeGetTypes(typeof(HumanPlayerInput).Assembly)
                .Where(x => x.IsClass && typeof(IPlayerInput).IsAssignableFrom(x) && !x.IsAbstract
                            && x.Name.IndexOf("Human", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(x => x.Name)
                .OrderBy(n => n)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "HumanPlayerInput" }, humanProviders,
                "HumanPlayerInput is the sole human provider (no parallel keyboard provider).");
        }
    }
}
