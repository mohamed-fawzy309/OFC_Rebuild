using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 76 — Physical Profile (PlayerDefinition.PhysicalProfile = Age, HeightCm, WeightKg).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// PhysicalProfile was introduced in Task 66 as the player's biophysical/biographical group.
    /// Task 76 confirms and locks its semantics: exactly Age / HeightCm / WeightKg using REAL-WORLD
    /// units (years / centimetres / kilograms) — NOT PlayerStats 1-99 ratings, and strictly separate
    /// from PlayerStats.Physical (Strength/Stamina/Jumping/Aggression). It verifies no duplication
    /// (Identity/Profile/PlayerStats/configs), no runtime state (no CurrentAge/CurrentHeight/
    /// CurrentWeight), no gameplay/progression/physics/UI, goalkeepers use the same unified
    /// PhysicalProfile, and units are consistent (cm/kg encoded in field names, not mixed).
    ///
    /// Validation policy: basic conceptual bounds (realistic age, positive plausible height/weight)
    /// are recorded as POLICY/DEFERRED — Task 76 does NOT hardcode extreme ranges or a validation
    /// engine (broader attribute validation belongs to a later task, Task 78).
    /// </summary>
    public class PhysicalProfileTests
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

        private static Type PhysProfileType() => FindType(Prefix + "PhysicalProfile");

        // ---- Category structure / exact fields ----

        [Test]
        public void PhysicalProfile_Exists_AsSerializableData()
        {
            var t = PhysProfileType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "PhysicalProfile must be a [Serializable] data group inside PlayerDefinition, not a MonoBehaviour/ScriptableObject system.");
        }

        [Test]
        public void PhysicalProfile_Contains_ExactlyAgeHeightWeight()
        {
            var names = PhysProfileType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            // Implemented as Age / HeightCm / WeightKg (unit-encoded names). Exactly three fields.
            Assert.AreEqual(3, names.Length, "PhysicalProfile must contain exactly Age, HeightCm, WeightKg.");
            foreach (var expected in new[] { "Age", "HeightCm", "WeightKg" })
            {
                Assert.IsTrue(names.Contains(expected), $"PhysicalProfile must contain '{expected}'.");
            }
        }

        // ---- 76.1 Age ----

        [Test]
        public void Age_Exists_AsChronologicalInteger()
        {
            var f = PhysProfileType().GetField("Age", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "PhysicalProfile must have Age.");
            Assert.AreEqual(typeof(int), f.FieldType,
                "Age must be a plain integer (chronological years), not a float rating.");
        }

        [Test]
        public void Age_IsNotAPlayerRating()
        {
            // Age does not use the PlayerStats 1-99 scale and is not a 0.0f-99.0f rating.
            Assert.AreEqual(typeof(int), PhysProfileType().GetField("Age", BindingFlags.Public | BindingFlags.Instance).FieldType);
            Assert.IsNull(PhysProfileType().GetField("AgeRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Age must NOT be represented as AgeRating.");
            Assert.IsNull(PhysProfileType().GetField("ExperienceRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Age must NOT be an experience/stamina/physical rating.");
        }

        // ---- 76.2 Height ----

        [Test]
        public void Height_Exists_AsRealWorldMeasurement()
        {
            var f = PhysProfileType().GetField("HeightCm", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "PhysicalProfile must have HeightCm (height in centimetres).");
            Assert.AreEqual(typeof(float), f.FieldType,
                "Height must be a float (real-world measurement), not an int rating.");
        }

        [Test]
        public void Height_IsNotAPlayerRating()
        {
            Assert.IsNull(PhysProfileType().GetField("HeightRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Height must NOT be represented as HeightRating.");
        }

        // ---- 76.3 Weight ----

        [Test]
        public void Weight_Exists_AsRealWorldMeasurement()
        {
            var f = PhysProfileType().GetField("WeightKg", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "PhysicalProfile must have WeightKg (weight in kilograms).");
            Assert.AreEqual(typeof(float), f.FieldType,
                "Weight must be a float (real-world measurement), not an int rating.");
        }

        [Test]
        public void Weight_IsNotAPlayerRating()
        {
            Assert.IsNull(PhysProfileType().GetField("WeightRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Weight must NOT be represented as WeightRating.");
        }

        [Test]
        public void AgeHeightWeight_DoNotUse_PlayerStatsRatingScale()
        {
            // These physical fields must not be forced into the PlayerStats 1-99 rating policy.
            var ps = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)ps.GetField("RatingMin").GetRawConstantValue());
            Assert.AreEqual(99, (int)ps.GetField("RatingMax").GetRawConstantValue());
            Assert.AreEqual(50, (int)ps.GetField("RatingDefault").GetRawConstantValue());
            // None of Age/HeightCm/WeightKg has a default of 50 (they are not ratings).
            var obj = Activator.CreateInstance(PhysProfileType());
            foreach (var name in new[] { "Age", "HeightCm", "WeightKg" })
            {
                var val = (float)Convert.ChangeType(
                    PhysProfileType().GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(obj),
                    typeof(float));
                Assert.AreNotEqual(50f, val,
                    $"'{name}' must NOT default to PlayerStats.RatingDefault (50) — it is not a 1-99 rating.");
            }
        }

        // ---- Unit consistency ----

        [Test]
        public void Units_AreExplicitAndConsistent()
        {
            // Height in centimetres (cm), Weight in kilograms (kg), Age in years. Field names carry
            // units so no mixing occurs (no bare indistinct Height/Weight double).
            Assert.IsNotNull(PhysProfileType().GetField("HeightCm", BindingFlags.Public | BindingFlags.Instance),
                "Height must use centimetres (HeightCm) consistently.");
            Assert.IsNotNull(PhysProfileType().GetField("WeightKg", BindingFlags.Public | BindingFlags.Instance),
                "Weight must use kilograms (WeightKg) consistently.");
            // No alternate bare unit fields (avoids cm/m or kg/g mixing).
            Assert.IsNull(PhysProfileType().GetField("Height", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "No bare 'Height' field may mix units — HeightCm is authoritative.");
            Assert.IsNull(PhysProfileType().GetField("Weight", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "No bare 'Weight' field may mix units — WeightKg is authoritative.");
        }

        // ---- Separate from PlayerStats.Physical ----

        [Test]
        public void PhysicalProfile_DoesNotContain_Ratings()
        {
            // Physical ratings (Strength/Stamina/Jumping/Aggression) belong to PlayerStats.Physical,
            // NOT PhysicalProfile.
            foreach (var name in new[] { "Strength", "Stamina", "Jumping", "Aggression" })
            {
                Assert.IsNull(PhysProfileType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PhysicalProfile must NOT contain the rating '{name}'.");
            }
        }

        [Test]
        public void PhysicalProfile_IsSeparateFrom_PlayerStatsPhysical()
        {
            // Two distinct concepts: PhysicalProfile = what the player IS physically (biographical);
            // PlayerStats.Physical = how the player is RATED physically (Strength/Stamina/Jumping/
            // Aggression). They are different types with no shared fields.
            var physProfile = PhysProfileType();
            var physStats = FindType(Prefix + "PhysicalStats");
            Assert.AreNotEqual(physProfile.FullName, physStats.FullName,
                "PhysicalProfile and PhysicalStats must be distinct data types.");
            foreach (var profileField in physProfile.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsNull(physStats.GetField(profileField.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{profileField.Name}' must not be duplicated into PhysicalStats.");
            }
        }

        // ---- No duplication ----

        [Test]
        public void AgeHeightWeight_AreNotDuplicated_AtPlayerDefinitionTopLevel()
        {
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in new[] { "Age", "HeightCm", "WeightKg", "Height", "Weight" })
            {
                Assert.IsNull(pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{name}' must live ONLY under PhysicalProfile, not on PlayerDefinition top level.");
            }
        }

        [Test]
        public void AgeHeightWeight_AreNotDuplicated_InIdentity_Profile_OrPlayerStats()
        {
            foreach (var owner in new[] { "Identity", "Profile", "PlayerStats" })
            {
                var t = FindType(Prefix + owner);
                foreach (var name in new[] { "Age", "HeightCm", "WeightKg", "Height", "Weight" })
                {
                    Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"'{owner}' must NOT duplicate '{name}' — it belongs to PhysicalProfile.");
                }
            }
        }

        [Test]
        public void AgeHeightWeight_AreNotDuplicated_InSystemConfigs()
        {
            foreach (var cfg in new[] { "MovementConfig", "BallConfig", "DribbleConfig", "AnimationConfig" })
            {
                var t = FindType(Prefix + cfg);
                foreach (var name in new[] { "PlayerAge", "PlayerHeight", "PlayerWeight", "HeightCm", "WeightKg" })
                {
                    Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"'{cfg}' must NOT carry the player's '{name}'.");
                }
            }
        }

        // ---- No runtime state / no gameplay / no physics / no progression ----

        [Test]
        public void PhysicalProfile_ContainsNoRuntimeState()
        {
            var t = PhysProfileType();
            foreach (var name in new[] { "CurrentAge", "CurrentHeight", "CurrentWeight", "CurrentBodyState",
                "CurrentFatigue", "BirthDate", "ProgressionState" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PhysicalProfile must NOT hold runtime state '{name}'.");
            }
        }

        [Test]
        public void PhysicalProfile_DoesNotReferenceUnityRuntimeObjects()
        {
            foreach (var f in PhysProfileType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(Transform), f.FieldType);
                Assert.AreNotEqual(typeof(Rigidbody), f.FieldType);
                Assert.AreNotEqual(typeof(Collider), f.FieldType);
                Assert.AreNotEqual(typeof(GameObject), f.FieldType);
                Assert.AreNotEqual(typeof(MonoBehaviour), f.FieldType);
            }
        }

        [Test]
        public void PhysicalProfile_ContainsNoGameplayLogic()
        {
            var t = PhysProfileType();
            foreach (var name in new[] { "SetAge", "SetHeight", "SetWeight", "Grow", "AgeUp",
                "DepleteStamina", "GetHeadingBonusFromHeight", "GetJumpPowerFromHeight",
                "GetStrengthFromWeight", "GetAccelerationFromWeight", "CalculateBodyMassIndex" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PhysicalProfile must NOT implement gameplay via '{name}'.");
            }
        }

        [Test]
        public void PhysicalProfile_DoesNotCreatePhysicsSystem()
        {
            // Height/Weight are data; no Rigidbody mass from Weight, no Collider from Height, no
            // physics scaling, no PlayerPhysicsConfig.
            Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.Name == "PlayerPhysicsConfig"),
                "No PlayerPhysicsConfig may be created for Task 76.");
        }

        [Test]
        public void PhysicalProfile_DoesNotCreateProgressionSystem()
        {
            foreach (var n in new[] { "CareerSystem", "AgingSystem", "PlayerGrowthSystem",
                "AttributeGrowthSystem", "ProgressionSystem", "BirthDate" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.Name == n),
                    $"No '{n}' may be created for Task 76.");
            }
        }

        [Test]
        public void PhysicalProfile_DoesNotCreateUI()
        {
            foreach (var n in new[] { "PlayerBioUI", "PlayerHeightUI", "PlayerWeightUI",
                "PlayerAgeUI", "PlayerCardUI" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.Name == n),
                    $"No '{n}' UI may be created for Task 76.");
            }
        }

        // ---- Goalkeeper unified rule ----

        [Test]
        public void Goalkeepers_UseSame_PhysicalProfile()
        {
            // No separate GoalkeeperPhysicalProfile model; every player (GK and outfield) uses the
            // single PhysicalProfile (Age/HeightCm/WeightKg).
            Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.Name == "GoalkeeperPhysicalProfile"),
                "No GoalkeeperPhysicalProfile split model may exist.");
            Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .Any(t => t.Name == "GoalkeeperPhysicalProfile"),
                "Every player must use the unified PhysicalProfile.");
        }

        // ---- Position independence / data only ----

        [Test]
        public void PhysicalProfile_IsIndependentOfPosition()
        {
            var names = PhysProfileType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            Assert.IsFalse(names.Contains("PrimaryPosition"),
                "PhysicalProfile must not be driven by PrimaryPosition.");
        }

        [Test]
        public void PhysicalProfile_DoesNotCreateTeamSystem()
        {
            // ClubReference remains owned by Identity; no Team/Squad/Club system here.
            foreach (var n in new[] { "TeamDefinition", "Squad", "ClubSystem" })
            {
                Assert.IsNull(PhysProfileType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PhysicalProfile must NOT implement '{n}'.");
            }
        }

        [Test]
        public void PhysicalProfile_IsDataOnlySerializableGroup()
        {
            // Prefer keeping PhysicalProfile as part of PlayerDefinition (existing pattern), NOT a
            // separate ScriptableObject asset.
            var t = PhysProfileType();
            Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(t) && t != typeof(ScriptableObject),
                "PhysicalProfile must NOT be its own ScriptableObject — it is a serializable group inside PlayerDefinition.");
        }
    }
}
