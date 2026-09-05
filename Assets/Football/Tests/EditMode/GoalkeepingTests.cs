using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 74 — Goalkeeping category (Diving, Handling, Kicking, Positioning, Reflexes, Parrying).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Goalkeeping was introduced with the full approved PlayerStats implementation in Task 67 as
    /// the <c>GoalkeepingStats</c> category. Task 74 confirms it: exactly the six approved player
    /// ratings, unified PlayerStats rating type/range/default (int [1..99], default 50), no
    /// unapproved attributes, no runtime state (no CurrentSave/CurrentDive/CurrentCatch/CurrentParry/
    /// CurrentGKPosition/CurrentReaction/CurrentBallControl), no gameplay logic (no save/catch/dive/
    /// distribution formulas), no physics/animation/AI, no duplication (not on PlayerDefinition
    /// top-level, not into any config), no separate GoalkeeperDefinition/GoalkeeperPlayerStats model,
    /// no GoalkeeperConfig/ShootConfig/PlayerConfig manufactured, no Goalkeeper System/Controller/AI
    /// created. The unified model stores Goalkeeping for EVERY player (outfield players retain it);
    /// PrimaryPosition == GK selects the primary profile but does not alter the underlying data.
    /// </summary>
    public class GoalkeepingTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ApprovedAttributes = new[]
        {
            "Diving", "Handling", "Kicking", "Positioning", "Reflexes", "Parrying"
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

        private static Type GoalkeepingType() => FindType(Prefix + "GoalkeepingStats");

        // ---- Category structure ----

        [Test]
        public void GoalkeepingCategory_Exists_AsSerializableData()
        {
            var t = GoalkeepingType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Goalkeeping must be a [Serializable] data category, not a MonoBehaviour/ScriptableObject system.");
        }

        [Test]
        public void Goalkeeping_Contains_ExactlyTheSixApprovedAttributes()
        {
            var names = GoalkeepingType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(ApprovedAttributes.OrderBy(x => x).ToArray(), names,
                "Goalkeeping must contain exactly the six approved attributes, nothing more/less.");
        }

        [Test]
        public void Goalkeeping_ContainsNoUnapprovedAttributes()
        {
            foreach (var name in new[] { "GKCommunication", "CrossClaiming", "OneOnOne", "RushOut",
                "Punching", "AerialAbility", "Composure", "Distribution", "Sweeping", "PenaltySaving" })
            {
                Assert.IsNull(GoalkeepingType().GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"'{name}' is NOT an approved Goalkeeping attribute.");
            }
        }

        // ---- Individual attribute existence ----

        [Test]
        public void Diving_Exists_AsAuthoredRating()
        {
            var f = GoalkeepingType().GetField("Diving", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Diving must exist in GoalkeepingStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Diving must be an int rating.");
        }

        [Test]
        public void Handling_Exists_AsAuthoredRating()
        {
            var f = GoalkeepingType().GetField("Handling", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Handling must exist in GoalkeepingStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Handling must be an int rating.");
        }

        [Test]
        public void Kicking_Exists_AsAuthoredRating()
        {
            var f = GoalkeepingType().GetField("Kicking", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Kicking must exist in GoalkeepingStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Kicking must be an int rating.");
        }

        [Test]
        public void Positioning_Exists_AsAuthoredRating()
        {
            var f = GoalkeepingType().GetField("Positioning", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Positioning must exist in GoalkeepingStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Positioning must be an int rating.");
        }

        [Test]
        public void Reflexes_Exists_AsAuthoredRating()
        {
            var f = GoalkeepingType().GetField("Reflexes", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Reflexes must exist in GoalkeepingStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Reflexes must be an int rating.");
        }

        [Test]
        public void Parrying_Exists_AsAuthoredRating()
        {
            var f = GoalkeepingType().GetField("Parrying", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Parrying must exist in GoalkeepingStats.");
            Assert.AreEqual(typeof(int), f.FieldType, "Parrying must be an int rating.");
        }

        // ---- Rating representation (int 1-99, default 50) ----

        [Test]
        public void AllSixAttributes_UsePlayerStatsRatingRepresentation()
        {
            var stats = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)stats.GetField("RatingMin").GetRawConstantValue());
            Assert.AreEqual(99, (int)stats.GetField("RatingMax").GetRawConstantValue());
            Assert.AreEqual(50, (int)stats.GetField("RatingDefault").GetRawConstantValue());

            var obj = Activator.CreateInstance(GoalkeepingType());
            foreach (var name in ApprovedAttributes)
            {
                var f = GoalkeepingType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"'{name}' must exist.");
                Assert.AreEqual(typeof(int), f.FieldType, $"'{name}' must be int (rating), not float.");
                Assert.AreEqual(50, (int)f.GetValue(obj), $"'{name}' must default to PlayerStats.RatingDefault.");
            }
        }

        [Test]
        public void GoalkeepingAttrAreNotFloatWorldUnits()
        {
            foreach (var f in GoalkeepingType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(typeof(int), f.FieldType,
                    $"Goalkeeping attribute '{f.Name}' must be an int rating, not a float m/s/seconds/Newton value.");
            }
        }

        // ---- Runtime / gameplay / AI / physics / animation separation ----

        [Test]
        public void Goalkeeping_ContainsNoRuntimeState()
        {
            var t = GoalkeepingType();
            foreach (var name in new[] { "CurrentSave", "CurrentDive", "CurrentCatch", "CurrentParry",
                "CurrentGKPosition", "CurrentReaction", "CurrentBallControl", "CurrentPossession",
                "SaveCount", "DiveDistance", "ReactionTime", "CatchState" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Goalkeeping must NOT hold runtime state '{name}'.");
                Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Goalkeeping must NOT expose runtime state '{name}'.");
            }
        }

        [Test]
        public void Goalkeeping_DoesNotReferenceUnityRuntimeObjects()
        {
            foreach (var f in GoalkeepingType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
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
        public void Goalkeeping_ContainsNoGameplayLogic()
        {
            var t = GoalkeepingType();
            foreach (var name in new[] { "CalculateSaveProbability", "CalculateDiveDistance",
                "CalculateReactionTime", "CalculateCatchProbability", "CalculateDeflectionAngle",
                "CalculateKickForce", "ExecuteSave", "ExecuteCatch", "ExecuteDive", "ApplyForce",
                "CalculateOverallRating" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Goalkeeping must NOT derive gameplay via '{name}'.");
                Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Goalkeeping must NOT expose gameplay via '{name}'.");
            }
        }

        [Test]
        public void Goalkeeping_DoesNotBecomeAI()
        {
            var t = GoalkeepingType();
            foreach (var name in new[] { "GoalkeeperAI", "GKDecisionSystem", "SaveDecisionSystem",
                "GoalkeeperBehaviorTree", "CurrentAIState", "DecisionSystem", "ReactionSystem",
                "GoalkeeperAISystem", "TargetSelection", "TacticalPositioning" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Goalkeeping must NOT hold AI state '{name}'.");
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Goalkeeping must NOT implement AI via '{name}'.");
            }
        }

        [Test]
        public void Goalkeeping_DoesNotBecomePhysics()
        {
            var t = GoalkeepingType();
            foreach (var name in new[] { "BallVelocity", "KickForce", "DiveForce", "DeflectionAngle",
                "Mass", "Impulse", "Physics" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Goalkeeping must NOT hold physics concept '{name}'.");
            }
        }

        [Test]
        public void Goalkeeping_DoesNotBecomeAnimation()
        {
            var t = GoalkeepingType();
            foreach (var name in new[] { "GoalkeeperAnimator", "SaveAnimator", "DiveAnimationController",
                "CurrentAnimation", "AnimationState" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Goalkeeping must NOT hold animation state '{name}'.");
            }
        }

        [Test]
        public void Goalkeeping_DoesNotCalculateOverallRating()
        {
            var t = GoalkeepingType();
            Assert.IsNull(t.GetMethod("CalculateOverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetField("OverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        // ---- No duplication / unified model ----

        [Test]
        public void GoalkeepingAttributes_AreNotDuplicated_AtPlayerDefinitionLevel()
        {
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in ApprovedAttributes)
            {
                Assert.IsNull(pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{name}' must live ONLY under PlayerStats.Goalkeeping, not on PlayerDefinition.");
            }
        }

        [Test]
        public void GoalkeepingRatings_NotDuplicated_InSystemConfigs()
        {
            foreach (var configName in new[] { "BallConfig", "MovementConfig", "DribbleConfig", "AnimationConfig" })
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
        public void NoGoalkeeperConfig_WasManufactured()
        {
            foreach (var name in new[] { "GoalkeeperConfig", "GKConfig", "ShootConfig", "PlayerConfig" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.FullName == Prefix + name),
                    $"{name} must NOT be manufactured for Task 74.");
            }
        }

        [Test]
        public void NoGoalkeeperSystems_WereCreated()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "GoalkeeperSystem", "GoalkeeperController", "GoalkeeperAI",
                "GKDecisionSystem", "SaveDecisionSystem", "GoalkeeperBehaviorTree", "SaveSystem",
                "DiveController", "GoalkeeperDiveSystem", "SaveAnimationSystem", "GoalkeeperAnimator",
                "DiveAnimationController", "SaveAnimator", "ReactionSystem", "GoalkeeperAISystem",
                "GoalkeeperValidator", "GoalkeepingSystem" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"Goalkeeping task must NOT create '{name}'.");
            }
        }

        [Test]
        public void NoSeparateGoalkeeperPlayerDataModel()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "GoalkeeperDefinition", "GoalkeeperPlayerStats",
                "GoalkeeperStatsAsset", "GoalkeeperData", "GoalkeeperProfile" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"No separate '{name}' player-data model may exist — the unified PlayerStats must be used.");
            }
        }

        // ---- Unified model / GK role / outfield retention ----

        [Test]
        public void Goalkeeping_IsSeventhStoredCategory_ForEveryPlayer()
        {
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance),
                "Goalkeeping must be a stored category for every player (unified model).");
            var f = ps.GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(Prefix + "GoalkeepingStats", f.FieldType.FullName,
                "The Goalkeeping field must be strongly typed as GoalkeepingStats.");
        }

        [Test]
        public void Goalkeeping_IsPrimaryProfile_WhenPrimaryPositionIsGK()
        {
            // Role rule (presentation/role): PrimaryPosition == GK selects Goalkeeping as primary.
            var profile = FindType(Prefix + "Profile");
            var pp = profile.GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(pp, "Profile must expose PrimaryPosition.");
            var enumType = pp.FieldType;
            Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(enumType));
            var gk = Enum.Parse(enumType, "GK");
            Assert.IsNotNull(gk, "PlayerPosition enum must contain GK.");
            // The GK role is expressed in Profile (presentation/role), NOT by removing/mutating data.
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance),
                "The Goalkeeping category must remain stored regardless of role.");
        }

        [Test]
        public void OutfieldPlayers_RetainGoalkeepingData()
        {
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance),
                "Outfield players must retain the Goalkeeping category via unified PlayerStats — an outfield player may be assigned to GK later.");
            // No split model removes GK stats from outfield players.
            foreach (var name in new[] { "OutfieldPlayerStats", "OutfieldPlayerDefinition",
                "GoalkeeperPlayerStats", "GoalkeeperDefinition" })
            {
                Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"No split '{name}' model may exist.");
            }
        }

        [Test]
        public void Goalkeeping_IsIndependentOfPrimaryPosition()
        {
            var names = GoalkeepingType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            Assert.IsFalse(names.Contains("PrimaryPosition"), "Goalkeeping must not be driven by PrimaryPosition.");
        }

        // ---- Handling vs Parrying distinction ----

        [Test]
        public void Handling_And_Parrying_AreDistinctFields()
        {
            var t = GoalkeepingType();
            Assert.IsNotNull(t.GetField("Handling", BindingFlags.Public | BindingFlags.Instance),
                "Handling (secure catch/control) must exist.");
            Assert.IsNotNull(t.GetField("Parrying", BindingFlags.Public | BindingFlags.Instance),
                "Parrying (redirect/deflect when clean handling is not possible) must exist.");
            Assert.AreNotEqual(t.GetField("Handling", BindingFlags.Public | BindingFlags.Instance),
                t.GetField("Parrying", BindingFlags.Public | BindingFlags.Instance),
                "Handling and Parrying must be distinct attributes.");
        }
    }
}
