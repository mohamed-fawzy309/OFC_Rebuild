using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 78 — Attribute Validation (player rating validation authority).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// The authoritative PlayerStats validation mechanism is the existing
    /// <c>PlayerStats.GetInvalidRatings()</c> (single authority, introduced in Task 67). This fixture
    /// locks the Task 78 policy: authoritative range 1-99, default 50, all SEVEN categories and all
    /// THIRTY-FIVE approved attributes validated by ONE centralized method (no category-specific
    /// validators), invalid authored data DETECTED and REPORTED with an actionable diagnosis (category
    /// + attribute + value + range), never silently clamped or reset to the default. It verifies that
    /// Profile (WeakFoot/SkillRating/OverallRating/positions/foot/card/playstyle) and PhysicalProfile
    /// (Age/HeightCm/WeightKg) are NOT dragged into the 1-99 stat validation, that validation is
    /// explicit/bounded (no per-frame Update/FixedUpdate), and that no gameplay logic was added.
    /// </summary>
    public class AttributeValidationTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly Dictionary<string, string[]> Approved = new Dictionary<string, string[]>
        {
            { "PaceStats", new[] { "Acceleration", "SprintSpeed" } },
            { "ShootingStats", new[] { "AttackingAwareness", "Finishing", "ShotPower",
                "LongShots", "Volleys", "Penalties" } },
            { "PassingStats", new[] { "Vision", "ShortPassing", "LongPassing", "Crossing",
                "FreeKickAccuracy", "Curve" } },
            { "DribblingStats", new[] { "Dribbling", "BallControl", "TightPossession",
                "Agility", "Balance", "Reactions" } },
            { "DefendingStats", new[] { "DefensiveAwareness", "Interceptions",
                "StandingTackle", "SlidingTackle", "Heading" } },
            { "PhysicalStats", new[] { "Strength", "Stamina", "Jumping", "Aggression" } },
            { "GoalkeepingStats", new[] { "Diving", "Handling", "Kicking", "Positioning",
                "Reflexes", "Parrying" } }
        };

        private static readonly string[] CategoryFields =
        {
            "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping"
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

        private static Type StatsType() => FindType(Prefix + "PlayerStats");

        private static object NewStats() => Activator.CreateInstance(StatsType());

        private static object GetCategory(object ps, string catField)
        {
            return StatsType().GetField(catField, BindingFlags.Public | BindingFlags.Instance).GetValue(ps);
        }

        private static void SetAttribute(object ps, string catField, string attr, int value)
        {
            GetCategory(ps, catField).GetType()
                .GetField(attr, BindingFlags.Public | BindingFlags.Instance)
                .SetValue(GetCategory(ps, catField), value);
        }

        private static List<string> GetProblems(object ps)
        {
            return (List<string>)StatsType().GetMethod("GetInvalidRatings",
                BindingFlags.Public | BindingFlags.Instance).Invoke(ps, null);
        }

        // ---- 78.1 Minimum / maximum (single authority) ----

        [Test]
        public void RatingMin_Is1()
        {
            Assert.AreEqual(1, (int)StatsType().GetField("RatingMin").GetRawConstantValue(),
                "PlayerStats.RatingMin must be 1.");
        }

        [Test]
        public void RatingMax_Is99()
        {
            Assert.AreEqual(99, (int)StatsType().GetField("RatingMax").GetRawConstantValue(),
                "PlayerStats.RatingMax must be 99, NOT 100.");
        }

        [Test]
        public void RatingDefault_Is50()
        {
            Assert.AreEqual(50, (int)StatsType().GetField("RatingDefault").GetRawConstantValue(),
                "PlayerStats.RatingDefault must be 50.");
        }

        [Test]
        public void Range_IsDefined_Once_OnPlayerStatsOnly()
        {
            // No category may define its own RatingMin/RatingMax/RatingDefault (no per-category
            // duplicate ranges).
            foreach (var statsType in Approved.Keys)
            {
                var t = FindType(Prefix + statsType);
                foreach (var name in new[] { "RatingMin", "RatingMax", "RatingDefault" })
                {
                    Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Instance | BindingFlags.Static),
                        $"'{statsType}' must NOT duplicate the range constant '{name}' — it is defined once on PlayerStats.");
                }
            }
        }

        // ---- 78.1 All attributes integer ----

        [Test]
        public void AllPlayerStatsAttributes_AreIntegers()
        {
            foreach (var statsType in Approved.Keys)
            {
                foreach (var f in FindType(Prefix + statsType).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.AreEqual(typeof(int), f.FieldType,
                        $"Attribute '{statsType}.{f.Name}' must be int (no float/double rating).");
                }
            }
        }

        // ---- 78.4 All seven categories validated ----

        [Test]
        public void AllSevenCategories_AreValidated()
        {
            // Setting one attribute out of range in each category must produce a reported problem.
            foreach (var catField in CategoryFields)
            {
                var anyAttr = Approved.First(k => CategoryField(k.Key) == catField).Value[0];
                var ps = NewStats();
                SetAttribute(ps, catField, anyAttr, 100);
                Assert.IsNotEmpty(GetProblems(ps),
                    $"Category '{catField}' must be included in PlayerStats validation.");
            }
        }

        [Test]
        public void All35ApprovedAttributes_AreValidated()
        {
            var all = Approved.Values.SelectMany(v => v).ToArray();
            Assert.AreEqual(35, all.Length, "There must be exactly 35 approved PlayerStats attributes.");
            Assert.AreEqual(35, all.Distinct().Count(), "Approved attributes must not be duplicated.");
            foreach (var statsType in Approved.Keys)
            {
                var names = FindType(Prefix + statsType)
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(f => f.Name)
                    .ToArray();
                CollectionAssert.AreEquivalent(Approved[statsType], names,
                    $"Category '{statsType}' must expose exactly its approved attributes.");
            }
        }

        // ---- 78.4 Per-category: every attribute is validated ----

        [Test]
        public void Pace_AllAttributes_AreValidated()
        {
            AssertAllAttributesDetected("PaceStats");
        }

        [Test]
        public void Shooting_AllAttributes_AreValidated()
        {
            AssertAllAttributesDetected("ShootingStats");
        }

        [Test]
        public void Passing_AllAttributes_AreValidated()
        {
            AssertAllAttributesDetected("PassingStats");
        }

        [Test]
        public void Dribbling_AllAttributes_AreValidated()
        {
            AssertAllAttributesDetected("DribblingStats");
        }

        [Test]
        public void Defending_AllAttributes_AreValidated()
        {
            AssertAllAttributesDetected("DefendingStats");
        }

        [Test]
        public void Physical_AllAttributes_AreValidated()
        {
            AssertAllAttributesDetected("PhysicalStats");
        }

        [Test]
        public void Goalkeeping_AllAttributes_AreValidated()
        {
            AssertAllAttributesDetected("GoalkeepingStats");
        }

        private static void AssertAllAttributesDetected(string statsType)
        {
            var catField = CategoryField(statsType);
            foreach (var attr in Approved[statsType])
            {
                var ps = NewStats();
                SetAttribute(ps, catField, attr, 100);
                var problems = GetProblems(ps);
                Assert.IsTrue(problems.Any(p => p.Contains(attr) && p.Contains("100")),
                    $"'{statsType}.{attr}' set to 100 must be reported as invalid.");
            }
        }

        private static string CategoryField(string statsType) => statsType.Replace("Stats", "");

        // ---- Boundary acceptance: 1 / 50 / 99 are valid for every category ----

        [Test]
        public void MinimumBoundary_1_IsValid_ForAllCategories()
        {
            foreach (var catField in CategoryFields)
            {
                var anyAttr = Approved.First(k => CategoryField(k.Key) == catField).Value[0];
                var ps = NewStats();
                SetAttribute(ps, catField, anyAttr, 1);
                Assert.IsEmpty(GetProblems(ps),
                    $"Value 1 in '{catField}' must be accepted (valid lower boundary).");
            }
        }

        [Test]
        public void MaximumBoundary_99_IsValid_ForAllCategories()
        {
            foreach (var catField in CategoryFields)
            {
                var anyAttr = Approved.First(k => CategoryField(k.Key) == catField).Value[0];
                var ps = NewStats();
                SetAttribute(ps, catField, anyAttr, 99);
                Assert.IsEmpty(GetProblems(ps),
                    $"Value 99 in '{catField}' must be accepted (valid upper boundary).");
            }
        }

        [Test]
        public void Default_50_IsValid_ForAllCategories()
        {
            var ps = NewStats();
            Assert.IsEmpty(GetProblems(ps), "All-default (50) ratings must validate clean for every category.");
        }

        // ---- Invalid values detected (no silent mutation) ----

        [Test]
        public void Zero_IsInvalid_ForAllCategories()
        {
            AssertAllInvalidForCategories(0);
        }

        [Test]
        public void Hundred_IsInvalid_ForAllCategories()
        {
            AssertAllInvalidForCategories(100);
        }

        [Test]
        public void NegativeValues_AreInvalid_ForAllCategories()
        {
            AssertAllInvalidForCategories(-1);
        }

        [Test]
        public void ValuesAbove99_AreInvalid_ForAllCategories()
        {
            AssertAllInvalidForCategories(150);
        }

        private static void AssertAllInvalidForCategories(int value)
        {
            foreach (var catField in CategoryFields)
            {
                foreach (var attr in Approved.First(k => CategoryField(k.Key) == catField).Value)
                {
                    var ps = NewStats();
                    SetAttribute(ps, catField, attr, value);
                    Assert.IsTrue(GetProblems(ps).Any(p => p.Contains(attr) && p.Contains(value.ToString())),
                        $"'{catField}.{attr}' = {value} must be detected as invalid.");
                }
            }
        }

        // ---- Invalid data reported / not silently mutated ----

        [Test]
        public void InvalidValues_AreReported_NotSilentlyMutated()
        {
            var ps = NewStats();
            SetAttribute(ps, "Pace", "Acceleration", 0);
            SetAttribute(ps, "Goalkeeping", "Parrying", 150);
            var problems = GetProblems(ps);

            Assert.IsTrue(problems.Any(p => p.Contains("Acceleration") && p.Contains("0")),
                "Acceleration=0 must be reported.");
            Assert.IsTrue(problems.Any(p => p.Contains("Parrying") && p.Contains("150")),
                "Parrying=150 must be reported.");

            // Nothing may be silently clamped/reset: the authored values stay as authored.
            Assert.AreEqual(0, (int)GetCategory(ps, "Pace").GetType()
                .GetField("Acceleration", BindingFlags.Public | BindingFlags.Instance).GetValue(GetCategory(ps, "Pace")),
                "Validation must NOT clamp 0 -> 1.");
            Assert.AreEqual(150, (int)GetCategory(ps, "Goalkeeping").GetType()
                .GetField("Parrying", BindingFlags.Public | BindingFlags.Instance).GetValue(GetCategory(ps, "Goalkeeping")),
                "Validation must NOT clamp 150 -> 99 or reset to the default.");
        }

        [Test]
        public void InvalidValue_IsNotReplacedByDefault50()
        {
            // The default (50) is NOT a fallback that silently replaces invalid authored data.
            var ps = NewStats();
            SetAttribute(ps, "Passing", "Vision", -1);
            var problems = GetProblems(ps);
            Assert.IsTrue(problems.Any(p => p.Contains("Vision") && p.Contains("-1")),
                "Vision=-1 must be reported, not silently reset to 50.");
            Assert.AreEqual(-1, (int)GetCategory(ps, "Passing").GetType()
                .GetField("Vision", BindingFlags.Public | BindingFlags.Instance).GetValue(GetCategory(ps, "Passing")),
                "Validation must NOT replace invalid value with the 50 default.");
        }

        [Test]
        public void ValidValues_ProduceNoErrors()
        {
            // Explicitly set every attribute to a valid boundary/median value.
            var ps = NewStats();
            foreach (var statsType in Approved.Keys)
            {
                var catField = CategoryField(statsType);
                foreach (var attr in Approved[statsType])
                {
                    SetAttribute(ps, catField, attr, 99);
                }
            }
            Assert.IsEmpty(GetProblems(ps), "All-99 ratings are valid (upper boundary) and must produce no errors.");
        }

        // ---- Actionable diagnostic ----

        [Test]
        public void Validation_IdentifiesCategoryAttributeValueAndRange()
        {
            var ps = NewStats();
            SetAttribute(ps, "Shooting", "Finishing", 101);
            var problems = GetProblems(ps);
            var matched = problems.FirstOrDefault(p => p.Contains("Finishing"));
            Assert.IsNotNull(matched, "The diagnostic must name the attribute.");
            Assert.IsTrue(matched.Contains("Shooting"), "The diagnostic must name the category.");
            Assert.IsTrue(matched.Contains("101"), "The diagnostic must include the offending value.");
            Assert.IsTrue(matched.Contains("[1..99]"), "The diagnostic must include the expected range.");
        }

        // ---- Specific attribute placement (validated in the correct category) ----

        [Test]
        public void Curve_IsValidatedAsPassing()
        {
            AssertAttributeDetectedIn("Passing", "Curve");
        }

        [Test]
        public void TightPossession_IsValidatedAsDribbling()
        {
            AssertAttributeDetectedIn("Dribbling", "TightPossession");
        }

        [Test]
        public void Heading_IsValidatedAsDefending()
        {
            AssertAttributeDetectedIn("Defending", "Heading");
        }

        private static void AssertAttributeDetectedIn(string catField, string attr)
        {
            var ps = NewStats();
            SetAttribute(ps, catField, attr, 100);
            var problems = GetProblems(ps);
            Assert.IsTrue(problems.Any(p => p.Contains(catField) && p.Contains(attr)),
                $"'{attr}' must be validated as part of '{catField}'.");
            // And no other category must own it: setting it invalid ONLY there yields a single problem
            // naming that category (no other category reports it).
            foreach (var other in CategoryFields.Where(c => c != catField))
            {
                Assert.IsFalse(problems.Any(p => p.Contains(other)),
                    $"No other category ('{other}') should report '{attr}'.");
            }
        }

        // ---- Goalkeeping validated for every player (unified) ----

        [Test]
        public void Goalkeeping_Attributes_AreValidatedForEveryPlayer()
        {
            // Goalkeeping is a stored category of the unified PlayerStats; its attributes are validated
            // identically, with no separate goalkeeper validation path.
            foreach (var attr in Approved["GoalkeepingStats"])
            {
                var ps = NewStats();
                SetAttribute(ps, "Goalkeeping", attr, 0);
                Assert.IsTrue(GetProblems(ps).Any(p => p.Contains(attr)),
                    $"Every player's Goalkeeping attribute '{attr}' must be validated.");
            }
            foreach (var name in new[] { "GoalkeeperPlayerStats", "GoalkeeperValidation" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(x => x.Name == name),
                    $"No separate '{name}' may exist — the unified PlayerStats must be validated.");
            }
        }

        // ---- Validation boundaries (Profile / PhysicalProfile NOT validated as stats) ----

        [Test]
        public void PlayerStatsValidation_DoesNotValidateProfileFields()
        {
            // Setting a Profile field out of the 1-99 range must NOT be reported by GetInvalidRatings()
            // (Profile fields are not PlayerStats ratings).
            foreach (var name in new[] { "WeakFoot", "SkillRating", "OverallRating", "PrimaryPosition",
                "CardType", "PlayStyle", "PreferredFoot" })
            {
                // These Profile members are on PlayerDefinition.Profile, NOT on PlayerStats — confirm no
                // such field leaked into PlayerStats and is therefore not validated as a stat.
                Assert.IsNull(StatsType().GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"Profile field '{name}' must not appear on PlayerStats (so it is not 1-99 validated).");
            }
        }

        [Test]
        public void PlayerStatsValidation_DoesNotValidatePhysicalProfile()
        {
            foreach (var name in new[] { "Age", "HeightCm", "WeightKg" })
            {
                Assert.IsNull(StatsType().GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"PhysicalProfile measurement '{name}' must not appear on PlayerStats (so it is not 1-99 validated).");
            }
            // No category field may be a PhysicalProfile measurement.
            foreach (var catField in CategoryFields)
            {
                foreach (var name in new[] { "Age", "HeightCm", "WeightKg" })
                {
                    Assert.IsNull(GetCategoryFieldType(catField).GetField(name, BindingFlags.Public | BindingFlags.Instance),
                        $"'{catField}' must not hold PhysicalProfile measurement '{name}'.");
                }
            }
        }

        private static Type GetCategoryFieldType(string catField)
        {
            return StatsType().GetField(catField, BindingFlags.Public | BindingFlags.Instance).FieldType;
        }

        // ---- No per-category validator / no per-frame / no gameplay ----

        [Test]
        public void NoCategorySpecificValidator_WasCreated()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "PaceValidator", "ShootingValidator", "PassingValidator",
                "DribblingValidator", "DefendingValidator", "PhysicalValidator", "GoalkeepingValidator",
                "AttributeValidator", "PlayerStatsValidator" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null
                    && t.Namespace.StartsWith("Football")),
                    $"No '{name}' may be created — validation is centralized in PlayerStats.GetInvalidRatings().");
            }
        }

        [Test]
        public void NoPerFrameValidation_WasCreated()
        {
            var t = StatsType();
            Assert.IsNull(t.GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayerStats must NOT validate every frame (no Update).");
            Assert.IsNull(t.GetMethod("FixedUpdate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayerStats must NOT validate every fixed step (no FixedUpdate).");
            Assert.IsNull(t.GetField("ValidationTimer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "No validation polling/timer may be created.");
        }

        [Test]
        public void NoGameplayLogic_WasCreatedForValidation()
        {
            var t = StatsType();
            foreach (var name in new[] { "CalculateOverallRating", "GetOverallRating", "CalculateSpeed",
                "ResolveRatings", "NormalizeRatings", "ApplyClamping" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Validation must NOT implement gameplay/normalization via '{name}'.");
            }
        }
    }
}
