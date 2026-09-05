using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Football.Core;
using Football.Input;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 124 — Skill Input contract.
    ///
    /// Task 124 establishes (at the explicit user requirement) a device-neutral
    /// SKILL INPUT for technical football skill / dribble moves (Step Over,
    /// Body Feint, Ball Roll, Roulette, Elastico, Rainbow Flick, etc.).
    ///
    /// The input model is: SKILL BUTTON + DIRECTION.
    ///
    /// The contract carries:
    ///   - Skill request (one-shot bool pulse)
    ///   - Skill direction (device-neutral Vector2; zero = no direction)
    ///
    /// The direction is PLAYER INTENT. It does NOT identify a specific skill
    /// move, animation, or gameplay result. Which skill move actually executes
    /// is a gameplay responsibility, not an input responsibility.
    ///
    /// SkillRating remains player data/capability — NOT Skill Input.
    /// No SkillMoveSystem/SkillMoveController/SkillMoveAnimation/SkillAnimatorBridge
    /// was created.
    ///
    /// These tests inspect actual assembled types (reflection). They verify the
    /// INPUT CONTRACT only, never skill gameplay, execution, animation, physics,
    /// stamina, cooldown, success/failure, or UI.
    /// </summary>
    public class SkillInputContractTests
    {
        private static IEnumerable<Assembly> SkillInputAssemblies()
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
            => SkillInputAssemblies()
                .SelectMany(SafeGetTypes)
                .Select(t => t.FullName ?? t.Name)
                .ToArray();

        private static void AssertNoTypeNamed(string[] names, string[] forbidden, string why)
        {
            foreach (var f in forbidden)
            {
                Assert.IsFalse(names.Any(n => n.EndsWith("." + f) || n == f),
                    $"'{f}' must not exist — {why}");
            }
        }

        [Test]
        public void IPlayerInput_HasSkillRequest_AsReadOnlyBool()
        {
            var p = typeof(IPlayerInput).GetProperty("Skill");
            Assert.IsNotNull(p, "IPlayerInput must expose the skill request (Skill).");
            Assert.AreEqual(typeof(bool), p.PropertyType,
                "Skill must be a bool (one-shot skill request pulse).");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.Skill is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void IPlayerInput_HasSkillDirection_AsReadOnlyVector2()
        {
            var p = typeof(IPlayerInput).GetProperty("SkillDirection");
            Assert.IsNotNull(p, "IPlayerInput must expose the skill direction (SkillDirection).");
            Assert.AreEqual(typeof(Vector2), p.PropertyType,
                "SkillDirection must be a device-neutral Vector2.");
            Assert.IsNull(p.SetMethod,
                "IPlayerInput.SkillDirection is a provider contract — read-only from the consumer side.");
        }

        [Test]
        public void InputFrame_MatchesContract_ForSkillRequestAndDirection()
        {
            var reqContract = typeof(IPlayerInput).GetProperty("Skill");
            var reqSnap = typeof(InputFrame).GetProperty("Skill");
            Assert.IsNotNull(reqSnap, "InputFrame must expose Skill.");
            Assert.AreEqual(reqContract.PropertyType, reqSnap.PropertyType,
                "InputFrame.Skill must be the same bool type as IPlayerInput.Skill.");
            Assert.IsNull(reqSnap.SetMethod, "InputFrame.Skill must be read-only.");

            var dirContract = typeof(IPlayerInput).GetProperty("SkillDirection");
            var dirSnap = typeof(InputFrame).GetProperty("SkillDirection");
            Assert.IsNotNull(dirSnap, "InputFrame must expose SkillDirection.");
            Assert.AreEqual(dirContract.PropertyType, dirSnap.PropertyType,
                "InputFrame.SkillDirection must be the same Vector2 type as IPlayerInput.SkillDirection.");
            Assert.IsNull(dirSnap.SetMethod, "InputFrame.SkillDirection must be read-only.");
        }

        [Test]
        public void AllFourProviders_ExposeSkillRequest_AndDirection_OnTheSameContract()
        {
            var providers = SafeGetTypes(typeof(HumanPlayerInput).Assembly)
                .Where(t => t.IsClass && typeof(IPlayerInput).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "AIPlayerInput", "HumanPlayerInput", "NetworkPlayerInput", "ReplayPlayerInput" },
                providers,
                "Exactly the four known providers must implement IPlayerInput.");

            foreach (var name in providers)
            {
                var t = typeof(HumanPlayerInput).Assembly.GetType("Football.Input." + name);
                var req = t.GetProperty("Skill");
                Assert.IsNotNull(req, $"'{name}' must expose Skill.");
                Assert.AreEqual(typeof(bool), req.PropertyType, $"'{name}' Skill must be bool.");
                Assert.AreEqual(false, req.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' Skill must default to false (no request by default).");

                var dir = t.GetProperty("SkillDirection");
                Assert.IsNotNull(dir, $"'{name}' must expose SkillDirection.");
                Assert.AreEqual(typeof(Vector2), dir.PropertyType, $"'{name}' SkillDirection must be Vector2.");
                Assert.AreEqual(Vector2.zero, dir.GetValue(Activator.CreateInstance(t)),
                    $"'{name}' SkillDirection must default to zero (no direction by default).");
            }
        }

        [Test]
        public void SkillDirection_Zero_MeansNoDirection()
        {
            var dir = typeof(InputFrame).GetProperty("SkillDirection");
            Assert.AreEqual(Vector2.zero, dir.GetValue(default(InputFrame)),
                "A default/zero SkillDirection must represent 'no direction'.");
            Assert.IsFalse((bool)typeof(InputFrame).GetProperty("Skill").GetValue(default(InputFrame)),
                "A default InputFrame must carry NO skill request.");
        }

        [Test]
        public void SkillDirection_CanRepresentNonZero_DeviceNeutralDirections()
        {
            var ctor = typeof(InputFrame).GetConstructors().Single();
            var f = (InputFrame)ctor.Invoke(new object[]
            {
                Vector2.zero, Vector2.zero,             // move, look
                false, false, false, false,               // sprint, pass, shoot, tackle
                false, Vector2.zero,                      // switchPlayer, switchDirection
                true, new Vector2(0f, 1f),                // skill (true), skillDirection (up)
                false, false, false                        // interact, cancel, pause
            });
            Assert.IsTrue(f.Skill);
            Assert.AreEqual(new Vector2(0f, 1f), f.SkillDirection,
                "A non-zero SkillDirection must be representable and device-neutral.");
            Assert.IsTrue(f.Skill && f.SkillDirection != Vector2.zero,
                "Skill-with-direction must be representable (request true + non-zero direction).");
        }

        [Test]
        public void Skill_HasSingleAuthority_NoDuplicateSkillAbstraction()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SkillInput", "SkillInputProvider", "SkillInputReader", "SkillReader",
                "SkillCommand", "SkillSource", "SkillRequest", "SkillRequestEvent",
                "SkillButton", "SkillFlag", "SkillInputData", "SkillInputState",
                "PlayerSkillInput", "PlayerSkillCommand", "SkillPlayerInput"
            }, "Skill input has ONE authority (IPlayerInput / InputFrame). No duplicate abstraction.");
        }

        [Test]
        public void Skill_IsTransient_NoBufferingQueueOrPriorityMachinery()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SkillBuffer", "SkillQueuedInput", "SkillQueue", "SkillEdgeDetector", "SkillEdge",
                "SkillPrioritizer", "SkillPrioritySource", "SkillHistory", "SkillRetention",
                "SkillStatePersister", "SkillDebouncer", "SkillCooldown", "SkillTimer"
            }, "Skill is a TRANSIENT one-shot request with no buffering (Task 132), priority "
               + "(Task 133), edge machinery, cooldown, or history.");
        }

        [Test]
        public void SkillContract_MustNotCarrySkillExecutionState()
        {
            var forbidden = new[]
            {
                "SelectedSkill", "ExecutedSkill", "SkillResult", "SkillState",
                "SkillAnimation", "SkillDefinition", "SkillCooldown", "SkillSuccess",
                "SkillMove", "SkillType", "SkillId", "SkillIndex", "SkillSlot"
            };
            foreach (var t in new[] { typeof(IPlayerInput), typeof(InputFrame) })
            {
            foreach (var name in forbidden)
            {
                Assert.IsNull(t.GetProperty(name),
                    $"'{t.Name}' must not expose shooting-gameplay metadata '{name}' (mechanics are not input).");
            }
            }
        }

        [Test]
        public void NoSkillMoveSystem_InTheContract()
        {
            var names = GetAllTypeNames();
            AssertNoTypeNamed(names, new[]
            {
                "SkillMoveSystem", "SkillMoveController", "SkillMoveAnimation", "SkillAnimatorBridge"
            }, "No SkillMove system was created (forbidden by FootSkillRulesTests and user requirement).");

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var forbiddenDevices = new[]
            {
                "UnityEngine.InputSystem.Keyboard", "UnityEngine.InputSystem.Gamepad",
                "UnityEngine.InputSystem.Mouse", "UnityEngine.InputSystem.Touchscreen",
                "UnityEngine.InputSystem.InputAction", "UnityEngine.InputSystem.InputControl",
                "UnityEngine.KeyCode", "UnityEngine.Touch"
            };
            foreach (var p in typeof(InputFrame).GetProperties(flags))
            {
                Assert.IsFalse(forbiddenDevices.Contains(p.PropertyType.FullName),
                    $"InputFrame must not contain device type '{p.PropertyType}' on '{p.Name}'.");
            }
        }
    }
}
