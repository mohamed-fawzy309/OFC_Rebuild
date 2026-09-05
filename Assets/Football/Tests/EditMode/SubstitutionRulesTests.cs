using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 105 - Substitution Rules (data rules only).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance).
    ///
    /// Task 105 locks:
    ///   - MaxSubstitutions is the single authored MAXIMUM SUBSTITUTIONS rule field (int, default 5)
    ///     on MatchRulesDefinition (Header "Substitutions"). It is a non-negative limit; 0 is a
    ///     valid authored "no substitutions" configuration.
    ///   - SUBSTITUTION WINDOWS and EXTRA-TIME SUBSTITUTION policy are NOT ESTABLISHED by the
    ///     project -> POLICY/DEFERRED. No SubstitutionWindow/WindowCount/Slots/Batches and no
    ///     AllowExtraTimeSubstitutions field are invented (no speculative FIFA-style numbers).
    ///   - Substitution rules are AUTHORED match configuration ONLY. Runtime substitution state
    ///     (CurrentSubstitutions/CurrentBench/PlayersOnField/SubstitutionHistory/etc.),
    ///     bench logic, player swapping and substitution execution are RUNTIME responsibilities, NOT
    ///     MatchRulesDefinition and NOT Team Data movement. TeamDefinition owns Squad
    ///     (Starters/Substitutes, Task 92) and is unchanged.
    ///   - No SubstitutionSystem/SubstitutionManager/BenchManager/PlayerSwapSystem etc. and no
    ///     dependent GameClock change (GameClock remains runtime time authority).
    ///   - Validation lives in the Task 97 GetInvalidMatchRulesData boundary: negative
    ///     MaxSubstitutions is DETECTED + REPORTED, never silently clamped/reset. No independent
    ///     SubstitutionValidator is created.
    /// </summary>
    public class SubstitutionRulesTests
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

        private static object NewMatchRules() => ScriptableObject.CreateInstance(MatchRulesType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static MethodInfo MethodOf(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              BindingFlags.Static);

        private static object GetField(object target, string field) =>
            FieldOf(target.GetType(), field).GetValue(target);

        private static void SetField(object target, string field, object value) =>
            FieldOf(target.GetType(), field).SetValue(target, value);

        private static List<string> GetInvalidMatchRulesData(object rules) =>
            (List<string>)MethodOf(MatchRulesType(), "GetInvalidMatchRulesData").Invoke(rules, null);

        // ---- 105.1 Maximum substitutions ----

        [Test]
        public void MaxSubstitutionsExistsAsInt()
        {
            var f = FieldOf(MatchRulesType(), "MaxSubstitutions");
            Assert.IsNotNull(f, "MaxSubstitutions must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(int), f.FieldType, "MaxSubstitutions must be an int.");
            Assert.AreEqual(MatchRulesType(), f.DeclaringType,
                "MaxSubstitutions must be declared on MatchRulesDefinition.");
        }

        [Test]
        public void MaxSubstitutionsDefaultIsFive()
        {
            Assert.AreEqual(5, (int)GetField(NewMatchRules(), "MaxSubstitutions"),
                "MaxSubstitutions default must remain 5.");
        }

        [Test]
        public void NoDuplicateSubstitutionAuthority()
        {
            // MaxSubstitutions is the single authored substitution count rule field; no secondary
            // substitution-rule field/type shadows it.
            foreach (var n in new[] { "SubstitutionRule", "SubstitutionRules", "MaxSubs",
                                      "MaxSubstitutionCount", "SubsPerMatch", "TotalSubstitutions" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MaxSubstitutions is the sole substitution rule field; must not create '{n}'.");
            }
            Assert.IsFalse(HasTypeName("SubstitutionRule", "SubstitutionRules"),
                "No duplicate substitution-rule representation type may be created.");
        }

        // ---- 105.5 Validation (negative detected, no silent mutation) ----

        [Test]
        public void NegativeMaxSubstitutionsIsDetected()
        {
            var r = NewMatchRules();
            SetField(r, "MaxSubstitutions", -1);
            var p = GetInvalidMatchRulesData(r);
            Assert.IsTrue(p.Any(x => x.Contains("MaxSubstitutions")),
                "Negative MaxSubstitutions must be DETECTED + REPORTED.");
        }

        [Test]
        public void InvalidValueIsNotSilentlyMutated()
        {
            // Validation must not clamp/reset -1 to 0.
            var r = NewMatchRules();
            SetField(r, "MaxSubstitutions", -1);
            GetInvalidMatchRulesData(r);
            Assert.AreEqual(-1, (int)GetField(r, "MaxSubstitutions"),
                "GetInvalidMatchRulesData must NOT silently clamp MaxSubstitutions.");
        }

        [Test]
        public void ZeroMaxSubstitutionsIsValid()
        {
            var r = NewMatchRules();
            SetField(r, "MaxSubstitutions", 0);
            Assert.IsEmpty(GetInvalidMatchRulesData(r),
                "0 is a valid authored 'no substitutions' limit.");
        }

        [Test]
        public void NoIndependentSubstitutionValidator()
        {
            Assert.IsFalse(HasTypeName("SubstitutionValidator", "SubstitutionRulesValidator"),
                "Substitution validation must stay in the Task 97 GetInvalidMatchRulesData boundary; no independent SubstitutionValidator.");
        }

        // ---- 105.2 / 105.3 POLICY/DEFERRED (windows & extra-time) ----

        [Test]
        public void SubstitutionWindowsArePolicyOrDeferred()
        {
            // No FIFA-style window count is established by the project architecture.
            Assert.IsFalse(HasTypeName("SubstitutionWindow", "SubstitutionWindows",
                    "SubstitutionWindowCount", "SubstitutionPeriods", "SubstitutionSlots",
                    "SubstitutionBatches"),
                "Substitution windows are not established; must not invent a window type.");
            foreach (var n in new[] { "SubstitutionWindow", "SubstitutionWindows",
                                      "SubstitutionWindowCount", "SubstitutionPeriods",
                                      "SubstitutionSlots", "SubstitutionBatches" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Substitution windows are not established; must not add '{n}' to match rules.");
            }
        }

        [Test]
        public void ExtraTimeSubstitutionsArePolicyOrDeferred()
        {
            // Task 100 (extra time) established UseExtraTime/ExtraTimeDurationSeconds but no
            // substitution policy; a separate extra-time substitution rule is not established.
            Assert.IsNull(FieldOf(MatchRulesType(), "AllowExtraTimeSubstitutions"),
                "Extra-time substitution policy is not established; must not invent AllowExtraTimeSubstitutions.");
            Assert.IsFalse(HasTypeName("ExtraTimeSubstitutionRule", "ExtraTimeSubstitutionPolicy"),
                "Extra-time substitution policy is not established; no independent rule type.");
        }

        // ---- 105.4 Runtime / ownership boundaries ----

        [Test]
        public void NoRuntimeSubstitutionStateOnMatchRules()
        {
            foreach (var n in new[] { "CurrentSubstitutions", "CurrentSubstitutionCount",
                                      "CurrentBench", "CurrentPlayers", "SubstitutionHistory",
                                      "LastSubstitution", "ActiveSubstitution",
                                      "SubstitutionState", "PendingSubstitution" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition is authored config; must not hold runtime substitution state '{n}'.");
            }
        }

        [Test]
        public void NoSubstitutionGameplaySystems()
        {
            Assert.IsFalse(HasTypeName("SubstitutionSystem", "SubstitutionManager",
                    "SquadRuntimeSystem", "BenchManager", "PlayerSwapSystem",
                    "SubstitutionController", "LineupManager", "StartingElevenController"),
                "Task 105 is data rules only; no substitution/bench/swap gameplay systems.");
        }

        [Test]
        public void NoSquadDuplicationOnMatchRules()
        {
            // TeamDefinition owns Squad (Starters/Substitutes, Task 92). MatchRulesDefinition must
            // not duplicate bench/membership.
            foreach (var n in new[] { "Starters", "Substitutes", "Bench", "Squad" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Match Rules must not own Squad/bench membership; must not create '{n}'.");
            }
        }

        [Test]
        public void NoPlayerOrTeamDataContamination()
        {
            // Substitution rules must not add player/team authored fields.
            foreach (var n in new[] { "SubstitutionEfficiency", "BenchPlayerRating",
                                      "SubstitutionTactics", "TeamBench" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Substitution rules must not carry player/team data; must not create '{n}'.");
            }
        }
    }
}
