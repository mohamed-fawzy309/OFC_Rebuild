using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 69 — Shooting category (AttackingAwareness, Finishing, ShotPower, LongShots, Volleys,
    /// Penalties).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Shooting was introduced with the full approved PlayerStats implementation in Task 67 as the
    /// <c>ShootingStats</c> category. Task 69 confirms it: exactly the six approved player ratings,
    /// unified PlayerStats rating type/range/default, no unapproved attributes (Curve /
    /// FreeKickAccuracy are NOT here), no runtime state, no gameplay/physics logic or shot formulas,
    /// no duplication (not on PlayerDefinition, not into BallConfig/ShootConfig as a player rating),
    /// Goalkeepers retain Shooting (unified model), and no Shooting-style gameplay systems created.
    /// </summary>
    public class ShootingTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ApprovedAttributes = new[]
        {
            "AttackingAwareness", "Finishing", "ShotPower", "LongShots", "Volleys", "Penalties"
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

        private static Type ShootingType()
        {
            return FindType(Prefix + "ShootingStats");
        }

        // ---- Category structure ----

        [Test]
        public void ShootingCategory_Exists_AsSerializableData()
        {
            var t = ShootingType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Shooting must be a [Serializable] data category, not a MonoBehaviour/ScriptableObject system.");
        }

        [Test]
        public void Shooting_Contains_ExactlyTheSixApprovedAttributes()
        {
            var names = ShootingType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(ApprovedAttributes.OrderBy(x => x).ToArray(), names,
                "Shooting must contain exactly the six approved attributes, nothing more/less.");
        }

        [Test]
        public void Shooting_HasNoUnapprovedAttributes()
        {
            foreach (var name in new[] { "Curve", "ShotAccuracy", "ShotPlacement", "Composure",
                "HeadingAccuracy", "FreeKickAccuracy" })
            {
                Assert.IsNull(ShootingType().GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"'{name}' is NOT an approved Shooting attribute (Curve/FreeKickAccuracy belong to Passing).");
            }
        }

        // ---- Rating representation (unified) ----

        [Test]
        public void AllSixAttributes_UsePlayerStatsRatingRepresentation()
        {
            var stats = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)stats.GetField("RatingMin").GetRawConstantValue());
            Assert.AreEqual(99, (int)stats.GetField("RatingMax").GetRawConstantValue());
            Assert.AreEqual(50, (int)stats.GetField("RatingDefault").GetRawConstantValue());

            var obj = Activator.CreateInstance(ShootingType());
            foreach (var name in ApprovedAttributes)
            {
                var f = ShootingType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"'{name}' must exist.");
                Assert.AreEqual(typeof(int), f.FieldType, $"'{name}' must be int (rating), not float.");
                Assert.AreEqual(50, (int)f.GetValue(obj), $"'{name}' must default to PlayerStats.RatingDefault.");
            }
        }

        [Test]
        public void ShootingAttrAreNotFloatWorldUnits()
        {
            foreach (var f in ShootingType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(typeof(int), f.FieldType,
                    $"Shooting attribute '{f.Name}' must be an int rating, not a float force/world value.");
            }
        }

        // ---- Runtime / gameplay separation ----

        [Test]
        public void Shooting_ContainsNoRuntimeState()
        {
            var t = ShootingType();
            foreach (var name in new[] { "CurrentShotPower", "CurrentBallVelocity", "CurrentKickForce",
                "Velocity", "Position", "IsShooting", "CurrentStamina" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Shooting must NOT hold runtime state '{name}'.");
            }
        }

        [Test]
        public void Shooting_DoesNotReferenceUnityRuntimeObjects()
        {
            var t = ShootingType();
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
        public void Shooting_ContainsNoGameplayMethods()
        {
            var t = ShootingType();
            foreach (var name in new[] { "CalculateShotPower", "CalculateShotAccuracy", "CalculateGoalProbability",
                "GetShotPhysics", "CalculateFinishing", "ApplyForce" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Shooting must NOT derive gameplay via '{name}'.");
                Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Shooting must NOT expose gameplay via '{name}'.");
            }
        }

        // ---- No duplication / system separation ----

        [Test]
        public void ShootingAttributes_AreNotDuplicated_AtPlayerDefinitionLevel()
        {
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in ApprovedAttributes.Concat(new[] { "Shooting" }))
            {
                Assert.IsNull(pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{name}' must live ONLY under PlayerStats.Shooting, not on PlayerDefinition.");
            }
        }

        [Test]
        public void ShootingRatings_NotDuplicated_AsPlayerRatings_InBallOrShootConfig()
        {
            // BallConfig is SYSTEM tuning (KickForce/PassForce/etc.); no ShootingConfig exists yet.
            // Neither may be the authoritative player-rating source. No "Finishing"/"ShotPower"
            // authored rating field is stored on BallConfig.
            var ball = FindType(Prefix + "BallConfig");
            foreach (var name in ApprovedAttributes)
            {
                Assert.IsNull(ball.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"BallConfig must NOT carry the player rating '{name}'.");
            }
            Assert.IsNull(FindType(Prefix + "ShootingStats").GetField("ShootConfig",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        [Test]
        public void ShootingDoesNotCalculateOverallRating()
        {
            var t = ShootingType();
            Assert.IsNull(t.GetMethod("CalculateOverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetProperty("OverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetField("OverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        // ---- Goalkeeper unification / position independence ----

        [Test]
        public void Goalkeepers_RetainShootingData()
        {
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Shooting", BindingFlags.Public | BindingFlags.Instance),
                "Every player (including GK) must retain the Shooting category via unified PlayerStats.");
            Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .Any(t => t.Name == "GoalkeeperPlayerStats" || t.Name == "GoalkeeperShooting"),
                "No separate goalkeeper stats/split model may exist.");
        }

        [Test]
        public void Shooting_IsIndependentOfPrimaryPosition()
        {
            var names = ShootingType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            Assert.IsFalse(names.Contains("PrimaryPosition"), "Shooting must not be driven by PrimaryPosition.");
        }

        // ---- No gameplay systems created ----

        [Test]
        public void NoShootingGameplaySystems_WereCreated()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "ShootingSystem", "ShotSystem", "ShotController",
                "ShootingController", "PenaltyShotSystem", "PenaltyKickController", "VolleySystem",
                "FinishingSystem", "AttackingAwarenessSystem", "AIDecisionSystem" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"Shooting task must NOT create '{name}'.");
            }
        }
    }
}
