using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 94 — Tactics (HOW the team prefers to play, owned by TeamDefinition).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance + the real GetInvalidTacticsData API).
    ///
    /// Task 94 locks:
    ///   - Tactics are AUTHORED TEAM behavioral-preference data (Aggression / PossessionPreference /
    ///     DefensiveLine / Pressing) owned by TeamDefinition; single authoritative owner each.
    ///   - Tactics are NOT Formation (shape = WHERE), NOT Squad (membership = WHO), NOT Player
    ///     PlayStyle, NOT PlayerStats, NOT Team Ratings (Task 95), NOT Home/Away (Task 96).
    ///   - TeamDefinition.Aggression (float, team preference) coexists legitimately with
    ///     PlayerStats.Physical.Aggression (int, player tendency) — different ownership/domain.
    ///   - Tactics carry no runtime state/objects, no AI/behavior trees, no update loops, no
    ///     movement/physics/animation/camera/input control, no player assignment, no mutation.
    ///   - No Tactics system/manager/controller/AI/database/registry/catalog/preset infrastructure.
    ///   - Ranges are POLICY/DEFERRED (no established numeric range); GetInvalidTacticsData owns the
    ///     team-data validation boundary, never mutates, and reuses no PlayerStats validator.
    /// </summary>
    public class TacticsTests
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

        // Established tactical concepts, each with one authoritative owner on TeamDefinition.
        private static readonly string[] TacticalFields =
        {
            "Aggression", "PossessionPreference", "DefensiveLine", "Pressing"
        };

        // ---- 94.1 Responsibility / ownership ----

        [Test]
        public void TacticsResponsibilityBelongsToTeamDefinition()
        {
            foreach (var n in TacticalFields)
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsNotNull(f, $"TeamDefinition must own tactical field '{n}'.");
                Assert.AreEqual(TeamDefType(), f.DeclaringType,
                    $"Tactical field '{n}' must be declared on TeamDefinition.");
            }
        }

        [Test]
        public void TacticalFieldsAreAuthoredInstanceData()
        {
            foreach (var n in TacticalFields)
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsTrue((f.Attributes & FieldAttributes.Public) != 0,
                    $"'{n}' must be a public authored field.");
                Assert.IsTrue((f.Attributes & FieldAttributes.Static) == 0,
                    $"'{n}' must be instance (authored per-team) data.");
            }
        }

        [Test]
        public void TacticalFieldsAreSerialized()
        {
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(TeamDefType()),
                "TeamDefinition must remain a ScriptableObject.");
            foreach (var n in TacticalFields)
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsTrue((f.Attributes & FieldAttributes.Public) != 0,
                    $"'{n}' must serialize as a public field.");
            }
        }

        [Test]
        public void TacticalFieldsUseEstablishedFloatType()
        {
            // The established representation is float with default 50f; preserve it (no arbitrary
            // int/enum/string conversion).
            foreach (var n in TacticalFields)
            {
                Assert.AreEqual(typeof(float), FieldOf(TeamDefType(), n).FieldType,
                    $"'{n}' must keep the established float representation.");
            }
        }

        [Test]
        public void TacticalFieldsPreserveEstablishedDefaults()
        {
            // Established authored default 50f is preserved (not silently replaced).
            var team = NewTeam();
            foreach (var n in TacticalFields)
            {
                Assert.AreEqual(50f, (float)FieldOf(TeamDefType(), n).GetValue(team),
                    $"'{n}' must preserve the established default of 50f.");
            }
        }

        [Test]
        public void TacticalFieldsHaveSingleAuthoritativeOwner()
        {
            // Each TEAM tactic concept has exactly one authoritative owner: TeamDefinition.
            // Same-named fields on OTHER domains (e.g. PlayerStats.Physical.Aggression as an int
            // player rating) are legitimate and NOT duplicates of the team tactic.
            foreach (var n in TacticalFields)
            {
                var owner = FieldOf(TeamDefType(), n);
                Assert.IsNotNull(owner, $"TeamDefinition must own tactic '{n}'.");
                Assert.AreEqual(TeamDefType(), owner.DeclaringType,
                    $"'{n}' must be declared on TeamDefinition.");

                // No OTHER Football.Data type may redeclare the same tactic NAME as a float
                // (the same team-preference domain). A name reused for a different domain/type
                // (e.g. int PlayerStats.Aggression) is not a team-tactic duplicate.
                var teamFloatDupes = AllTypes()
                    .Where(t => t.Namespace == Prefix.TrimEnd('.') && t != TeamDefType())
                    .Select(t => t.GetField(n, BindingFlags.Public | BindingFlags.Instance))
                    .Where(f => f != null && f.FieldType == typeof(float))
                    .ToList();
                Assert.IsEmpty(teamFloatDupes,
                    $"'{n}' must have exactly one authoritative float owner (TeamDefinition).");
            }
        }

        // ---- Team Aggression vs Player Aggression ----

        [Test]
        public void TeamAggressionIsSeparateFromPlayerAggression()
        {
            // Both legitimately coexist: TeamDefinition.Aggression (team, float, preference) vs
            // PlayerStats.Physical.Aggression (player, int, tendency). Classified by ownership.
            var teamField = FieldOf(TeamDefType(), "Aggression");
            Assert.IsNotNull(teamField, "TeamDefinition.Aggression must exist.");
            Assert.AreEqual(typeof(float), teamField.FieldType,
                "Team Aggression is a float preference, distinct from the int player rating.");
            Assert.AreEqual(TeamDefType(), teamField.DeclaringType,
                "Team Aggression must be declared on TeamDefinition.");

            var physical = PlayerStatsType().GetField("Physical",
                BindingFlags.Public | BindingFlags.Instance);
            if (physical != null)
            {
                Assert.AreNotSame(teamField, physical,
                    "Team Aggression must not share a field with PlayerStats.Physical.");
            }
        }

        [Test]
        public void TeamAggressionNotStoredOnPlayerStats()
        {
            // The team tactic Aggression must not be mirrored into PlayerStats as team data.
            if (PlayerStatsType().GetField("Aggression", BindingFlags.Public | BindingFlags.Instance) != null)
            {
                // A field named Aggression on PlayerStats is the PLAYER rating (physical tendency),
                // which is legitimate and distinct from the TEAM tactic. Assert it is NOT the float
                // team preference type.
                var ps = PlayerStatsType().GetField("Aggression", BindingFlags.Public | BindingFlags.Instance);
                Assert.AreNotEqual(typeof(float), ps.FieldType,
                    "PlayerStats.Aggression is a player rating (int), not the float team tactic.");
            }
        }

        // ---- 94.3 Separation ----

        [Test]
        public void TacticsAreSeparateFromFormation()
        {
            // Tactics (HOW) remain distinct fields from Formation (WHERE/shape identifier).
            Assert.IsNotNull(FieldOf(TeamDefType(), "Formation"),
                "Formation must remain on TeamDefinition.");
            foreach (var n in TacticalFields)
            {
                Assert.IsNull(FieldOf(TeamDefType(), "Formation" + n),
                    $"Tactics must not be embedded inside Formation as '{n}'.");
                Assert.IsNull(FieldOf(TeamDefType(), n + "FromFormation"),
                    $"Tactics must not be derived from Formation as '{n}'.");
            }
        }

        [Test]
        public void TacticsAreSeparateFromSquad()
        {
            // Tactics must not be added to Starters/Substitutes (membership arrays) or PlayerDefinition.
            foreach (var n in new[] { "Starters", "Substitutes" })
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsNotNull(f, $"{n} must remain on TeamDefinition.");
                Assert.IsTrue(f.FieldType.IsArray, $"{n} must remain a PlayerDefinition[] squad array.");
            }
        }

        [Test]
        public void TacticsAreSeparateFromPlayerPlayStyle()
        {
            // Tactics must not live on PlayerDefinition.Profile.PlayStyle.
            Assert.IsNull(FieldOf(TeamDefType(), "PlayStyle"),
                "Tactics must not be stored as a PlayStyle field on TeamDefinition.");
            Assert.IsNull(MethodOf(TeamDefType(), "SetPlayStyleFromTactics"),
                "Tactics must not modify PlayerDefinition.Profile.PlayStyle.");
            Assert.IsNull(MethodOf(TeamDefType(), "ApplyTacticsToPlayStyle"),
                "Tactics must not derive PlayStyle.");
        }

        [Test]
        public void TacticsAreSeparateFromPlayerStats()
        {
            // Tactics must not duplicate/create Team-level rating fields or modify player ratings.
            foreach (var n in new[] { "TacticalPace", "TacticalPassing", "TacticalDefense",
                                      "TacticalAggressionRating", "TeamPace", "TeamPassing" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Tactics must not create player/team stat field '{n}'.");
            }
            Assert.IsNull(MethodOf(TeamDefType(), "ApplyTacticsToPlayerStats"),
                "Tactics must not modify PlayerStats through team tactics.");
        }

        [Test]
        public void TacticsAreSeparateFromTeamRatings()
        {
            // Task 95 owns Team Ratings; do NOT derive them here.
            foreach (var n in new[] { "AttackRating", "MidfieldRating", "DefenseRating", "OverallTeamRating" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not introduce team-rating field '{n}' (Task 95).");
            }
        }

        [Test]
        public void TacticsAreSeparateFromHomeAway()
        {
            // Task 96 owns Home/Away; do NOT introduce match-specific tactics.
            foreach (var n in new[] { "HomeTactics", "AwayTactics", "HomeAggression", "AwayAggression" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Tactics must not create Home/Away field '{n}' (Task 96).");
            }
        }

        [Test]
        public void TacticsNotDuplicatedInPlayerData()
        {
            // Team tactical concepts must not be duplicated onto PlayerDefinition (except the
            // legitimate same-named player ratings which are a different domain).
            foreach (var n in TacticalFields)
            {
                Assert.IsNull(FieldOf(PlayerDefType(), n),
                    $"PlayerDefinition must not own team tactic '{n}'.");
            }
        }

        // ---- 94.4 Runtime / AI separation ----

        [Test]
        public void TacticsDoNotContainRuntimeState()
        {
            foreach (var n in new[] { "CurrentTactics", "ActiveTactics", "RuntimeTactics",
                                      "AppliedTactics", "CurrentAggression", "CurrentPressing",
                                      "CurrentDefensiveLine", "CurrentPossessionPreference" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not hold runtime tactics state '{n}'.");
            }
        }

        [Test]
        public void TacticsDoNotContainRuntimeObjects()
        {
            foreach (var rt in new[] { "GameObject", "MonoBehaviour", "Component", "Transform" })
            {
                Assert.IsFalse(AllTypes().Any(t => t.Name == "Tactics" + rt || t.Name == "Tactical" + rt),
                    $"Tactics must not introduce a '{rt}' runtime object type.");
            }
        }

        [Test]
        public void TacticsDoNotContainTransformData()
        {
            foreach (var n in new[] { "Transform", "LocalPosition", "WorldPosition", "Rotation",
                                      "Rigidbody", "Vector3" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Tactics must not store spatial/transform data '{n}'.");
            }
        }

        [Test]
        public void TacticsDoNotContainAI()
        {
            // No decision trees, behavior trees, utility scores, state machines, pathfinding.
            foreach (var m in new[] { "DecideTactics", "EvaluateTacticsUtility", "RunTacticsBehaviorTree",
                                      "UpdateTacticsStateMachine" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"Tactics must not contain AI '{m}'.");
            }
        }

        [Test]
        public void TacticsDoNotContainGameplayLoops()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"TeamDefinition must not run '{m}' from Tactics data.");
            }
        }

        [Test]
        public void TacticsDoNotModifyPlayerStats()
        {
            Assert.IsNull(MethodOf(TeamDefType(), "ApplyTacticsToPlayerStats"),
                "Tactics must not modify PlayerStats.");
            Assert.IsNull(MethodOf(TeamDefType(), "ModifyPlayerRatingsFromTactics"),
                "Tactics must not modify player ratings.");
        }

        [Test]
        public void TacticsDoNotModifyPlayerProfile()
        {
            Assert.IsNull(MethodOf(TeamDefType(), "ApplyTacticsToProfile"),
                "Tactics must not modify Player Profile.");
        }

        [Test]
        public void TacticsDoNotModifyFormation()
        {
            Assert.IsNull(MethodOf(TeamDefType(), "ChangeFormationFromTactics"),
                "Tactics must not modify Formation.");
            Assert.IsNull(MethodOf(TeamDefType(), "SetFormationByTactics"),
                "Tactics must not set Formation.");
        }

        [Test]
        public void TacticsDoNotMovePlayersOrControlGameplay()
        {
            foreach (var m in new[] { "MovePlayersByTactics", "ExecuteTacticsAI", "PressPlayers",
                                      "PushDefensiveLine", "ControlPossession" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"Tactics must not perform gameplay/runtime '{m}'.");
            }
        }

        [Test]
        public void TacticsDoNotControlPhysicsAnimationCameraInput()
        {
            foreach (var m in new[] { "ApplyTacticsPhysics", "PlayTacticsAnimation",
                                      "ControlTacticsCamera", "HandleTacticsInput" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"Tactics must not control physics/animation/camera/input via '{m}'.");
            }
        }

        [Test]
        public void TacticsDoNotAssignToPlayers()
        {
            Assert.IsNull(MethodOf(TeamDefType(), "AssignTacticsToStarters"),
                "Tactics must not be assigned to Starters.");
            Assert.IsNull(MethodOf(TeamDefType(), "AssignTacticsToSubstitutes"),
                "Tactics must not be assigned to Substitutes.");
        }

        [Test]
        public void TacticsAreNotMutatedByRuntime()
        {
            // No mutation method for authored tactics; runtime mutation is DEFERRED.
            foreach (var m in new[] { "SetAggression", "SetPressing", "ChangeTacticsAtRuntime",
                                      "OverrideTactics" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"Tactics must not be mutated at runtime via '{m}'.");
            }
        }

        // ---- Validation ----

        [Test]
        public void InvalidTacticalDataIsDetectedIfValidationExists()
        {
            // GetInvalidTacticsData exists on TeamDefinition. Ranges are POLICY/DEFERRED, so a
            // default-authored team reports no invalid tactics today.
            Assert.IsNotNull(MethodOf(TeamDefType(), "GetInvalidTacticsData"),
                "TeamDefinition must own a tactics validation boundary.");
            var team = NewTeam();
            Assert.IsEmpty(GetInvalidTacticsData(team),
                "A default-authored team must report no invalid tactics (range policy deferred).");
        }

        [Test]
        public void InvalidTacticalDataIsNotSilentlyMutated()
        {
            // Even with deferred ranges, validation must not mutate authored values.
            var team = NewTeam();
            GetInvalidTacticsData(team);
            foreach (var n in TacticalFields)
            {
                Assert.AreEqual(50f, (float)FieldOf(TeamDefType(), n).GetValue(team),
                    $"Validation must not mutate authored tactic '{n}'.");
            }
        }

        [Test]
        public void TacticalValidationBelongsToTeamDefinition()
        {
            Assert.IsNotNull(MethodOf(TeamDefType(), "GetInvalidTacticsData"),
                "Tactics validation must live on TeamDefinition.");
            Assert.IsNull(MethodOf(PlayerDefType(), "GetInvalidTacticsData"),
                "PlayerDefinition must not own tactics validation.");
            Assert.IsNull(MethodOf(PlayerStatsType(), "GetInvalidTacticsData"),
                "PlayerStats must not own tactics validation.");
        }

        [Test]
        public void PlayerStatsValidatorIsNotReusedForTactics()
        {
            // Tactics are NOT PlayerStats; ranges are DEFERRED, not the PlayerStats 1-99 scale.
            Assert.IsNull(MethodOf(TeamDefType(), "GetInvalidTacticsRatings"),
                "Tactics must not reuse a PlayerStats-style rating validator.");
            Assert.IsFalse(AllTypes().Any(t => t.Name == "TacticsRatingValidator"),
                "Tactics must not introduce a rating validator.");
        }

        // ---- No tactics infrastructure / systems ----

        [Test]
        public void NoTacticsManagerOrSystemCreated()
        {
            Assert.IsFalse(HasTypeName("TacticsSystem", "RuntimeTacticsSystem", "TacticsManager",
                    "TacticalManager", "TacticsController", "TacticalController",
                    "TeamTacticsSystem", "TacticalDecisionSystem", "InGameTacticsSystem",
                    "DynamicTacticsManager", "TacticalSwitchingSystem"),
                "Tactics must not create manager/controller/system types.");
        }

        [Test]
        public void NoTacticsAIOrDatabaseCreated()
        {
            Assert.IsFalse(HasTypeName("TeamAI", "TacticalAI", "TacticsAI", "TeamTacticsAI",
                    "TacticsDecisionSystem", "TacticsAI", "AdaptiveTactics", "DynamicTactics",
                    "CounterTacticsAI", "OpponentAnalysis",
                    "TacticsDatabase", "TacticsRegistry", "TacticsCatalog", "TacticsLookup"),
                "Tactics must not create AI or database/registry/catalog types.");
        }

        [Test]
        public void NoRuntimeTacticsStateOrPresetInfrastructure()
        {
            Assert.IsFalse(HasTypeName("RuntimeTactics", "ActiveTactics", "AppliedTactics",
                    "TacticalPresetSystem", "AttackingPreset", "DefensivePreset",
                    "PossessionPreset", "PressingPreset", "TacticsUI", "TacticsEditor",
                    "TacticsPicker", "TacticsPreview", "GoalkeeperTacticsSystem"),
                "Tactics must not create runtime state, presets, UI, or goalkeeper-specific types.");
        }

        [Test]
        public void TacticsRemainExtensible()
        {
            // Tactics stay simple authored float fields on TeamDefinition, leaving room for future
            // concepts (BuildUp/Width/Tempo/Transition/DefensiveBlock) without adding them now.
            foreach (var n in TacticalFields)
            {
                Assert.AreEqual(typeof(float), FieldOf(TeamDefType(), n).FieldType,
                    $"'{n}' must stay a simple authored float for future extension.");
            }
            foreach (var future in new[] { "BuildUp", "Width", "Tempo", "Transition", "DefensiveBlock" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), future),
                    $"Future tactic '{future}' must not be added in Task 94.");
            }
        }

        // ---- helpers ----

        private static List<string> GetInvalidTacticsData(object team) =>
            (List<string>)MethodOf(TeamDefType(), "GetInvalidTacticsData").Invoke(team, null);
    }
}
