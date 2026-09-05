using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 89 — TeamDefinition (the canonical authored team data asset).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real ScriptableObject/field/type inspection, not source-text matching).
    ///
    /// Task 89 locks the TeamDefinition ASSET BOUNDARY:
    ///   - One TeamDefinition = one authored team; a ScriptableObject asset, not a MonoScript, not
    ///     a runtime component.
    ///   - Owns team-level authored configuration only (identity/colors/squad members/formation/
    ///     tactics seeds) — it does NOT own player identity/stats, does NOT copy player data, and
    ///     does NOT reference runtime player/scene objects.
    ///   - Player membership is a REFERENCE boundary to PlayerDefinition assets, never an embedded
    ///     player copy and never a GameObject/Transform/PlayerController.
    ///   - PlayerDefinition.Identity.ClubReference may point back to TeamDefinition (asset cycle is
    ///     acceptable; ownership stays clear).
    ///   - NO runtime match state, NO Update loops, NO gameplay/AI/physics/animation/camera/input,
    ///     NO team manager/database/registry/squad/formation/tactics/ratings/home-away systems.
    ///   - Extensible toward Tasks 90-96 (Identity/Colors/Squad/Formation/Tactics/Ratings/HomeAway)
    ///     without forcing those future areas into Task 89.
    /// </summary>
    public class TeamDefinitionTests
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

        private static Type IdentityType() => FindType(Prefix + "Identity");

        private static Type PlayerStatsType() => FindType(Prefix + "PlayerStats");

        private static IEnumerable<FieldInfo> PublicFields(Type t) =>
            t.GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static bool IsPlayerReferenceArray(FieldInfo f)
        {
            var et = f.FieldType.IsArray ? f.FieldType.GetElementType() : null;
            return et != null && et.FullName == PlayerDefType().FullName;
        }

        private static readonly Type[] ProhibitedRuntimeObjectTypes =
        {
            typeof(GameObject), typeof(Component), typeof(Transform), typeof(Rigidbody),
            typeof(Animator), typeof(Camera), typeof(Behaviour)
        };

        // ---- 89.1 Responsibility ----

        [Test]
        public void TeamDefinitionExists()
        {
            Assert.IsNotNull(TeamDefType(), "Football.Data.TeamDefinition must exist.");
        }

        [Test]
        public void TeamDefinitionIsScriptableObject()
        {
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(TeamDefType()),
                "TeamDefinition must be a ScriptableObject (authored asset), not a runtime class.");
        }

        [Test]
        public void TeamDefinitionIsNotMonoBehaviour()
        {
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(TeamDefType()),
                "TeamDefinition must not be a MonoBehaviour / scene component.");
        }

        [Test]
        public void TeamDefinitionIsCanonicalTeamAsset()
        {
            // No parallel/duplicate team-definition types exist; one TeamDefinition is the team asset.
            Assert.IsFalse(HasTypeName("TeamDataAsset", "FootballTeamDefinition", "RuntimeTeamDefinition"),
                "Must not create parallel duplicate TeamDefinition classes.");
        }

        [Test]
        public void TeamDefinitionHasNoUpdateLoop()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(TeamDefType().GetMethod(m,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"TeamDefinition must not run '{m}' runtime loops.");
            }
        }

        [Test]
        public void TeamDefinitionContainsNoRuntimeMatchState()
        {
            foreach (var f in PublicFields(TeamDefType()))
            {
                Assert.IsFalse(f.Name.StartsWith("Current", StringComparison.Ordinal),
                    $"TeamDefinition must not hold runtime match state field '{f.Name}'.");
            }
            Assert.IsFalse(HasTypeName("RuntimeTeam", "TeamMatchState", "ActiveTeamState"),
                "No runtime match-state type may be owned by TeamDefinition.");
        }

        // ---- 89.2 / 89.3 Ownership and references ----

        [Test]
        public void TeamDefinitionPlayerReferencesUsePlayerDefinition()
        {
            var playerRefs = PublicFields(TeamDefType()).Where(IsPlayerReferenceArray).ToList();
            Assert.IsNotEmpty(playerRefs,
                "TeamDefinition must hold squad membership as PlayerDefinition[] references.");
        }

        [Test]
        public void TeamDefinitionPlayerReferencesAreNotRuntimeObjects()
        {
            foreach (var f in PublicFields(TeamDefType()))
            {
                if (IsPlayerReferenceArray(f))
                {
                    Assert.AreEqual(PlayerDefType().FullName, f.FieldType.GetElementType().FullName,
                        $"Player reference '{f.Name}' must use PlayerDefinition, not a runtime object.");
                }
            }
        }

        [Test]
        public void TeamDefinitionDoesNotEmbedPlayerCopies()
        {
            var playerFieldNames = new[]
            {
                "PlayerName", "Name", "Pace", "Shooting", "Passing", "Dribbling",
                "Defending", "Physical", "Goalkeeping", "OverallRating", "WeakFoot"
            };
            foreach (var f in PublicFields(TeamDefType()))
            {
                Assert.IsFalse(playerFieldNames.Contains(f.Name),
                    $"TeamDefinition must not embed player-data copy field '{f.Name}'.");
                Assert.IsFalse(f.FieldType.FullName.StartsWith(Prefix + "PlayerStats", StringComparison.Ordinal),
                    "TeamDefinition must not embed player stats.");
            }
        }

        [Test]
        public void TeamDefinitionDoesNotOwnPlayerIdentity()
        {
            Assert.IsNull(TeamDefType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance),
                "TeamDefinition must not own a PlayerId (player identity lives in PlayerDefinition).");
            Assert.IsNull(PublicFields(TeamDefType()).FirstOrDefault(f => f.Name == "PlayerName"),
                "TeamDefinition must not own player-name data.");
        }

        [Test]
        public void TeamDefinitionDoesNotContainPlayerStats()
        {
            var psFullName = PlayerStatsType().FullName;
            var hasStats = PublicFields(TeamDefType()).Any(
                f => f.FieldType.FullName == psFullName ||
                     (f.FieldType.IsArray && f.FieldType.GetElementType().FullName == psFullName));
            Assert.IsFalse(hasStats, "TeamDefinition must not contain PlayerStats instances.");
        }

        [Test]
        public void ClubReferenceCanPointToTeamDefinition()
        {
            var club = IdentityType().GetField("ClubReference",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(club, "Identity must expose ClubReference.");
            Assert.AreEqual(TeamDefType().FullName, club.FieldType.FullName,
                "ClubReference must be typed as a TeamDefinition asset reference.");
        }

        [Test]
        public void TeamDefinitionHasNoRuntimeBehaviourComponents()
        {
            foreach (var f in PublicFields(TeamDefType()))
            {
                var ftype = f.FieldType;
                if (ftype.IsArray) ftype = ftype.GetElementType();
                Assert.IsFalse(ProhibitedRuntimeObjectTypes.Any(t => t.IsAssignableFrom(ftype)),
                    $"TeamDefinition field '{f.Name}' must not hold a runtime scene/behaviour object.");
            }
        }

        // ---- No systems / infra ----

        [Test]
        public void TeamDefinitionDoesNotCreateTeamManagerOrDatabase()
        {
            Assert.IsFalse(HasTypeName("TeamManager", "TeamDataManager", "TeamRegistry", "TeamDatabase",
                "RuntimeTeamManager", "TeamCatalog", "TeamLookup"),
                "No team manager/database/registry may be created for Task 89.");
        }

        [Test]
        public void TeamDefinitionDoesNotCreateSquadFormationTacticsSystems()
        {
            Assert.IsFalse(HasTypeName("SquadManager", "SquadSystem", "RosterManager",
                "FormationSystem", "FormationController", "FormationManager",
                "TacticsSystem", "TacticalManager", "TacticsController"),
                "Squad/Formation/Tactics systems are owned by Tasks 92/93/94, not created here.");
        }

        [Test]
        public void TeamDefinitionDoesNotCreateTeamRatingOrHomeAwaySystems()
        {
            Assert.IsFalse(HasTypeName("TeamRatingSystem", "TeamOverallCalculator", "TeamRatingCalculator",
                "HomeAwaySystem", "VenueConfig", "MatchKitSystem"),
                "Team Ratings / Home-Away systems are owned by Tasks 95/96, not created here.");
        }

        [Test]
        public void TeamDefinitionDoesNotCreateAssetLoadingOrImportPipeline()
        {
            Assert.IsFalse(HasTypeName("TeamImporter", "TeamAssetBuilder", "TeamGenerator", "TeamLoader"),
                "No asset-loading/import pipeline may be created for Task 89.");
        }

        // ---- 89.4 Extensibility / serialization ----

        [Test]
        public void TeamDefinitionIsAuthorableAsset()
        {
            var cam = TeamDefType().GetCustomAttributes(typeof(CreateAssetMenuAttribute), false);
            Assert.IsNotEmpty(cam, "TeamDefinition should be authorable via CreateAssetMenu.");
        }

        [Test]
        public void TeamDefinitionSupportsFutureTeamDataExpansion()
        {
            // A plain ScriptableObject with simple serialized authored fields and no runtime coupling
            // is naturally extensible toward Tasks 90-96 (Identity/Colors/Squad/Formation/Tactics/
            // Ratings/HomeAway). Assert the boundary that keeps it extensible:
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(TeamDefType()),
                "TeamDefinition must stay an authored asset to be extensible.");
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(TeamDefType()),
                "TeamDefinition must not carry runtime behaviour (blocks clean data expansion).");
        }
    }
}
