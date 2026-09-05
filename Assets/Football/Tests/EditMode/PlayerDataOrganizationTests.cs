using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 84 — Player Data Organization.
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 84 locks the canonical player-data organization:
    ///   PlayerDefinition → Identity / PhysicalProfile / Profile / PlayerStats
    ///   - Each field has ONE clear category owner and every PlayerStats attribute has ONE
    ///     authoritative owner.
    ///   - Category boundaries are not mixed (identity/profile/physical/stats stay separate).
    ///   - Strong typing is preserved (enums, List&lt;PlayerPosition&gt;, TeamDefinition ref, category
    ///     classes) — no arbitrary strings for controlled domain concepts.
    ///   - No duplicate player attributes; no flat PlayerDefinition ratings; no separate goalkeeper
    ///     player model; no runtime state; no gameplay logic; no organization "system".
    /// </summary>
    public class PlayerDataOrganizationTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] AllRatingAttributes =
        {
            "Acceleration", "SprintSpeed",
            "AttackingAwareness", "Finishing", "ShotPower", "LongShots", "Volleys", "Penalties",
            "Vision", "ShortPassing", "LongPassing", "Crossing", "FreeKickAccuracy", "Curve",
            "Dribbling", "BallControl", "TightPossession", "Agility", "Balance", "Reactions",
            "DefensiveAwareness", "Interceptions", "StandingTackle", "SlidingTackle", "Heading",
            "Strength", "Stamina", "Jumping", "Aggression",
            "Diving", "Handling", "Kicking", "Positioning", "Reflexes", "Parrying"
        };

        private static Dictionary<string, string[]> CategoryAttributes = new Dictionary<string, string[]>
        {
            ["Pace"] = new[] { "Acceleration", "SprintSpeed" },
            ["Shooting"] = new[] { "AttackingAwareness", "Finishing", "ShotPower", "LongShots", "Volleys", "Penalties" },
            ["Passing"] = new[] { "Vision", "ShortPassing", "LongPassing", "Crossing", "FreeKickAccuracy", "Curve" },
            ["Dribbling"] = new[] { "Dribbling", "BallControl", "TightPossession", "Agility", "Balance", "Reactions" },
            ["Defending"] = new[] { "DefensiveAwareness", "Interceptions", "StandingTackle", "SlidingTackle", "Heading" },
            ["Physical"] = new[] { "Strength", "Stamina", "Jumping", "Aggression" },
            ["Goalkeeping"] = new[] { "Diving", "Handling", "Kicking", "Positioning", "Reflexes", "Parrying" }
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

        private static IEnumerable<FieldInfo> PublicFields(Type t) =>
            t.GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static Dictionary<string, Type> FieldMap(Type t) =>
            PublicFields(t).ToDictionary(f => f.Name, f => f.FieldType);

        private static Type PlayerDefType() => FindType(Prefix + "PlayerDefinition");
        private static Type IdentityType() => FindType(Prefix + "Identity");
        private static Type PhysicalType() => FindType(Prefix + "PhysicalProfile");
        private static Type ProfileType() => FindType(Prefix + "Profile");
        private static Type StatsType() => FindType(Prefix + "PlayerStats");
        private static Type PositionType() => FindType(Prefix + "PlayerPosition");
        private static Type TeamDefType() => FindType(Prefix + "TeamDefinition");

        // ---- 84.1 Category structure ----

        [Test]
        public void PlayerDefinitionHasExactlyFourTopLevelDataGroups()
        {
            var fields = FieldMap(PlayerDefType());
            Assert.AreEqual(4, fields.Keys.Count,
                "PlayerDefinition must expose exactly four top-level data groups.");
            foreach (var g in new[] { "Identity", "Physical", "Profile", "PlayerStats" })
            {
                Assert.IsTrue(fields.ContainsKey(g), $"PlayerDefinition must have group '{g}'.");
            }
        }

        [Test]
        public void IdentityGroupContainsOnlyIdentityFields()
        {
            var fields = FieldMap(IdentityType());
            Assert.That(fields.Keys, Is.EquivalentTo(new[] { "PlayerId", "Name", "Nationality", "ClubReference" }),
                "Identity must contain exactly PlayerId/Name/Nationality/ClubReference.");
        }

        [Test]
        public void PhysicalProfileContainsOnlyPhysicalFields()
        {
            var fields = FieldMap(PhysicalType());
            Assert.That(fields.Keys, Is.EquivalentTo(new[] { "Age", "HeightCm", "WeightKg" }),
                "PhysicalProfile must contain exactly Age/HeightCm/WeightKg.");
        }

        [Test]
        public void ProfileContainsOnlyProfileFields()
        {
            var fields = FieldMap(ProfileType());
            Assert.That(fields.Keys, Is.EquivalentTo(new[]
            {
                "PrimaryPosition", "SecondaryPositions", "PreferredFoot", "WeakFoot",
                "SkillRating", "OverallRating", "CardType", "PlayStyle"
            }), "Profile must contain exactly the eight approved profile fields.");
        }

        [Test]
        public void PlayerStatsContainsExactlySevenCategories()
        {
            var fields = FieldMap(StatsType());
            var categories = CategoryAttributes.Keys.ToList();
            Assert.That(fields.Keys, Is.EquivalentTo(categories),
                "PlayerStats must contain exactly the seven approved categories.");
        }

        [Test]
        public void EveryCategoryContainsExactlyApprovedAttributes()
        {
            foreach (var kv in CategoryAttributes)
            {
                var catType = StatsType().GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance).FieldType;
                var catFields = PublicFields(catType);
                var intNames = catFields.Where(f => f.FieldType == typeof(int)).Select(f => f.Name).ToList();
                Assert.That(intNames, Is.EquivalentTo(kv.Value.ToList()),
                    $"PlayerStats.{kv.Key} must contain exactly the approved attributes.");
            }
        }

        // ---- 84.2 Strong typing ----

        [Test]
        public void AllProfileEnumsAreStronglyTyped()
        {
            var f = FieldMap(ProfileType());
            Assert.IsTrue(f["PrimaryPosition"].IsEnum, "PrimaryPosition must be an enum.");
            Assert.IsTrue(f["PreferredFoot"].IsEnum, "PreferredFoot must be an enum.");
            Assert.IsTrue(f["CardType"].IsEnum, "CardType must be an enum.");
            Assert.IsTrue(f["PlayStyle"].IsEnum, "PlayStyle must be an enum.");
        }

        [Test]
        public void SecondaryPositionsUsesPlayerPositionType()
        {
            var f = FieldMap(ProfileType());
            var field = f["SecondaryPositions"];
            Assert.IsTrue(field.IsGenericType && field.GetGenericTypeDefinition() == typeof(List<>),
                "SecondaryPositions must be a List.");
            Assert.IsTrue(field.GetGenericArguments().Contains(PositionType()),
                "SecondaryPositions must be List<PlayerPosition>.");
        }

        [Test]
        public void ClubReferenceUsesTeamDefinitionReference()
        {
            var f = FieldMap(IdentityType());
            Assert.AreEqual(TeamDefType(), f["ClubReference"],
                "ClubReference must be a TeamDefinition reference.");
        }

        [Test]
        public void StatCategoriesAreStronglyTypedClasses()
        {
            var f = FieldMap(StatsType());
            foreach (var kv in CategoryAttributes)
            {
                var catType = f[kv.Key];
                Assert.AreNotEqual(typeof(int), catType, $"Category '{kv.Key}' must be a class, not a flat rating.");
                Assert.IsFalse(catType.IsEnum && catType != typeof(int),
                    $"Category '{kv.Key}' must be a dedicated class type, not an enum.");
            }
        }

        [Test]
        public void IdentityStringsUsedOnlyForIdentityConcepts()
        {
            // Identity string fields are acceptable; controlled domain concepts are NOT strings.
            var playerId = IdentityType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(playerId, "PlayerId must exist.");
            // PrimaryPosition/PreferredFoot/CardType/PlayStyle must never be authoritative strings.
            var profile = FieldMap(ProfileType());
            foreach (var n in new[] { "PrimaryPosition", "PreferredFoot", "CardType", "PlayStyle" })
            {
                Assert.IsTrue(profile[n].IsEnum, $"{n} must be strongly typed, not a string.");
            }
        }

        // ---- 84.3 Duplicate prevention ----

        [Test]
        public void NoIdentityFieldInPlayerStats()
        {
            foreach (var n in new[] { "PlayerId", "Name", "Nationality", "ClubReference" })
            {
                Assert.IsNull(StatsType().GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerStats must NOT contain identity field '{n}'.");
            }
        }

        [Test]
        public void NoProfileFieldInPlayerStats()
        {
            foreach (var n in new[] { "PrimaryPosition", "SecondaryPositions", "PreferredFoot", "WeakFoot", "SkillRating", "OverallRating", "CardType", "PlayStyle" })
            {
                Assert.IsNull(StatsType().GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerStats must NOT contain profile field '{n}'.");
            }
        }

        [Test]
        public void NoPhysicalProfileFieldInPlayerStats()
        {
            foreach (var n in new[] { "Age", "HeightCm", "WeightKg", "PhysicalProfile" })
            {
                Assert.IsNull(StatsType().GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerStats must NOT contain physical-profile field '{n}'.");
            }
        }

        [Test]
        public void NoPlayerStatFieldInIdentity()
        {
            foreach (var n in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping", "PlayerStats" })
            {
                Assert.IsNull(IdentityType().GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"Identity must NOT contain stat field '{n}'.");
            }
        }

        [Test]
        public void NoPlayerStatFieldInProfile()
        {
            foreach (var n in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping", "PlayerStats" })
            {
                Assert.IsNull(ProfileType().GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"Profile must NOT contain stat field '{n}'.");
            }
        }

        [Test]
        public void NoPlayerStatFieldInPhysicalProfile()
        {
            foreach (var n in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping", "PlayerStats" })
            {
                Assert.IsNull(PhysicalType().GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"PhysicalProfile must NOT contain stat field '{n}'.");
            }
        }

        [Test]
        public void EachPlayerRatingHasOneAuthoritativeOwner()
        {
            // Collect every int rating field across all seven category classes and require uniqueness.
            var seen = new Dictionary<string, string>();
            foreach (var kv in CategoryAttributes)
            {
                var catType = StatsType().GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance).FieldType;
                foreach (var f in PublicFields(catType).Where(x => x.FieldType == typeof(int)))
                {
                    Assert.IsFalse(seen.ContainsKey(f.Name),
                        $"Rating attribute '{f.Name}' is duplicated (already in {seen.GetValueOrDefault(f.Name)}): found again in {kv.Key}.");
                    seen[f.Name] = kv.Key;
                }
            }
        }

        [Test]
        public void AllApprovedRatingsArePresentAcrossCategories()
        {
            var present = new List<string>();
            foreach (var kv in CategoryAttributes)
            {
                var catType = StatsType().GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance).FieldType;
                present.AddRange(PublicFields(catType).Where(x => x.FieldType == typeof(int)).Select(x => x.Name));
            }
            foreach (var a in AllRatingAttributes)
            {
                Assert.Contains(a, present, $"Approved rating attribute '{a}' must exist in exactly one category.");
            }
        }

        [Test]
        public void NoDuplicateProfileFields()
        {
            // Profile fields must appear only on Profile, never on Identity/PhysicalProfile/PlayerStats.
            foreach (var n in new[] { "PrimaryPosition", "SecondaryPositions", "PreferredFoot", "WeakFoot", "SkillRating", "OverallRating", "CardType", "PlayStyle" })
            {
                Assert.IsNull(IdentityType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"Identity must not hold '{n}'.");
                Assert.IsNull(PhysicalType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"PhysicalProfile must not hold '{n}'.");
                Assert.IsNull(StatsType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"PlayerStats must not hold '{n}'.");
            }
        }

        [Test]
        public void NoDuplicatePhysicalProfileFields()
        {
            foreach (var n in new[] { "Age", "HeightCm", "WeightKg" })
            {
                Assert.IsNull(IdentityType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"Identity must not hold '{n}'.");
                Assert.IsNull(ProfileType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"Profile must not hold '{n}'.");
                Assert.IsNull(StatsType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"PlayerStats must not hold '{n}'.");
            }
        }

        [Test]
        public void NoDuplicateIdentityFields()
        {
            foreach (var n in new[] { "PlayerId", "Name", "Nationality", "ClubReference" })
            {
                Assert.IsNull(ProfileType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"Profile must not hold '{n}'.");
                Assert.IsNull(PhysicalType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"PhysicalProfile must not hold '{n}'.");
                Assert.IsNull(StatsType().GetField(n, BindingFlags.Public | BindingFlags.Instance), $"PlayerStats must not hold '{n}'.");
            }
        }

        [Test]
        public void CurveExistsOnlyInPassing()
        {
            Assert.IsNotNull(FindType(Prefix + "PassingStats").GetField("Curve", BindingFlags.Public | BindingFlags.Instance),
                "Curve must live in Passing.");
            foreach (var cat in new[] { "Pace", "Shooting", "Dribbling", "Defending", "Physical", "Goalkeeping" })
            {
                var ct = StatsType().GetField(cat, BindingFlags.Public | BindingFlags.Instance).FieldType;
                Assert.IsNull(ct.GetField("Curve", BindingFlags.Public | BindingFlags.Instance), $"Curve must not live in {cat}.");
            }
        }

        [Test]
        public void TightPossessionExistsOnlyInDribbling()
        {
            Assert.IsNotNull(FindType(Prefix + "DribblingStats").GetField("TightPossession", BindingFlags.Public | BindingFlags.Instance),
                "TightPossession must live in Dribbling.");
            foreach (var cat in new[] { "Pace", "Shooting", "Passing", "Defending", "Physical", "Goalkeeping" })
            {
                var ct = StatsType().GetField(cat, BindingFlags.Public | BindingFlags.Instance).FieldType;
                Assert.IsNull(ct.GetField("TightPossession", BindingFlags.Public | BindingFlags.Instance), $"TightPossession must not live in {cat}.");
            }
        }

        [Test]
        public void HeadingExistsOnlyInDefending()
        {
            Assert.IsNotNull(FindType(Prefix + "DefendingStats").GetField("Heading", BindingFlags.Public | BindingFlags.Instance),
                "Heading must live in Defending.");
            foreach (var cat in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Physical", "Goalkeeping" })
            {
                var ct = StatsType().GetField(cat, BindingFlags.Public | BindingFlags.Instance).FieldType;
                Assert.IsNull(ct.GetField("Heading", BindingFlags.Public | BindingFlags.Instance), $"Heading must not live in {cat}.");
            }
        }

        [Test]
        public void NoFlatPlayerDefinitionRatings()
        {
            var pd = PlayerDefType();
            foreach (var n in AllRatingAttributes)
            {
                Assert.IsNull(pd.GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerDefinition must not expose a flat rating field '{n}'.");
            }
        }

        [Test]
        public void PhysicalProfileDistinctFromPlayerStatsPhysical()
        {
            // PhysicalProfile {Age, HeightCm, WeightKg} vs PlayerStats.Physical {Strength, Stamina, Jumping, Aggression}.
            var physProf = FieldMap(PhysicalType());
            Assert.That(physProf.Keys, Is.EquivalentTo(new[] { "Age", "HeightCm", "WeightKg" }));
            var physStatType = StatsType().GetField("Physical", BindingFlags.Public | BindingFlags.Instance).FieldType;
            Assert.That(PublicFields(physStatType).Select(x => x.Name),
                Is.EquivalentTo(new[] { "Strength", "Stamina", "Jumping", "Aggression" }));
        }

        // ---- 84.4 Identity/stat separation & organization ----

        [Test]
        public void GoalkeepingIsUnifiedWithPlayerStats()
        {
            Assert.IsNotNull(StatsType().GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance),
                "Goalkeeping must be a PlayerStats category (not a separate model).");
        }

        [Test]
        public void NoSeparateGoalkeeperDefinition()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperDefinition", "GoalkeeperProfile", "GKPlayerDefinition"),
                "No separate goalkeeper player model may exist.");
        }

        [Test]
        public void NoParallelCategoryDefinitions()
        {
            Assert.IsFalse(HasTypeName("PaceDefinition", "ShootingDefinition", "PassingDefinition", "DribblingDefinition", "DefendingDefinition", "PhysicalDefinition"),
                "No parallel '*Definition' category classes may exist alongside the Stats classes.");
        }

        [Test]
        public void NoRuntimeStateAdded()
        {
            foreach (var t in new[] { PlayerDefType(), IdentityType(), PhysicalType(), ProfileType(), StatsType() })
            {
                foreach (var n in new[] { "CurrentPosition", "CurrentRole", "CurrentStamina", "CurrentClub", "CurrentCardType", "CurrentPlayStyle" })
                {
                    Assert.IsNull(t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"{t.Name} must not hold runtime field '{n}'.");
                }
            }
        }

        [Test]
        public void NoGameplayLogicAdded()
        {
            foreach (var t in new[] { PlayerDefType(), IdentityType(), PhysicalType(), ProfileType(), StatsType() })
            {
                foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
                {
                    Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"{t.Name} must not expose '{m}'.");
                }
            }
        }

        [Test]
        public void NoOrganizationSystemCreated()
        {
            Assert.IsFalse(HasTypeName("PlayerDataManager", "PlayerOrganizationSystem", "PlayerCategorySystem", "PlayerAttributeSystem"),
                "No player-data organization/manager/attribute system may be created.");
        }

        [Test]
        public void DataGroupsRemainSerializable()
        {
            // The grouped data classes are [Serializable] so they serialize under ScriptableObject.
            foreach (var name in new[] { "Identity", "PhysicalProfile", "Profile", "PlayerStats",
                "PaceStats", "ShootingStats", "PassingStats", "DribblingStats", "DefendingStats",
                "PhysicalStats", "GoalkeepingStats" })
            {
                var t = FindType(Prefix + name);
                Assert.IsTrue(t.IsDefined(typeof(SerializableAttribute), false),
                    $"'{name}' must be [Serializable] to group under the ScriptableObject.");
            }
        }

        [Test]
        public void PlayerDefinitionRemainsAuthoritativeContainer()
        {
            // PlayerDefinition is a ScriptableObject container exposing exactly the four groups.
            Assert.IsTrue(typeof(UnityEngine.ScriptableObject).IsAssignableFrom(PlayerDefType()),
                "PlayerDefinition must be a ScriptableObject.");
            Assert.AreEqual(4, FieldMap(PlayerDefType()).Count);
        }
    }
}
