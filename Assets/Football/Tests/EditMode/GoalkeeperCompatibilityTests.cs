using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 83 — Goalkeeper Compatibility.
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 83 locks the DATA/ARCHITECTURE relationship between Goalkeeping stats and the
    /// Goalkeeper role:
    ///   - Goalkeeping stats (Diving/Handling/Kicking/Positioning/Reflexes/Parrying) live in the
    ///     unified <c>PlayerStats</c> and exist for EVERY player, regardless of PrimaryPosition.
    ///   - The Goalkeeper ROLE is authored ONLY by <c>Profile.PrimaryPosition == GK</c>; there is no
    ///     separate <c>IsGoalkeeper</c> flag or second source of truth.
    ///   - Outfield players keep their Goalkeeping data (so they may later be used as GK), with no
    ///     copy/convert/remove of stats.
    ///   - Runtime role/position is NOT stored in Profile/PlayerDefinition.
    ///   - No separate goalkeeper data model, no GK-specific player data, no stat conversion, and no
    ///     Goalkeeper gameplay/AI/physics/animation (a future GoalkeeperConfig, if any, is SYSTEM
    ///     configuration only, never player ratings).
    /// </summary>
    public class GoalkeeperCompatibilityTests
    {
        private const string Prefix = "Football.Data.";
        private static readonly string[] GkAttributes =
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

        private static IEnumerable<Type> AllTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes);
        }

        private static bool HasTypeName(params string[] names)
        {
            var all = AllTypes().ToList();
            return names.Any(n => all.Any(t => t.Name == n));
        }

        private static bool HasTypeNameIn(Assembly asm, params string[] names)
        {
            var all = SafeGetTypes(asm).ToList();
            return names.Any(n => all.Any(t => t.Name == n));
        }

        private static Type PlayerDefinitionType() => FindType(Prefix + "PlayerDefinition");

        private static Type ProfileType() => FindType(Prefix + "Profile");

        private static Type PlayerStatsType() => FindType(Prefix + "PlayerStats");

        private static Type GoalkeepingStatsType() => FindType(Prefix + "GoalkeepingStats");

        private static Type PositionType() => FindType(Prefix + "PlayerPosition");

        private static object NewPlayer() => Activator.CreateInstance(PlayerDefinitionType());

        private static FieldInfo PlayerStatsField() =>
            PlayerDefinitionType().GetField("PlayerStats", BindingFlags.Public | BindingFlags.Instance);

        private static FieldInfo GoalkeepingField() =>
            PlayerStatsType().GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance);

        // ---- 83.1 Goalkeeping stats available for every player ----

        [Test]
        public void EveryPlayerHasGoalkeepingCategory()
        {
            var f = GoalkeepingField();
            Assert.IsNotNull(f, "PlayerStats must have a Goalkeeping category.");
            Assert.AreEqual(Prefix + "GoalkeepingStats", f.FieldType.FullName,
                "Goalkeeping must be of type GoalkeepingStats.");
        }

        [Test]
        public void EveryPlayerHasAllSixGoalkeepingAttributes()
        {
            var fields = GoalkeepingStatsType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var a in GkAttributes)
            {
                Assert.IsTrue(fields.Any(x => x.Name == a && x.FieldType == typeof(int)),
                    $"GoalkeepingStats must define int attribute '{a}'.");
            }
        }

        [Test]
        public void GoalkeepingStatsAreNotConditionalOnPrimaryPosition()
        {
            // Goalkeeping is an unconditional field on PlayerStats; no conditional/nullable model.
            Assert.IsNotNull(GoalkeepingField(), "Goalkeeping must always be present.");
            // A fresh PlayerStats instance owns a Goalkeeping object (not null).
            var ps = Activator.CreateInstance(PlayerStatsType());
            Assert.IsNotNull(GoalkeepingField().GetValue(ps),
                "A default PlayerStats must contain a Goalkeeping object (never null).");
        }

        [Test]
        public void GoalkeepingExistsForOutfieldProfiles()
        {
            // Default Profile has PrimaryPosition = ST (an outfield position) yet PlayerStats.Goalkeeping exists.
            var player = NewPlayer();
            var ps = PlayerStatsField().GetValue(player);
            Assert.IsNotNull(GoalkeepingField().GetValue(ps),
                "An outfield primary profile must still carry Goalkeeping data.");
        }

        [Test]
        public void GoalkeepingExistsForGKProfiles()
        {
            // A GK profile still owns PlayerStats.Goalkeeping (and keeps all categories).
            Assert.IsTrue(Enum.GetNames(PositionType()).Contains("GK"),
                "GK must be an approved PlayerPosition.");
            Assert.IsNotNull(GoalkeepingField(), "GK profile relies on the same PlayerStats.Goalkeeping.");
        }

        [Test]
        public void GoalkeepingIsPartOfUnifiedPlayerStats()
        {
            // Goalkeeping sits alongside the six outfield categories in the SAME PlayerStats container.
            foreach (var c in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping" })
            {
                Assert.IsNotNull(PlayerStatsType().GetField(c, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerStats must contain every category including '{c}'.");
            }
        }

        // ---- 83.2 Separate role from stats ----

        [Test]
        public void PrimaryPositionGKSelectsGoalkeeperRole()
        {
            // The authorative authored role is PrimaryPosition == GK (the single source of truth).
            Assert.IsTrue(Enum.GetNames(PositionType()).Contains("GK"),
                "PrimaryPosition must include GK to designate the goalkeeper role.");
            Assert.IsNotNull(ProfileType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance),
                "Profile must expose PrimaryPosition as the role authority.");
        }

        [Test]
        public void NonGKPrimaryPositionSelectsOutfieldProfile()
        {
            // Default PrimaryPosition must be a non-GK outfield position (ST), never interpreted as GK here.
            var player = NewPlayer();
            var profile = PlayerDefinitionType().GetField("Profile", BindingFlags.Public | BindingFlags.Instance).GetValue(player);
            var prim = (int)ProfileType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance).GetValue(profile);
            var names = Enum.GetNames(PositionType());
            var primName = names[prim];
            Assert.AreNotEqual("GK", primName,
                "A default player must be an OUTFIELD primary position; GK is an explicit authored choice.");
        }

        [Test]
        public void GoalkeepingStatsDoNotDetermineGoalkeeperRole()
        {
            // No field/method ties Goalkeeping ratings to the role; stats never assign the GK role.
            foreach (var sig in new[] { "IsGoalkeeper", "Goalkeeper" })
            {
                Assert.IsNull(ProfileType().GetField(sig, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Profile must not derive goalkeeper role from a '{sig}' flag.");
            }
            Assert.IsNull(PlayerStatsType().GetField("IsGoalkeeper", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayerStats must not carry an IsGoalkeeper flag.");
        }

        [Test]
        public void NoIsGoalkeeperDuplicateFlag()
        {
            foreach (var t in new[] { PlayerDefinitionType(), ProfileType(), PlayerStatsType() })
            {
                Assert.IsNull(t.GetField("IsGoalkeeper", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"{t.Name} must NOT duplicate role with an IsGoalkeeper flag.");
            }
        }

        [Test]
        public void NoSeparateGoalkeeperStatsModel()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperPlayerStats", "GKPlayerStats"),
                "No separate goalkeeper player-stats model may exist.");
        }

        [Test]
        public void GoalkeepingUsesUnifiedPlayerStats()
        {
            // Goalkeeping is a field inside the SAME PlayerStats as the outfield categories.
            var pd = NewPlayer();
            var ps = PlayerStatsField().GetValue(pd);
            Assert.IsNotNull(GoalkeepingField().GetValue(ps), "Goalkeeping must be reachable via unified PlayerStats.");
        }

        // ---- 83.3 Outfield player goalkeeper compatibility ----

        [Test]
        public void OutfieldPlayerCanRetainGoalkeepingData()
        {
            // An outfield player (default ST) keeps Goalkeeping data available for later GK use.
            var player = NewPlayer();
            var ps = PlayerStatsField().GetValue(player);
            var gk = GoalkeepingField().GetValue(ps);
            Assert.IsNotNull(gk, "An outfield player must retain Goalkeeping data.");
            foreach (var a in GkAttributes)
            {
                Assert.IsNotNull(GoalkeepingStatsType().GetField(a, BindingFlags.Public | BindingFlags.Instance),
                    $"Outfield players must still hold '{a}'.");
            }
        }

        [Test]
        public void OutfieldPlayerGoalkeepingDataIsNotCopied()
        {
            Assert.IsFalse(HasTypeName("ConvertedGoalkeeperStats", "EmergencyGoalkeeperStats", "TemporaryGoalkeeperStats"),
                "Using an outfield player as GK must NOT create copied/converted GK stats.");
        }

        [Test]
        public void RuntimeGoalkeeperRoleNotStoredInProfile()
        {
            foreach (var n in new[] { "CurrentRole", "CurrentPosition", "RuntimeRole", "RuntimePosition" })
            {
                Assert.IsNull(ProfileType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Runtime role '{n}' must NOT be stored in Profile.");
            }
        }

        [Test]
        public void NoCurrentPositionOrRoleFieldCreated()
        {
            var pd = PlayerDefinitionType();
            foreach (var n in new[] { "CurrentPosition", "CurrentRole", "RuntimePosition", "RuntimeRole" })
            {
                Assert.IsNull(pd.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PlayerDefinition must NOT hold runtime role '{n}'.");
            }
        }

        [Test]
        public void GoalkeeperRoleDoesNotModifyGoalkeepingStats()
        {
            // No runtime mutation of Goalkeeping when role is set.
            foreach (var m in new[] { "SetAsGoalkeeper", "ConvertToGoalkeeper", "AssignGoalkeeper" })
            {
                Assert.IsNull(PlayerDefinitionType().GetMethod(m,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"No runtime '{m}' method may mutate Goalkeeping/role in PlayerDefinition.");
            }
        }

        [Test]
        public void GoalkeeperRoleDoesNotRemoveOutfieldStats()
        {
            // A GK primary profile still retains all six outfield categories (unified model).
            foreach (var c in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical" })
            {
                Assert.IsNotNull(PlayerStatsType().GetField(c, BindingFlags.Public | BindingFlags.Instance),
                    $"A GK profile must still keep the outfield category '{c}'.");
            }
        }

        // ---- 83.4 Future goalkeeper specialization boundary ----

        [Test]
        public void NoStatConversionToGoalkeeping()
        {
            foreach (var m in new[] { "ConvertToGoalkeeping", "CalculateGoalkeepingFromPhysical", "ApplyStatConversion" })
            {
                Assert.IsNull(PlayerStatsType().GetMethod(m,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"No automatic stat-to-Goalkeeping conversion '{m}' may exist.");
            }
        }

        [Test]
        public void NoSeparateGoalkeeperDefinition()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperDefinition", "GoalkeeperPlayerStats", "GKPlayerStats"),
                "No duplicate goalkeeper player-data model may exist.");
        }

        [Test]
        public void FutureGoalkeeperConfigRemainsSystemBoundary()
        {
            // Task 83 does not create GoalkeeperConfig; it is a future SYSTEM configuration, not player ratings.
            Assert.IsFalse(HasTypeName("GoalkeeperConfig"),
                "GoalkeeperConfig must not be created in Task 83 (future system-config boundary only).");
        }

        [Test]
        public void NoGoalkeeperAIWasCreated()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperAI", "GKDecisionSystem", "SaveDecisionSystem", "GoalkeeperBehavior"),
                "No goalkeeper AI/decision system may be created.");
        }

        [Test]
        public void NoGoalkeeperPhysicsWasCreated()
        {
            Assert.IsFalse(HasTypeName("DivePhysics", "SavePhysics", "CatchPhysics", "ParryPhysics"),
                "No goalkeeper physics may be created.");
        }

        [Test]
        public void NoGoalkeeperAnimationWasCreated()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperAnimator", "SaveAnimator", "DiveAnimator"),
                "No goalkeeper animation system may be created.");
        }

        [Test]
        public void NoGoalkeeperGameplayWasCreated()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperGameplay", "GoalkeeperController", "GoalkeeperRuntimeSystem"),
                "No goalkeeper gameplay/runtime controller may be created in Task 83.");
        }

        [Test]
        public void NoGoalkeeperOverallRatingCalculation()
        {
            foreach (var sig in new[] { "GoalkeeperOverallRatingCalculator", "OverallRatingCalculator" })
            {
                Assert.IsFalse(HasTypeName(sig), $"No '{sig}' may exist.");
            }
        }

        [Test]
        public void NoGoalkeeperSpecificPlayerDataAsset()
        {
            Assert.IsFalse(HasTypeNameIn(PlayerDefinitionType().Assembly, "GoalkeeperPlayerDefinition"),
                "No goalkeeper-specific player data asset type may exist.");
        }

        [Test]
        public void GoalkeepingDoesNotControlGameplay()
        {
            // Goalkeeping data lives on a plain data class with no gameplay/lifecycle methods.
            var dataTypes = new[] { PlayerDefinitionType(), ProfileType(), PlayerStatsType() };
            foreach (var t in dataTypes)
            {
                foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
                {
                    Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"{t.Name} must not expose '{m}' (data class, not a gameplay system).");
                }
            }
        }
    }
}
