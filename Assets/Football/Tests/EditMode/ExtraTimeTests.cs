using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 100 - Extra Time (authored extra-time configuration).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance).
    ///
    /// Task 100 locks:
    ///   - `UseExtraTime` (bool, default FALSE) = opt-in switch; a match is NOT assumed to have
    ///     extra time.
    ///   - `ExtraTimeDurationSeconds` (float, default 1800) = the TOTAL extra-time block duration,
    ///     in seconds (single field; unambiguous total, NOT per-period).
    ///   - Number of extra periods is NOT ESTABLISHED -> no `ExtraPeriods`/period-count field
    ///     (POLICY: no invented period count).
    ///   - Extra time is SEPARATE from regulation time (does not inflate MatchDurationSeconds) and
    ///     SEPARATE from the penalty shootout (`UsePenaltyShootout` stays an independent flag).
    ///   - GameClock is the single elapsed-time authority with NO extra-time/overtime state; no
    ///     ExtraTimeSystem/OvertimeManager/ExtraTimeClock; GameStateId has no ExtraTime state.
    ///   - Validation (GetInvalidMatchRulesData) DETECTS + REPORTS extra time enabled with a
    ///     non-positive duration and never mutates.
    /// </summary>
    public class ExtraTimeTests
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

        private static Type GameStateIdType() => FindType("Football.Core.GameStateId");

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

        private static object GetField(object target, string field) =>
            FieldOf(target.GetType(), field).GetValue(target);

        private static double MatchDurationSecondsOf(object rules) =>
            (double)PropertyOf(rules.GetType(), "MatchDurationSeconds").GetValue(rules, null);

        private static List<string> GetInvalidMatchRulesData(object rules) =>
            (List<string>)MethodOf(MatchRulesType(), "GetInvalidMatchRulesData").Invoke(rules, null);

        // ---- 100.1 Representation ----

        [Test]
        public void UseExtraTimeIsAuthoredBoolOnMatchRules()
        {
            var f = FieldOf(MatchRulesType(), "UseExtraTime");
            Assert.IsNotNull(f, "UseExtraTime must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(bool), f.FieldType, "UseExtraTime must be a bool.");
            Assert.AreEqual(MatchRulesType(), f.DeclaringType, "UseExtraTime must be declared on MatchRulesDefinition.");
        }

        [Test]
        public void ExtraTimeDurationSecondsIsAuthoredFloatTotal()
        {
            var f = FieldOf(MatchRulesType(), "ExtraTimeDurationSeconds");
            Assert.IsNotNull(f, "ExtraTimeDurationSeconds must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(float), f.FieldType, "ExtraTimeDurationSeconds must be a float (seconds).");
            Assert.AreEqual(1800f, (float)f.GetValue(NewMatchRules()), 1e-6f,
                "Default total extra-time duration must be 1800s (30 min).");
        }

        [Test]
        public void ExtraTimeNotOwnedByPlayerOrTeam()
        {
            Assert.IsNull(FieldOf(PlayerDefType(), "UseExtraTime"), "PlayerDefinition must not own extra time.");
            Assert.IsNull(FieldOf(TeamDefType(), "UseExtraTime"), "TeamDefinition must not own extra time.");
            Assert.IsNull(FieldOf(PlayerDefType(), "ExtraTimeDurationSeconds"), "PlayerDefinition must not own extra-time duration.");
            Assert.IsNull(FieldOf(TeamDefType(), "ExtraTimeDurationSeconds"), "TeamDefinition must not own extra-time duration.");
        }

        // ---- 100.2 Opt-in (not assumed) ----

        [Test]
        public void ExtraTimeIsNotEnabledByDefault()
        {
            // A match is NOT assumed to have extra time; it is opt-in.
            Assert.IsFalse((bool)GetField(NewMatchRules(), "UseExtraTime"),
                "UseExtraTime must default to false (extra time is optional, not assumed).");
        }

        [Test]
        public void NoExtraTimePeriodCountField()
        {
            // Number of extra periods is not established; no invented period-count field.
            foreach (var n in new[] { "ExtraPeriods", "ExtraPeriodCount", "NumExtraPeriods",
                                      "UseOvertime", "ExtraPeriodDurationSeconds" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Extra-time period count is POLICY/deferred; must not create '{n}'.");
            }
        }

        // ---- 100.3 Relationship to regulation time ----

        [Test]
        public void ExtraTimeDoesNotInflateRegulationMatchDuration()
        {
            // Regulation MatchDurationSeconds (half x halves) is independent of enabling extra time.
            var r = NewMatchRules();
            SetField(r, "UseExtraTime", true);
            SetField(r, "ExtraTimeDurationSeconds", 1800f);
            Assert.AreEqual(5400d, MatchDurationSecondsOf(r), 1e-9,
                "Extra time must not inflate the regulation match duration.");
        }

        [Test]
        public void NoCombinedTotalDurationIncludingExtraTime()
        {
            Assert.IsNull(PropertyOf(MatchRulesType(), "TotalDurationIncludingExtraTime"),
                "No combined regulation+extra-time total is created (extra time is separate/optional).");
            Assert.IsNull(PropertyOf(MatchRulesType(), "FullMatchDurationSeconds"),
                "No speculative combined match-duration total is created.");
        }

        // ---- 100.4 Relationship to penalty shootout ----

        [Test]
        public void ExtraTimeIsSeparateFromPenaltyShootout()
        {
            // UsePenaltyShootout stays an independent flag; enabling extra time does not couple to it.
            var f = FieldOf(MatchRulesType(), "UsePenaltyShootout");
            Assert.IsNotNull(f, "UsePenaltyShootout must remain an independent authored flag on MatchRulesDefinition.");
            var r = NewMatchRules();
            SetField(r, "UseExtraTime", true);
            Assert.IsFalse((bool)f.GetValue(r),
                "Enabling extra time must NOT auto-enable the penalty shootout.");
        }

        // ---- 100.5 GameClock / no second clock ----

        [Test]
        public void GameClockHasNoExtraTimeState()
        {
            foreach (var n in new[] { "IsExtraTime", "ExtraTimeDurationSeconds", "CurrentPeriod",
                                      "IsOvertime", "InExtraTime" })
            {
                Assert.IsNull(FieldOf(GameClockType(), n),
                    $"GameClock must not hold extra-time/overtime state '{n}'.");
            }
        }

        [Test]
        public void NoExtraTimeSystemOrSecondClockCreated()
        {
            Assert.IsFalse(HasTypeName("ExtraTimeSystem", "OvertimeManager", "ExtraTimeManager",
                    "ExtraTimeClock", "OvertimeClock", "ExtraTimeController"),
                "Task 100 must not create extra-time systems or a second clock.");
        }

        [Test]
        public void GameStateIdHasNoExtraTimeState()
        {
            var names = Enum.GetNames(GameStateIdType());
            Assert.IsFalse(names.Any(x => x.IndexOf("Extra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          x.IndexOf("Overtime", StringComparison.OrdinalIgnoreCase) >= 0),
                "Extra time is additional Playing time, not a distinct GameStateId state.");
        }

        // ---- 100.6 Validation coherence ----

        [Test]
        public void ExtraTimeEnabledWithNonPositiveDurationIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "UseExtraTime", true);
            SetField(r, "ExtraTimeDurationSeconds", 0f);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsTrue(p.Any(x => x.Contains("ExtraTimeDurationSeconds")),
                "Extra time enabled with a non-positive duration must be detected and reported.");
        }

        [Test]
        public void ExtraTimeDisabledIgnoresDuration()
        {
            // When extra time is disabled the duration value is immaterial (not reported).
            var r = NewMatchRules();
            SetField(r, "UseExtraTime", false);
            SetField(r, "ExtraTimeDurationSeconds", -5f);
            Assert.IsEmpty(GetInvalidMatchRulesData(r).Where(x => x.Contains("ExtraTimeDurationSeconds")).ToList(),
                "A non-positive extra-time duration is immaterial when extra time is disabled.");
        }

        [Test]
        public void InvalidExtraTimeIsNotSilentlyMutated()
        {
            var r = NewMatchRules();
            SetField(r, "UseExtraTime", true);
            SetField(r, "ExtraTimeDurationSeconds", 0f);
            GetInvalidMatchRulesData(r);
            Assert.IsTrue((bool)GetField(r, "UseExtraTime"), "Validation must not reset UseExtraTime.");
            Assert.AreEqual(0f, (float)GetField(r, "ExtraTimeDurationSeconds"), 1e-6f,
                "Validation must not clamp the invalid extra-time duration.");
        }
    }
}
