using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 107 — MovementConfig (gameplay tuning).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 107 establishes MovementConfig as the authored GAMEPLAY SYSTEM TUNING owner for how
    /// movement is interpreted/applied (world-unit floats: speeds m/s, acceleration m/s², turn
    /// degrees/s, stamina drain/regen rates). It is DISTINCT from:
    ///   - PLAYER DATA: PlayerStats.Pace.Acceleration/SprintSpeed and PlayerStats.Physical.Stamina
    ///     are player CAPABILITY ratings (int, default 50), which remain on PlayerStats. MovementConfig
    ///     may reuse same-named fields by design (system interpretation, not the player attribute
    ///     source) — the two layers coexist and are never merged or deleted.
    ///   - MATCH DATA: MatchRulesDefinition owns match rules; none are duplicated here.
    ///   - TEAM DATA: no TeamId/Squad/Formation/Tactics.
    ///   - BALL CONFIG: BallConfig remains the sole ball physics owner (Mass/Radius/GravityMultiplier
    ///     are NOT duplicated here).
    ///
    /// AUDIT: there is NO movement gameplay runtime yet (documented Task 55 finding) — no movement
    /// controller/driver, no CharacterController, no velocity/speed/grounded runtime state, no
    /// consumer of IPlayerInput. Therefore MovementConfig holds forward-specified authored tuning
    /// with no live consumer; these are VALID FUTURE CONFIG (not dead/obsolete). No movement gameplay
    /// system is manufactured here (data-architecture only).
    ///
    /// MovementConfig must NOT be a runtime controller: no runtime state (CurrentSpeed/Velocity/
    /// Stamina/Direction/Position/Rotation/State/Timer/Cooldown/Target), no Update/FixedUpdate/
    /// LateUpdate, and no runtime scene references (Transform/Rigidbody/Collider/CharacterController/
    /// Animator/MonoBehaviour/GameObject). GroundLayer is a LayerMask — a valid authored configuration
    /// reference (ground-surface identifier), not a runtime component dependency.
    /// </summary>
    public class MovementConfigTests
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

        private static Type MovementConfigType() => FindType(Prefix + "MovementConfig");

        private static Type PaceStatsType() => FindType(Prefix + "PaceStats");

        private static Type PhysicalStatsType() => FindType(Prefix + "PhysicalStats");

        private static object NewConfig() => ScriptableObject.CreateInstance(MovementConfigType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        // ---- 107.1 Representation / ScriptableObject strategy ----

        [Test]
        public void MovementConfig_IsAnAuthoredScriptableObject()
        {
            var t = MovementConfigType();
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(t) && t != typeof(ScriptableObject),
                "MovementConfig must be an authored ScriptableObject (authored, shared, inspector-tunable system config).");
        }

        [Test]
        public void MovementConfig_IsNotARuntimeController()
        {
            var t = MovementConfigType();
            Assert.IsTrue(t.IsClass, "MovementConfig must be a data class.");
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(t),
                "MovementConfig is authored config, not a MonoBehaviour controller.");
        }

        [Test]
        public void NoDuplicateMovementConfigTypeExists()
        {
            Assert.IsFalse(HasTypeName("MovementSettings", "MovementParameters", "MovementDefinition",
                "MovementTuning", "PlayerMovementConfig", "LocomotionConfig"),
                "MovementConfig is the single movement system-tuning owner; no duplicate/obsolete type.");
        }

        // ---- 107.2 Configuration fields ----

        [Test]
        public void Speeds_ArePresent_AsSystemTuningFloats()
        {
            var t = MovementConfigType();
            foreach (var n in new[] { "WalkSpeed", "RunSpeed", "SprintSpeed", "BackpedalSpeed" })
            {
                var f = FieldOf(t, n);
                Assert.IsNotNull(f, $"'{n}' must exist (movement system tuning).");
                Assert.AreEqual(typeof(float), f.FieldType, $"'{n}' must be a float (world-unit m/s tuning).");
            }
        }

        [Test]
        public void Acceleration_Turns_ArePresent_AsSystemTuningFloats()
        {
            var t = MovementConfigType();
            foreach (var n in new[] { "Acceleration", "Deceleration", "SprintAcceleration",
                                      "TurnSpeed", "SprintTurnSpeed" })
            {
                var f = FieldOf(t, n);
                Assert.IsNotNull(f, $"'{n}' must exist (movement response tuning).");
                Assert.AreEqual(typeof(float), f.FieldType, $"'{n}' must be a float (unit-bearing tuning).");
            }
        }

        [Test]
        public void Defaults_ArePositiveAndSensible_ForCoreMovement()
        {
            var c = NewConfig();
            Assert.Greater((float)FieldOf(MovementConfigType(), "WalkSpeed").GetValue(c), 0f);
            Assert.Greater((float)FieldOf(MovementConfigType(), "RunSpeed").GetValue(c), 0f);
            Assert.Greater((float)FieldOf(MovementConfigType(), "SprintSpeed").GetValue(c), 0f);
            Assert.Greater((float)FieldOf(MovementConfigType(), "Acceleration").GetValue(c), 0f);
            Assert.Greater((float)FieldOf(MovementConfigType(), "Deceleration").GetValue(c), 0f);
            Assert.Greater((float)FieldOf(MovementConfigType(), "TurnSpeed").GetValue(c), 0f);
        }

        [Test]
        public void SprintSpeed_IsHigherThanRunSpeed_IsHigherThanWalkSpeed_ByDefault()
        {
            var c = NewConfig();
            float walk = (float)FieldOf(MovementConfigType(), "WalkSpeed").GetValue(c);
            float run = (float)FieldOf(MovementConfigType(), "RunSpeed").GetValue(c);
            float sprint = (float)FieldOf(MovementConfigType(), "SprintSpeed").GetValue(c);
            Assert.IsTrue(walk < run && run < sprint,
                "Default speed ordering must be walk < run < sprint.");
        }

        // ---- 107.3 Player data separation ----

        [Test]
        public void PlayerStats_PaceAccelerationAndSprintSpeed_RemainOnPlayerStats()
        {
            Assert.IsNotNull(FieldOf(PaceStatsType(), "Acceleration"),
                "PlayerStats.Pace.Acceleration (player capability rating) must remain Player Data.");
            Assert.IsNotNull(FieldOf(PaceStatsType(), "SprintSpeed"),
                "PlayerStats.Pace.SprintSpeed (player capability rating) must remain Player Data.");
        }

        [Test]
        public void PlayerStats_PhysicalStamina_RemainsOnPlayerStats()
        {
            Assert.IsNotNull(FieldOf(PhysicalStatsType(), "Stamina"),
                "PlayerStats.Physical.Stamina (player capability rating) must remain Player Data.");
        }

        [Test]
        public void MovementConfig_DoesNotDuplicate_PlayerIdentityOrProfile()
        {
            foreach (var n in new[] { "PlayerId", "PlayerName", "Nationality", "PrimaryPosition",
                                      "PreferredFoot", "WeakFoot", "SkillRating", "CardType", "PlayStyle" })
            {
                Assert.IsNull(FieldOf(MovementConfigType(), n),
                    $"MovementConfig must not own Player identity/profile data '{n}'.");
            }
        }

        // ---- 107.7 Match / Team / Ball duplication ----

        [Test]
        public void MovementConfig_DoesNotDuplicate_MatchRules()
        {
            foreach (var n in new[] { "MatchDurationSeconds", "HalfDurationSeconds", "NumHalves",
                                      "UseExtraTime", "UsePenaltyShootout", "EnforceOffside",
                                      "EnforceFouls", "EnforceMatchCards", "MaxSubstitutions",
                                      "MatchRestartType" })
            {
                Assert.IsNull(FieldOf(MovementConfigType(), n),
                    $"MatchRulesDefinition owns match rules; MovementConfig must not duplicate '{n}'.");
            }
        }

        [Test]
        public void MovementConfig_DoesNotDuplicate_TeamData()
        {
            foreach (var n in new[] { "TeamId", "Squad", "Formation", "Tactics", "TeamRatings" })
            {
                Assert.IsNull(FieldOf(MovementConfigType(), n),
                    $"TeamDefinition owns team data; MovementConfig must not duplicate '{n}'.");
            }
        }

        [Test]
        public void MovementConfig_DoesNotDuplicate_BallPhysics()
        {
            foreach (var n in new[] { "BallMass", "BallRadius", "GravityMultiplier" })
            {
                Assert.IsNull(FieldOf(MovementConfigType(), n),
                    $"BallConfig is the sole ball physics owner; MovementConfig must not duplicate '{n}'.");
            }
        }

        // ---- 107.4 Runtime safety ----

        [Test]
        public void MovementConfig_HoldsNoRuntimeState()
        {
            foreach (var n in new[]
            {
                "CurrentSpeed", "CurrentVelocity", "CurrentAcceleration", "CurrentDirection",
                "CurrentRotation", "CurrentPosition", "CurrentStamina", "CurrentMovementState",
                "LastMovement", "LastSpeed", "Velocity", "Position", "Rotation", "State", "Timer",
                "Cooldown", "Target", "ElapsedSeconds", "IsGrounded", "IsSprinting"
            })
            {
                Assert.IsNull(FieldOf(MovementConfigType(), n),
                    $"MovementConfig must not hold runtime state '{n}'.");
            }
        }

        [Test]
        public void MovementConfig_HasNoUpdateLoops()
        {
            var t = MovementConfigType();
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"MovementConfig must not contain '{m}' (authored config, not a gameplay loop).");
            }
        }

        [Test]
        public void MovementConfig_HasNoInappropriateRuntimeUnityReferences()
        {
            var t = MovementConfigType();
            foreach (var n in new[] { "Transform", "Rigidbody", "Collider", "CharacterController",
                                      "Animator", "MonoBehaviour", "GameObject" })
            {
                Assert.IsNull(FieldOf(t, n),
                    $"MovementConfig must not carry a runtime scene reference '{n}'.");
            }
        }

        [Test]
        public void GroundLayer_IsAValidLayerMaskConfigReference()
        {
            // LayerMask is a serializable authored identifier (which surface is 'ground'), NOT a
            // runtime component dependency. This is a legitimate configuration reference.
            var f = FieldOf(MovementConfigType(), "GroundLayer");
            Assert.IsNotNull(f, "GroundLayer must exist as the ground-surface config identifier.");
            Assert.AreEqual(typeof(LayerMask), f.FieldType,
                "GroundLayer must be a LayerMask (config reference, not a runtime component).");
        }

        // ---- No movement gameplay system manufactured (hard boundary) ----

        [Test]
        public void NoMovementGameplaySystemWasCreated()
        {
            Assert.IsFalse(HasTypeName("MovementSystem", "PlayerController", "SprintController",
                "MovementController", "LocomotionSystem", "StaminaSystem", "PlayerMovementDriver",
                "MovementEngine"),
                "Task 107 is config architecture; no movement gameplay system may be created.");
        }
    }
}
