using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 66 — PlayerDefinition (the unified player data container).
    ///
    /// Runtime/Data is the no-asmdef folder, so its types (PlayerDefinition, Identity,
    /// PhysicalProfile, Profile, PlayerStats) compile into the auto-generated Assembly-CSharp.
    /// asmdef assemblies (like this test assembly) cannot hold a compile-time reference to
    /// Assembly-CSharp, so these tests inspect the actual types by reflection from the loaded
    /// AppDomain. This is "actual type inspection" (not source.Contains). It verifies the
    /// container architecture, the identity/profile/physical/stat boundaries, the unified
    /// goalkeeper rule, and the data-only (no runtime state / no gameplay) policy.
    /// </summary>
    public class PlayerDefinitionTests
    {
        private const string TypePrefix = "Football.Data.";

        private static Type FindType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.FullName == fullName);
            Assert.IsNotNull(type, $"Type '{fullName}' must exist in loaded assemblies.");
            return type;
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try
            {
                return asm.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null).ToArray();
            }
        }

        // ---- 66.1 Responsibility / data-only ----

        [Test]
        public void PlayerDefinition_Exists_AndIsScriptableObject()
        {
            var t = FindType(TypePrefix + "PlayerDefinition");
            Assert.AreEqual(typeof(ScriptableObject), t.BaseType,
                "PlayerDefinition must be a ScriptableObject (authored configuration), not a MonoBehaviour runtime system.");
        }

        [Test]
        public void PlayerDefinition_IsDataOnly_NoRuntimeObjectFields()
        {
            var t = FindType(TypePrefix + "PlayerDefinition");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(Transform), f.FieldType, $"'{f.Name}' must not reference Transform.");
                Assert.AreNotEqual(typeof(Rigidbody), f.FieldType, $"'{f.Name}' must not reference Rigidbody.");
                Assert.AreNotEqual(typeof(GameObject), f.FieldType, $"'{f.Name}' must not reference GameObject.");
                Assert.AreNotEqual(typeof(MonoBehaviour), f.FieldType, $"'{f.Name}' must not reference MonoBehaviour.");
                Assert.AreNotEqual(typeof(Collider), f.FieldType, $"'{f.Name}' must not reference Collider.");
            }
        }

        [Test]
        public void PlayerDefinition_DoesNotOwnGameplayOrInputOrAnimationOrAIOrCamera()
        {
            var t = FindType(TypePrefix + "PlayerDefinition");
            foreach (var name in new[]
            {
                "PlayerController", "MovementController", "MovementSystem", "ShootingSystem",
                "PassingSystem", "DribblingSystem", "TackleSystem", "GoalkeeperSystem", "AIController",
                "AnimationController", "CameraController", "Input"
            })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PlayerDefinition must NOT own '{name}'.");
            }
        }

        // ---- 66.1 Group boundaries ----

        [Test]
        public void PlayerDefinition_HasIdentityField()
        {
            var t = FindType(TypePrefix + "PlayerDefinition");
            var f = t.GetField("Identity", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "PlayerDefinition must contain an Identity group.");
        }

        [Test]
        public void PlayerDefinition_HasPhysicalProfileField()
        {
            var t = FindType(TypePrefix + "PlayerDefinition");
            var f = t.GetField("Physical", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "PlayerDefinition must contain a PhysicalProfile group.");
        }

        [Test]
        public void PlayerDefinition_HasProfileField()
        {
            var t = FindType(TypePrefix + "PlayerDefinition");
            var f = t.GetField("Profile", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "PlayerDefinition must contain a Profile group.");
        }

        [Test]
        public void PlayerDefinition_HasPlayerStatsReferenceBoundary()
        {
            var t = FindType(TypePrefix + "PlayerDefinition");
            var f = t.GetField("PlayerStats", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "PlayerDefinition must contain a PlayerStats boundary (the unified stat container).");
            Assert.AreEqual(TypePrefix + "PlayerStats", f.FieldType.FullName,
                "'PlayerStats' field must reference the Football.Data.PlayerStats type.");
        }

        // ---- 66.2 Identity ----

        [Test]
        public void IdentityBoundary_HasRequiredFields()
        {
            var t = FindType(TypePrefix + "Identity");
            AssertField(t, "PlayerId");
            AssertField(t, "Name");
            AssertField(t, "Nationality");
            AssertField(t, "ClubReference");
        }

        [Test]
        public void Identity_PlayerId_IsExplicitStableAuthoredValue()
        {
            var t = FindType(TypePrefix + "Identity");
            var f = t.GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "PlayerId must be represented explicitly (not an array index).");
            Assert.That(
                f.FieldType == typeof(string) ||
                f.FieldType.IsEnum ||
                f.FieldType.IsValueType,
                "PlayerId must be a stable typed identity, not a mutable runtime-generated random value.");
        }

        [Test]
        public void Identity_DoesNotContainProfileOrPhysicalData()
        {
            var t = FindType(TypePrefix + "Identity");
            var names = t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToArray();
            foreach (var forbidden in new[] { "Age", "HeightCm", "WeightKg", "PrimaryPosition", "SkillRating",
                "OverallRating", "Pace", "Shooting", "Passing", "Goalkeeping" })
            {
                Assert.IsFalse(names.Contains(forbidden),
                    $"Identity must NOT contain '{forbidden}' — identity is not profile/physical/stat data.");
            }
        }

        [Test]
        public void NationalityBoundaryExists()
        {
            var t = FindType(TypePrefix + "Identity");
            AssertField(t, "Nationality", "Nationality must be explicitly represented (not omitted).");
        }

        [Test]
        public void ClubReferenceBoundaryExists_AsReferenceNotEmbeddedTeam()
        {
            var t = FindType(TypePrefix + "Identity");
            var f = t.GetField("ClubReference", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "ClubReference boundary must exist.");
            Assert.AreEqual(TypePrefix + "TeamDefinition", f.FieldType.FullName,
                "ClubReference must be a reference to the authored TeamDefinition (not an embedded mutable copy, not a runtime object).");
        }

        // ---- 66.3 PhysicalProfile ----

        [Test]
        public void PhysicalProfileBoundary_HasAgeHeightWeight()
        {
            var t = FindType(TypePrefix + "PhysicalProfile");
            AssertField(t, "Age");
            AssertField(t, "HeightCm");
            AssertField(t, "WeightKg");
        }

        [Test]
        public void PhysicalProfile_DoesNotContainRatings()
        {
            var t = FindType(TypePrefix + "PhysicalProfile");
            var names = t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToArray();
            foreach (var forbidden in new[] { "Pace", "Shooting", "Passing", "Dribbling",
                "OverallRating", "SkillRating" })
            {
                Assert.IsFalse(names.Contains(forbidden),
                    $"PhysicalProfile must NOT contain '{forbidden}' — physical profile is real measurements, not ratings.");
            }
        }

        // ---- 66.3 Profile ----

        [Test]
        public void ProfileBoundary_HasRequiredFields()
        {
            var t = FindType(TypePrefix + "Profile");
            AssertField(t, "PrimaryPosition");
            AssertField(t, "SecondaryPositions");
            AssertField(t, "PreferredFoot");
            AssertField(t, "WeakFoot");
            AssertField(t, "SkillRating");
            AssertField(t, "OverallRating");
            AssertField(t, "CardType");
            AssertField(t, "PlayStyle");
        }

        [Test]
        public void GoalkeeperRule_PrimaryPositionGK_SelectsGoalkeeper()
        {
            // The GK enum value exists and is the signal — goalkeeper status is determined by
            // PrimaryPosition == GK, NOT by the presence of Goalkeeping stats or a bool flag.
            var posType = FindType(TypePrefix + "PlayerPosition");
            Assert.IsTrue(Enum.IsDefined(posType, "GK"),
                "PlayerPosition must have GK so PrimaryPosition == GK is the goalkeeper signal.");

            var profile = FindType(TypePrefix + "Profile");
            var f = profile.GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f.DeclaringType, "PrimaryPosition must be a real field.");
            Assert.AreEqual(posType, f.FieldType,
                "PrimaryPosition must be strongly typed (PlayerPosition), not a string or bool.");
        }

        [Test]
        public void UnifiedModel_NoSeparateGoalkeeperDefinition()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var tam in new[] { "GoalkeeperDefinition", "OutfieldPlayerDefinition",
                "AttackerDefinition", "DefenderDefinition", "GKDefinition" })
            {
                var hit = assemblies.SelectMany(SafeGetTypes)
                    .Any(t => t.Name == tam && t.Namespace != null && t.Namespace.StartsWith("Football"));
                Assert.IsFalse(hit, $"No separate '{tam}' type is allowed — the unified PlayerDefinition must be used.");
            }
        }

        [Test]
        public void UnifiedModel_NoIsGoalkeeperBoolFlag()
        {
            // Goalkeeper status must be derived from PrimaryPosition, not a mutable bool flag.
            var t = FindType(TypePrefix + "PlayerDefinition");
            foreach (var grp in new[] { t, FindType(TypePrefix + "Identity"),
                FindType(TypePrefix + "Profile"), FindType(TypePrefix + "PlayerStats") })
            {
                Assert.IsNull(grp.GetField("IsGoalkeeper", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    "Goalkeeper status must NOT be a bool flag — it is derived from PrimaryPosition == GK.");
            }
        }

        [Test]
        public void PlayerStats_IsPartOfUnifiedModel_ForEveryPlayer()
        {
            // Every player holds the unified PlayerStats container (incl. Goalkeeping data for
            // outfield players). No Goalkeeping-only split model exists.
            var ps = FindType(TypePrefix + "PlayerStats");
            Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(ps) && ps != typeof(ScriptableObject),
                "PlayerStats must be a value/data container within the unified model, not a separate ScriptableObject system.");
            Assert.AreEqual(TypePrefix + "PlayerStats", ps.FullName);
            Assert.IsTrue(ps.IsClass && !ps.IsAbstract && ps.IsSerializable,
                "PlayerStats must be a serializable container class held per player.");
        }

        // ---- 66.3 / 66.5 Strongly-typed enums ----

        [Test]
        public void Profile_IsStronglyTypedNonString()
        {
            var t = FindType(TypePrefix + "Profile");
            foreach (var name in new[] { "PrimaryPosition", "PreferredFoot", "CardType", "PlayStyle" })
            {
                var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"'{name}' must exist.");
                Assert.AreNotEqual(typeof(string), f.FieldType,
                    $"'{name}' must be strongly typed (enum), not a raw string.");
            }
        }

        // ---- 66.4 No runtime state anywhere in the model ----

        [Test]
        public void NoRuntimeStateFieldsInModel()
        {
            var modelTypes = new[]
            {
                TypePrefix + "PlayerDefinition", TypePrefix + "Identity",
                TypePrefix + "PhysicalProfile", TypePrefix + "Profile", TypePrefix + "PlayerStats"
            };
            foreach (var full in modelTypes)
            {
                var t = FindType(full);
                foreach (var name in new[]
                {
                    "CurrentPosition", "CurrentVelocity", "CurrentStamina", "CurrentState",
                    "CurrentPossession", "CurrentInput", "CurrentAnimationState", "CurrentAIState",
                    "Position", "Velocity", "Health", "Score"
                })
                {
                    Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"'{full}' must NOT own runtime state '{name}'.");
                    Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"'{full}' must NOT own runtime state '{name}'.");
                }
            }
        }

        [Test]
        public void ScriptableObjectPlayerDefinition_DoesNotOwnRuntimeState()
        {
            // PlayerDefinition (ScriptableObject) must hold only auth-group fields, never mutable
            // runtime state value fields.
            var t = FindType(TypePrefix + "PlayerDefinition");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsTrue(f.FieldType.IsClass || f.FieldType.IsInterface,
                    $"PlayerDefinition field '{f.Name}' should be a group reference, not a runtime value field.");
            }
        }

        // ---- 66.5 Extensibility / clear grouping ----

        [Test]
        public void ModelGroupsIdentity_PhysProfile_Profile_Stats_AreDistinct()
        {
            var identity = FindType(TypePrefix + "Identity");
            var phys = FindType(TypePrefix + "PhysicalProfile");
            var profile = FindType(TypePrefix + "Profile");
            var stats = FindType(TypePrefix + "PlayerStats");

            Assert.IsNotNull(identity);
            Assert.IsNotNull(phys);
            Assert.IsNotNull(profile);
            Assert.IsNotNull(stats);

            // Identity, Physical, Profile, Stats must be separate serializable types (clear
            // grouping, extensible without a god-object), all under Football.Data.
            foreach (var t in new[] { identity, phys, profile, stats })
            {
                Assert.IsTrue(t.IsSerializable, $"{t.FullName} must be serializable authored data.");
                Assert.AreEqual("Football.Data", t.Namespace);
            }
        }

        // ---- helpers ----

        private static void AssertField(Type t, string name, string message = null)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, message ?? $"'{t.Name}' must define '{name}'.");
        }
    }
}
