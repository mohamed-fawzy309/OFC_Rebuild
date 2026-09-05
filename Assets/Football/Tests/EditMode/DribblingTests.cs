using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 71 — Dribbling category (Dribbling, BallControl, TightPossession, Agility, Balance,
    /// Reactions).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Dribbling was introduced with the full approved PlayerStats implementation in Task 67 as the
    /// <c>DribblingStats</c> category. Task 71 confirms it: exactly the six approved player ratings,
    /// unified PlayerStats rating type/range/default (int [1..99], default 50), TightPossession
    /// belongs to Dribbling, no unapproved attributes, no runtime state, no gameplay/physics/AI/
    /// animation logic, no duplication (not on PlayerDefinition, not into BallConfig/MovementConfig/
    /// DribbleConfig as a player rating), Goalkeepers retain Dribbling (unified model), and no
    /// dribbling gameplay systems created.
    /// </summary>
    public class DribblingTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ApprovedAttributes = new[]
        {
            "Dribbling", "BallControl", "TightPossession", "Agility", "Balance", "Reactions"
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

        private static Type DribblingType() => FindType(Prefix + "DribblingStats");

        // ---- Category structure ----

        [Test]
        public void DribblingCategory_Exists_AsSerializableData()
        {
            var t = DribblingType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Dribbling must be a [Serializable] data category, not a MonoBehaviour/ScriptableObject system.");
        }

        [Test]
        public void Dribbling_Contains_ExactlyTheSixApprovedAttributes()
        {
            var names = DribblingType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(ApprovedAttributes.OrderBy(x => x).ToArray(), names,
                "Dribbling must contain exactly the six approved attributes, nothing more/less.");
        }

        [Test]
        public void Dribbling_ContainsNoUnapprovedAttributes()
        {
            foreach (var name in new[] { "FirstTouch", "Technique", "DribbleSpeed", "Curve",
                "Composure", "Acceleration", "TurnSpeed", "DribbleStickiness" })
            {
                Assert.IsNull(DribblingType().GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"'{name}' is NOT an approved Dribbling attribute.");
            }
        }

        // ---- TightPossession placement ----

        [Test]
        public void TightPossession_BelongsToDribbling_NotPhysicalPassingOrConfig()
        {
            Assert.IsNotNull(DribblingType().GetField("TightPossession", BindingFlags.Public | BindingFlags.Instance),
                "TightPossession must be in Dribbling.");
            foreach (var other in new[] { "PhysicalStats", "PassingStats", "MovementConfig", "BallConfig", "DribbleConfig" })
            {
                Assert.IsNull(FindType(Prefix + other).GetField("TightPossession", BindingFlags.Public | BindingFlags.Instance),
                    $"TightPossession must NOT be in {other} — it belongs to Dribbling.");
            }
        }

        // ---- Rating representation (int 1-99, default 50) ----

        [Test]
        public void AllSixAttributes_UsePlayerStatsRatingRepresentation()
        {
            var stats = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)stats.GetField("RatingMin").GetRawConstantValue());
            Assert.AreEqual(99, (int)stats.GetField("RatingMax").GetRawConstantValue());
            Assert.AreEqual(50, (int)stats.GetField("RatingDefault").GetRawConstantValue());

            var obj = Activator.CreateInstance(DribblingType());
            foreach (var name in ApprovedAttributes)
            {
                var f = DribblingType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"'{name}' must exist.");
                Assert.AreEqual(typeof(int), f.FieldType, $"'{name}' must be int (rating), not float.");
                Assert.AreEqual(50, (int)f.GetValue(obj), $"'{name}' must default to PlayerStats.RatingDefault.");
            }
        }

        [Test]
        public void DribblingAttrAreNotFloatWorldUnits()
        {
            foreach (var f in DribblingType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(typeof(int), f.FieldType,
                    $"Dribbling attribute '{f.Name}' must be an int rating, not a float m/s/angle/force value.");
            }
        }

        // ---- Runtime / gameplay / physics / AI / animation separation ----

        [Test]
        public void Dribbling_ContainsNoRuntimeState()
        {
            var t = DribblingType();
            foreach (var name in new[] { "CurrentDribbleState", "CurrentBallControl", "CurrentPossession",
                "CurrentTurnRate", "CurrentDirection", "CurrentReactionTime", "IsDribbling",
                "CurrentBallVelocity", "IsGrounded", "CurrentStamina" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Dribbling must NOT hold runtime state '{name}'.");
            }
        }

        [Test]
        public void Dribbling_DoesNotReferenceUnityRuntimeObjects()
        {
            var t = DribblingType();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(Transform), f.FieldType);
                Assert.AreNotEqual(typeof(Rigidbody), f.FieldType);
                Assert.AreNotEqual(typeof(GameObject), f.FieldType);
                Assert.AreNotEqual(typeof(MonoBehaviour), f.FieldType);
                Assert.AreNotEqual(typeof(Animator), f.FieldType);
                Assert.AreNotEqual(typeof(CharacterController), f.FieldType);
            }
        }

        [Test]
        public void Dribbling_ContainsNoGameplayLogic()
        {
            var t = DribblingType();
            foreach (var name in new[] { "CalculateDribbleSpeed", "CalculateTurnRate", "CalculateControlRadius",
                "CalculateStickiness", "CalculateReactionTime", "CalculateFirstTouchResult", "ApplyForce" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Dribbling must NOT derive gameplay via '{name}'.");
                Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Dribbling must NOT expose gameplay via '{name}'.");
            }
        }

        [Test]
        public void Dribbling_DoesNotCalculateOverallRating()
        {
            var t = DribblingType();
            Assert.IsNull(t.GetMethod("CalculateOverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetField("OverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        // ---- No duplication / system separation ----

        [Test]
        public void DribblingAttributes_AreNotDuplicated_AtPlayerDefinitionLevel()
        {
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in ApprovedAttributes.Concat(new[] { "Dribbling" }))
            {
                Assert.IsNull(pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{name}' must live ONLY under PlayerStats.Dribbling, not on PlayerDefinition.");
            }
        }

        [Test]
        public void DribblingRatings_NotDuplicated_InSystemConfigs()
        {
            // BallConfig / MovementConfig / DribbleConfig are SYSTEM tuning (world units), NOT the
            // authoritative player-rating source. None of the six player ratings may be stored there.
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
        public void NoDribblingGameplaySystems_WereCreated()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "DribblingSystem", "DribbleController", "DribbleStateMachine",
                "DribbleAnimator", "DribbleAnimationController", "ReactionSystem", "DribblingValidator" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"Dribbling task must NOT create '{name}'.");
            }
        }

        // ---- Goalkeeper unification / position independence ----

        [Test]
        public void Goalkeepers_RetainDribblingData()
        {
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Dribbling", BindingFlags.Public | BindingFlags.Instance),
                "Every player (including GK) must retain the Dribbling category via unified PlayerStats.");
            Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .Any(t => t.Name == "GoalkeeperPlayerStats" || t.Name == "GoalkeeperDribbling"),
                "No separate goalkeeper stats/split model may exist.");
        }

        [Test]
        public void Dribbling_IsIndependentOfPrimaryPosition()
        {
            var names = DribblingType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            Assert.IsFalse(names.Contains("PrimaryPosition"), "Dribbling must not be driven by PrimaryPosition.");
        }
    }
}
