using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 72 — Defending category (DefensiveAwareness, Interceptions, StandingTackle,
    /// SlidingTackle, Heading).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Defending was introduced with the full approved PlayerStats implementation in Task 67 as the
    /// <c>DefendingStats</c> category. Task 72 confirms it: exactly the five approved player
    /// ratings, unified PlayerStats rating type/range/default (int [1..99], default 50), Heading
    /// belongs to Defending, no unapproved attributes, no runtime state, no gameplay/physics/AI/
    /// animation logic, no duplication (not on PlayerDefinition, not into BallConfig/MovementConfig/
    /// DribbleConfig as a player rating; no TackleConfig manufactured), Goalkeepers retain
    /// Defending (unified model), and no defending gameplay systems created.
    /// </summary>
    public class DefendingTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ApprovedAttributes = new[]
        {
            "DefensiveAwareness", "Interceptions", "StandingTackle", "SlidingTackle", "Heading"
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

        private static Type DefendingType() => FindType(Prefix + "DefendingStats");

        // ---- Category structure ----

        [Test]
        public void DefendingCategory_Exists_AsSerializableData()
        {
            var t = DefendingType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Defending must be a [Serializable] data category, not a MonoBehaviour/ScriptableObject system.");
        }

        [Test]
        public void Defending_Contains_ExactlyTheFiveApprovedAttributes()
        {
            var names = DefendingType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(ApprovedAttributes.OrderBy(x => x).ToArray(), names,
                "Defending must contain exactly the five approved attributes, nothing more/less.");
        }

        [Test]
        public void Defending_ContainsNoUnapprovedAttributes()
        {
            foreach (var name in new[] { "TackleTiming", "Positioning", "Marking", "SlideTackle",
                "Standing", "Composure", "Aerial", "TackleRange", "DefensiveReactionTime" })
            {
                Assert.IsNull(DefendingType().GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"'{name}' is NOT an approved Defending attribute.");
            }
        }

        // ---- Heading placement ----

        [Test]
        public void Heading_BelongsToDefending_NotShootingPhysicalOrDribbling()
        {
            Assert.IsNotNull(DefendingType().GetField("Heading", BindingFlags.Public | BindingFlags.Instance),
                "Heading must be in Defending.");
            foreach (var other in new[] { "ShootingStats", "PhysicalStats", "DribblingStats" })
            {
                Assert.IsNull(FindType(Prefix + other).GetField("Heading", BindingFlags.Public | BindingFlags.Instance),
                    $"Heading must NOT be in {other} — it belongs to Defending.");
            }
        }

        // ---- Rating representation (int 1-99, default 50) ----

        [Test]
        public void AllFiveAttributes_UsePlayerStatsRatingRepresentation()
        {
            var stats = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)stats.GetField("RatingMin").GetRawConstantValue());
            Assert.AreEqual(99, (int)stats.GetField("RatingMax").GetRawConstantValue());
            Assert.AreEqual(50, (int)stats.GetField("RatingDefault").GetRawConstantValue());

            var obj = Activator.CreateInstance(DefendingType());
            foreach (var name in ApprovedAttributes)
            {
                var f = DefendingType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"'{name}' must exist.");
                Assert.AreEqual(typeof(int), f.FieldType, $"'{name}' must be int (rating), not float.");
                Assert.AreEqual(50, (int)f.GetValue(obj), $"'{name}' must default to PlayerStats.RatingDefault.");
            }
        }

        [Test]
        public void DefendingAttrAreNotFloatWorldUnits()
        {
            foreach (var f in DefendingType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(typeof(int), f.FieldType,
                    $"Defending attribute '{f.Name}' must be an int rating, not a float m/s/force/angle value.");
            }
        }

        // ---- Runtime / gameplay / physics / AI / animation separation ----

        [Test]
        public void Defending_ContainsNoRuntimeState()
        {
            var t = DefendingType();
            foreach (var name in new[] { "CurrentDefensiveState", "CurrentTackle", "CurrentInterception",
                "CurrentHeadingState", "CurrentMarkingTarget", "CurrentDefensivePosition", "IsTackling",
                "IsMarking", "CurrentJumpHeight", "CurrentAerialState" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Defending must NOT hold runtime state '{name}'.");
            }
        }

        [Test]
        public void Defending_DoesNotReferenceUnityRuntimeObjects()
        {
            var t = DefendingType();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
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
        public void Defending_ContainsNoGameplayLogic()
        {
            var t = DefendingType();
            foreach (var name in new[] { "CalculateTackleSuccess", "CalculateInterceptionProbability",
                "CalculateHeadingForce", "CalculateTackleRange", "CalculateDefensiveReactionTime",
                "ExecuteTackle", "ApplyForce" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Defending must NOT derive gameplay via '{name}'.");
                Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Defending must NOT expose gameplay via '{name}'.");
            }
        }

        [Test]
        public void Defending_DoesNotCalculateOverallRating()
        {
            var t = DefendingType();
            Assert.IsNull(t.GetMethod("CalculateOverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetField("OverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        // ---- No duplication / system separation ----

        [Test]
        public void DefendingAttributes_AreNotDuplicated_AtPlayerDefinitionLevel()
        {
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in ApprovedAttributes.Concat(new[] { "Defending" }))
            {
                Assert.IsNull(pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{name}' must live ONLY under PlayerStats.Defending, not on PlayerDefinition.");
            }
        }

        [Test]
        public void DefendingRatings_NotDuplicated_InSystemConfigs()
        {
            // BallConfig / MovementConfig / DribbleConfig are SYSTEM tuning (world units), NOT the
            // authoritative player-rating source. None of the five player ratings may be stored there.
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
        public void NoTackleConfig_WasManufactured()
        {
            // Audited: no TackleConfig.cs exists. This task must NOT create one.
            Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.FullName == Prefix + "TackleConfig"),
                "TackleConfig must NOT be manufactured for Task 72.");
        }

        [Test]
        public void NoDefendingGameplaySystems_WereCreated()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "DefendingSystem", "DefensiveAI", "DefensivePositioningSystem",
                "MarkingSystem", "InterceptionSystem", "HeadingSystem", "TackleSystem", "TackleAnimator",
                "HeadingAnimator", "DefendingAnimationController", "AIDefenseSystem",
                "DefensiveDecisionSystem", "TargetSelectionSystem", "DefendingValidator" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"Defending task must NOT create '{name}'.");
            }
        }

        // ---- Goalkeeper unification / position independence ----

        [Test]
        public void Goalkeepers_RetainDefendingData()
        {
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Defending", BindingFlags.Public | BindingFlags.Instance),
                "Every player (including GK) must retain the Defending category via unified PlayerStats.");
            Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .Any(t => t.Name == "GoalkeeperPlayerStats" || t.Name == "GoalkeeperDefending"),
                "No separate goalkeeper stats/split model may exist.");
        }

        [Test]
        public void Defending_IsIndependentOfPrimaryPosition()
        {
            var names = DefendingType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            Assert.IsFalse(names.Contains("PrimaryPosition"), "Defending must not be driven by PrimaryPosition.");
        }
    }
}
