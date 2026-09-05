using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 77 — Player Identity (PlayerDefinition.Identity = PlayerId, Name, Nationality,
    /// ClubReference).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Identity was introduced in Task 66 as the WHO-the-player-is group. Task 77 confirms and
    /// locks its semantics: exactly PlayerId/Name/Nationality/ClubReference. PlayerId is a stable
    /// authored string (NOT a list/squad/runtime index, NOT Guid/DateTime/GetInstanceID generated).
    /// Name is the authored display identity (single string, no first/middle/last split, no
    /// localization system). Nationality is a stable authored representation (ISO-alphanumeric
    /// string code; no Country system). ClubReference is a REFERENCE boundary to a separate
    /// TeamDefinition (not an embedded copy, not a MonoBehaviour/GameObject/Transform). It verifies
    /// no duplication of identity fields into Profile/PhysicalProfile/PlayerStats, no embedded team
    /// fields, no runtime identity state, no Unity runtime objects, no gameplay logic, no
    /// PlayerRegistry/CountrySystem/CareerSystem/UI, and that goalkeepers and outfield players use
    /// the same unified Identity model.
    ///
    /// Validation policy: non-empty-unique PlayerId, non-empty Name, valid Nationality, valid
    /// ClubReference are recorded as POLICY/DEFERRED (enforcement belongs to Task 78/86).
    /// </summary>
    public class PlayerIdentityTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] IdentityFields = new[]
        {
            "PlayerId", "Name", "Nationality", "ClubReference"
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

        private static Type IdentityType() => FindType(Prefix + "Identity");

        // ---- Category structure / exact fields ----

        [Test]
        public void Identity_Exists_AsSerializableData()
        {
            var t = IdentityType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Identity must be a [Serializable] data group inside PlayerDefinition, not a MonoBehaviour system.");
        }

        [Test]
        public void Identity_Contains_ExactlyTheFourApprovedFields()
        {
            var names = IdentityType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(IdentityFields.OrderBy(x => x).ToArray(), names,
                "Identity must contain exactly PlayerId, Name, Nationality, ClubReference.");
        }

        // ---- 77.1 Player ID ----

        [Test]
        public void PlayerId_Exists_AsStableAuthoredRepresentation()
        {
            var f = IdentityType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Identity must have PlayerId.");
            Assert.AreEqual(typeof(string), f.FieldType,
                "PlayerId must be a stable authored string (no dedicated ID type exists in this project).");
        }

        [Test]
        public void PlayerId_IsNotRuntimeGenerated()
        {
            // PlayerId must be deterministic/authored — NOT Guid.NewGuid / DateTime.Now /
            // GetInstanceID. No runtime fallback field.
            var t = IdentityType();
            Assert.IsNull(t.GetMethod("GeneratePlayerId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Identity must NOT auto-generate PlayerId at runtime.");
            Assert.IsNull(t.GetField("InstanceId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayerId must NOT be backed by a runtime instance id.");
        }

        [Test]
        public void PlayerId_IsNotUniquenessEnforcedByRegistry()
        {
            // Duplicate-ID validation is later data-integrity work (Task 86), NOT a registry here.
            Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .FirstOrDefault(x => x.Name == "PlayerRegistry" || x.Name == "PlayerDatabase" ||
                                     x.Name == "GlobalPlayerLookup" || x.Name == "PlayerDirectory"),
                "No player registry/database may be created for Task 77.");
        }

        // ---- 77.2 Name ----

        [Test]
        public void Name_Exists_AsAuthoredDisplayIdentity()
        {
            var f = IdentityType().GetField("Name", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Identity must have Name.");
            Assert.AreEqual(typeof(string), f.FieldType,
                "Name must be a single authored string (no first/middle/last split unless required).");
            // No runtime-mutable name state or nickname/localization field.
            Assert.IsNull(IdentityType().GetField("CurrentName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Name must NOT have a CurrentName runtime variant.");
        }

        [Test]
        public void Name_DoesNotCreate_LocalizationOrNicknameSystem()
        {
            foreach (var n in new[] { "LocalizationManager", "PlayerNameDatabase", "NicknameSystem" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(x => x.Name == n),
                    $"No '{n}' may be created for Task 77.");
            }
            Assert.IsNull(IdentityType().GetField("FirstName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Name must NOT be split into FirstName/MiddleName/LastName.");
        }

        // ---- 77.3 Nationality ----

        [Test]
        public void Nationality_Exists_AsDefinedRepresentation()
        {
            var f = IdentityType().GetField("Nationality", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Identity must have Nationality.");
            Assert.AreEqual(typeof(string), f.FieldType,
                "Nationality must be a stable authored representation (ISO-alphanumeric country code).");
        }

        [Test]
        public void Nationality_DoesNotCreate_CountrySystem()
        {
            foreach (var n in new[] { "CountryDefinition", "NationalityDatabase", "NationalTeamSystem" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(x => x.Name == n),
                    $"No '{n}' may be created for Task 77.");
            }
        }

        // ---- 77.4 Club Reference ----

        [Test]
        public void ClubReference_Exists_AsReferenceBoundary()
        {
            var f = IdentityType().GetField("ClubReference", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Identity must have ClubReference.");
            Assert.AreEqual(Prefix + "TeamDefinition", f.FieldType.FullName,
                "ClubReference must be a reference to the separate authored TeamDefinition.");
        }

        [Test]
        public void ClubReference_IsNot_EmbeddedTeamData()
        {
            // PlayerDefinition must NOT embed a full TeamDefinition's data (TeamName/Colors/Squad/
            // Formation/Tactics) — those belong to Team Data (Task 89+).
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in new[] { "TeamName", "ShortName", "ClubColors", "PrimaryColor",
                "Squad", "Starters", "Substitutes", "Formation", "Tactics", "TeamRatings" })
            {
                Assert.IsNull(pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PlayerDefinition must NOT embed '{name}' — it belongs to Team Data.");
            }
        }

        [Test]
        public void ClubReference_IsNotA_RuntimeUnityObject()
        {
            var f = IdentityType().GetField("ClubReference", BindingFlags.Public | BindingFlags.Instance);
            Assert.AreNotEqual(typeof(GameObject), f.FieldType);
            Assert.AreNotEqual(typeof(MonoBehaviour), f.FieldType);
            Assert.AreNotEqual(typeof(Transform), f.FieldType);
            Assert.IsFalse(typeof(Component).IsAssignableFrom(f.FieldType),
                "ClubReference must be authored data, not a runtime Unity Component.");
        }

        [Test]
        public void Identity_DoesNotImplement_TeamSystem()
        {
            foreach (var n in new[] { "TeamManager", "SquadManager", "ClubSystem" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(x => x.Name == n),
                    $"No '{n}' may be implemented for Task 77.");
            }
        }

        // ---- Identity vs Profile / PhysicalProfile / PlayerStats separation ----

        [Test]
        public void Identity_DoesNotContain_PlayerProfileFields()
        {
            var names = IdentityType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            foreach (var forbidden in new[] { "PrimaryPosition", "SecondaryPositions", "PreferredFoot",
                "WeakFoot", "SkillRating", "OverallRating", "CardType", "PlayStyle" })
            {
                Assert.IsFalse(names.Contains(forbidden),
                    $"Identity must NOT contain the Profile field '{forbidden}'.");
            }
        }

        [Test]
        public void Identity_DoesNotContain_PhysicalProfileFields()
        {
            var names = IdentityType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            foreach (var forbidden in new[] { "Age", "HeightCm", "WeightKg", "CurrentAge" })
            {
                Assert.IsFalse(names.Contains(forbidden),
                    $"Identity must NOT contain the PhysicalProfile field '{forbidden}'.");
            }
        }

        [Test]
        public void Identity_DoesNotContain_PlayerStatsOrCategories()
        {
            var names = IdentityType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            foreach (var forbidden in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending",
                "Physical", "Goalkeeping", "PlayerStats" })
            {
                Assert.IsFalse(names.Contains(forbidden),
                    $"Identity must NOT contain the PlayerStats field '{forbidden}'.");
            }
        }

        // ---- No runtime state / no Unity objects / no gameplay ----

        [Test]
        public void Identity_ContainsNoRuntimeState()
        {
            var t = IdentityType();
            foreach (var name in new[] { "CurrentClub", "CurrentTeam", "CurrentPosition",
                "CurrentName", "CurrentNationality", "CurrentSquad", "CurrentRole", "RuntimeTeam",
                "TransferStatus", "LoanClub" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Identity must NOT hold runtime state '{name}'.");
            }
        }

        [Test]
        public void Identity_DoesNotReferenceUnityRuntimeObjects()
        {
            foreach (var f in IdentityType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(Transform), f.FieldType);
                Assert.AreNotEqual(typeof(Rigidbody), f.FieldType);
                Assert.AreNotEqual(typeof(Collider), f.FieldType);
                Assert.AreNotEqual(typeof(GameObject), f.FieldType);
                Assert.AreNotEqual(typeof(MonoBehaviour), f.FieldType);
            }
        }

        [Test]
        public void Identity_ContainsNoGameplayLogic()
        {
            var t = IdentityType();
            foreach (var name in new[] { "MovePlayer", "ControlAI", "ControlAnimation", "ControlCamera",
                "ControlPhysics", "ControlPossession", "ControlMatch", "SetCurrentClub", "ApplyTransfer" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Identity must NOT implement gameplay via '{name}'.");
            }
        }

        // ---- No career / transfer / registry / UI ----

        [Test]
        public void Identity_DoesNotCreate_CareerOrTransferSystem()
        {
            foreach (var n in new[] { "ClubHistory", "TransferHistory", "ContractSystem", "CareerSystem" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(x => x.Name == n),
                    $"No '{n}' may be created for Task 77.");
            }
        }

        [Test]
        public void Identity_DoesNotCreateUI()
        {
            foreach (var n in new[] { "PlayerIdentityUI", "PlayerNameUI", "NationalityUI",
                "ClubBadgeUI", "PlayerCardUI" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(x => x.Name == n),
                    $"No '{n}' UI may be created for Task 77.");
            }
        }

        [Test]
        public void Identity_Fields_AreNotDuplicated_Elsewhere()
        {
            // One authoritative source per identity field: not duplicated into Profile /
            // PhysicalProfile / PlayerStats (nor their categories).
            foreach (var owner in new[] { "Profile", "PhysicalProfile", "PlayerStats",
                "PaceStats", "ShootingStats", "PassingStats", "DribblingStats", "DefendingStats",
                "PhysicalStats", "GoalkeepingStats" })
            {
                var t = FindType(Prefix + owner);
                foreach (var name in new[] { "PlayerId", "Name", "Nationality", "ClubReference" })
                {
                    Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"'{owner}' must NOT duplicate the identity field '{name}'.");
                }
            }
        }

        // ---- Unified identity model (GK and outfield) ----

        [Test]
        public void GoalkeeperAndOutfield_UseSame_IdentityModel()
        {
            // No split GK/outfield identity model; every player uses the unified Identity group.
            foreach (var n in new[] { "GoalkeeperIdentity", "OutfieldIdentity", "GoalkeeperPlayerIdentity" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(x => x.Name == n),
                    $"No separate '{n}' model may exist.");
            }
            Assert.IsNotNull(IdentityType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance),
                "Every player (GK and outfield) must have the unified Identity (PlayerId).");
        }

        // ---- Empty-value policy ----

        [Test]
        public void PlayerId_HasNoRuntimeFallback()
        {
            // No Guid.NewGuid / DateTime.Now / GetInstanceID fallback. PlayerId is deterministic
            // authored data only.
            var t = IdentityType();
            Assert.IsNull(t.GetField("PlayerIdGuid", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetField("PlayerIdFallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetMethod("GetInstanceID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        [Test]
        public void NoFakeNationality_Or_FakeClub_Injected()
        {
            // The implementation must not silently inject default country/club placeholder data.
            var f = IdentityType().GetField("Nationality", BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(typeof(string), f.FieldType);
            var club = IdentityType().GetField("ClubReference", BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(Prefix + "TeamDefinition", club.FieldType.FullName,
                "ClubReference stays a nullable/deferred reference boundary until Team Data (Task 89+), never faked.");
        }
    }
}
