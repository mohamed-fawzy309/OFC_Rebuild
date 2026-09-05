using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 67 — PlayerStats (football attribute container).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef), which this test
    /// assembly cannot reference at compile time — so the actual types are inspected by reflection
    /// from the loaded AppDomain (real type/field inspection, not source.Contains).
    ///
    /// Verifies the seven strongly-typed categories, the exact approved attribute sets, strict
    /// separation from identity/physical/profile and runtime state, the consistent integer rating
    /// scale [RatingMin..RatingMax], explicit validation (invalid ratings are DETECTED, never
    /// silently clamped), the Curve-in-Passing and TightPossession-in-Dribbling rules, the unified
    /// Goalkeeping-for-every-player rule, and that no unapproved/duplicate attributes exist.
    /// </summary>
    public class PlayerStatsTests
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

        private static IEnumerable<string> PublicInstanceFieldNames(Type t)
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(f => f.Name)
                .Select(f => f.Name);
        }

        // ---- 67.1 Responsibility ----

        [Test]
        public void PlayerStats_Exists_IsSerializableDataClass()
        {
            var t = FindType(Prefix + "PlayerStats");
            Assert.IsTrue(t.IsClass && !t.IsAbstract, "PlayerStats must be a class (data container).");
            Assert.IsTrue(t.IsSerializable, "PlayerStats must be [Serializable] authored data.");
            Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(t),
                "PlayerStats must NOT be a ScriptableObject asset — it is the stats group held inside the PlayerDefinition asset.");
        }

        [Test]
        public void PlayerStats_HasExactlySevenCategories()
        {
            var t = FindType(Prefix + "PlayerStats");
            var fields = t
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsLiteral) // const Rating* are not instance fields anyway
                .ToDictionary(f => f.Name, f => f.FieldType.FullName);

            Assert.Contains("Pace", fields.Keys);
            Assert.Contains("Shooting", fields.Keys);
            Assert.Contains("Passing", fields.Keys);
            Assert.Contains("Dribbling", fields.Keys);
            Assert.Contains("Defending", fields.Keys);
            Assert.Contains("Physical", fields.Keys);
            Assert.Contains("Goalkeeping", fields.Keys);
            Assert.AreEqual(7, fields.Count,
                "PlayerStats must contain exactly seven category fields (6 outfield + Goalkeeping).");
        }

        // ---- 67.2 Separation ----

        [Test]
        public void PlayerStats_SeparatesStatsFromIdentity()
        {
            AssertNoFields(IdentityAndProfileAndPhysicalNames(),
                "Identity data must not appear in PlayerStats.");
        }

        [Test]
        public void PlayerStats_SeparatesStatsFromPhysicalProfile()
        {
            AssertNoFields(new[] { "Age", "HeightCm", "WeightKg" },
                "Physical-profile data must not appear in PlayerStats.");
        }

        [Test]
        public void PlayerStats_SeparatesStatsFromProfile()
        {
            AssertNoFields(new[]
            {
                "PrimaryPosition", "SecondaryPositions", "PreferredFoot", "WeakFoot",
                "SkillRating", "OverallRating", "CardType", "PlayStyle"
            }, "Profile data must not appear in PlayerStats.");
        }

        private static void AssertNoFields(IEnumerable<string> forbidden, string message)
        {
            var types = new[]
            {
                FindType(Prefix + "PlayerStats"),
                FindType(Prefix + "PaceStats"),
                FindType(Prefix + "ShootingStats"),
                FindType(Prefix + "PassingStats"),
                FindType(Prefix + "DribblingStats"),
                FindType(Prefix + "DefendingStats"),
                FindType(Prefix + "PhysicalStats"),
                FindType(Prefix + "GoalkeepingStats")
            };
            foreach (var t in types)
            {
                foreach (var name in forbidden)
                {
                    Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        message + $" ('{t.Name}.{name}')");
                    Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        message + $" ('{t.Name}.{name}')");
                }
            }
        }

        // ---- 67.3 Categories exist ----

        [Test]
        public void Categories_Exist_AsStronglyTypedTypes()
        {
            var ps = FindType(Prefix + "PlayerStats");
            foreach (var (field, type) in new[]
            {
                ("Pace", "PaceStats"), ("Shooting", "ShootingStats"), ("Passing", "PassingStats"),
                ("Dribbling", "DribblingStats"), ("Defending", "DefendingStats"),
                ("Physical", "PhysicalStats"), ("Goalkeeping", "GoalkeepingStats")
            })
            {
                var f = ps.GetField(field, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"PlayerStats must have field '{field}'.");
                Assert.AreEqual(Prefix + type, f.FieldType.FullName,
                    $"'{field}' must be strongly typed as {type}, not string/Dictionary.");
            }
        }

        [Test]
        public void Curve_BelongsToPassing_Only()
        {
            var passing = FindType(Prefix + "PassingStats");
            Assert.IsNotNull(passing.GetField("Curve", BindingFlags.Public | BindingFlags.Instance),
                "Curve belongs to Passing.");
            foreach (var other in new[] { "ShootingStats", "DribblingStats" })
            {
                Assert.IsNull(FindType(Prefix + other).GetField("Curve", BindingFlags.Public | BindingFlags.Instance),
                    $"Curve must NOT be in {other}.");
            }
        }

        [Test]
        public void TightPossession_BelongsToDribbling_Only()
        {
            var drib = FindType(Prefix + "DribblingStats");
            Assert.IsNotNull(drib.GetField("TightPossession", BindingFlags.Public | BindingFlags.Instance),
                "TightPossession belongs to Dribbling.");
            foreach (var other in new[] { "PaceStats", "ShootingStats", "PassingStats",
                "DefendingStats", "PhysicalStats", "GoalkeepingStats" })
            {
                Assert.IsNull(FindType(Prefix + other).GetField("TightPossession", BindingFlags.Public | BindingFlags.Instance),
                    $"TightPossession must NOT be in {other}.");
            }
        }

        [Test]
        public void Goalkeeping_IsSeventhStoredCategory_ForEveryPlayer()
        {
            var ps = FindType(Prefix + "PlayerStats");
            var f = ps.GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Goalkeeping must be a stored category for every player (unified model).");
            // No separate goalkeeper stats asset/type split.
            foreach (var name in new[] { "GoalkeeperPlayerStats", "GoalkeeperDefinition" })
            {
                var dup = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football"));
                Assert.IsFalse(dup, $"No separate '{name}' type may exist — the unified PlayerStats model must be used.");
            }
        }

        // ---- 67.3 Approved attribute sets per category ----

        [Test]
        public void EachCategory_Contains_ExactlyTheApprovedAttributes()
        {
            var approved = new Dictionary<string, string[]>
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

            var allApproved = approved.Values.SelectMany(v => v).ToArray();
            foreach (var kv in approved)
            {
                var t = FindType(Prefix + kv.Key);
                var actual = PublicInstanceFieldNames(t).ToArray();
                Assert.AreEqual(kv.Value.OrderBy(x => x).ToArray(), actual,
                    $"Category '{kv.Key}' must contain exactly the approved attributes, nothing more/less.");
            }
            Assert.AreEqual(allApproved.Length, allApproved.Distinct().Count(),
                "Approved attributes must not be duplicated across categories.");
        }

        [Test]
        public void NoUnapprovedAttributes_AcrossAllCategories()
        {
            var approved = new HashSet<string>(new[]
            {
                "Acceleration", "SprintSpeed",
                "AttackingAwareness", "Finishing", "ShotPower", "LongShots", "Volleys", "Penalties",
                "Vision", "ShortPassing", "LongPassing", "Crossing", "FreeKickAccuracy", "Curve",
                "Dribbling", "BallControl", "TightPossession", "Agility", "Balance", "Reactions",
                "DefensiveAwareness", "Interceptions", "StandingTackle", "SlidingTackle", "Heading",
                "Strength", "Stamina", "Jumping", "Aggression",
                "Diving", "Handling", "Kicking", "Positioning", "Reflexes", "Parrying"
            });
            foreach (var cat in new[] { "PaceStats", "ShootingStats", "PassingStats", "DribblingStats",
                "DefendingStats", "PhysicalStats", "GoalkeepingStats" })
            {
                foreach (var name in PublicInstanceFieldNames(FindType(Prefix + cat)))
                {
                    Assert.IsTrue(approved.Contains(name),
                        $"'{name}' in '{cat}' is NOT an approved Task 67 attribute.");
                }
            }
        }

        // ---- 67.4 Rating scale ----

        [Test]
        public void RatingScale_IsDefined_ConsistentInteger()
        {
            var t = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)t.GetField("RatingMin").GetRawConstantValue(), "RatingMin must be 1.");
            Assert.AreEqual(99, (int)t.GetField("RatingMax").GetRawConstantValue(), "RatingMax must be 99.");
            Assert.AreEqual(50, (int)t.GetField("RatingDefault").GetRawConstantValue(), "RatingDefault must be 50.");
        }

        [Test]
        public void AllAttributeFields_AreInteger()
        {
            foreach (var cat in new[] { "PaceStats", "ShootingStats", "PassingStats", "DribblingStats",
                "DefendingStats", "PhysicalStats", "GoalkeepingStats" })
            {
                var t = FindType(Prefix + cat);
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.AreEqual(typeof(int), f.FieldType,
                        $"Attribute '{cat}.{f.Name}' must be int (simplest consistent authored rating type).");
                }
            }
        }

        [Test]
        public void AttributeFields_DefaultTo_RatingDefault()
        {
            foreach (var cat in new[] { "PaceStats", "ShootingStats", "PassingStats", "DribblingStats",
                "DefendingStats", "PhysicalStats", "GoalkeepingStats" })
            {
                var obj = Activator.CreateInstance(FindType(Prefix + cat));
                foreach (var f in FindType(Prefix + cat).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.AreEqual(50, (int)f.GetValue(obj),
                        $"'{cat}.{f.Name}' must default to RatingDefault (50).");
                }
            }
        }

        [Test]
        public void WeakFootAndSkillRating_AreNotForcedIntoStatScale()
        {
            // WeakFoot / SkillRating belong to Profile (own conceptual scale), not PlayerStats.
            AssertNoFields(new[] { "WeakFoot", "SkillRating" },
                "WeakFoot/SkillRating must remain in Profile, not PlayerStats.");
            foreach (var full in new[] { Prefix + "PlayerStats", Prefix + "PaceStats",
                Prefix + "ShootingStats", Prefix + "PassingStats", Prefix + "DribblingStats",
                Prefix + "DefendingStats", Prefix + "PhysicalStats", Prefix + "GoalkeepingStats" })
            {
                var t = FindType(full);
                Assert.IsNull(t.GetField("OverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"'{full}' must NOT hold OverallRating — it is authored in Profile, not coupled to PlayerStats.");
            }
        }

        // ---- 67.5 Validation ----

        [Test]
        public void ValidationRule_Exists_DetectsOutOfRange()
        {
            var t = FindType(Prefix + "PlayerStats");
            var m = t.GetMethod("GetInvalidRatings", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(m, "PlayerStats must expose explicit validation (GetInvalidRatings).");
            Assert.AreEqual(typeof(List<string>), m.ReturnType);
        }

        [Test]
        public void Validation_DoesNotSilentlyClamp_ItReports()
        {
            var psType = FindType(Prefix + "PlayerStats");
            var ps = Activator.CreateInstance(psType);
            var pace = psType.GetField("Pace", BindingFlags.Public | BindingFlags.Instance).GetValue(ps);
            pace.GetType().GetField("SprintSpeed", BindingFlags.Public | BindingFlags.Instance).SetValue(pace, 150);
            pace.GetType().GetField("Acceleration", BindingFlags.Public | BindingFlags.Instance).SetValue(pace, 0);

            var problems = (List<string>)psType.GetMethod("GetInvalidRatings",
                BindingFlags.Public | BindingFlags.Instance).Invoke(ps, null);

            Assert.IsTrue(problems.Any(p => p.Contains("SprintSpeed") && p.Contains("150")),
                "An out-of-range high value (150) must be reported, NOT clamped to 99.");
            Assert.IsTrue(problems.Any(p => p.Contains("Acceleration") && p.Contains("0")),
                "A below-range value (0) must be reported, NOT clamped to 1.");
            // Ensure SprintSpeed was NOT silently mutated to 99.
            Assert.AreEqual(150, (int)pace.GetType().GetField("SprintSpeed",
                BindingFlags.Public | BindingFlags.Instance).GetValue(pace),
                "Validation must not mutate/clamp authored data.");
        }

        [Test]
        public void Validation_ValidData_ReportsNoProblems()
        {
            var psType = FindType(Prefix + "PlayerStats");
            var ps = Activator.CreateInstance(psType);
            var problems = (List<string>)psType.GetMethod("GetInvalidRatings",
                BindingFlags.Public | BindingFlags.Instance).Invoke(ps, null);
            CollectionAssert.IsEmpty(problems, "Default (valid) ratings must validate clean.");
        }

        // ---- No runtime state / no gameplay ----

        [Test]
        public void PlayerStats_ContainsNoRuntimeState()
        {
            AssertNoFields(new[]
            {
                "CurrentStamina", "CurrentHealth", "CurrentPosition", "CurrentVelocity",
                "CurrentState", "CurrentPossession", "CurrentSpeed", "CurrentInput",
                "CurrentAnimation", "CurrentAIState", "Position", "Velocity", "Score"
            }, "PlayerStats must NOT hold runtime state.");
        }

        [Test]
        public void PlayerStats_ContainsNoGameplayLogic()
        {
            var t = FindType(Prefix + "PlayerStats");
            foreach (var name in new[] { "CalculateSpeed", "CalculateShotPower", "CalculateDribble",
                "CalculateDefense", "CalculateOverallRating", "GetOverallRating" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance),
                    $"PlayerStats must NOT derive gameplay ('{name}').");
            }
        }

        // ---- helpers ----

        private static string[] IdentityAndProfileAndPhysicalNames()
        {
            return new[]
            {
                "PlayerId", "Name", "Nationality", "ClubReference",
                "Age", "HeightCm", "WeightKg",
                "PrimaryPosition", "SecondaryPositions", "PreferredFoot", "WeakFoot",
                "SkillRating", "OverallRating", "CardType", "PlayStyle"
            };
        }
    }
}
