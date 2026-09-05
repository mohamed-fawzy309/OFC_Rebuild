using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 98 - Match Duration (authoritative match-duration representation).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance + the real MatchDurationSeconds API).
    ///
    /// Task 98 locks:
    ///   - Match duration is DERIVED as HalfDurationSeconds x NumHalves; there is NO separate
    ///     authored/duplicated MatchDuration field (single authoritative source, no consistency hazard).
    ///   - MatchDurationSeconds is a read-only computed double matching the GameClock feed boundary
    ///     (Football.Core.GameClock.RegulationDurationSeconds is an injected read-only double).
    ///   - MatchDurationSeconds is CONFIGURATION (computed expression), NOT runtime elapsed state
    ///     (no CurrentMatchDuration/ElapsedSeconds/CurrentMatchTime on MatchRulesDefinition).
    ///   - GameClock remains the single runtime elapsed-time authority (no second clock here).
    ///   - Validation coherence: a non-positive derived match duration corresponds to an invalid
    ///     authored half-duration/halves which GetInvalidMatchRulesData DETECTS + REPORTS.
    /// </summary>
    public class MatchDurationTests
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

        private static Type PlayerDefType() => FindType(Prefix + "PlayerDefinition");

        private static Type TeamDefType() => FindType(Prefix + "TeamDefinition");

        private static Type GameClockType() => FindType("Football.Core.GameClock");

        private static object NewMatchRules() => ScriptableObject.CreateInstance(MatchRulesType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static PropertyInfo PropertyOf(Type t, string name) =>
            t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

        private static MethodInfo MethodOf(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              BindingFlags.Static);

        private static void SetField(object target, string field, object value) =>
            FieldOf(target.GetType(), field).SetValue(target, value);

        private static double MatchDurationSecondsOf(object rules) =>
            (double)PropertyOf(rules.GetType(), "MatchDurationSeconds").GetValue(rules, null);

        private static List<string> GetInvalidMatchRulesData(object rules) =>
            (List<string>)MethodOf(MatchRulesType(), "GetInvalidMatchRulesData").Invoke(rules, null);

        // ---- 98.1 Representation ----

        [Test]
        public void MatchDurationSecondsIsAReadOnlyComputedDouble()
        {
            var p = PropertyOf(MatchRulesType(), "MatchDurationSeconds");
            Assert.IsNotNull(p,
                "MatchRulesDefinition must expose a MatchDurationSeconds computed property.");
            Assert.AreEqual(typeof(double), p.PropertyType,
                "MatchDurationSeconds must be a double (matches the GameClock feed boundary).");
            Assert.IsNull(p.SetMethod,
                "MatchDurationSeconds must be read-only (computed expression, not authored/state).");
        }

        [Test]
        public void MatchDurationIsDerivedFromDefaults()
        {
            // HalfDurationSeconds=2700 x NumHalves=2 => 5400s (90 min) by default.
            Assert.AreEqual(5400d, MatchDurationSecondsOf(NewMatchRules()), 1e-9,
                "Default match duration must be 5400s (2700s x 2 halves).");
        }

        [Test]
        public void MatchDurationEqualsHalfTimesHalves()
        {
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", 3000f);
            SetField(r, "NumHalves", 2);
            Assert.AreEqual(6000d, MatchDurationSecondsOf(r), 1e-9,
                "Match duration must equal HalfDurationSeconds x NumHalves (3000 x 2 = 6000).");
        }

        [Test]
        public void MatchDurationScalesWithHalves()
        {
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", 2700f);
            SetField(r, "NumHalves", 4);
            Assert.AreEqual(10800d, MatchDurationSecondsOf(r), 1e-9,
                "Match duration must scale with the number of halves (2700 x 4 = 10800).");
        }

        [Test]
        public void NoSeparateAuthoredMatchDurationField()
        {
            // No duplicated authored duration source (no authoring hazard / consistency split).
            foreach (var n in new[] { "MatchDuration", "MatchDurationSeconds", "RegulationDuration" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition must NOT hold an authored MatchDuration field '{n}' (duration is derived).");
            }
        }

        // ---- 98.2 GameClock boundary ----

        [Test]
        public void GameClockRegulationDurationIsDouble()
        {
            var p = GameClockType().GetProperty("RegulationDurationSeconds",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(p, "GameClock must expose RegulationDurationSeconds.");
            Assert.AreEqual(typeof(double), p.PropertyType,
                "GameClock.RegulationDurationSeconds must be a double feed boundary.");
        }

        [Test]
        public void MatchDurationMatchesGameClockFeedType()
        {
            Assert.AreEqual(typeof(double), PropertyOf(MatchRulesType(), "MatchDurationSeconds").PropertyType,
                "MatchDurationSeconds must feed the double GameClock.RegulationDurationSeconds boundary directly.");
        }

        [Test]
        public void NoSecondClockOrDurationSystems()
        {
            Assert.IsFalse(HasTypeName("MatchClock", "MatchTimer", "SecondGameClock", "MatchTimeSystem",
                    "MatchDurationSystem", "MatchDurationManager"),
                "Task 98 must not create a second clock or duration system.");
        }

        // ---- 98.3 Validation coherence ----

        [Test]
        public void ValidDurationMeansNoProblems()
        {
            var r = NewMatchRules();
            Assert.Greater(MatchDurationSecondsOf(r), 0d);
            Assert.IsEmpty(GetInvalidMatchRulesData(r));
        }

        [Test]
        public void NonPositiveHalfDurationYieldsInvalidDurationAndIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", 0f);
            Assert.LessOrEqual(MatchDurationSecondsOf(r), 0d,
                "A non-positive half duration must yield a non-positive derived match duration.");
            Assert.IsTrue(GetInvalidMatchRulesData(r).Any(x => x.Contains("HalfDurationSeconds")),
                "The invalid authored half duration must be detected and reported.");
        }

        [Test]
        public void ZeroHalvesYieldsZeroDurationAndIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "NumHalves", 0);
            Assert.AreEqual(0d, MatchDurationSecondsOf(r), 1e-9,
                "Zero halves must yield a zero derived match duration.");
            Assert.IsTrue(GetInvalidMatchRulesData(r).Any(x => x.Contains("NumHalves")),
                "The invalid authored NumHalves must be detected and reported.");
        }

        // ---- 98.4 Runtime/config separation + ownership ----

        [Test]
        public void MatchDurationOwnedByMatchRulesDefinition()
        {
            Assert.IsNotNull(PropertyOf(MatchRulesType(), "MatchDurationSeconds"),
                "MatchRulesDefinition owns the authoritative match-duration expression.");
            Assert.IsNull(PropertyOf(PlayerDefType(), "MatchDurationSeconds"),
                "PlayerDefinition must not own match duration.");
            Assert.IsNull(PropertyOf(TeamDefType(), "MatchDurationSeconds"),
                "TeamDefinition must not own match duration.");
        }

        [Test]
        public void NoCurrentRuntimeDurationStateOnMatchRules()
        {
            foreach (var n in new[] { "CurrentMatchDuration", "CurrentMatchTime", "CurrentHalfDuration",
                                      "ElapsedSeconds", "CurrentClock" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition must not hold runtime elapsed duration state '{n}'.");
            }
        }
    }
}
