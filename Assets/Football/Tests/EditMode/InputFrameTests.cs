using System.Linq;
using System.Reflection;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 117 — InputFrame focused contract tests.
    ///
    /// Verifies that <see cref="InputFrame"/> is the device-neutral gameplay input
    /// snapshot value: pure data, immutable, default-zero, and containing exactly the
    /// gameplay-relevant contract values with NO device/provider/runtime leakage.
    /// </summary>
    public class InputFrameTests
    {
        [Test]
        public void InputFrame_Exists_AsReadonlyValueType()
        {
            var t = typeof(InputFrame);
            Assert.IsTrue(t.IsValueType, "InputFrame must be a value type.");
            Assert.IsTrue(t.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), false),
                "InputFrame must be a readonly struct.");
        }

        [Test]
        public void InputFrame_Defaults_AreZeroOrFalse()
        {
            var f = default(InputFrame);
            Assert.AreEqual(Vector2.zero, f.MoveDirection);
            Assert.AreEqual(Vector2.zero, f.LookDirection);
            Assert.IsFalse(f.Sprint);
            Assert.IsFalse(f.Pass);
            Assert.IsFalse(f.Shoot);
            Assert.IsFalse(f.Tackle);
            Assert.IsFalse(f.SwitchPlayer);
            Assert.AreEqual(Vector2.zero, f.SwitchDirection);
            Assert.IsFalse(f.Skill);
            Assert.AreEqual(Vector2.zero, f.SkillDirection);
            Assert.IsFalse(f.Interact);
            Assert.IsFalse(f.Cancel);
            Assert.IsFalse(f.Pause);
        }

        [Test]
        public void InputFrame_Constructor_PopulatesAllMembers()
        {
            var f = new InputFrame(
                new Vector2(1f, 0.5f),
                new Vector2(0f, 1f),
                true, true, true, true, true, new Vector2(0.5f, -0.5f),
                true, new Vector2(0.3f, 0.7f),
                true, true, true);

            Assert.AreEqual(new Vector2(1f, 0.5f), f.MoveDirection);
            Assert.AreEqual(new Vector2(0f, 1f), f.LookDirection);
            Assert.IsTrue(f.Sprint);
            Assert.IsTrue(f.Pass);
            Assert.IsTrue(f.Shoot);
            Assert.IsTrue(f.Tackle);
            Assert.IsTrue(f.SwitchPlayer);
            Assert.AreEqual(new Vector2(0.5f, -0.5f), f.SwitchDirection);
            Assert.IsTrue(f.Skill);
            Assert.AreEqual(new Vector2(0.3f, 0.7f), f.SkillDirection);
            Assert.IsTrue(f.Interact);
            Assert.IsTrue(f.Cancel);
            Assert.IsTrue(f.Pause);
        }

        [Test]
        public void InputFrame_IsImmutable_HasNoSetters()
        {
            foreach (var p in typeof(InputFrame).GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsNull(p.SetMethod, $"InputFrame property '{p.Name}' must not be settable.");
            }
        }

        [Test]
        public void InputFrame_ContainsNoDeviceOrProviderState()
        {
            var forbidden = new[]
            {
                "UnityEngine.Input", "UnityEngine.InputSystem", "InputAction", "InputControl",
                "Keyboard", "Gamepad", "Mouse", "IPlayerInput", "MonoBehaviour"
            };

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            foreach (var f in typeof(InputFrame).GetFields(flags))
            {
                Assert.IsFalse(forbidden.Contains(f.FieldType.FullName),
                    $"InputFrame must not contain device/provider field '{f.Name}' of type '{f.FieldType}'.");
            }
            foreach (var p in typeof(InputFrame).GetProperties(flags))
            {
                Assert.IsFalse(forbidden.Contains(p.PropertyType.FullName),
                    $"InputFrame must not contain device/provider property '{p.Name}' of type '{p.PropertyType}'.");
            }
        }

        [Test]
        public void InputFrame_ContainsNoRuntimeOrLifecycleMethods()
        {
            var forbidden = new[] { "Update", "FixedUpdate", "LateUpdate" };
            foreach (var m in forbidden)
            {
                Assert.IsNull(typeof(InputFrame).GetMethod(m,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"InputFrame must not contain '{m}'.");
            }
        }

        [Test]
        public void InputFrame_Members_MatchCurrentInputContract()
        {
            var names = typeof(InputFrame).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    "Cancel", "Interact", "LookDirection", "MoveDirection",
                    "Pass", "Pause", "Shoot", "Skill", "SkillDirection",
                    "Sprint", "SwitchDirection", "SwitchPlayer", "Tackle"
                },
                names,
                "InputFrame must expose exactly the gameplay-relevant contact values.");
        }

        [Test]
        public void InputFrame_DoesNotContainProviderGate_IsEnabled()
        {
            Assert.IsNull(typeof(InputFrame).GetProperty("IsEnabled"),
                "InputFrame must exclude the provider enable gate (IsEnabled).");
        }

        [Test]
        public void InputFrame_Copy_HasValueSemantics()
        {
            var a = new InputFrame(
                new Vector2(1f, 0f), new Vector2(0f, 1f),
                true, false, true, false, true, new Vector2(-1f, 0f),
                true, new Vector2(0.5f, -0.5f),
                false, false, false);
            var b = a;
            Assert.AreEqual(a.MoveDirection, b.MoveDirection);
            Assert.AreEqual(a.Pass, b.Pass);
            Assert.AreEqual(a.SwitchDirection, b.SwitchDirection);
            Assert.AreEqual(a.SkillDirection, b.SkillDirection);
        }
    }
}