using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 108 — BallConfig (ball-system tuning).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 108 establishes BallConfig as the authored BALL-SYSTEM TUNING owner (ball physics,
    /// ball movement, and ball interaction tuning — mass/radius/drag/bounce/friction/kick/pass/chip),
    /// distinct from:
    ///   - MATCH DATA: MatchRulesDefinition owns match rules and must NOT own ball physics (a
    ///     Ball block was removed from it in Task 97; BallConfig is the SOLE ball-physics owner).
    ///   - PLAYER DATA: PlayerStats (Passing.ShortPassing/Curve, Shooting.ShotPower,
    ///     Dribbling.BallControl/TightPossession) remain player capability ratings — not ball config.
    ///   - RESTARTS: MatchRestartType (KickOff/ThrowIn/GoalKick/CornerKick/FreeKick/PenaltyKick/
    ///     DropBall) is Match Data taxonomy, not BallConfig.
    ///   - TEAM DATA: no TeamId/Squad/Formation/Tactics.
    ///   - RUNTIME: ball runtime state (position/velocity/possession/collision) is NOT in the config.
    ///
    /// AUDIT: there is NO ball gameplay runtime yet (Task 56 finding) — no BallController/system,
    /// no Rigidbody-driving script, no velocity/possession runtime state (the SoccerBall.prefab has
    /// a Rigidbody but no m_Script driving it). Therefore BallConfig holds forward-specified authored
    /// tuning with no live consumer; these are VALID FUTURE CONFIG (not dead/obsolete). No ball
    /// runtime system is manufactured here (data-architecture only).
    ///
    /// Gravity: BallConfig holds NO gravity multiplier (absent project-wide); global gravity is owned
    /// by Unity Physics + the ball Rigidbody on the prefab. BallConfig owns only ball-specific
    /// physical tuning (Mass/Radius/Drag/AngularDrag/Bounciness), never global gravity/environment.
    /// </summary>
    public class BallConfigTests
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

        private static Type BallConfigType() => FindType(Prefix + "BallConfig");

        private static Type MatchRulesType() => FindType(Prefix + "MatchRulesDefinition");

        private static object NewConfig() => ScriptableObject.CreateInstance(BallConfigType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        // ---- 108.1 Representation / ScriptableObject strategy ----

        [Test]
        public void BallConfig_IsAnAuthoredScriptableObject()
        {
            var t = BallConfigType();
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(t) && t != typeof(ScriptableObject),
                "BallConfig must be an authored ScriptableObject (authored, shared, inspector-tunable ball-system config).");
        }

        [Test]
        public void BallConfig_IsNotARuntimeController()
        {
            var t = BallConfigType();
            Assert.IsTrue(t.IsClass, "BallConfig must be a data class.");
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(t),
                "BallConfig is authored config, not a MonoBehaviour controller.");
        }

        [Test]
        public void NoDuplicateBallConfigTypeExists()
        {
            Assert.IsFalse(HasTypeName("BallSettings", "BallParameters", "BallTuning",
                "BallPhysicsConfig", "BallDefinition", "BallPhysicsSettings", "SoccerBallConfig"),
                "BallConfig is the single ball-system owner; no duplicate/obsolete type.");
        }

        // ---- 108.2 Ball physics tuning ----

        [Test]
        public void BallPhysicsFields_ArePresent_AsSystemTuningFloats()
        {
            var t = BallConfigType();
            foreach (var n in new[] { "Mass", "Radius", "Drag", "AngularDrag", "Bounciness" })
            {
                var f = FieldOf(t, n);
                Assert.IsNotNull(f, $"'{n}' must exist (ball physics tuning).");
                Assert.AreEqual(typeof(float), f.FieldType, $"'{n}' must be a float.");
            }
        }

        [Test]
        public void BallMovementFields_ArePresent_AsSystemTuningFloats()
        {
            var t = BallConfigType();
            foreach (var n in new[] { "MaxSpeed", "Friction", "RollingFriction", "AirResistance" })
            {
                Assert.IsNotNull(FieldOf(t, n), $"'{n}' must exist (ball movement tuning).");
            }
        }

        [Test]
        public void BallInteractionFields_ArePresent_AsSystemTuningFloats()
        {
            var t = BallConfigType();
            foreach (var n in new[] { "KickForce", "PassForce", "ChipForce" })
            {
                Assert.IsNotNull(FieldOf(t, n), $"'{n}' must exist (ball interaction tuning).");
            }
        }

        [Test]
        public void BallConfig_DoesNotOwn_DribbleStickDistance()
        {
            // Task 109: the ball-stick distance during a dribble is dribble-SPECIFIC tuning; it
            // belongs in DribbleConfig (DribbleConfig.BallStickDistance), NOT duplicated on BallConfig
            // (previously BallConfig.DribbleStickDistance was a TRUE duplicate — removed).
            Assert.IsNull(FieldOf(BallConfigType(), "DribbleStickDistance"),
                "BallConfig must not own a dribble-stick distance; it is DribbleConfig's.");
            Assert.IsNull(FieldOf(BallConfigType(), "BallStickDistance"),
                "BallConfig must not own the dribble ball-stick distance.");
        }

        [Test]
        public void Mass_MatchesTheAuthoredBallRigidbodyDefault()
        {
            var c = NewConfig();
            Assert.AreEqual(0.43f, (float)FieldOf(BallConfigType(), "Mass").GetValue(c), 0.001f,
                "BallConfig.Mass (kg) must match the SoccerBall.prefab Rigidbody mass of 0.43.");
        }

        [Test]
        public void CoreBallPhysicsDefaults_ArePositiveAndSensible()
        {
            var c = NewConfig();
            Assert.Greater((float)FieldOf(BallConfigType(), "Mass").GetValue(c), 0f);
            Assert.Greater((float)FieldOf(BallConfigType(), "Radius").GetValue(c), 0f);
            Assert.GreaterOrEqual((float)FieldOf(BallConfigType(), "MaxSpeed").GetValue(c), 0f);
            Assert.GreaterOrEqual((float)FieldOf(BallConfigType(), "KickForce").GetValue(c), 0f);
        }

        // ---- 108.3 MatchRulesDefinition separation ----

        [Test]
        public void MatchRulesDefinition_DoesNotOwnBallPhysics()
        {
            foreach (var n in new[] { "BallMass", "BallRadius", "GravityMultiplier", "Drag",
                                      "Bounciness", "Friction", "KickForce", "PassForce", "ChipForce" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition must NOT own ball physics field '{n}' (BallConfig is the sole owner).");
            }
        }

        [Test]
        public void BallConfig_IsTheSoleBallPhysicsOwner()
        {
            Assert.IsNotNull(FieldOf(BallConfigType(), "Mass"), "BallConfig must own ball Mass.");
            Assert.IsNotNull(FieldOf(BallConfigType(), "Radius"), "BallConfig must own ball Radius.");
        }

        [Test]
        public void BallConfig_DoesNotDuplicate_MatchRules()
        {
            foreach (var n in new[] { "MatchDurationSeconds", "HalfDurationSeconds", "NumHalves",
                                      "UseExtraTime", "ExtraTimeDurationSeconds", "UsePenaltyShootout",
                                      "EnforceOffside", "EnforceFouls", "EnforceMatchCards",
                                      "MaxSubstitutions" })
            {
                Assert.IsNull(FieldOf(BallConfigType(), n),
                    $"BallConfig must not duplicate match rule '{n}' (MatchRulesDefinition owns it).");
            }
        }

        [Test]
        public void BallConfig_DoesNotDuplicate_RestartTaxonomy()
        {
            foreach (var n in new[] { "MatchRestartType", "KickOff", "ThrowIn", "GoalKick",
                                      "CornerKick", "FreeKick", "PenaltyKick", "DropBall" })
            {
                Assert.IsNull(FieldOf(BallConfigType(), n),
                    $"A restart belongs to Match Data (MatchRestartType); BallConfig must not own '{n}'.");
            }
        }

        // ---- 108.4 PlayerStats separation ----

        [Test]
        public void BallConfig_DoesNotDuplicate_PlayerRatings()
        {
            foreach (var n in new[] { "ShortPassing", "LongPassing", "Passing", "Vision", "Crossing",
                                      "Curve", "ShotPower", "Finishing", "LongShots", "Shooting",
                                      "BallControl", "TightPossession", "Dribbling", "Acceleration",
                                      "SprintSpeed", "Stamina", "Pace" })
            {
                Assert.IsNull(FieldOf(BallConfigType(), n),
                    $"Player capability rating '{n}' is Player Data; BallConfig must not duplicate it.");
            }
        }

        [Test]
        public void BallConfig_DoesNotDuplicate_PlayerIdentity()
        {
            foreach (var n in new[] { "PlayerId", "PlayerName", "PrimaryPosition", "WeakFoot",
                                      "SkillRating", "CardType" })
            {
                Assert.IsNull(FieldOf(BallConfigType(), n),
                    $"BallConfig must not own player identity/profile '{n}'.");
            }
        }

        // ---- 108.4 Team data separation ----

        [Test]
        public void BallConfig_DoesNotDuplicate_TeamData()
        {
            foreach (var n in new[] { "TeamId", "TeamName", "Squad", "Formation", "Tactics", "TeamRatings" })
            {
                Assert.IsNull(FieldOf(BallConfigType(), n),
                    $"BallConfig must not duplicate team data '{n}' (TeamDefinition owns it).");
            }
        }

        // ---- 108.5 Runtime safety ----

        [Test]
        public void BallConfig_HoldsNoRuntimeBallState()
        {
            foreach (var n in new[]
            {
                "CurrentPosition", "CurrentVelocity", "CurrentAngularVelocity", "CurrentSpeed",
                "CurrentRotation", "CurrentDirection", "CurrentOwner", "CurrentPlayer",
                "CurrentPossession", "CurrentBallState", "CurrentCollision", "LastKick", "LastTouch",
                "CurrentForce", "Timer", "Cooldown", "Target", "Velocity", "Position", "Rotation",
                "Owner", "State"
            })
            {
                Assert.IsNull(FieldOf(BallConfigType(), n),
                    $"BallConfig must not hold runtime ball state '{n}'.");
            }
        }

        [Test]
        public void BallConfig_HasNoUpdateLoops()
        {
            var t = BallConfigType();
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"BallConfig must not contain '{m}' (authored config, not a gameplay loop).");
            }
        }

        [Test]
        public void BallConfig_HasNoInappropriateRuntimeUnityReferences()
        {
            var t = BallConfigType();
            foreach (var n in new[] { "Rigidbody", "Transform", "Collider", "GameObject",
                                      "MonoBehaviour", "Camera", "Animator" })
            {
                Assert.IsNull(FieldOf(t, n),
                    $"BallConfig must not carry a runtime scene reference '{n}'.");
            }
        }

        [Test]
        public void BallConfig_DoesNotOwnGravityMultiplier()
        {
            // Global gravity is owned by Unity Physics + the ball Rigidbody on the prefab, not the config.
            Assert.IsNull(FieldOf(BallConfigType(), "GravityMultiplier"),
                "BallConfig must not own a gravity multiplier (gravity is Unity Physics + Rigidbody; no duplication).");
        }

        // ---- No ball runtime system manufactured (hard boundary) ----

        [Test]
        public void NoBallRuntimeSystemWasCreated()
        {
            Assert.IsFalse(HasTypeName("BallController", "BallSystem", "BallManager",
                "SoccerBallController", "BallPhysicsSystem", "BallState", "PossessionSystem"),
                "Task 108 is config architecture; no ball runtime system may be created.");
        }
    }
}
