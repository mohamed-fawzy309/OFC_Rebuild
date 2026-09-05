using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 109 — DribbleConfig (dribbling-system tuning).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 109 establishes DribbleConfig as the authored DRIBBLING-SYSTEM TUNING owner, distinct from:
    ///   - PLAYER DATA: PlayerStats.Dribbling (Dribbling/BallControl/TightPossession/Agility/Balance/
    ///     Reactions) are PLAYER RATINGS, NOT dribble config. They stay on PlayerStats.
    ///   - BALL CONFIG: BallConfig owns generic ball physics (mass/radius/drag/bounce) and generic
    ///     kick/pass/chip interaction. DribbleConfig owns dribble-SPECIFIC touch/stick/possession
    ///     behavior. Task 109 removes the TRUE DUPLICATE BallConfig.DribbleStickDistance, leaving the
    ///     ball-stick distance solely on DribbleConfig.BallStickDistance.
    ///   - MOVEMENT CONFIG: base locomotion (walk/run/sprint/accel/turn) stays on MovementConfig;
    ///     dribble-specific movement modifiers (MaxDribbleSpeed/DribbleAcceleration/
    ///     SpeedPenaltyAtHighDribble) are dribbling tuning on DribbleConfig.
    ///   - MATCH/TEAM/RESTARTS: none.
    ///   - INPUT/ANIMATION/AI: no button mapping, no animation state, no AI decision state.
    ///   - RUNTIME: no dribble runtime state (position/possession/touch/timer), no gameplay loops, no
    ///     scene references; no dribble gameplay system is manufactured (data-architecture only).
    ///
    /// AUDIT: there is NO dribble gameplay runtime yet (no dribble/possession/ball-control system,
    /// no player controller) — Task 56 finding. DribbleConfig therefore holds forward-specified
    /// authored tuning with no live consumer; these are VALID FUTURE CONFIG, not dead/obsolete. No
    /// consumer is fabricated and no dribble gameplay system is created here.
    /// </summary>
    public class DribbleConfigTests
    {
        private const string Prefix = "Football.Data.";

        private static Type FindType(string fullName)
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(x => x.FullName == fullName);
            Assert.IsNotNull(t, $"Type '{fullName}' must exist.");
            return t;
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null).ToArray(); }
        }

        private static IEnumerable<Type> AllTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes);
        }

        private static bool HasTypeName(params string[] names)
        {
            var all = AllTypes().ToList();
            return names.Any(n => all.Any(t => t.Name == n && t.Namespace != null
                                               && t.Namespace.StartsWith("Football")));
        }

        private static Type DribbleConfigType() => FindType(Prefix + "DribbleConfig");

        private static Type BallConfigType() => FindType(Prefix + "BallConfig");

        private static Type MovementConfigType() => FindType(Prefix + "MovementConfig");

        private static object NewConfig() => ScriptableObject.CreateInstance(DribbleConfigType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        // ---- 109.1 Representation / ScriptableObject strategy ----

        [Test]
        public void DribbleConfig_IsAnAuthoredScriptableObject()
        {
            var t = DribbleConfigType();
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(t) && t != typeof(ScriptableObject),
                "DribbleConfig must be an authored ScriptableObject (authored, shared, inspector-tunable dribbling config).");
        }

        [Test]
        public void DribbleConfig_IsNotARuntimeController()
        {
            var t = DribbleConfigType();
            Assert.IsTrue(t.IsClass, "DribbleConfig must be a data class.");
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(t),
                "DribbleConfig is authored config, not a MonoBehaviour controller.");
        }

        [Test]
        public void NoDuplicateDribbleConfigTypeExists()
        {
            Assert.IsFalse(HasTypeName("DribbleSettings", "DribbleParameters", "DribbleTuning",
                "DribbleDefinition", "BallControlConfig", "PossessionConfig", "TouchConfig"),
                "DribbleConfig is the single dribbling-system owner; no duplicate/obsolete type.");
        }

        // ---- 109.2 Dribbling-system tuning fields ----

        [Test]
        public void DribbleControlFields_ArePresent_AsSystemTuningFloats()
        {
            var t = DribbleConfigType();
            foreach (var n in new[] { "BallStickDistance", "BallStickHeight", "BallControlRadius" })
            {
                var f = FieldOf(t, n);
                Assert.IsNotNull(f, $"'{n}' must exist (dribble control tuning).");
                Assert.AreEqual(typeof(float), f.FieldType, $"'{n}' must be a float.");
            }
        }

        [Test]
        public void DribbleSpeedFields_ArePresent_AsSystemTuningFloats()
        {
            var t = DribbleConfigType();
            foreach (var n in new[] { "MaxDribbleSpeed", "DribbleAcceleration", "SpeedPenaltyAtHighDribble" })
            {
                Assert.IsNotNull(FieldOf(t, n), $"'{n}' must exist (dribble speed tuning).");
            }
        }

        [Test]
        public void DribbleFootAndTouchFields_ArePresent_AsSystemTuningFloats()
        {
            var t = DribbleConfigType();
            foreach (var n in new[] { "FootSwitchInterval", "FootSwitchAngle", "TouchForce", "TouchInterval" })
            {
                Assert.IsNotNull(FieldOf(t, n), $"'{n}' must exist (dribble foot/touch tuning).");
            }
        }

        [Test]
        public void DribbleDefaults_ArePositiveAndSensible()
        {
            var c = NewConfig();
            Assert.AreEqual(0.5f, (float)FieldOf(DribbleConfigType(), "BallStickDistance").GetValue(c), 0.001f,
                "BallStickDistance default must be a sensible ball-at-feet distance (m).");
            Assert.Greater((float)FieldOf(DribbleConfigType(), "MaxDribbleSpeed").GetValue(c), 0f);
            Assert.GreaterOrEqual((float)FieldOf(DribbleConfigType(), "SpeedPenaltyAtHighDribble").GetValue(c), 0f);
            Assert.Greater((float)FieldOf(DribbleConfigType(), "FootSwitchInterval").GetValue(c), 0f);
            Assert.Greater((float)FieldOf(DribbleConfigType(), "TouchInterval").GetValue(c), 0f);
        }

        // ---- Ball-stick distance single ownership (Task 109 duplicate removal) ----

        [Test]
        public void BallStickDistance_OwnedByDribbleConfig_NotBallConfig()
        {
            // Task 109 removed the TRUE DUPLICATE BallConfig.DribbleStickDistance; the sole
            // ball-stick distance is DribbleConfig.BallStickDistance.
            Assert.IsNotNull(FieldOf(DribbleConfigType(), "BallStickDistance"),
                "DribbleConfig must own the dribble ball-stick distance.");
            Assert.IsNull(FieldOf(BallConfigType(), "DribbleStickDistance"),
                "BallConfig must NOT own DribbleStickDistance (moved to DribbleConfig).");
            Assert.IsNull(FieldOf(BallConfigType(), "BallStickDistance"),
                "BallConfig must NOT own BallStickDistance.");
        }

        // ---- 109.3 PlayerStats.Dribbling separation ----

        [Test]
        public void DribbleConfig_DoesNotDuplicate_PlayerRatings()
        {
            foreach (var n in new[] { "Dribbling", "BallControl", "TightPossession",
                                      "Agility", "Balance", "Reactions" })
            {
                Assert.IsNull(FieldOf(DribbleConfigType(), n),
                    $"Player rating '{n}' is Player Data (PlayerStats.Dribbling); DribbleConfig must not duplicate it.");
            }
        }

        // ---- 109.4 BallConfig separation ----

        [Test]
        public void DribbleConfig_DoesNotDuplicate_GenericBallPhysics()
        {
            foreach (var n in new[] { "Mass", "Radius", "Drag", "AngularDrag", "Bounciness", "MaxSpeed",
                                      "Friction", "RollingFriction", "AirResistance",
                                      "KickForce", "PassForce", "ChipForce" })
            {
                Assert.IsNull(FieldOf(DribbleConfigType(), n),
                    $"Generic ball tuning '{n}' is BallConfig's; DribbleConfig must not duplicate it.");
            }
        }

        // ---- 109.5 MovementConfig separation ----

        [Test]
        public void DribbleConfig_DoesNotDuplicate_BaseMovement()
        {
            foreach (var n in new[] { "WalkSpeed", "RunSpeed", "SprintSpeed", "BackpedalSpeed",
                                      "Acceleration", "Deceleration", "SprintAcceleration",
                                      "TurnSpeed", "SprintTurnSpeed" })
            {
                Assert.IsNull(FieldOf(DribbleConfigType(), n),
                    $"Base locomotion '{n}' is MovementConfig's; DribbleConfig must not duplicate it.");
            }
        }

        // ---- 109.3/6 Match / team / restart separation ----

        [Test]
        public void DribbleConfig_DoesNotDuplicate_MatchOrTeamData()
        {
            foreach (var n in new[] { "MatchDurationSeconds", "HalfDurationSeconds", "NumHalves",
                                      "UseExtraTime", "UsePenaltyShootout", "EnforceOffside",
                                      "MaxSubstitutions", "TeamId", "TeamName", "Formation" })
            {
                Assert.IsNull(FieldOf(DribbleConfigType(), n),
                    $"Match/team '{n}' belongs elsewhere; DribbleConfig must not duplicate it.");
            }
        }

        // ---- 109.6 Runtime safety ----

        [Test]
        public void DribbleConfig_HoldsNoRuntimeDribbleState()
        {
            foreach (var n in new[]
            {
                "CurrentDribbleState", "CurrentBall", "CurrentOwner", "CurrentTarget", "CurrentTouch",
                "CurrentPossession", "CurrentControl", "CurrentDirection", "CurrentDistance",
                "CurrentVelocity", "CurrentPosition", "LastTouch", "LastDribble", "Cooldown",
                "Timer", "State", "Target"
            })
            {
                Assert.IsNull(FieldOf(DribbleConfigType(), n),
                    $"DribbleConfig must not hold runtime dribble state '{n}'.");
            }
        }

        [Test]
        public void DribbleConfig_HasNoUpdateLoops()
        {
            var t = DribbleConfigType();
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"DribbleConfig must not contain '{m}' (authored config, not a gameplay loop).");
            }
        }

        [Test]
        public void DribbleConfig_HasNoInappropriateRuntimeUnityReferences()
        {
            var t = DribbleConfigType();
            foreach (var n in new[] { "Rigidbody", "Transform", "Collider", "GameObject",
                                      "MonoBehaviour", "Camera", "Animator" })
            {
                Assert.IsNull(FieldOf(t, n),
                    $"DribbleConfig must not carry a runtime scene reference '{n}'.");
            }
        }

        // ---- 109.6 Input / animation / AI boundaries ----

        [Test]
        public void DribbleConfig_HoldsNoInputMapping()
        {
            foreach (var n in new[] { "DribbleButton", "ModifierButton", "DribbleAxis", "InputAxis",
                                      "Joystick", "DribbleKey", "ModifierAxis" })
            {
                Assert.IsNull(FieldOf(DribbleConfigType(), n),
                    $"DribbleConfig must not hold input/button mapping '{n}' (input = separate layer).");
            }
        }

        [Test]
        public void DribbleConfig_HoldsNoAnimationState()
        {
            foreach (var n in new[] { "CurrentAnimation", "CurrentClip", "AnimatorState",
                                      "CurrentBlend", "DribbleClip" })
            {
                Assert.IsNull(FieldOf(DribbleConfigType(), n),
                    $"DribbleConfig must not hold animation state '{n}'.");
            }
        }

        [Test]
        public void DribbleConfig_HoldsNoAIState()
        {
            foreach (var n in new[] { "CurrentDecision", "CurrentBehavior", "CurrentIntention",
                                      "CurrentTargetPlayer" })
            {
                Assert.IsNull(FieldOf(DribbleConfigType(), n),
                    $"DribbleConfig must not hold AI state '{n}' (AI tuning belongs to AIConfig).");
            }
        }

        // ---- No dribble gameplay system manufactured (hard boundary) ----

        [Test]
        public void NoDribbleGameplaySystem_WasCreated()
        {
            Assert.IsFalse(HasTypeName("DribbleSystem", "DribbleManager", "BallControlSystem",
                "PossessionSystem", "SkillMoveSystem", "DribbleController"),
                "Task 109 is config architecture; no dribble gameplay system may be created.");
        }
    }
}
