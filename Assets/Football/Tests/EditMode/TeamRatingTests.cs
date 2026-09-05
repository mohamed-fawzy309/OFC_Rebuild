using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 95 — Team Ratings (TEAM-LEVEL authored assessment, owned by TeamDefinition).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance + the real GetInvalidTeamRatingsData API).
    ///
    /// Task 95 locks:
    ///   - Team Ratings, if established, are AUTHORED TEAM-LEVEL data owned by TeamDefinition and
    ///     are NOT PlayerStats, NOT individual attributes, NOT runtime performance, NOT match
    ///     statistics, NOT AI state.
    ///   - NO rating representation/range/default is currently established (POLICY / DEFERRED), and
    ///     DERIVATION is DEFERRED — never invented, never derived from Squad/PlayerStats, never forced
    ///     onto the PlayerStats 1-99 scale. The common rating names (AttackRating/MidfieldRating/
    ///     DefenseRating/TeamOverallRating etc.) remain ABSENT (locked by prior tasks).
    ///   - Ratings are not owned by PlayerDefinition/Squad/Formation/Tactics/Home-Away; no runtime
    ///     rating state, no match statistics, no rating calculators/systems/managers.
    /// </summary>
    public class TeamRatingTests
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
            return names.Any(n => all.Any(t => t.Name == n));
        }

        private static Type TeamDefType() => FindType(Prefix + "TeamDefinition");

        private static Type PlayerDefType() => FindType(Prefix + "PlayerDefinition");

        private static Type PlayerStatsType() => FindType(Prefix + "PlayerStats");

        private static object NewTeam() => ScriptableObject.CreateInstance(TeamDefType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static MethodInfo MethodOf(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              BindingFlags.Static);

        // ---- 95.1 Responsibility / ownership ----

        [Test]
        public void TeamRatingValidationBelongsToTeamDefinition()
        {
            Assert.IsNotNull(MethodOf(TeamDefType(), "GetInvalidTeamRatingsData"),
                "Team Ratings validation must live on TeamDefinition.");
            Assert.IsNull(MethodOf(PlayerDefType(), "GetInvalidTeamRatingsData"),
                "PlayerDefinition must not own Team Ratings validation.");
            Assert.IsNull(MethodOf(PlayerStatsType(), "GetInvalidTeamRatingsData"),
                "PlayerStats must not own Team Ratings validation.");
        }

        [Test]
        public void TeamRatingsAreNotOwnedByPlayerOrOtherTeamDomains()
        {
            // Ratings are a team-data concern owned ONLY by TeamDefinition (currently deferred).
            foreach (var n in new[] { "Starters", "Substitutes", "Formation", "Aggression",
                                      "PrimaryColor", "TeamId" })
            {
                Assert.IsNotNull(FieldOf(TeamDefType(), n),
                    $"{n} must remain a team field (for the ownership boundary check).");
            }
            Assert.IsNull(FieldOf(PlayerDefType(), "TeamRatings"),
                "PlayerDefinition must not hold a TeamRatings sub-object.");
            Assert.IsNull(FieldOf(PlayerDefType(), "TeamRating"),
                "PlayerDefinition must not hold a TeamRating field.");
        }

        [Test]
        public void TeamRatingNamesRemainAbsent()
        {
            // The common team-rating names are NOT established; they remain absent (deferred), as
            // locked by prior task tests. Do not bloat TeamDefinition with invented rating fields.
            foreach (var n in new[] { "AttackRating", "MidfieldRating", "DefenseRating",
                                      "OverallRating", "TeamRating", "TeamOverallRating",
                                      "OverallTeamRating", "Offense", "Defense",
                                      "TeamPace", "TeamShooting", "TeamPassing",
                                      "TeamDribbling", "TeamPhysical" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Team Rating field '{n}' is not established and must remain absent (DEFERRED).");
            }
        }

        [Test]
        public void TeamRatingsAreNotPlayerStats()
        {
            // Team Ratings must not be packaged as PlayerStats or as player ratings.
            foreach (var n in new[] { "Pace", "Shooting", "Passing", "Dribbling",
                                      "Defending", "Physical", "Goalkeeping" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not hold PlayerStats concept '{n}' as a team rating.");
            }
            Assert.IsNull(MethodOf(TeamDefType(), "CopyRatingsFromPlayerStats"),
                "Team Ratings must not copy from PlayerStats");
        }

        // ---- 95.2 Representation / derivation ----

        [Test]
        public void TeamRatingRepresentationIsDeferred()
        {
            // No representation exists; it is documented POLICY/DEFERRED, not invented.
            Assert.IsNull(MethodOf(TeamDefType(), "GetTeamAttackRating"),
                "No rating accessor may be invented when representation is deferred.");
            Assert.IsNull(MethodOf(TeamDefType(), "GetTeamOverallRating"),
                "No derived overall rating may be invented (DERIVATION = DEFERRED).");
        }

        [Test]
        public void TeamRatingPlayerStatsScaleNotReused()
        {
            // Team Ratings are NOT the PlayerStats 1-99 scale.
            Assert.IsNull(MethodOf(TeamDefType(), "GetInvalidTeamRatingsAsPlayerStats"),
                "Team Ratings must not be validated as PlayerStats.");
            Assert.IsFalse(HasTypeName("TeamRatingValidator", "TeamRatingStats"),
                "Team Ratings must not reuse PlayerStats rating infrastructure.");
        }

        // ---- 95.3 / 95.4 Separation & runtime boundary ----

        [Test]
        public void TeamRatingsAreSeparateFromSquadFormationTacticsHomeAway()
        {
            // No rating-values embedded in other team domains; no rating modifiers anywhere.
            foreach (var m in new[] { "ModifyTeamRatingFromSquad", "ModifyTeamRatingFromFormation",
                                      "ModifyTeamRatingFromTactics", "ModifyTeamRatingFromHomeAway",
                                      "CalculateTeamRatingFromPlayers", "DeriveTeamRatingsFromSquad",
                                      "SquadRatingCalculator", "TeamOverallCalculator" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"Team Ratings must not be derived/modified by '{m}'.");
            }
        }

        [Test]
        public void TeamRatingsHaveNoRuntimeOrMatchState()
        {
            foreach (var n in new[] { "CurrentTeamRating", "RuntimeTeamRating", "LiveAttackRating",
                                      "LiveDefenseRating", "CurrentOverallRating",
                                      "Goals", "Assists", "Wins", "Losses", "Possession",
                                      "Shots", "ShotsOnTarget", "CurrentForm" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not hold runtime/match statistic '{n}'.");
            }
        }

        [Test]
        public void NoTeamRatingSystemOrInfrastructure()
        {
            Assert.IsFalse(HasTypeName("TeamRatingSystem", "TeamRatingManager",
                    "TeamOverallCalculator", "TeamRatingCalculator", "TeamRatingDatabase",
                    "TeamRatingRegistry", "TeamRatingController", "PlayerToTeamRatingConverter",
                    "AveragePlayerRatings", "SquadRatingCalculator"),
                "Team Ratings must not create system/manager/calculator/database/registry/converter types.");
        }

        [Test]
        public void TeamRatingsDoNotRunAIOrLoops()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate", "DecideRatingsAI" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"TeamDefinition must not run '{m}' from Team Ratings.");
            }
        }

        [Test]
        public void InvalidTeamRatingDataIsNotMutated()
        {
            // Even though representation is deferred, the validation boundary must never mutate.
            var team = NewTeam();
            GetInvalidTeamRatingsData(team);
            // No rated authored field exists to mutate; boundary returns empty and is stable.
            Assert.IsEmpty(GetInvalidTeamRatingsData(team));
        }

        // ---- helpers ----

        private static List<string> GetInvalidTeamRatingsData(object team) =>
            (List<string>)MethodOf(TeamDefType(), "GetInvalidTeamRatingsData").Invoke(team, null);
    }
}
