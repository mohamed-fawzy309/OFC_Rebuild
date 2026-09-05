using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 97 - MatchRulesDefinition (canonical AUTHORED MATCH CONFIGURATION container).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance + the real GetInvalidMatchRulesData API).
    ///
    /// Task 97 locks:
    ///   - MatchRulesDefinition is the canonical authored MATCH RULES / CONFIGURATION owner.
    ///   - Conflict #1 (ball scope): the Ball block was REMOVED from MatchRulesDefinition; BallConfig
    ///     is the SOLE ball-physics owner (Mass/Radius present on BallConfig, absent on
    ///     MatchRulesDefinition). No ball-physics duplication.
    ///   - Match rules vs RUNTIME MATCH STATE separation (no CurrentHalf/CurrentScore/... on rules).
    ///   - No Player Data (PlayerDefinition) or Team Data (TeamDefinition) duplication on rules.
    ///   - GameClock remains the single runtime elapsed-time authority (no MatchClock/MatchTimer/
    ///     SecondGameClock/HalfManager).
    ///   - Validation (GetInvalidMatchRulesData) DETECTS + REPORTS container-level integrity,
    ///     never mutates (no clamp/reset/fabricate). Later-rule semantics (98-106) are NOT pre-empted.
    /// </summary>
    public class MatchRulesTests
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

        private static Type MatchRulesType() => FindType(Prefix + "MatchRulesDefinition");

        private static Type BallConfigType() => FindType(Prefix + "BallConfig");

        private static object NewMatchRules() => ScriptableObject.CreateInstance(MatchRulesType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static MethodInfo MethodOf(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              BindingFlags.Static);

        private static void SetField(object target, string field, object value) =>
            FieldOf(target.GetType(), field).SetValue(target, value);

        private static List<string> GetInvalidMatchRulesData(object rules) =>
            (List<string>)MethodOf(MatchRulesType(), "GetInvalidMatchRulesData").Invoke(rules, null);

        // ---- 97.1 Canonical container + ownership ----

        [Test]
        public void MatchRulesDefinitionIsAnAuthoredScriptableObject()
        {
            var t = MatchRulesType();
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(t),
                "MatchRulesDefinition must be an authored ScriptableObject.");
        }

        [Test]
        public void MatchRulesFieldsOwnedByMatchRulesDefinition()
        {
            // Retained canonical match-rules fields live on MatchRulesDefinition.
            foreach (var n in new[]
            {
                "HalfDurationSeconds", "NumHalves", "UseExtraTime", "ExtraTimeDurationSeconds",
                "UsePenaltyShootout", "FieldLength", "FieldWidth", "GoalWidth", "GoalHeight",
                "GoalDepth", "CenterCircleRadius", "PenaltyAreaLength", "PenaltyAreaWidth",
                "GoalAreaLength", "GoalAreaWidth", "MaxSubstitutions", "EnforceOffside"
            })
            {
                var f = FieldOf(MatchRulesType(), n);
                Assert.IsNotNull(f, $"'{n}' must exist on MatchRulesDefinition.");
                Assert.AreEqual(MatchRulesType(), f.DeclaringType,
                    $"'{n}' must be declared on MatchRulesDefinition.");
            }
        }

        [Test]
        public void Conflict1BallBlockRemovedFromMatchRulesDefinition()
        {
            // Decision A: the duplicate Ball block is REMOVED from MatchRulesDefinition.
            foreach (var n in new[] { "BallRadius", "BallMass", "GravityMultiplier" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition must NOT own ball physics field '{n}' (BallConfig is the sole owner).");
            }
        }

        [Test]
        public void BallConfigRemainsTheSoleBallPhysicsOwner()
        {
            // BallConfig still owns Radius/Mass (the canonical ball physics).
            Assert.IsNotNull(FieldOf(BallConfigType(), "Radius"),
                "BallConfig must retain ownership of ball Radius.");
            Assert.IsNotNull(FieldOf(BallConfigType(), "Mass"),
                "BallConfig must retain ownership of ball Mass.");
        }

        [Test]
        public void MatchRulesDoNotDuplicateBallConfigPhysics()
        {
            // No ball/dribble tuning field may be authored on the match rules container. (Including
            // DribbleStickDistance — a dribble-specific ball-stick tuning value owned by DribbleConfig.)
            foreach (var n in new[]
            {
                "Radius", "Mass", "Drag", "AngularDrag", "Bounciness", "MaxSpeed", "Friction",
                "RollingFriction", "AirResistance", "KickForce", "PassForce", "ChipForce",
                "DribbleStickDistance"
            })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition must not duplicate ball/dribble tuning field '{n}'.");
            }
        }

        [Test]
        public void MatchRulesDoNotHoldRuntimeMatchState()
        {
            // Rules are authored configuration; current runtime state must NOT live here.
            foreach (var n in new[]
            {
                "CurrentMatchTime", "CurrentHalf", "CurrentScore", "HomeGoals", "AwayGoals",
                "CurrentCards", "CurrentSubstitutions", "CurrentPossession", "CurrentRestart",
                "CurrentMatchState", "ElapsedSeconds", "CurrentClock"
            })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition must not hold runtime match state '{n}'.");
            }
        }

        [Test]
        public void MatchRulesDoNotDuplicatePlayerData()
        {
            foreach (var n in new[] { "PlayerId", "PlayerName", "PlayerStats", "PlayerPosition",
                                      "WeakFoot", "SkillRating" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition must not duplicate Player data '{n}'.");
            }
        }

        [Test]
        public void MatchRulesDoNotDuplicateTeamData()
        {
            foreach (var n in new[] { "TeamId", "TeamName", "Squad", "Formation", "Tactics",
                                      "TeamRatings", "HomeKitMaterial", "AwayKitMaterial" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition must not duplicate Team data '{n}'.");
            }
        }

        [Test]
        public void NoSecondClockCreated()
        {
            // GameClock is the single runtime elapsed-time authority.
            Assert.IsFalse(HasTypeName("MatchClock", "MatchTimer", "SecondGameClock", "MatchTimeSystem"),
                "No second clock / match timer must be created.");
        }

        // ---- 97.2 Validation boundary ----

        [Test]
        public void ValidMatchRulesHaveNoProblems()
        {
            // Fresh instance with default authored values is valid.
            Assert.IsEmpty(GetInvalidMatchRulesData(NewMatchRules()));
        }

        [Test]
        public void NumHalvesZeroIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "NumHalves", 0);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsTrue(p.Any(x => x.Contains("NumHalves")), "NumHalves=0 must be detected.");
        }

        [Test]
        public void NumHalvesNegativeIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "NumHalves", -1);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsTrue(p.Any(x => x.Contains("NumHalves")), "Negative NumHalves must be detected.");
        }

        [Test]
        public void HalfDurationSecondsZeroIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", 0f);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsTrue(p.Any(x => x.Contains("HalfDurationSeconds")),
                "HalfDurationSeconds=0 must be detected.");
        }

        [Test]
        public void HalfDurationSecondsNegativeIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", -5f);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsTrue(p.Any(x => x.Contains("HalfDurationSeconds")),
                "Negative HalfDurationSeconds must be detected.");
        }

        [Test]
        public void ExtraTimeEnabledWithZeroDurationIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "UseExtraTime", true);
            SetField(r, "ExtraTimeDurationSeconds", 0f);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsTrue(p.Any(x => x.Contains("ExtraTimeDurationSeconds")),
                "Extra time enabled with non-positive duration must be detected.");
        }

        [Test]
        public void ExtraTimeDisabledWithZeroDurationIsNotReported()
        {
            // Extra-time duration is only validated when extra time is actually enabled.
            var r = NewMatchRules();
            SetField(r, "UseExtraTime", false);
            SetField(r, "ExtraTimeDurationSeconds", 0f);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsFalse(p.Any(x => x.Contains("ExtraTimeDurationSeconds")),
                "A zero extra-time duration is immaterial when extra time is disabled.");
        }

        [Test]
        public void MaxSubstitutionsNegativeIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "MaxSubstitutions", -1);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsTrue(p.Any(x => x.Contains("MaxSubstitutions")),
                "Negative MaxSubstitutions must be detected.");
        }

        [Test]
        public void MaxSubstitutionsZeroIsValid()
        {
            var r = NewMatchRules();
            SetField(r, "MaxSubstitutions", 0);
            Assert.IsEmpty(GetInvalidMatchRulesData(r));
        }

        [Test]
        public void FieldDimensionsZeroAreDetected()
        {
            foreach (var n in new[]
            {
                "FieldLength", "FieldWidth", "GoalWidth", "GoalHeight", "GoalDepth",
                "CenterCircleRadius", "PenaltyAreaLength", "PenaltyAreaWidth",
                "GoalAreaLength", "GoalAreaWidth"
            })
            {
                var r = NewMatchRules();
                SetField(r, n, 0f);
                var p = GetInvalidMatchRulesData(r);
                Assert.IsTrue(p.Any(x => x.Contains(n)),
                    $"Field dimension '{n}' = 0 must be detected.");
            }
        }

        [Test]
        public void FieldDimensionsNegativeAreDetected()
        {
            foreach (var n in new[] { "FieldLength", "FieldWidth", "GoalWidth", "GoalHeight",
                                      "GoalDepth", "CenterCircleRadius" })
            {
                var r = NewMatchRules();
                SetField(r, n, -1f);
                var p = GetInvalidMatchRulesData(r);
                Assert.IsTrue(p.Any(x => x.Contains(n)),
                    $"Field dimension '{n}' negative must be detected.");
            }
        }

        [Test]
        public void InvalidMatchRulesAreNotSilentlyMutated()
        {
            var r = NewMatchRules();
            SetField(r, "NumHalves", 0);
            SetField(r, "HalfDurationSeconds", 0f);
            GetInvalidMatchRulesData(r);
            Assert.AreEqual(0, (int)FieldOf(MatchRulesType(), "NumHalves").GetValue(r),
                "Validation must not clamp/replace the invalid NumHalves.");
            Assert.AreEqual(0f, (float)FieldOf(MatchRulesType(), "HalfDurationSeconds").GetValue(r),
                "Validation must not clamp/replace the invalid HalfDurationSeconds.");
        }

        [Test]
        public void ValidationLivesOnMatchRulesDefinition()
        {
            Assert.IsNotNull(MethodOf(MatchRulesType(), "GetInvalidMatchRulesData"),
                "Match Rules validation must live on MatchRulesDefinition.");
        }
    }
}
