using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 93 — Formation (TEAM SHAPE, owned by TeamDefinition).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance + the real GetInvalidFormationData API).
    ///
    /// Task 93 locks:
    ///   - Formation is AUTHORED TEAM SHAPE data on TeamDefinition (authored identifier string with
    ///     an established default), NOT Squad membership (WHO) and NOT Tactics (HOW).
    ///   - Formation never modifies PlayerDefinition (PrimaryPosition/SecondaryPositions/PlayStyle),
    ///     never owns/copies PlayerDefinition, never controls Squad.
    ///   - No team ratings, no Home/Away, no runtime player positions, no Transform/GameObject data,
    ///     no movement/AI/physics/animation control, no formation system/manager/controller/database/
    ///     registry/UI.
    ///   - Validation (GetInvalidFormationData on TeamDefinition) DETECTS + REPORTS empty/whitespace
    ///     formation and never mutates.
    /// </summary>
    public class FormationTests
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

        private static List<string> GetInvalidFormationData(object team)
        {
            return (List<string>)MethodOf(TeamDefType(), "GetInvalidFormationData").Invoke(team, null);
        }

        private static void SetFormation(object team, string value)
        {
            FieldOf(TeamDefType(), "Formation").SetValue(team, value);
        }

        private static string FormationOf(object team) =>
            (string)FieldOf(TeamDefType(), "Formation").GetValue(team);

        // ---- 93.1 Representation ----

        [Test]
        public void FormationExists()
        {
            Assert.IsNotNull(FieldOf(TeamDefType(), "Formation"),
                "TeamDefinition must have a Formation field.");
        }

        [Test]
        public void FormationBelongsToTeamDefinition()
        {
            Assert.AreEqual(TeamDefType(), FieldOf(TeamDefType(), "Formation").DeclaringType,
                "Formation must be declared on TeamDefinition.");
        }

        [Test]
        public void FormationIsAuthoredData()
        {
            var f = FieldOf(TeamDefType(), "Formation");
            Assert.IsTrue((f.Attributes & FieldAttributes.Public) != 0,
                "Formation must be a public authored field.");
            Assert.IsTrue((f.Attributes & FieldAttributes.Static) == 0,
                "Formation must be instance (authored per-team) data.");
        }

        [Test]
        public void FormationIsSerialized()
        {
            // Formation is a public serialized field on the ScriptableObject TeamDefinition.
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(TeamDefType()),
                "TeamDefinition must remain a ScriptableObject.");
            var f = FieldOf(TeamDefType(), "Formation");
            Assert.IsTrue((f.Attributes & FieldAttributes.Public) != 0,
                "Formation must serialize as a public field.");
        }

        [Test]
        public void FormationRepresentationUsesEstablishedType()
        {
            // The established representation is an authored string identifier ("4-3-3", "4-4-2").
            Assert.AreEqual(typeof(string), FieldOf(TeamDefType(), "Formation").FieldType,
                "Formation must use the established string identifier representation.");
        }

        [Test]
        public void FormationHasEstablishedDefault()
        {
            // Pre-existing authored default "4-4-2" is preserved (established, not random).
            var team = NewTeam();
            Assert.AreEqual("4-4-2", FormationOf(team),
                "Formation must keep its established authored default.");
        }

        // ---- 93.2 / 93.3 Separation ----

        [Test]
        public void FormationIsSeparateFromSquad()
        {
            // Squad (Starters/Substitutes) stays PlayerDefinition[]; Formation stays a shape identifier.
            foreach (var n in new[] { "Starters", "Substitutes" })
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsNotNull(f, $"{n} must remain on TeamDefinition.");
                Assert.IsTrue(f.FieldType.IsArray, $"{n} must remain an array.");
                Assert.AreEqual(typeof(string), FieldOf(TeamDefType(), "Formation").FieldType,
                    "Formation must remain a shape identifier, not a squad array.");
            }
        }

        [Test]
        public void FormationDoesNotOwnPlayerData()
        {
            foreach (var n in new[] { "Identity", "PlayerStats", "Profile", "Physical",
                                      "Pace", "Shooting", "OverallRating", "PlayerName" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Formation must not own player-data field '{n}'.");
            }
        }

        [Test]
        public void FormationDoesNotDuplicatePlayerDefinition()
        {
            Assert.IsFalse(HasTypeName("FormationSlot", "FormationPlayer", "FormationLineup",
                    "FormationPlayerDefinition", "FormationPositionRecord"),
                "Formation must not introduce duplicate player/slot copy types.");
        }

        [Test]
        public void FormationDoesNotOverridePlayerPositions()
        {
            Assert.IsNull(MethodOf(TeamDefType(), "AssignFormationPositions"),
                "Formation must not override PlayerDefinition.PrimaryPosition/SecondaryPositions.");
            Assert.IsNull(MethodOf(TeamDefType(), "ApplyFormationToPlayers"),
                "Formation must not apply positions to players.");
        }

        [Test]
        public void FormationDoesNotModifyPlayStyle()
        {
            Assert.IsNull(MethodOf(TeamDefType(), "SetPlayStyleFromFormation"),
                "Formation must not modify PlayerDefinition.Profile.PlayStyle.");
        }

        [Test]
        public void FormationDoesNotContainTactics()
        {
            // Tactics are separate authored fields (Task 94); Formation carries no tactic behavior.
            Assert.IsNull(FieldOf(TeamDefType(), "FormationPressing"),
                "Formation must not embed tactic data.");
            Assert.IsNull(FieldOf(TeamDefType(), "FormationAggression"),
                "Formation must not embed tactic data.");
            Assert.IsNull(MethodOf(TeamDefType(), "ExecutePressingFromFormation"),
                "Formation must not execute tactics.");
        }

        [Test]
        public void FormationDoesNotContainTeamRatings()
        {
            foreach (var n in new[] { "AttackRating", "MidfieldRating", "DefenseRating", "TeamOverallRating" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Formation must not create team-rating field '{n}' (Task 95).");
            }
        }

        [Test]
        public void FormationDoesNotContainHomeAwayState()
        {
            foreach (var n in new[] { "HomeFormation", "AwayFormation" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Formation must not create Home/Away field '{n}' (Task 96).");
            }
        }

        [Test]
        public void FormationDoesNotContainRuntimePlayerPositions()
        {
            foreach (var n in new[] { "CurrentFormation", "RuntimeFormation", "ActiveFormation",
                                      "MatchFormation", "CurrentPlayerPositions",
                                      "CurrentFormationPositions", "AppliedFormation",
                                      "RuntimeFormationSlots", "CurrentFormationPosition",
                                      "RuntimeFormationPosition", "CurrentSlot", "ActiveFormationSlot" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not hold runtime formation/position state '{n}'.");
            }
        }

        [Test]
        public void FormationDoesNotContainTransformData()
        {
            foreach (var n in new[] { "Transform", "LocalPosition", "WorldPosition", "Rotation",
                                      "Rigidbody", "GameObject", "FormationPosition" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Formation must not store spatial/transform data '{n}'.");
            }
            foreach (var rt in new[] { "Transform", "GameObject", "Rigidbody", "Vector3" })
            {
                Assert.IsFalse(AllTypes().Any(t => t.Name == "Formation" + rt),
                    $"Formation must not introduce a '{rt}' spatial type.");
            }
        }

        // ---- 93.4 Validation ----

        [Test]
        public void ValidFormationHasNoProblems()
        {
            var team = NewTeam();
            SetFormation(team, "4-3-3");
            Assert.IsEmpty(GetInvalidFormationData(team),
                "A valid authored formation must report no problems.");
        }

        [Test]
        public void EmptyFormationIsDetected()
        {
            var team = NewTeam();
            SetFormation(team, "");
            var problems = GetInvalidFormationData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("Formation is empty")),
                "An empty formation must be detected and reported.");
        }

        [Test]
        public void WhitespaceFormationIsDetected()
        {
            var team = NewTeam();
            SetFormation(team, "   ");
            var problems = GetInvalidFormationData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("Formation is whitespace")),
                "A whitespace formation must be detected and reported.");
        }

        [Test]
        public void InvalidFormationIsNotSilentlyMutated()
        {
            var team = NewTeam();
            SetFormation(team, "");
            GetInvalidFormationData(team);
            Assert.AreEqual("", FormationOf(team),
                "Validation must not auto-fill a default formation (e.g. 4-3-3).");
        }

        [Test]
        public void FormationValidationBelongsToTeamData()
        {
            Assert.IsNotNull(MethodOf(TeamDefType(), "GetInvalidFormationData"),
                "Formation validation must live on TeamDefinition.");
            Assert.IsNull(MethodOf(PlayerDefType(), "GetInvalidFormationData"),
                "PlayerDefinition must not own formation validation.");
            Assert.IsNull(MethodOf(PlayerStatsType(), "GetInvalidFormationData"),
                "PlayerStats must not own formation validation.");
        }

        // ---- No formation systems / gameplay ----

        [Test]
        public void FormationDoesNotCreateFormationSystems()
        {
            Assert.IsFalse(HasTypeName("FormationController", "FormationManager", "FormationSystem",
                    "RuntimeFormationSystem", "FormationRuntime", "FormationAI",
                    "FormationDecisionSystem", "FormationTacticalAI",
                    "FormationMovement", "FormationPositionController", "FormationRepositionSystem",
                    "FormationDatabase", "FormationRegistry", "FormationCatalog", "FormationLookup",
                    "FormationPresetSystem", "FormationUI", "FormationEditor", "FormationPicker",
                    "FormationPreview"),
                "Formation must not create controller/manager/system/AI/movement/database/registry/UI types.");
        }

        [Test]
        public void FormationDoesNotMovePlayersOrControlGameplay()
        {
            foreach (var m in new[] { "MovePlayersToFormation", "ExecuteFormationAI",
                                      "ApplyFormationPhysics", "PlayFormationAnimation" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"Formation must not perform gameplay/runtime '{m}'.");
            }
        }

        [Test]
        public void FormationHasNoRuntimeLoops()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"TeamDefinition must not run '{m}' from Formation data.");
            }
        }

        [Test]
        public void FormationDoesNotControlSquadMembership()
        {
            Assert.IsNull(MethodOf(TeamDefType(), "ReorderSquadByFormation"),
                "Formation must not modify Squad membership/order.");
            Assert.IsNull(MethodOf(TeamDefType(), "AssignStartersToFormationSlots"),
                "Starter-to-formation-slot assignment must remain DEFERRED, not auto-applied.");
        }

        [Test]
        public void FormationRemainsExtensible()
        {
            // Formation stays a simple authored field, leaving room for Tasks 94-96.
            Assert.AreEqual(typeof(string), FieldOf(TeamDefType(), "Formation").FieldType,
                "Formation must stay a simple authored field for future extension.");
        }
    }
}
