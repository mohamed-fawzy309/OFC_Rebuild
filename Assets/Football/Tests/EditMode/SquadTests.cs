using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 92 — Squad (membership owned by TeamDefinition).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance + the real GetInvalidSquadData API).
    ///
    /// Task 92 locks:
    ///   - TeamDefinition owns SQUAD MEMBERSHIP via PlayerDefinition[] Starters / Substitutes
    ///     references. PlayerDefinition remains the single authoritative source of player data.
    ///   - The squad MUST NOT copy/embed player data, player identity, or PlayerStats.
    ///   - Membership is a PlayerDefinition reference, not a PlayerId-string copy, array index, or
    ///     runtime GameObject.
    ///   - Squad validation (GetInvalidSquadData on TeamDefinition) DETECTS + REPORTS: null
    ///     references, duplicates within a list, and Starter/Substitute overlap — never mutating.
    ///   - No runtime lineup/current-state, no formation positions, no tactical instructions, and no
    ///     squad/roster/transfer/lineup/substitution/manager systems.
    /// </summary>
    public class SquadTests
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

        private static Type IdentityType() => FindType(Prefix + "Identity");

        private static object NewTeam() => ScriptableObject.CreateInstance(TeamDefType());

        private static object NewPlayer() => ScriptableObject.CreateInstance(PlayerDefType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static MethodInfo MethodOf(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              BindingFlags.Static);

        private static List<string> GetInvalidSquadData(object team)
        {
            return (List<string>)MethodOf(TeamDefType(), "GetInvalidSquadData").Invoke(team, null);
        }

        private static void SetSquad(object team, string listName, object[] players)
        {
            var elementType = TeamDefType().GetField(listName).FieldType.GetElementType();
            var arr = Array.CreateInstance(elementType, players.Length);
            for (var i = 0; i < players.Length; i++)
            {
                arr.SetValue(players[i], i);
            }
            FieldOf(TeamDefType(), listName).SetValue(team, arr);
        }

        private static object[] Refs(params object[] players) => players;

        private static void SetPlayerId(object player, string playerId)
        {
            var identity = FieldOf(PlayerDefType(), "Identity").GetValue(player);
            FieldOf(IdentityType(), "PlayerId").SetValue(identity, playerId);
        }

        private static string PlayerIdOf(object player)
        {
            var identity = FieldOf(PlayerDefType(), "Identity").GetValue(player);
            return (string)FieldOf(IdentityType(), "PlayerId").GetValue(identity);
        }

        // ---- 92.1 Squad ownership ----

        [Test]
        public void TeamDefinitionOwnsSquadMembership()
        {
            Assert.IsNotNull(FieldOf(TeamDefType(), "Starters"),
                "TeamDefinition must own Starters.");
            Assert.IsNotNull(FieldOf(TeamDefType(), "Substitutes"),
                "TeamDefinition must own Substitutes.");
        }

        [Test]
        public void StartersUsePlayerDefinitionReferences()
        {
            var f = FieldOf(TeamDefType(), "Starters");
            Assert.IsTrue(f.FieldType.IsArray, "Starters must be an array.");
            Assert.AreEqual(PlayerDefType(), f.FieldType.GetElementType(),
                "Starters must be PlayerDefinition[], not runtime objects.");
        }

        [Test]
        public void SubstitutesUsePlayerDefinitionReferences()
        {
            var f = FieldOf(TeamDefType(), "Substitutes");
            Assert.IsTrue(f.FieldType.IsArray, "Substitutes must be an array.");
            Assert.AreEqual(PlayerDefType(), f.FieldType.GetElementType(),
                "Substitutes must be PlayerDefinition[], not runtime objects.");
        }

        [Test]
        public void SquadDoesNotEmbedPlayerData()
        {
            foreach (var n in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending",
                                      "Physical", "Goalkeeping", "OverallRating", "WeakFoot", "Name" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not embed player-data field '{n}' in its squad.");
            }
        }

        [Test]
        public void SquadDoesNotOwnPlayerStats()
        {
            Assert.IsNull(FieldOf(TeamDefType(), "PlayerStats"),
                "TeamDefinition must not own PlayerStats in its squad.");
        }

        [Test]
        public void SquadUsesReferenceIdentity_RatherThanIdCopy()
        {
            // Membership is a PlayerDefinition reference; no separate PlayerId-string squad field.
            Assert.IsNull(FieldOf(TeamDefType(), "SquadPlayerId"),
                "Squad must not store a copied PlayerId string.");
            Assert.IsNull(FieldOf(TeamDefType(), "PlayerIds"),
                "Squad must not store a PlayerId array instead of PlayerDefinition references.");
        }

        [Test]
        public void SquadDoesNotCreatePlayerCopyTypes()
        {
            Assert.IsFalse(HasTypeName("SquadPlayerDefinition", "TeamPlayerDefinition",
                    "EmbeddedPlayer", "EmbeddedPlayerData", "PlayerCopy", "SquadPlayer"),
                "Team must represent squad membership via PlayerDefinition references, not copy types.");
        }

        // ---- 92.3 Starters / Substitutes integrity ----

        [Test]
        public void ValidSquadHasNoProblems()
        {
            var team = NewTeam();
            Assert.IsEmpty(GetInvalidSquadData(team),
                "An empty squad should report no problems.");
        }

        [Test]
        public void DuplicateStartersAreDetected()
        {
            var team = NewTeam();
            var p = NewPlayer(); SetPlayerId(p, "P001");
            // P001 appears twice in Starters.
            SetSquad(team, "Starters", Refs(p, p));
            var problems = GetInvalidSquadData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("P001") && x.Contains("multiple times") && x.Contains("Starters")),
                "Duplicate Starters reference must be detected and reported.");
        }

        [Test]
        public void DuplicateSubstitutesAreDetected()
        {
            var team = NewTeam();
            var p = NewPlayer(); SetPlayerId(p, "P002");
            SetSquad(team, "Substitutes", Refs(p, p));
            var problems = GetInvalidSquadData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("P002") && x.Contains("multiple times") && x.Contains("Substitutes")),
                "Duplicate Substitutes reference must be detected and reported.");
        }

        [Test]
        public void DistinctReferencesAreNotDuplicates()
        {
            var team = NewTeam();
            var p1 = NewPlayer(); SetPlayerId(p1, "P001");
            var p2 = NewPlayer(); SetPlayerId(p2, "P002");
            SetSquad(team, "Starters", Refs(p1, p2));
            SetSquad(team, "Substitutes", Refs(p1));
            var problems = GetInvalidSquadData(team);
            Assert.IsEmpty(problems.Where(x => x.Contains("multiple times")).ToList(),
                "Distinct PlayerDefinition references must not be reported as duplicates.");
        }

        [Test]
        public void StarterAndSubstituteOverlapIsDetected()
        {
            var team = NewTeam();
            var p = NewPlayer(); SetPlayerId(p, "P003");
            SetSquad(team, "Starters", Refs(p));
            SetSquad(team, "Substitutes", Refs(p));
            var problems = GetInvalidSquadData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("P003") && x.Contains("both Starters and Substitutes")),
                "A player in both Starters and Substitutes must be detected and reported.");
        }

        [Test]
        public void NullStarterIsDetected()
        {
            var team = NewTeam();
            SetSquad(team, "Starters", new object[] { null });
            var problems = GetInvalidSquadData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("Starters") && x.Contains("null")),
                "A null Starters reference must be detected and reported.");
        }

        [Test]
        public void NullSubstituteIsDetected()
        {
            var team = NewTeam();
            SetSquad(team, "Substitutes", new object[] { null });
            var problems = GetInvalidSquadData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("Substitutes") && x.Contains("null")),
                "A null Substitutes reference must be detected and reported.");
        }

        [Test]
        public void SquadValidationDoesNotMutateSquad()
        {
            var team = NewTeam();
            var p = NewPlayer(); SetPlayerId(p, "P004");
            SetSquad(team, "Starters", Refs(p, p)); // duplicate
            SetSquad(team, "Substitutes", Refs(p)); // overlap
            var before = GetInvalidSquadData(team);
            // Validation runs again; original references/counts must be unchanged.
            var starters = (Array)FieldOf(TeamDefType(), "Starters").GetValue(team);
            var subs = (Array)FieldOf(TeamDefType(), "Substitutes").GetValue(team);
            Assert.AreEqual(2, starters.Length, "Validation must not remove duplicates.");
            Assert.AreEqual(1, subs.Length, "Validation must not remove overlap.");
            Assert.IsTrue(before.Count > 0, "Invalid squad must be reported.");
        }

        [Test]
        public void SquadValidationDoesNotMutatePlayerDefinition()
        {
            var team = NewTeam();
            var p = NewPlayer(); SetPlayerId(p, "P005");
            var other = NewPlayer(); SetPlayerId(other, "P006");
            SetSquad(team, "Starters", Refs(p));
            GetInvalidSquadData(team);
            Assert.AreEqual("P005", PlayerIdOf(p),
                "Squad validation must not mutate a referenced PlayerDefinition's identity.");
            Assert.AreEqual("P006", PlayerIdOf(other),
                "Unreferenced players must remain untouched.");
        }

        [Test]
        public void SquadValidationOwnsTeamDefinition()
        {
            Assert.IsNotNull(MethodOf(TeamDefType(), "GetInvalidSquadData"),
                "Squad validation must live on TeamDefinition.");
            Assert.IsNull(MethodOf(PlayerDefType(), "GetInvalidSquadData"),
                "PlayerDefinition must not own squad validation.");
            Assert.IsNull(MethodOf(PlayerStatsType(), "GetInvalidSquadData"),
                "PlayerStats must not own squad validation.");
        }

        [Test]
        public void SquadValidationReportsActionableMessages()
        {
            var team = NewTeam();
            var p = NewPlayer(); SetPlayerId(p, "P007");
            SetSquad(team, "Starters", Refs(p, p));
            var problems = GetInvalidSquadData(team);
            Assert.IsTrue(problems.All(x => x.Length > 0), "Messages must be non-empty and descriptive.");
            Assert.IsFalse(problems.Contains("Invalid squad."),
                "Validation must avoid vague 'Invalid squad.' messages.");
        }

        // ---- Boundaries: Formation / Tactics / Runtime / Systems ----

        [Test]
        public void SquadDoesNotStoreFormationPositions()
        {
            foreach (var n in new[] { "PlayerSlot", "FormationPosition", "TacticalPosition", "SlotX", "SlotY", "SquadPosition" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Squad must not store formation position '{n}' (Formation is Task 93).");
            }
        }

        [Test]
        public void SquadDoesNotStoreTacticalInstructions()
        {
            foreach (var n in new[] { "PressingRole", "MarkingAssignment", "AttackInstruction", "DefensiveInstruction" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Squad must not store tactical instruction '{n}' (Tactics is Task 94).");
            }
        }

        [Test]
        public void SquadDoesNotCreateRuntimeLineup()
        {
            foreach (var n in new[] { "CurrentStarters", "CurrentSubstitutes", "ActiveLineup", "RuntimeSquad", "MatchdaySquad" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not hold runtime lineup state '{n}'.");
            }
        }

        [Test]
        public void SquadDoesNotUseRuntimePlayerObjects()
        {
            foreach (var rt in new[] { "GameObject", "PlayerController", "MonoBehaviour" })
            {
                Assert.IsFalse(AllTypes().Any(t => t.FullName == Prefix + rt),
                    $"Squad must not rely on a runtime '{rt}' player type.");
            }
        }

        [Test]
        public void GoalkeepersUseNormalPlayerDefinitionReference()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperSquadEntry", "GoalkeeperSquadPlayer"),
                "Goalkeepers must remain PlayerDefinition, without goalkeeper-specific squad data.");
        }

        [Test]
        public void SquadDoesNotCreateManagerOrSystemTypes()
        {
            Assert.IsFalse(HasTypeName("SquadManager", "RosterManager", "SquadController", "RuntimeSquadManager",
                    "LineupManager", "StartingElevenController", "SubstitutionManager",
                    "TransferSystem", "TransferManager", "LoanSystem", "FreeAgentSystem",
                    "MatchdaySquadSystem", "MatchRosterSystem",
                    "ClubReferenceSynchronizer", "TeamMembershipSync", "PlayerTransferSystem"),
                "Squad membership must not create manager/roster/transfer/lineup/substitution systems.");
        }

        [Test]
        public void PlayerDefinitionRemainsSingleAuthoritativePlayerData()
        {
            // Identity / PhysicalProfile / Profile / PlayerStats stay on PlayerDefinition.
            foreach (var n in new[] { "Identity", "Physical", "Profile", "PlayerStats" })
            {
                Assert.IsNotNull(FieldOf(PlayerDefType(), n),
                    $"PlayerDefinition must keep owning {n}.");
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition squad must not take over {n}.");
            }
        }

        [Test]
        public void SquadDoesNotOverridePlayerPrimaryPosition()
        {
            // Squad membership does not rewrite Profile.PrimaryPosition.
            Assert.IsNull(MethodOf(TeamDefType(), "AssignSquadPositions"),
                "TeamDefinition must not assign formation/squad positions to players.");
        }

        [Test]
        public void SquadHasNoRuntimeLoops()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"TeamDefinition must not run '{m}' from squad data.");
            }
        }
    }
}
