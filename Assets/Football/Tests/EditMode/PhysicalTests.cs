using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 73 — Physical category (Strength, Stamina, Jumping, Aggression).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Physical was introduced with the full approved PlayerStats implementation in Task 67 as the
    /// <c>PhysicalStats</c> category. Task 73 confirms it: exactly the four approved player ratings,
    /// unified PlayerStats rating type/range/default (int [1..99], default 50), no unapproved
    /// attributes, no runtime state (no CurrentStamina/CurrentStrength/CurrentJump/CurrentFatigue/
    /// CurrentAggression/CurrentVelocity), no gameplay formulas (no CalculateJumpHeight/CalculateStamina/
    /// strength/aggression/physical-impact), no physics references (no Rigidbody/Transform/velocity/
    /// jump mechanics), no AI (Aggression is not an AI decision state), no duplication (not on
    /// PlayerDefinition, not into MovementConfig/BallConfig/DribbleConfig as a player rating; no
    /// TackleConfig/PhysicalConfig/StaminaConfig manufactured), no Physical/Stamina/Jump/Aggression
    /// systems created, Goalkeepers retain Physical (unified model), independent of PrimaryPosition,
    /// and no OverallRating calculation.
    /// </summary>
    public class PhysicalTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ApprovedAttributes = new[]
        {
            "Strength", "Stamina", "Jumping", "Aggression"
        };

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

        private static Type PhysicalType() => FindType(Prefix + "PhysicalStats");

        // ---- Category structure ----

        [Test]
        public void PhysicalCategory_Exists_AsSerializableData()
        {
            var t = PhysicalType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Physical must be a [Serializable] data category, not a MonoBehaviour/ScriptableObject system.");
        }

        [Test]
        public void Physical_Contains_ExactlyTheFourApprovedAttributes()
        {
            var names = PhysicalType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(ApprovedAttributes.OrderBy(x => x).ToArray(), names,
                "Physical must contain exactly the four approved attributes, nothing more/less.");
        }

        [Test]
        public void Physical_ContainsNoUnapprovedAttributes()
        {
            foreach (var name in new[] { "Balance", "Agility", "Reactions", "Heading", "Composure",
                "Athleticism", "Endurance", "Power", "StaminaRate", "JumpPower", "TacklingStrength" })
            {
                Assert.IsNull(PhysicalType().GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"'{name}' is NOT an approved Physical attribute.");
            }
        }

        // ---- Individual attribute existence ----

        [Test]
        public void Strength_Exists_AsAuthoredRating()
        {
            var f = PhysicalType().GetField("Strength", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Strength must exist in PhysicalStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Strength must be an int rating.");
        }

        [Test]
        public void Stamina_Exists_AsAuthoredCapability()
        {
            var f = PhysicalType().GetField("Stamina", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Stamina must exist in PhysicalStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Stamina must be an int rating.");
        }

        [Test]
        public void Jumping_Exists_AsAuthoredRating()
        {
            var f = PhysicalType().GetField("Jumping", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Jumping must exist in PhysicalStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Jumping must be an int rating.");
        }

        [Test]
        public void Aggression_Exists_AsAuthoredRating()
        {
            var f = PhysicalType().GetField("Aggression", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Aggression must exist in PhysicalStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Aggression must be an int rating.");
        }

        // ---- Rating representation (int 1-99, default 50) ----

        [Test]
        public void AllFourAttributes_UsePlayerStatsRatingRepresentation()
        {
            var stats = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)stats.GetField("RatingMin").GetRawConstantValue());
            Assert.AreEqual(99, (int)stats.GetField("RatingMax").GetRawConstantValue());
            Assert.AreEqual(50, (int)stats.GetField("RatingDefault").GetRawConstantValue());

            var obj = Activator.CreateInstance(PhysicalType());
            foreach (var name in ApprovedAttributes)
            {
                var f = PhysicalType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"'{name}' must exist.");
                Assert.AreEqual(typeof(int), f.FieldType, $"'{name}' must be int (rating), not float.");
                Assert.AreEqual(50, (int)f.GetValue(obj), $"'{name}' must default to PlayerStats.RatingDefault.");
            }
        }

        [Test]
        public void PhysicalAttrAreNotFloatWorldUnits()
        {
            foreach (var f in PhysicalType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(typeof(int), f.FieldType,
                    $"Physical attribute '{f.Name}' must be an int rating, not a float kg/m/s/Newton/Joule value.");
            }
        }

        // ---- Runtime / gameplay / physics / AI separation ----

        [Test]
        public void Physical_ContainsNoRuntimeState()
        {
            var t = PhysicalType();
            foreach (var name in new[] { "CurrentStrength", "CurrentStamina", "Fatigue", "StaminaRemaining",
                "CurrentJump", "CurrentFatigue", "CurrentAggression", "CurrentVelocity", "CurrentStaminaPercent",
                "RecoveryTimer", "JumpHeight", "StaminaUsed" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Physical must NOT hold runtime state '{name}'.");
                Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Physical must NOT expose runtime state '{name}'.");
            }
        }

        [Test]
        public void Stamina_IsNot_CurrentStamina()
        {
            // Physical.Stamina is the authored CAPABILITY. CurrentStamina / StaminaRemaining / Fatigue
            // are runtime state that must NOT be stored in PlayerStats (explicit Task 73 rule).
            Assert.IsNotNull(PhysicalType().GetField("Stamina", BindingFlags.Public | BindingFlags.Instance),
                "Authored Stamina must exist in PhysicalStats.");
            var psType = FindType(Prefix + "PlayerStats");
            foreach (var name in new[] { "CurrentStamina", "StaminaRemaining", "Fatigue", "RecoveryTimer" })
            {
                Assert.IsNull(PhysicalType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Physical must NOT store runtime stamina '{name}'.");
                Assert.IsNull(psType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PlayerStats must NOT store runtime stamina '{name}'.");
            }
        }

        [Test]
        public void Physical_DoesNotReferenceUnityRuntimeObjects()
        {
            foreach (var f in PhysicalType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(Transform), f.FieldType);
                Assert.AreNotEqual(typeof(Rigidbody), f.FieldType);
                Assert.AreNotEqual(typeof(Collider), f.FieldType);
                Assert.AreNotEqual(typeof(GameObject), f.FieldType);
                Assert.AreNotEqual(typeof(MonoBehaviour), f.FieldType);
                Assert.AreNotEqual(typeof(Animator), f.FieldType);
            }
        }

        [Test]
        public void Physical_ContainsNoGameplayLogic()
        {
            var t = PhysicalType();
            foreach (var name in new[] { "CalculateJumpHeight", "CalculateJump", "CalculateStamina",
                "CalculateStrength", "CalculateAggression", "CalculatePhysicalImpact", "CalculateFatigue",
                "ConsumeStamina", "RegenerateStamina", "ExecuteJump", "ApplyForce", "CalculateOverallRating" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Physical must NOT derive gameplay via '{name}'.");
                Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Physical must NOT expose gameplay via '{name}'.");
            }
        }

        [Test]
        public void Jumping_IsNotAWorldUnit_OrJumpMechanic()
        {
            // Jumping is an authored rating, NOT jump height / vertical velocity / jump force.
            foreach (var name in new[] { "JumpHeight", "VerticalVelocity", "JumpForce", "JumpDuration",
                "JumpVelocity", "JumpPower" })
            {
                Assert.IsNull(PhysicalType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Physical must NOT hold jump world data '{name}'.");
            }
        }

        [Test]
        public void Aggression_IsNotAnAIState()
        {
            // Aggression is PLAYER DATA, not an AI decision channel. Explicit Task 73.4 rule.
            foreach (var name in new[] { "AggressionAI", "AggressionState", "AggressionSystem",
                "DecisionSystem", "BehaviorSystem", "CurrentAggression" })
            {
                Assert.IsNull(PhysicalType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Physical must NOT hold AI state '{name}'.");
            }
        }

        [Test]
        public void Physical_DoesNotCalculateOverallRating()
        {
            var t = PhysicalType();
            Assert.IsNull(t.GetMethod("CalculateOverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetField("OverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        // ---- No duplication / system separation ----

        [Test]
        public void PhysicalAttributes_AreNotDuplicated_AtPlayerDefinitionLevel()
        {
            // NOTE: PlayerDefinition.Physical IS a legitimate field, but it is the biographical
            // PhysicalProfile (Age/HeightCm/WeightKg), NOT the PlayerStats.Physical rating category.
            // The four RATING attributes must not be carried at PlayerDefinition top level.
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in ApprovedAttributes)
            {
                Assert.IsNull(pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{name}' must live ONLY under PlayerStats.Physical, not on PlayerDefinition.");
            }
        }

        [Test]
        public void PhysicalRatings_NotDuplicated_InSystemConfigs()
        {
            // MovementConfig / BallConfig / DribbleConfig are SYSTEM tuning (world units), NOT the
            // authoritative player-rating source. None of the four player ratings may be stored there.
            foreach (var configName in new[] { "BallConfig", "MovementConfig", "DribbleConfig" })
            {
                var cfg = FindType(Prefix + configName);
                foreach (var name in ApprovedAttributes)
                {
                    Assert.IsNull(cfg.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"'{configName}' must NOT carry the player rating '{name}'.");
                }
            }
        }

        [Test]
        public void NoPhysicalConfig_WasManufactured()
        {
            // Audited: no PhysicalConfig.cs / StaminaConfig.cs exists. This task must NOT create one.
            foreach (var name in new[] { "PhysicalConfig", "StaminaConfig", "TackleConfig", "JumpConfig",
                "AggressionConfig", "FatigueConfig" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.FullName == Prefix + name),
                    $"{name} must NOT be manufactured for Task 73.");
            }
        }

        [Test]
        public void NoPhysicalGameplaySystems_WereCreated()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "PhysicalSystem", "StaminaSystem", "JumpSystem", "AggressionSystem",
                "FatigueSystem", "PhysicalMovementSystem", "StrengthSystem", "RigidbodySystem",
                "PhysicalAI", "StaminaRegenSystem", "PhysicalValidator", "AISystem", "DecisionSystem",
                "BehaviorSystem", "TackleDecisionSystem", "BehaviorTree" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"Physical task must NOT create '{name}'.");
            }
        }

        // ---- Goalkeeper unification / position independence ----

        [Test]
        public void Goalkeepers_RetainPhysicalData()
        {
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Physical", BindingFlags.Public | BindingFlags.Instance),
                "Every player (including GK) must retain the Physical category via unified PlayerStats.");
            Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .Any(t => t.Name == "GoalkeeperPlayerStats" || t.Name == "GoalkeeperPhysical"),
                "No separate goalkeeper stats/split model may exist.");
        }

        [Test]
        public void Physical_IsIndependentOfPrimaryPosition()
        {
            var names = PhysicalType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            Assert.IsFalse(names.Contains("PrimaryPosition"), "Physical must not be driven by PrimaryPosition.");
        }

        // ---- Authority: physical owns ratings, not systems ----

        [Test]
        public void Physical_IsAuthoredRating_NotMassOrForce()
        {
            // Strength is NOT mass/force/impulse. No physics unit fields.
            foreach (var name in new[] { "Mass", "Force", "Impulse", "RigidbodyMass", "CurrentForce" })
            {
                Assert.IsNull(PhysicalType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Physical must NOT store physics concept '{name}'.");
            }
        }
    }
}
