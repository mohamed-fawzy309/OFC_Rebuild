using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 70 — Passing category (Vision, ShortPassing, LongPassing, Crossing, FreeKickAccuracy,
    /// Curve) + the approved global rating-range correction (max 100 -> 99).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection.
    ///
    /// Passing was introduced with the approved PlayerStats model in Task 67 as the
    /// <c>PassingStats</c> category. Task 70 confirms it with the corrected global PlayerStats
    /// rating range [1..99], default 50. It verifies exactly the six approved attributes, the
    /// Curve-in-Passing rule, no unapproved attributes, no runtime state, no gameplay/physics/AI
    /// logic, no duplication, Goalkeepers retain Passing, and that no PassSystem/PassConfig was
    /// manufactured.
    /// </summary>
    public class PassingTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ApprovedAttributes = new[]
        {
            "Vision", "ShortPassing", "LongPassing", "Crossing", "FreeKickAccuracy", "Curve"
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

        private static Type PassingType() => FindType(Prefix + "PassingStats");

        // ---- Global range (approved correction: 100 -> 99) ----

        [Test]
        public void GlobalRatingRange_IsNow_1To99()
        {
            var stats = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)stats.GetField("RatingMin").GetRawConstantValue(), "RatingMin must be 1.");
            Assert.AreEqual(99, (int)stats.GetField("RatingMax").GetRawConstantValue(),
                "Approved change: RatingMax must be 99, NOT 100.");
            Assert.AreEqual(50, (int)stats.GetField("RatingDefault").GetRawConstantValue(), "RatingDefault must be 50.");
        }

        // ---- Category structure ----

        [Test]
        public void PassingCategory_Exists_AsSerializableData()
        {
            var t = PassingType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Passing must be a [Serializable] data category, not a MonoBehaviour/ScriptableObject system.");
        }

        [Test]
        public void Passing_Contains_ExactlyTheSixApprovedAttributes()
        {
            var names = PassingType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(ApprovedAttributes.OrderBy(x => x).ToArray(), names,
                "Passing must contain exactly the six approved attributes, nothing more/less.");
        }

        [Test]
        public void Passing_HasNoUnapprovedAttributes()
        {
            foreach (var name in new[] { "PassPower", "PassAccuracy", "Composure", "CurveAmount",
                "FirstTouch", "Technique" })
            {
                Assert.IsNull(PassingType().GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"'{name}' is NOT an approved Passing attribute.");
            }
        }

        // ---- Curve belongs to Passing ----

        [Test]
        public void Curve_BelongsToPassing_NotShootingOrDribbling()
        {
            Assert.IsNotNull(PassingType().GetField("Curve", BindingFlags.Public | BindingFlags.Instance),
                "Curve must be in Passing.");
            foreach (var other in new[] { "ShootingStats", "DribblingStats" })
            {
                Assert.IsNull(FindType(Prefix + other).GetField("Curve", BindingFlags.Public | BindingFlags.Instance),
                    $"Curve must NOT be in {other} — it belongs to Passing.");
            }
        }

        // ---- Rating representation (updated 1..99) ----

        [Test]
        public void AllSixAttributes_UsePlayerStatsRatingRepresentation()
        {
            var obj = Activator.CreateInstance(PassingType());
            foreach (var name in ApprovedAttributes)
            {
                var f = PassingType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"'{name}' must exist.");
                Assert.AreEqual(typeof(int), f.FieldType, $"'{name}' must be int (rating), not float.");
                Assert.AreEqual(50, (int)f.GetValue(obj), $"'{name}' must default to PlayerStats.RatingDefault (50).");
            }
        }

        [Test]
        public void PassingAttrAreNotFloatWorldUnits_NotMetersOrForce()
        {
            foreach (var f in PassingType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(typeof(int), f.FieldType,
                    $"Passing attribute '{f.Name}' must be an int rating, not a float world-unit value.");
            }
        }

        // ---- Runtime / gameplay / AI / physics separation ----

        [Test]
        public void Passing_ContainsNoRuntimeState()
        {
            var t = PassingType();
            foreach (var name in new[] { "CurrentPassPower", "CurrentBallVelocity", "IsPassing",
                "Velocity", "Position", "Target", "IsCrossing" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Passing must NOT hold runtime state '{name}'.");
            }
        }

        [Test]
        public void Passing_DoesNotReferenceUnityRuntimeObjects()
        {
            var t = PassingType();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(Transform), f.FieldType);
                Assert.AreNotEqual(typeof(Rigidbody), f.FieldType);
                Assert.AreNotEqual(typeof(GameObject), f.FieldType);
                Assert.AreNotEqual(typeof(MonoBehaviour), f.FieldType);
                Assert.AreNotEqual(typeof(Animator), f.FieldType);
            }
        }

        [Test]
        public void Passing_ContainsNoGameplayMethods()
        {
            var t = PassingType();
            foreach (var name in new[] { "CalculatePassForce", "CalculatePassAccuracy", "CalculateCurve",
                "GetTrajectory", "SelectTarget", "CalculatePassBehavior", "ChooseTarget" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Passing must NOT derive gameplay/AI via '{name}'.");
            }
        }

        // ---- No duplication / no manufactured config ----

        [Test]
        public void PassingAttributes_AreNotDuplicated_AtPlayerDefinitionLevel()
        {
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in ApprovedAttributes.Concat(new[] { "Passing" }))
            {
                Assert.IsNull(pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{name}' must live ONLY under PlayerStats.Passing, not on PlayerDefinition.");
            }
        }

        [Test]
        public void NoPassSystemOrPassConfig_WasManufactured()
        {
            // Passing is player data; no pass gameplay system and no PassConfig were to be created
            // for this task (audited: none exists in the project).
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "PassSystem", "PassController", "PassingSystem", "VisionSystem",
                "PassingAI", "TargetSelectionSystem" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"Passing task must NOT create '{name}'.");
            }
            Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.FullName == Prefix + "PassConfig"),
                "PassConfig must NOT be manufactured for Task 70.");
        }

        // ---- Goalkeeper unification / position independence ----

        [Test]
        public void Goalkeepers_RetainPassingData()
        {
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Passing", BindingFlags.Public | BindingFlags.Instance),
                "Every player (including GK) must retain the Passing category via unified PlayerStats.");
            Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .Any(t => t.Name == "GoalkeeperPlayerStats" || t.Name == "GoalkeeperPassing"),
                "No separate goalkeeper stats/split model may exist.");
        }

        [Test]
        public void Passing_IsIndependentOfPrimaryPosition()
        {
            var names = PassingType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            Assert.IsFalse(names.Contains("PrimaryPosition"), "Passing must not be driven by PrimaryPosition.");
        }
    }
}
