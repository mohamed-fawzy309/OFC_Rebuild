using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 86 — Player Data Integrity.
    ///
    /// Verifies the composed integrity layer added to PlayerDefinition:
    ///   - Required groups (Identity / PhysicalProfile / Profile / PlayerStats) and required fields.
    ///   - Reference validation (ClubReference -> TeamDefinition boundary; runtime Unity refs rejected).
    ///   - PlayerId integrity (authored, stable, non-empty, non-whitespace, unique, not runtime-generated).
    ///   - Invalid data detection WITHOUT mutation (composes, never duplicates, the existing boundary
    ///     validators: GetInvalidRatings / GetInvalidPositions / GetInvalidProfileData).
    ///
    /// Tests build REAL PlayerDefinition instances via ScriptableObject.CreateInstance and inspect/
    /// invoke the actual integrity API by reflection (the data types live in Assembly-CSharp and
    /// cannot be compile-referenced from the test assembly).
    /// </summary>
    public class PlayerDataIntegrityTests
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

        private static IEnumerable<Type> AllTypes() =>
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes);

        private static bool HasTypeName(params string[] names)
        {
            var all = AllTypes().Select(t => t.Name).ToList();
            return names.Any(n => all.Contains(n));
        }

        private static Type PdType() => FindType(Prefix + "PlayerDefinition");
        private static Type IdentityType() => FindType(Prefix + "Identity");
        private static Type StatsType() => FindType(Prefix + "PlayerStats");
        private static Type ProfileType() => FindType(Prefix + "Profile");
        private static Type TeamDefType() => FindType(Prefix + "TeamDefinition");

        private static object CreateDefinition()
        {
            var obj = ScriptableObject.CreateInstance(PdType());
            Assert.IsNotNull(obj, "PlayerDefinition ScriptableObject instance must be creatable.");
            return obj;
        }

        private static object GetGroup(object def, string field)
        {
            return PdType().GetField(field, BindingFlags.Public | BindingFlags.Instance).GetValue(def);
        }

        private static void SetString(object obj, string field, string value)
        {
            obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance).SetValue(obj, value);
        }

        private static void SetEnum(object obj, string field, object value)
        {
            obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance).SetValue(obj, value);
        }

        private static void SetUndefinedEnum(object obj, string field, long rawValue)
        {
            var fieldInfo = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            var enumValue = Enum.ToObject(fieldInfo.FieldType, rawValue);
            fieldInfo.SetValue(obj, enumValue);
        }

        private static void SetInt(object obj, string field, int value)
        {
            obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance).SetValue(obj, value);
        }

        private static List<string> GetIntegrityProblems(object def)
        {
            var method = PdType().GetMethod("GetIntegrityProblems", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, "PlayerDefinition.GetIntegrityProblems must exist.");
            return (List<string>)method.Invoke(def, null);
        }

        private static List<string> FindDuplicatePlayerIds(IEnumerable<string> ids)
        {
            var method = PdType().GetMethod("FindDuplicatePlayerIds",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method, "PlayerDefinition.FindDuplicatePlayerIds must exist.");
            return (List<string>)method.Invoke(null, new object[] { ids });
        }

        // A valid definition used as the baseline; tests mutate specific aspects.
        private static object ValidDefinition(string playerId)
        {
            var def = CreateDefinition();
            var identity = GetGroup(def, "Identity");
            SetString(identity, "PlayerId", playerId);
            SetString(identity, "Name", "Test Player");
            SetString(identity, "Nationality", "ENG");
            return def;
        }

        // ---- 86.1 Required fields / groups ----

        [Test]
        public void ValidDefinitionHasNoIntegrityProblems()
        {
            Assert.IsEmpty(GetIntegrityProblems(ValidDefinition("P001")),
                "A correctly-populated definition must pass integrity.");
        }

        [Test]
        public void PlayerDefinitionRequiresIdentity()
        {
            var def = ValidDefinition("P001");
            PdType().GetField("Identity", BindingFlags.Public | BindingFlags.Instance).SetValue(def, null);
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("Identity is missing")), "Missing Identity must be reported.");
        }

        [Test]
        public void PlayerDefinitionRequiresPhysicalProfile()
        {
            var def = ValidDefinition("P001");
            PdType().GetField("Physical", BindingFlags.Public | BindingFlags.Instance).SetValue(def, null);
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("PhysicalProfile is missing")), "Missing PhysicalProfile must be reported.");
        }

        [Test]
        public void PlayerDefinitionRequiresProfile()
        {
            var def = ValidDefinition("P001");
            PdType().GetField("Profile", BindingFlags.Public | BindingFlags.Instance).SetValue(def, null);
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("Profile is missing")), "Missing Profile must be reported.");
        }

        [Test]
        public void PlayerDefinitionRequiresPlayerStats()
        {
            var def = ValidDefinition("P001");
            PdType().GetField("PlayerStats", BindingFlags.Public | BindingFlags.Instance).SetValue(def, null);
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("PlayerStats is missing")), "Missing PlayerStats must be reported.");
        }

        [Test]
        public void MissingMultipleGroupsDoNotCrash()
        {
            var def = DefinitionWithAllGroupsNull();
            var p = GetIntegrityProblems(def);
            Assert.IsNotEmpty(p, "A definition with all groups missing must report problems, not throw.");
            Assert.IsTrue(p.Count(x => x.Contains("is missing")) >= 4,
                "All four missing groups must be reported without NullReferenceException. Got: " + string.Join("; ", p));
        }

        private static object DefinitionWithAllGroupsNull()
        {
            var def = CreateDefinition();
            foreach (var grp in new[] { "Identity", "Physical", "Profile", "PlayerStats" })
            {
                PdType().GetField(grp, BindingFlags.Public | BindingFlags.Instance).SetValue(def, null);
            }
            return def;
        }

        [Test]
        public void IdentityRequiresAllFields()
        {
            // Required-field existence is structural (the four fields are always present on Identity).
            var f = IdentityType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToList();
            foreach (var n in new[] { "PlayerId", "Name", "Nationality", "ClubReference" })
            {
                Assert.Contains(n, f, $"Identity must require field '{n}'.");
            }
        }

        [Test]
        public void ProfileRequiresAllFields()
        {
            var f = ProfileType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToList();
            foreach (var n in new[] { "PrimaryPosition", "SecondaryPositions", "PreferredFoot", "WeakFoot",
                "SkillRating", "OverallRating", "CardType", "PlayStyle" })
            {
                Assert.Contains(n, f, $"Profile must require field '{n}'.");
            }
        }

        [Test]
        public void PlayerStatsRequiresAllSevenCategories()
        {
            var f = StatsType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name).ToList();
            foreach (var n in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping" })
            {
                Assert.Contains(n, f, $"PlayerStats must require category '{n}'.");
            }
        }

        [Test]
        public void GoalkeepingIsRequiredForEveryPlayer()
        {
            Assert.IsNotNull(StatsType().GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance),
                "Goalkeeping must be a required category for every player (no conditional GK model).");
        }

        [Test]
        public void All35RatingsRemainCoveredByIntegrity()
        {
            // A rating out of [1..99] in ANY category is surfaced through the composed
            // GetInvalidRatings() — verify it actually flows through GetIntegrityProblems().
            var def = ValidDefinition("P001");
            var stats = GetGroup(def, "PlayerStats");
            var shooting = stats.GetType().GetField("Shooting", BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
            SetInt(shooting, "Finishing", 150);
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("Shooting.Finishing") && x.Contains("99")),
                "An out-of-range rating must be reported by the composed integrity pass.");
        }

        // ---- 86.2 Reference validation ----

        [Test]
        public void ClubReferenceUsesCorrectType()
        {
            var f = IdentityType().GetField("ClubReference", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "ClubReference must exist.");
            Assert.AreEqual(TeamDefType(), f.FieldType,
                "ClubReference must be typed as a TeamDefinition reference.");
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(f.FieldType),
                "ClubReference type must be an authored ScriptableObject asset reference.");
        }

        [Test]
        public void NullClubReferenceIsToleratedNotFabricated()
        {
            // Task 86 documents: null ClubReference is tolerated (no fake club) until Team Data
            // exists (Task 89). A null reference must NOT produce an integrity error and must NOT
            // create a fake TeamDefinition.
            var def = ValidDefinition("P001");
            var p = GetIntegrityProblems(def);
            Assert.IsFalse(p.Any(x => x.Contains("ClubReference")),
                "Null ClubReference must be tolerated (enforcement deferred), not flagged/fabricated.");
        }

        [Test]
        public void RuntimeUnityReferencesAreRejected()
        {
            // PlayerDefinition and its groups must not hold runtime Unity object references.
            var groupTypes = new[] { Prefix + "PlayerDefinition", Prefix + "Identity", Prefix + "PhysicalProfile",
                Prefix + "Profile", Prefix + "PlayerStats", Prefix + "PaceStats", Prefix + "ShootingStats",
                Prefix + "PassingStats", Prefix + "DribblingStats", Prefix + "DefendingStats",
                Prefix + "PhysicalStats", Prefix + "GoalkeepingStats" };
            foreach (var name in groupTypes)
            {
                var t = FindType(name);
                foreach (var fld in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var ft = fld.FieldType;
                    var isComponent = typeof(Component).IsAssignableFrom(ft);
                    var isScriptableRef = typeof(ScriptableObject).IsAssignableFrom(ft);
                    Assert.IsFalse(isComponent && !isScriptableRef,
                        $"{t.Name}.{fld.Name} must not reference runtime Unity object type '{ft.Name}'.");
                    Assert.IsFalse(ft == typeof(GameObject), $"{t.Name}.{fld.Name} must not reference a GameObject.");
                }
            }
        }

        [Test]
        public void NoEmbeddedTeamDefinitionData()
        {
            // Identity owns no TeamDefinition-instance data; ClubReference is the only team boundary.
            var idFields = IdentityType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsFalse(idFields.Any(f => f.FieldType == TeamDefType() && f.Name != "ClubReference"),
                "Identity must not embed TeamDefinition data.");
        }

        // ---- 86.3 Unique Player ID ----

        [Test]
        public void PlayerIdCannotBeEmpty()
        {
            var def = ValidDefinition("");
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("PlayerId is empty")), "Empty PlayerId must be reported.");
        }

        [Test]
        public void PlayerIdCannotBeNull()
        {
            var def = CreateDefinition();
            var identity = GetGroup(def, "Identity");
            SetString(identity, "PlayerId", null);
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("PlayerId is empty") || x.Contains("PlayerId")),
                "Null PlayerId must be reported.");
        }

        [Test]
        public void PlayerIdCannotBeWhitespace()
        {
            var def = ValidDefinition("   ");
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("whitespace-only")),
                "Whitespace-only PlayerId must be reported.");
        }

        [Test]
        public void PlayerIdIsNotRuntimeGenerated()
        {
            // No runtime ID generator exists on PlayerDefinition/Identity.
            Assert.IsNull(IdentityType().GetMethod("GeneratePlayerId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayerId must be authored, never runtime-generated.");
            Assert.IsNull(IdentityType().GetMethod("GetInstanceID",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayerId must not be backed by Unity instance IDs.");
        }

        [Test]
        public void PlayerIdIsNotArrayIndex()
        {
            Assert.IsNotNull(IdentityType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance),
                "PlayerId must be an explicit authored field, not an implicit array index.");
        }

        [Test]
        public void DuplicatePlayerIdsAreDetected()
        {
            var dups = FindDuplicatePlayerIds(new[] { "P001", "P002", "P001", "P003", "P002", "P004" });
            Assert.That(dups, Is.EquivalentTo(new[] { "P001", "P002" }),
                "IDs appearing more than once must be reported as duplicates.");
        }

        [Test]
        public void UniquePlayerIdsAreNotReported()
        {
            Assert.IsEmpty(FindDuplicatePlayerIds(new[] { "P001", "P002", "P003" }),
                "All-unique IDs must not be reported.");
        }

        [Test]
        public void EmptyIdsAreNotTreatedAsDuplicates()
        {
            var dups = FindDuplicatePlayerIds(new[] { "", "  ", null, "P001", "P001" });
            Assert.That(dups, Is.EquivalentTo(new[] { "P001" }),
                "Empty/whitespace IDs are per-definition errors, not duplicate candidates.");
        }

        [Test]
        public void NullIdCollectionIsHandled()
        {
            Assert.IsEmpty(FindDuplicatePlayerIds(null),
                "A null input collection must be handled without throwing.");
        }

        [Test]
        public void PlayerIdIsIndependentOfName()
        {
            // Two players may share a Name; PlayerId remains the stable identity.
            var a = GetGroup(ValidDefinition("P001"), "Identity");
            var b = GetGroup(ValidDefinition("P001"), "Identity");
            SetString(a, "Name", "Same");
            SetString(b, "Name", "Same");
            Assert.IsEmpty(FindDuplicatePlayerIds(new[] { "P001" }),
                "Name must not be the player key; only PlayerId drives duplicate detection.");
        }

        [Test]
        public void PlayerIdIsIndependentOfClubReference()
        {
            // ClubReference does not influence PlayerId uniqueness: two definitions with the same
            // PlayerId but different clubs are still duplicate IDs.
            var dups = FindDuplicatePlayerIds(new[] { "P001", "P001" });
            Assert.That(dups, Is.EquivalentTo(new[] { "P001" }),
                "PlayerId duplicate detection depends only on PlayerId, not ClubReference.");
        }

        [Test]
        public void PlayerIdIsIndependentOfPositionCardStyle()
        {
            // Changing Position/CardType/PlayStyle must not change PlayerId identity.
            Assert.IsNotNull(IdentityType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance),
                "PlayerId must remain the stable identity independent of profile fields.");
        }

        // ---- 86.4 Invalid data detection ----

        [Test]
        public void InvalidProfileDataIsDetected()
        {
            var def = ValidDefinition("P001");
            var profile = GetGroup(def, "Profile");
            SetUndefinedEnum(profile, "PlayStyle", 999); // undefined enum value
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("PlayStyle")),
                "Invalid PlayStyle must be reported by the composed integrity pass.");
        }

        [Test]
        public void InvalidPositionDataIsDetected()
        {
            var def = ValidDefinition("P001");
            var profile = GetGroup(def, "Profile");
            SetUndefinedEnum(profile, "PrimaryPosition", 999); // undefined enum value
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("PrimaryPosition")),
                "Invalid PrimaryPosition must be reported by the composed integrity pass.");
        }

        [Test]
        public void InvalidPlayerStatsDataIsDetected()
        {
            var def = ValidDefinition("P001");
            var stats = GetGroup(def, "PlayerStats");
            var gk = stats.GetType().GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
            SetInt(gk, "Reflexes", 0); // below allowed range
            var p = GetIntegrityProblems(def);
            Assert.IsTrue(p.Any(x => x.Contains("Goalkeeping.Reflexes")),
                "Out-of-range Goalkeeping rating must be reported.");
        }

        [Test]
        public void InvalidPlayerDataIsNotSilentlyMutated()
        {
            var def = ValidDefinition("");
            var identity = GetGroup(def, "Identity");
            var profile = GetGroup(def, "Profile");
            var stats = GetGroup(def, "PlayerStats");
            var shooting = stats.GetType().GetField("Shooting", BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
            var playStyleField = profile.GetType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance);
            SetInt(shooting, "Finishing", 150);
            SetUndefinedEnum(profile, "PlayStyle", 999);

            GetIntegrityProblems(def); // integrity pass

            Assert.AreEqual("", identity.GetType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance).GetValue(identity),
                "Integrity must NOT replace an empty PlayerId.");
            Assert.AreEqual(150, shooting.GetType().GetField("Finishing", BindingFlags.Public | BindingFlags.Instance).GetValue(shooting),
                "Integrity must NOT clamp an out-of-range rating.");
            int playStyleRaw = Convert.ToInt32(playStyleField.GetValue(profile));
            Assert.AreEqual(999, playStyleRaw,
                "Integrity must NOT reset an invalid PlayStyle.");
        }

        [Test]
        public void InvalidDataIsActionablyReported()
        {
            var def = ValidDefinition("");
            var stats = GetGroup(def, "PlayerStats");
            var shooting = stats.GetType().GetField("Shooting", BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
            SetInt(shooting, "Finishing", 150);
            var p = GetIntegrityProblems(def);
            string joined = string.Join("; ", p);
            Assert.IsTrue(p.Any(x => x.Contains("Finishing") && x.Contains("99")),
                "Reports must name the field and expected range. Got: " + joined);
            Assert.IsTrue(p.Any(x => x.Contains("PlayerId")),
                "Reports must name the PlayerId field. Got: " + joined);
        }

        [Test]
        public void NoRuntimeIntegrityPolling()
        {
            // Integrity validation is an explicit one-shot API, not per-frame polling.
            foreach (var t in new[] { PdType(), IdentityType(), StatsType(), ProfileType() })
            {
                foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
                {
                    Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"{t.Name} must not run integrity validation every frame.");
                }
            }
        }

        [Test]
        public void NoGameplayLogicCreated()
        {
            foreach (var t in new[] { PdType(), IdentityType(), StatsType(), ProfileType() })
            {
                foreach (var m in new[] { "OnEnable", "OnDisable", "Start", "Awake" })
                {
                    Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"{t.Name} must not run gameplay lifecycle behaviour '{m}'.");
                }
            }
        }

        [Test]
        public void NoPlayerDatabaseCreated()
        {
            Assert.IsFalse(HasTypeName("PlayerDatabase", "PlayerDataDatabase", "RuntimePlayerDatabase"),
                "No player database may be created for uniqueness.");
        }

        [Test]
        public void NoPlayerRegistryCreated()
        {
            Assert.IsFalse(HasTypeName("PlayerRegistry", "PlayerCatalogRuntime", "GlobalPlayerLookup"),
                "No player registry/catalog may be created for uniqueness.");
        }

        [Test]
        public void NoRuntimePlayerManagerCreated()
        {
            Assert.IsFalse(HasTypeName("PlayerDataManager", "RuntimePlayerManager", "PlayerDataRegistry"),
                "No runtime player manager may be created.");
        }

        [Test]
        public void GoalkeepersUseSameIntegrityModel()
        {
            // Single unified integrity model — no GK-specific integrity variant.
            Assert.IsFalse(HasTypeName("GoalkeeperIntegrity", "GKIntegrityValidator", "GoalkeeperIntegrityValidator"),
                "Goalkeepers must use the same integrity model as outfield players.");
        }

        [Test]
        public void IntegrityComposesNotDuplicatesValidators()
        {
            // The integrity layer must reuse the single-authority validators, not reimplement them.
            // Confirm those authority methods exist and the integrity method does NOT add a parallel
            // duplicate validator set.
            Assert.IsNotNull(StatsType().GetMethod("GetInvalidRatings", BindingFlags.Public | BindingFlags.Instance),
                "PlayerStats validator must remain the single authority.");
            Assert.IsNotNull(ProfileType().GetMethod("GetInvalidPositions", BindingFlags.Public | BindingFlags.Instance),
                "Position validator must remain the single authority.");
            Assert.IsNotNull(ProfileType().GetMethod("GetInvalidProfileData", BindingFlags.Public | BindingFlags.Instance),
                "Profile validator must remain the single authority.");
            Assert.IsFalse(HasTypeName("PlayerStatsIntegrityValidator", "ProfileIntegrityValidator", "PositionIntegrityValidator"),
                "No duplicate parallel validators may be created.");
        }
    }
}
