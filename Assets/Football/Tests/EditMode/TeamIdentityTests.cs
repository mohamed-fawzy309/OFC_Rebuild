using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 90 — Team Identity (owned by TeamDefinition).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance + the real validation API).
    ///
    /// Task 90 locks:
    ///   - TeamId is authored, stable, deterministic, a string (not an int/Guid/instance-id/array
    ///     index), independent of TeamName and runtime object identity; never runtime-generated.
    ///   - TeamName is authored human-readable data, NOT the stable key, NOT derived.
    ///   - Team Identity belongs to TeamDefinition, NOT PlayerDefinition; PlayerId and TeamId are
    ///     completely separate concepts.
    ///   - Validation lives on TeamDefinition (GetInvalidTeamIdentity) in the team-data boundary:
    ///     DETECTS + REPORTS empty/whitespace TeamId/TeamName, NEVER mutates, never auto-fixes.
    ///   - Duplicate TeamIds are detected by an explicit minimal utility (FindDuplicateTeamIds), NOT
    ///     a database/registry.
    ///   - No runtime state/objects/loops/gameplay; no team manager/database/registry/localization/
    ///     country systems; extensible for Tasks 91-96.
    /// </summary>
    public class TeamIdentityTests
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

        private static object NewTeam() => ScriptableObject.CreateInstance(TeamDefType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static MethodInfo MethodOf(Type t, string name, bool isStatic = false) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              (isStatic ? BindingFlags.Static : 0));

        private static List<string> GetInvalidTeamIdentity(object team)
        {
            return (List<string>)MethodOf(TeamDefType(), "GetInvalidTeamIdentity")
                .Invoke(team, null);
        }

        private static void SetField(object team, string name, string value) =>
            FieldOf(TeamDefType(), name).SetValue(team, value);

        // ---- 90.1 Team ID ----

        [Test]
        public void TeamIdentityHasTeamId()
        {
            Assert.IsNotNull(FieldOf(TeamDefType(), "TeamId"), "TeamDefinition must have a TeamId field.");
        }

        [Test]
        public void TeamIdUsesStableAuthoredString()
        {
            var f = FieldOf(TeamDefType(), "TeamId");
            Assert.AreEqual(typeof(string), f.FieldType,
                "TeamId must be a string (authored, stable, can carry codes like ARS/FCB), not int/Guid.");
        }

        [Test]
        public void TeamIdIsNotRuntimeGenerated()
        {
            // No auto-generation method exists on the definition; TeamId is a plain serialized string.
            Assert.IsNull(MethodOf(TeamDefType(), "GenerateTeamId"),
                "TeamId must not be generated at runtime.");
            Assert.IsNull(MethodOf(TeamDefType(), "AutoAssignTeamId"),
                "TeamId must not be auto-assigned.");
        }

        [Test]
        public void TeamIdIsIndependentOfTeamName()
        {
            var idField = FieldOf(TeamDefType(), "TeamId");
            var nameField = FieldOf(TeamDefType(), "TeamName");
            // Separate, independently-authored field concepts (stable key vs display name).
            Assert.AreNotEqual(idField.Name, nameField.Name,
                "TeamId and TeamName must be separate fields (not the same field).");
            Assert.AreEqual(typeof(string), idField.FieldType,
                "TeamId is a string (independent authored identity).");
            Assert.AreEqual(typeof(string), nameField.FieldType,
                "TeamName is a string (independent authored display name).");
            Assert.AreNotEqual(idField, nameField,
                "TeamId and TeamName must not be the same field instance.");
        }

        // ---- 90.2 Team Name ----

        [Test]
        public void TeamIdentityHasTeamName()
        {
            Assert.IsNotNull(FieldOf(TeamDefType(), "TeamName"),
                "TeamDefinition must have a TeamName field.");
        }

        [Test]
        public void TeamNameIsAuthoredString()
        {
            Assert.AreEqual(typeof(string), FieldOf(TeamDefType(), "TeamName").FieldType,
                "TeamName must be an authored string, not runtime-generated.");
        }

        [Test]
        public void ExistingShortNameRemainsOwnedByTeamDefinition()
        {
            var f = FieldOf(TeamDefType(), "ShortName");
            Assert.IsNotNull(f, "Pre-existing ShortName identity field must be preserved.");
            Assert.AreEqual(typeof(string), f.FieldType);
        }

        // ---- 90.3 Team Identity Data / ownership ----

        [Test]
        public void TeamIdentityBelongsToTeamDefinition()
        {
            foreach (var n in new[] { "TeamId", "TeamName", "ShortName" })
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsNotNull(f, $"{n} must live on TeamDefinition.");
                Assert.AreEqual(TeamDefType(), f.DeclaringType, $"{n} must be declared on TeamDefinition.");
            }
        }

        [Test]
        public void TeamIdentityDoesNotBelongToPlayerDefinition()
        {
            foreach (var n in new[] { "TeamId", "TeamName", "ShortName" })
            {
                Assert.IsNull(FieldOf(PlayerDefType(), n), $"PlayerDefinition must NOT own '{n}'.");
            }
            // Identity group holds only player identity (PlayerId/Name), never team identity.
            Assert.IsNull(FieldOf(IdentityType(), "TeamId"), "Identity must not own TeamId.");
            Assert.IsNull(FieldOf(IdentityType(), "TeamName"), "Identity must not own TeamName.");
        }

        [Test]
        public void PlayerIdAndTeamIdRemainSeparate()
        {
            Assert.IsNotNull(FieldOf(IdentityType(), "PlayerId"), "Identity must own PlayerId.");
            var pid = FieldOf(IdentityType(), "PlayerId");
            Assert.AreEqual(typeof(string), pid.FieldType, "PlayerId is a string identity.");
            // The team's identity field is NOT named PlayerId and does not live on Identity/Player.
            Assert.IsNull(FieldOf(TeamDefType(), "PlayerId"), "TeamDefinition must not carry a PlayerId.");
            Assert.IsNotNull(FieldOf(TeamDefType(), "TeamId"), "Team identity is TeamId.");
        }

        [Test]
        public void TeamIdentityDoesNotContainPlayerStats()
        {
            var psFullName = PlayerStatsType().FullName;
            foreach (var f in TeamDefType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsFalse(f.FieldType.FullName == psFullName,
                    "Team identity must not embed PlayerStats.");
            }
        }

        [Test]
        public void ClubReferenceRelationshipRemainsValid()
        {
            var club = FieldOf(IdentityType(), "ClubReference");
            Assert.IsNotNull(club, "Identity must keep ClubReference.");
            Assert.AreEqual(TeamDefType().FullName, club.FieldType.FullName,
                "ClubReference must remain a TeamDefinition reference; team identity stays on TeamDefinition.");
        }

        // ---- 90.4 Validation ----

        [Test]
        public void EmptyTeamIdIsDetected()
        {
            var team = NewTeam();
            SetField(team, "TeamName", "Arsenal");
            SetField(team, "TeamId", "");
            var problems = GetInvalidTeamIdentity(team);
            Assert.IsTrue(problems.Any(p => p.Contains("TeamId")),
                "Empty TeamId must be reported. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void WhitespaceTeamIdIsDetected()
        {
            var team = NewTeam();
            SetField(team, "TeamName", "Arsenal");
            SetField(team, "TeamId", "   ");
            var problems = GetInvalidTeamIdentity(team);
            Assert.IsTrue(problems.Any(p => p.Contains("TeamId") && p.ToLowerInvariant().Contains("whitespace")),
                "Whitespace TeamId must be reported. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void EmptyTeamNameIsDetected()
        {
            var team = NewTeam();
            SetField(team, "TeamId", "ARS");
            SetField(team, "TeamName", "");
            var problems = GetInvalidTeamIdentity(team);
            Assert.IsTrue(problems.Any(p => p.Contains("TeamName")),
                "Empty TeamName must be reported. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void WhitespaceTeamNameIsDetected()
        {
            var team = NewTeam();
            SetField(team, "TeamId", "ARS");
            SetField(team, "TeamName", " \t ");
            var problems = GetInvalidTeamIdentity(team);
            Assert.IsTrue(problems.Any(p => p.Contains("TeamName") && p.ToLowerInvariant().Contains("whitespace")),
                "Whitespace TeamName must be reported. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void ValidTeamIdentityHasNoProblems()
        {
            var team = NewTeam();
            SetField(team, "TeamId", "ARS");
            SetField(team, "TeamName", "Arsenal");
            Assert.IsEmpty(GetInvalidTeamIdentity(team),
                "A valid team identity (non-empty TeamId + TeamName) must produce no problems.");
        }

        [Test]
        public void InvalidTeamIdentityIsNotSilentlyMutated()
        {
            var team = NewTeam();
            SetField(team, "TeamName", "  ");
            SetField(team, "TeamId", "");
            GetInvalidTeamIdentity(team);
            Assert.AreEqual("  ", FieldOf(TeamDefType(), "TeamName").GetValue(team),
                "Validation must NOT replace/rename the TeamName.");
            Assert.AreEqual("", FieldOf(TeamDefType(), "TeamId").GetValue(team),
                "Validation must NOT auto-generate/replace the TeamId.");
        }

        [Test]
        public void DuplicateTeamIdsAreDetected()
        {
            var m = MethodOf(TeamDefType(), "FindDuplicateTeamIds", isStatic: true);
            Assert.IsNotNull(m, "FindDuplicateTeamIds explicit utility must exist.");

            var arr = Array.CreateInstance(TeamDefType(), 4);
            arr.SetValue(MakeTeam("ARS"), 0);
            arr.SetValue(MakeTeam("FCB"), 1);
            arr.SetValue(MakeTeam("ARS"), 2);
            arr.SetValue(MakeTeam("TOT"), 3);

            var duplicates = (List<string>)m.Invoke(null, new object[] { arr });
            Assert.IsTrue(duplicates.Any(d => d == "ARS"),
                "Duplicate TeamId 'ARS' must be detected. Got: " + string.Join("; ", duplicates));
            Assert.IsFalse(duplicates.Any(d => d == "FCB" || d == "TOT"),
                "Non-duplicate TeamIds must not be reported.");
        }

        [Test]
        public void TeamIdUniquenessUsesExplicitBoundary()
        {
            // Uniqueness is an explicit static utility on TeamDefinition (no database/registry).
            Assert.IsNotNull(MethodOf(TeamDefType(), "FindDuplicateTeamIds", isStatic: true));
            Assert.IsFalse(HasTypeName("TeamDatabase", "TeamRegistry", "GlobalTeamLookup", "TeamCatalog"),
                "TeamId uniqueness must NOT use a database/registry.");
        }

        [Test]
        public void TeamIdentityValidationOwnerIsTeamDefinition()
        {
            Assert.IsNotNull(MethodOf(TeamDefType(), "GetInvalidTeamIdentity"),
                "Team Identity validation must live on TeamDefinition.");
            Assert.IsNull(MethodOf(PlayerDefType(), "GetInvalidTeamIdentity"),
                "Team Identity validation must NOT live on PlayerDefinition.");
        }

        // ---- Boundaries ----

        [Test]
        public void TeamIdentityContainsNoRuntimeState()
        {
            foreach (var f in TeamDefType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(f.Name.StartsWith("Current", StringComparison.Ordinal),
                    $"No runtime team state field '{f.Name}' may exist.");
            }
        }

        [Test]
        public void TeamIdentityFieldsAreNotRuntimeObjects()
        {
            foreach (var n in new[] { "TeamId", "TeamName", "ShortName" })
            {
                Assert.AreEqual(typeof(string), FieldOf(TeamDefType(), n).FieldType,
                    $"Identity field '{n}' must be a plain string, not a runtime object.");
            }
        }

        [Test]
        public void TeamIdentityContainsNoGameplayLogic()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"TeamDefinition must not run '{m}' runtime loops.");
            }
        }

        [Test]
        public void TeamIdentityDoesNotCreateIdentityInfrastructure()
        {
            Assert.IsFalse(HasTypeName("TeamManager", "RuntimeTeamManager", "TeamIdentityManager",
                "TeamDatabase", "TeamRegistry", "LocalizationSystem", "LocalizedTeamName",
                "TeamNameDatabase", "CountryDatabase", "CountryDefinition", "NationSystem"),
                "No team manager/database/registry/localization/country infrastructure may be created.");
        }

        [Test]
        public void TeamIdentityIsAuthoredAndSerializable()
        {
            // TeamDefinition is an authored asset; identity fields are plain serialized strings.
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(TeamDefType()),
                "TeamDefinition must remain an authored ScriptableObject asset.");
        }

        [Test]
        public void TeamIdentitySupportsFutureTeamData()
        {
            // Identity is plain authored scalar data with no runtime coupling, so Tasks 91-96
            // (Colors/Squad/Formation/Tactics/Ratings/HomeAway) can be added without redesign.
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(TeamDefType()));
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(TeamDefType()));
        }

        private static object MakeTeam(string teamId)
        {
            var team = NewTeam();
            SetField(team, "TeamId", teamId);
            SetField(team, "TeamName", "Team " + teamId);
            return team;
        }
    }
}
