using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 99 - Half Duration (authoritative half-duration representation).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance.
    ///
    /// Task 99 locks:
    ///   - The HALF DURATION is the single authored `float HalfDurationSeconds` (unit: seconds),
    ///     applied EQUALLY to every one of the `int NumHalves` halves. One authored half-duration
    ///     source; no per-half override/durations (separate half durations are NOT required ->
    ///     POLICY: no speculative per-half fields).
    ///   - Relationship to match duration: MatchDurationSeconds = HalfDurationSeconds x NumHalves
    ///     (Task 98). All halves are equal, so the match duration scales linearly with NumHalves.
    ///   - GameClock boundary: halves are a MATCH-STATE concern, NOT clock state. GameClock exposes
    ///     no half/period state (only Stopped/Running + ElapsedSeconds); a half boundary is a clock
    ///     Stop. No HalfManager / HalfTimeSystem / second half clock.
    ///   - No half-time gameplay is implemented.
    /// </summary>
    public class HalfDurationTests
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

        private static Type GameClockStateType() => FindType("Football.Core.GameClockState");

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

        // ---- 99.1 Representation ----

        [Test]
        public void HalfDurationSecondsIsAuthoredFloatOnMatchRules()
        {
            var f = FieldOf(MatchRulesType(), "HalfDurationSeconds");
            Assert.IsNotNull(f, "HalfDurationSeconds must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(float), f.FieldType,
                "HalfDurationSeconds must be a float (unit: seconds).");
            Assert.AreEqual(MatchRulesType(), f.DeclaringType,
                "HalfDurationSeconds must be declared on MatchRulesDefinition.");
        }

        [Test]
        public void HalfDurationDefaultIsFortyFiveMinutes()
        {
            var r = NewMatchRules();
            Assert.AreEqual(2700f, (float)FieldOf(MatchRulesType(), "HalfDurationSeconds").GetValue(r), 1e-6f,
                "Default half duration must be 2700s (45 minutes).");
        }

        [Test]
        public void NumHalvesIsAuthoredIntOnMatchRules()
        {
            var f = FieldOf(MatchRulesType(), "NumHalves");
            Assert.IsNotNull(f, "NumHalves must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(int), f.FieldType, "NumHalves must be an int.");
            Assert.AreEqual(MatchRulesType(), f.DeclaringType, "NumHalves must be declared on MatchRulesDefinition.");
            Assert.AreEqual(2, (int)f.GetValue(NewMatchRules()), "Default NumHalves must be 2.");
        }

        [Test]
        public void HalfDurationNotOwnedByPlayerOrTeam()
        {
            Assert.IsNull(FieldOf(PlayerDefType(), "HalfDurationSeconds"),
                "PlayerDefinition must not own half duration.");
            Assert.IsNull(FieldOf(TeamDefType(), "HalfDurationSeconds"),
                "TeamDefinition must not own half duration.");
        }

        // ---- 99.2 Equal halves / no per-half overrides ----

        [Test]
        public void EveryHalfSharesTheSingleAuthoredHalfDuration()
        {
            // One authored half-duration value (HalfDurationSeconds) applies to all halves:
            // there is no per-half override field to introduce inconsistency.
            foreach (var n in new[]
            {
                "FirstHalfDurationSeconds", "SecondHalfDurationSeconds", "HalfDurations",
                "PerHalfDurations", "HalfDurationOverride", "HalfFinalMinutes", "HalfDurationsSeconds"
            })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Separate per-half durations are not required; must not create '{n}'.");
            }
        }

        // ---- 99.3 Relationship to match duration ----

        [Test]
        public void MatchDurationIsHalfTimesHalves()
        {
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", 3000f);
            SetField(r, "NumHalves", 2);
            Assert.AreEqual(6000d, MatchDurationSecondsOf(r), 1e-9,
                "Match duration must equal half duration x number of halves.");
        }

        [Test]
        public void EqualHalvesScaleMatchDurationLinearlyWithHalves()
        {
            // Because every half has the same duration, adding an equal half scales match duration.
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", 2700f);
            SetField(r, "NumHalves", 3);
            Assert.AreEqual(8100d, MatchDurationSecondsOf(r), 1e-9,
                "Three equal 2700s halves must yield 8100s.");
        }

        // ---- 99.4 GameClock boundary ----

        [Test]
        public void GameClockHasNoHalfOrPeriodState()
        {
            foreach (var n in new[]
            {
                "CurrentHalf", "HalfNumber", "CurrentPeriod", "HalfDurationSeconds",
                "PeriodDurationSeconds", "HalfStartedAt"
            })
            {
                Assert.IsNull(FieldOf(GameClockType(), n),
                    $"GameClock must not hold half/period state '{n}' (halves are a match-state concern).");
            }
            Assert.IsNull(PropertyOf(GameClockType(), "CurrentHalf"),
                "GameClock must not expose CurrentHalf.");
        }

        [Test]
        public void GameClockStateIsOnlyStoppedAndRunning()
        {
            var names = Enum.GetNames(GameClockStateType());
            Assert.That(names, Is.EquivalentTo(new[] { "Stopped", "Running" }),
                "GameClockState must have exactly Stopped and Running (no Paused/HalfTime state).");
        }

        [Test]
        public void NoHalfManagerOrHalfTimeSystemCreated()
        {
            Assert.IsFalse(HasTypeName("HalfManager", "HalfTimeSystem", "HalfClock", "SecondHalfClock",
                    "HalftimeSystem", "HalfTimeManager"),
                "Task 99 must not create any half manager/system/second half clock.");
        }

        // ---- 99.5 Validation coherence ----

        [Test]
        public void NonPositiveHalfDurationYieldsInvalidAndDetected()
        {
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", 0f);
            Assert.LessOrEqual(MatchDurationSecondsOf(r), 0d,
                "A non-positive half duration must yield a non-positive derived match duration.");
            Assert.IsTrue(GetInvalidMatchRulesData(r).Any(x => x.Contains("HalfDurationSeconds")),
                "The invalid authored half duration must be detected and reported.");
        }

        [Test]
        public void ValidHalfConfigurationHasNoProblems()
        {
            var r = NewMatchRules();
            SetField(r, "HalfDurationSeconds", 2700f);
            SetField(r, "NumHalves", 2);
            Assert.Greater(MatchDurationSecondsOf(r), 0d);
            Assert.IsEmpty(GetInvalidMatchRulesData(r));
        }
    }
}
