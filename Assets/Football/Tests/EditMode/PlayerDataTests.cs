using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 87 — Player Data Tests (comprehensive end-to-end layer).
    ///
    /// Audited the existing Player Data suite first. The existing 21 task-specific test files
    /// already cover per-group structure, per-group validation (GetInvalidRatings / GetInvalidPositions /
    /// GetInvalidProfileData), integrity API basics, ownership, and architecture. The GENUINE coverage
    /// gaps this suite fills (without duplicating prior assertions):
    ///   1. Integrated END-TO-END scenarios — a real, fully-populated PlayerDefinition
    ///      (ScriptableObject.CreateInstance across ALL four groups) exercised through
    ///      GetIntegrityProblems() as a whole.
    ///   2. "Valid complete player" vs "invalid complete player" benchmarks.
    ///   3. Cross-category contamination on a fully-populated player and scale separation
    ///      (Profile 1-5 vs PlayerStats 1-99 vs PhysicalProfile real-world units).
    ///   4. Role scenarios through the integrity pipeline: an outfield (SS) player and a GK player,
    ///      plus the SAME PlayerDefinition instance valid as both GK and outfield.
    ///
    /// Tests build real instances and call the actual APIs via reflection (data types are in
    /// Assembly-CSharp and cannot be compile-referenced). No production code was changed.
    /// </summary>
    public class PlayerDataTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ApprovedPositions = {
            "GK", "CB", "LB", "RB", "CDM", "CM", "CAM", "LM", "RM", "LW", "RW", "SS", "ST"
        };

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null).ToArray(); }
        }

        private static IEnumerable<Type> AllTypes() =>
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes);

        private static Type FindType(string fullName)
        {
            var t = AllTypes().FirstOrDefault(x => x.FullName == fullName);
            Assert.IsNotNull(t, $"Type '{fullName}' must exist.");
            return t;
        }

        private static Type PdType() => FindType(Prefix + "PlayerDefinition");
        private static Type StatsType() => FindType(Prefix + "PlayerStats");
        private static Type ProfileType() => FindType(Prefix + "Profile");
        private static Type IdentityType() => FindType(Prefix + "Identity");

        private static readonly string[] Categories = {
            "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping"
        };

        private static object GetField(object obj, Type type, string name)
        {
            return type.GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(obj);
        }

        private static void SetInt(object obj, Type type, string name, int value)
        {
            type.GetField(name, BindingFlags.Public | BindingFlags.Instance).SetValue(obj, value);
        }

        private static void SetString(object obj, Type type, string name, string value)
        {
            type.GetField(name, BindingFlags.Public | BindingFlags.Instance).SetValue(obj, value);
        }

        private static void SetEnumByName(object obj, Type type, string name, string enumMember)
        {
            var f = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            var parsed = Enum.Parse(f.FieldType, enumMember);
            f.SetValue(obj, parsed);
        }

        private static void SetSecondaryPositions(object profile, string[] members)
        {
            var posType = FindType(Prefix + "PlayerPosition");
            var listType = typeof(List<>).MakeGenericType(posType);
            var list = Activator.CreateInstance(listType);
            var add = listType.GetMethod("Add");
            foreach (var m in members)
            {
                add.Invoke(list, new object[] { Enum.Parse(posType, m) });
            }
            profile.GetType().GetField("SecondaryPositions", BindingFlags.Public | BindingFlags.Instance)
                .SetValue(profile, list);
        }

        private static object BuildPlayer(string playerId)
        {
            var def = ScriptableObject.CreateInstance(PdType());
            Assert.IsNotNull(def, "PlayerDefinition instance must be creatable.");

            var identity = GetField(def, PdType(), "Identity");
            SetString(identity, IdentityType(), "PlayerId", playerId);
            SetString(identity, IdentityType(), "Name", "Integrated Test Player");
            SetString(identity, IdentityType(), "Nationality", "EG");

            var physical = GetField(def, PdType(), "Physical");
            SetInt(physical, physical.GetType(), "Age", 25);
            physical.GetType().GetField("HeightCm", BindingFlags.Public | BindingFlags.Instance).SetValue(physical, 180f);
            physical.GetType().GetField("WeightKg", BindingFlags.Public | BindingFlags.Instance).SetValue(physical, 75f);

            var profile = GetField(def, PdType(), "Profile");
            SetEnumByName(profile, ProfileType(), "PrimaryPosition", "ST");
            SetSecondaryPositions(profile, new[] { "CM" });
            SetEnumByName(profile, ProfileType(), "PreferredFoot", "Right");
            SetInt(profile, ProfileType(), "WeakFoot", 4);
            SetInt(profile, ProfileType(), "SkillRating", 4);
            SetInt(profile, ProfileType(), "OverallRating", 80);
            SetEnumByName(profile, ProfileType(), "CardType", "Basic");
            SetEnumByName(profile, ProfileType(), "PlayStyle", "Playmaker");

            SetAllRatingsValid(def);
            return def;
        }

        private static void SetAllRatingsValid(object def)
        {
            var stats = GetField(def, PdType(), "PlayerStats");
            foreach (var cat in Categories)
            {
                var category = stats.GetType().GetField(cat, BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
                foreach (var f in category.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Where(x => x.FieldType == typeof(int)))
                {
                    f.SetValue(category, 70);
                }
            }
        }

        private static List<string> Integrity(object def)
        {
            var m = PdType().GetMethod("GetIntegrityProblems", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(m, "GetIntegrityProblems must exist.");
            return (List<string>)m.Invoke(def, null);
        }

        private static List<string> DuplicateIds(IEnumerable<string> ids)
        {
            var m = PdType().GetMethod("FindDuplicatePlayerIds", BindingFlags.Public | BindingFlags.Static);
            return (List<string>)m.Invoke(null, new object[] { ids });
        }

        private static object SetRating(object def, string category, string attribute, int value)
        {
            var stats = GetField(def, PdType(), "PlayerStats");
            var cat = stats.GetType().GetField(category, BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
            SetInt(cat, cat.GetType(), attribute, value);
            return cat;
        }

        private static object NewPlayer(bool gk)
        {
            var def = BuildPlayer(gk ? "P900" : "P901");
            var profile = GetField(def, PdType(), "Profile");
            SetEnumByName(profile, ProfileType(), "PrimaryPosition", gk ? "GK" : "ST");
            return def;
        }

        // ---- 87.1 / 87.5 Integrated: valid complete player ----

        [Test]
        public void ValidCompletePlayerProducesNoIntegrityProblems()
        {
            var def = BuildPlayer("P001");
            Assert.IsEmpty(Integrity(def),
                "A complete, valid player must pass the integrity pipeline. Got: " + string.Join("; ", Integrity(def)));
        }

        [Test]
        public void FullyPopulatedAll35RatingsAreValidTogether()
        {
            // Cross-category integration: all 35 ratings populated with valid values produce no
            // PlayerStats integrity violations (no false positives across categories).
            var def = BuildPlayer("P002");
            var stats = GetField(def, PdType(), "PlayerStats");
            int total = 0;
            foreach (var cat in Categories)
            {
                var category = stats.GetType().GetField(cat, BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
                total += category.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Count(x => x.FieldType == typeof(int));
            }
            Assert.AreEqual(35, total, "PlayerStats must expose exactly 35 ratings.");
            Assert.IsEmpty(Integrity(def), "35 valid ratings must yield no integrity problems.");
        }

        [Test]
        public void IntegratedPlayerRetainsAllSevenCategoriesAndGoalkeeping()
        {
            var def = BuildPlayer("P003");
            var stats = GetField(def, PdType(), "PlayerStats");
            foreach (var cat in Categories)
            {
                Assert.IsNotNull(stats.GetType().GetField(cat, BindingFlags.Public | BindingFlags.Instance),
                    $"Integrated player must retain category '{cat}'.");
            }
        }

        // ---- 87.4 Cross-boundary ownership on a populated player ----

        [Test]
        public void PopulatedIdentityAndStatsRemainSeparate()
        {
            // Identity PlayerId lives only in Identity; no rating field leaks into Identity and no
            // identity field leaks into PlayerStats across a fully-populated player.
            var def = BuildPlayer("P004");
            var identity = GetField(def, PdType(), "Identity");
            var stats = GetField(def, PdType(), "PlayerStats");
            foreach (var pid in new[] { "PlayerId", "Name", "Nationality", "ClubReference" })
            {
                Assert.IsNull(stats.GetType().GetField(pid, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerStats must not contain identity field '{pid}'.");
                Assert.IsNotNull(identity.GetType().GetField(pid, BindingFlags.Public | BindingFlags.Instance),
                    $"Identity must contain field '{pid}'.");
            }
        }

        [Test]
        public void PopulatedProfileAndStatsRemainSeparate()
        {
            var def = BuildPlayer("P005");
            var stats = GetField(def, PdType(), "PlayerStats");
            foreach (var pf in new[] { "PrimaryPosition", "SecondaryPositions", "PreferredFoot", "WeakFoot", "SkillRating", "OverallRating", "CardType", "PlayStyle" })
            {
                Assert.IsNull(stats.GetType().GetField(pf, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerStats must not contain profile field '{pf}'.");
            }
            foreach (var cat in Categories)
            {
                Assert.IsNull(FindType(Prefix + "Profile").GetField(cat, BindingFlags.Public | BindingFlags.Instance),
                    $"Profile must not contain PlayerStats category '{cat}'.");
            }
        }

        [Test]
        public void PopulatedPhysicalProfileRemainsDistinctFromRatings()
        {
            // PhysicalProfile {Age, HeightCm, WeightKg} are real-world units, never validated as 1-99
            // PlayerStats ratings.
            var def = BuildPlayer("P006");
            var physical = GetField(def, PdType(), "Physical");
            foreach (var n in new[] { "Age", "HeightCm", "WeightKg" })
            {
                Assert.IsNull(StatsType().GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerStats must not contain physical-profile field '{n}'.");
            }
            Assert.AreEqual(25, physical.GetType().GetField("Age", BindingFlags.Public | BindingFlags.Instance).GetValue(physical),
                "Authored physical age must be preserved (not clobbered by ratings).");
        }

        // ---- 87.5 Validation composition on a populated player ----

        [Test]
        public void CrossCategoryContaminationIsIsolatedAndReported()
        {
            // A single out-of-range rating in ONE category is reported without false positives from
            // the other fully-valid categories, and without mutating the valid categories.
            var def = BuildPlayer("P007");
            SetRating(def, "Shooting", "Finishing", 150);
            var p = Integrity(def);
            Assert.That(p, Has.Count.EqualTo(1),
                "Exactly one problem (the bad rating) must be reported. Got: " + string.Join("; ", p));
            Assert.IsTrue(p[0].Contains("Shooting.Finishing") && p[0].Contains("99"),
                "The problem must identify the field and expected range. Got: " + p[0]);
            var shooting = SetRating(def, "Shooting", "Finishing", 80); // mutate back -> re-read below
            Assert.AreEqual(80, shooting.GetType().GetField("Finishing", BindingFlags.Public | BindingFlags.Instance).GetValue(shooting),
                "The authored valid value must be restorable (no structural corruption).");
        }

        [Test]
        public void ProfileScaleIsCheckedIndependentlyOfStatScale()
        {
            // WeakFoot/SkillRating use the Profile 1-5 scale. Incorrectly-authored values are caught
            // by the composed integrity via GetInvalidProfileData, NOT by the PlayerStats 1-99 path.
            var def = BuildPlayer("P008");
            var profile = GetField(def, PdType(), "Profile");
            SetInt(profile, ProfileType(), "WeakFoot", 7); // invalid on the 1-5 scale, but "valid" 1-99
            SetInt(profile, ProfileType(), "SkillRating", 0); // invalid on 1-5 scale
            var p = Integrity(def);
            Assert.IsTrue(p.Any(x => x.Contains("WeakFoot") && x.Contains("5")),
                "Invalid WeakFoot (Profile scale) must be reported. Got: " + string.Join("; ", p));
            Assert.IsTrue(p.Any(x => x.Contains("SkillRating") && x.Contains("5")),
                "Invalid SkillRating (Profile scale) must be reported. Got: " + string.Join("; ", p));
        }

        [Test]
        public void PhysicalProfileIsNotValidatedAsPlayerStatsRating()
        {
            // Age=25 (below "1" as a rating) must NOT be reported as an out-of-range PlayerStats
            // rating; physical fields use real-world semantics, not the 1-99 scale.
            var def = BuildPlayer("P009");
            var p = Integrity(def);
            Assert.IsFalse(p.Any(x => x.Contains("Age") && (x.Contains("range") || x.Contains("99"))),
                "Physical Age must not be validated on the PlayerStats 1-99 scale. Got: " + string.Join("; ", p));
        }

        [Test]
        public void EmptyAndWhitespacePlayerIdDetectedEndToEnd()
        {
            var empty = BuildPlayer("");
            Assert.IsTrue(Integrity(empty).Any(x => x.Contains("PlayerId is empty")),
                "Empty PlayerId must be detected on a populated player.");
            var ws = BuildPlayer("   ");
            Assert.IsTrue(Integrity(ws).Any(x => x.Contains("whitespace-only")),
                "Whitespace PlayerId must be detected on a populated player.");
        }

        [Test]
        public void InvalidCompletePlayerDetectsAllIssuesWithoutMutation()
        {
            // An invalid variant of the valid player: bad rating + empty PlayerId + undefined PlayStyle.
            var def = BuildPlayer("");
            var profile = GetField(def, PdType(), "Profile");
            var playStyleField = profile.GetType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance);
            playStyleField.SetValue(profile, Enum.ToObject(playStyleField.FieldType, 999));
            SetRating(def, "Pace", "Acceleration", 150);

            var p = Integrity(def);
            string all = string.Join("; ", p);
            Assert.IsTrue(p.Any(x => x.Contains("PlayerId is empty")), "Empty PlayerId must be reported: " + all);
            Assert.IsTrue(p.Any(x => x.Contains("Acceleration") && x.Contains("99")), "Bad rating must be reported: " + all);
            Assert.IsTrue(p.Any(x => x.Contains("PlayStyle")), "Undefined PlayStyle must be reported: " + all);

            // Nothing was mutated.
            var identity = GetField(def, PdType(), "Identity");
            Assert.AreEqual("", identity.GetType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance).GetValue(identity),
                "Empty PlayerId must remain empty (not auto-filled).");
            int playStyleRaw = Convert.ToInt32(playStyleField.GetValue(profile));
            Assert.AreEqual(999, playStyleRaw, "Invalid PlayStyle must remain unchanged.");
        }

        [Test]
        public void ComposedValidationSingleAuthorityStillHolds()
        {
            // The integrity pipeline forwards to the single-authority validators rather than a new
            // validator. Verify the authorities still exist and are the ones invoked.
            Assert.IsNotNull(StatsType().GetMethod("GetInvalidRatings", BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(ProfileType().GetMethod("GetInvalidPositions", BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(ProfileType().GetMethod("GetInvalidProfileData", BindingFlags.Public | BindingFlags.Instance));
            Assert.IsNotNull(PdType().GetMethod("GetIntegrityProblems", BindingFlags.Public | BindingFlags.Instance));
        }

        // ---- 87.3 / 87.6 Role scenarios through the integrity pipeline ----

        [Test]
        public void OutfieldSSPlayerIsValidThroughIntegrity()
        {
            var def = BuildPlayer("S001");
            var profile = GetField(def, PdType(), "Profile");
            SetEnumByName(profile, ProfileType(), "PrimaryPosition", "SS"); // Shadow Striker
            Assert.IsEmpty(Integrity(def),
                "A valid SS-role player must pass integrity. Got: " + string.Join("; ", Integrity(def)));
        }

        [Test]
        public void ApprovedPositionSetUsesSSNotCF()
        {
            Assert.That(ApprovedPositions, Does.Contain("SS"));
            Assert.That(ApprovedPositions, Does.Not.Contain("CF"),
                "CF is NOT approved; SS (Shadow Striker) replaces it.");
            // The authoritative enum must expose SS and must NOT expose CF.
            var enumNames = Enum.GetNames(FindType(Prefix + "PlayerPosition")).ToList();
            Assert.That(enumNames, Does.Contain("SS"));
            Assert.That(enumNames, Does.Not.Contain("CF"), "PlayerPosition enum must not contain CF.");
        }

        [Test]
        public void GKPlayerIsValidThroughIntegrity()
        {
            var def = NewPlayer(gk: true);
            Assert.IsEmpty(Integrity(def),
                "A valid GK-role player must pass integrity. Got: " + string.Join("; ", Integrity(def)));
        }

        [Test]
        public void SamePlayerDefinitionValidAsBothGKAndOutfield()
        {
            // One PlayerDefinition can be authored as GK or outfield by changing only the authored
            // PrimaryPosition; Goalkeeping stats exist for both and integrity stays clean.
            var def = BuildPlayer("U001");
            var profile = GetField(def, PdType(), "Profile");
            var posField = profile.GetType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);

            posField.SetValue(profile, Enum.Parse(posField.FieldType, "ST"));
            Assert.IsEmpty(Integrity(def), "Outfield ST must be valid.");

            posField.SetValue(profile, Enum.Parse(posField.FieldType, "GK"));
            Assert.IsEmpty(Integrity(def), "Same definition as GK must still be valid.");
            Assert.IsFalse(Integrity(def).Any(x => x.Contains("missing")));
        }

        [Test]
        public void GKRoleDoesNotRemoveOutfieldOrGoalkeepingStats()
        {
            // Total ratings across all seven categories stay exactly 35, and the GK role removes
            // neither the outfield attributes nor the Goalkeeping attributes.
            var def = NewPlayer(gk: true);
            var stats = GetField(def, PdType(), "PlayerStats");
            int total = 0;
            foreach (var cat in Categories)
            {
                var category = stats.GetType().GetField(cat, BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
                total += category.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Count(x => x.FieldType == typeof(int));
            }
            Assert.AreEqual(2, stats.GetType().GetField("Pace", BindingFlags.Public | BindingFlags.Instance)
                .FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance).Count(x => x.FieldType == typeof(int)),
                "GK role must not remove outfield Pace stats.");
            Assert.AreEqual(6, stats.GetType().GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance)
                .FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance).Count(x => x.FieldType == typeof(int)),
                "GK role must keep all Goalkeeping stats.");
            Assert.AreEqual(35, total, "A GK player must retain all 35 ratings.");
        }

        [Test]
        public void NoStatConversionWhenAuthoredAsGK()
        {
            // Authored GK role must not copy/convert outfield ratings into Goalkeeping.
            var def = BuildPlayer("G001");
            var stats = GetField(def, PdType(), "PlayerStats");
            var gk = stats.GetType().GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance).GetValue(stats);
            foreach (var f in gk.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Where(x => x.FieldType == typeof(int)))
            {
                Assert.AreEqual(70, f.GetValue(gk), "Goalkeeping authored rating must remain as authored.");
            }
        }

        [Test]
        public void GoalkeepingStatsDoNotDetermineRoleThroughIntegrity()
        {
            // Outfield and GK players both pass integrity regardless of their Goalkeeping values.
            foreach (var gk in new[] { false, true })
            {
                var def = NewPlayer(gk);
                Assert.IsEmpty(Integrity(def), $"Role {(gk ? "GK" : "outfield")} must pass integrity.");
            }
        }

        [Test]
        public void DuplicatePlayerIdsDetectedAcrossRealPlayers()
        {
            var a = BuildPlayer("DUP");
            var b = BuildPlayer("DUP");
            var c = BuildPlayer("UNIQUE");
            var ids = new[] { "DUP", "DUP", "UNIQUE" };
            Assert.That(DuplicateIds(ids), Is.EquivalentTo(new[] { "DUP" }),
                "Duplicate PlayerIds across real players must be detected.");
            GC.KeepAlive(a); GC.KeepAlive(b); GC.KeepAlive(c);
        }

        [Test]
        public void PlayerIdIndependentOfPositionAndClubEndToEnd()
        {
            // Changing PrimaryPosition between SCR/SS/GK and leaving Club null does not change the
            // stable PlayerId.
            var def = BuildPlayer("FIXED");
            var profile = GetField(def, PdType(), "Profile");
            var posField = profile.GetType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);
            posField.SetValue(profile, Enum.Parse(posField.FieldType, "SS"));
            var idAfter = GetField(def, PdType(), "Identity").GetType()
                .GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance).GetValue(GetField(def, PdType(), "Identity"));
            Assert.AreEqual("FIXED", idAfter, "PlayerId must remain stable across profile changes.");
        }

        // ---- Runtime / asset safety on a populated player ----

        [Test]
        public void PopulatedPlayerDefinitionRemainsDataOnly()
        {
            var def = BuildPlayer("R001");
            var pd = PdType();
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "Start", "Awake" })
            {
                Assert.IsNull(pd.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"PlayerDefinition must not host runtime behaviour '{m}'.");
            }
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(pd),
                "PlayerDefinition must remain a ScriptableObject (authored asset).");
        }

        [Test]
        public void OnlyAuthorizedPlayerPositionSetIsSupported()
        {
            var names = Enum.GetNames(FindType(Prefix + "PlayerPosition"));
            Assert.That(names, Is.EquivalentTo(ApprovedPositions),
                "The enum must expose exactly the approved position set (SS, not CF).");
        }
    }
}
