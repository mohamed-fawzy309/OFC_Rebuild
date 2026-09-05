using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 101 - Penalty Rules (data rules only; distinguishes Penalty Kick Restart from Penalty
    /// Shootout).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance).
    ///
    /// Task 101 locks:
    ///   - PENALTY KICK RESTART and PENALTY SHOOTOUT are SEPARATE concepts. They are NOT the same.
    ///   - Penalty Kick Restart is a RESTART TYPE; the restart taxonomy belongs to Task 106, not
    ///     Task 101. No dedicated penalty-kick-restart rule data is created here (DEFERRED to 106).
    ///   - Penalty Shootout AVAILABILITY is the single established authored flag
    ///     `UsePenaltyShootout` (bool, default FALSE, opt-in) on MatchRulesDefinition.
    ///   - Shootout STRUCTURE (rounds/kicks/sudden death) is NOT ESTABLISHED -> POLICY/DEFERRED:
    ///     no invented `ShootoutRounds`/`ShootoutKicks`/`ShootoutSuddenDeath` fields.
    ///   - No coupling with extra time (UseExtraTime independent) or with Player Data rating
    ///     `Penalties`/`PenaltySaving` (player capability, not match rules).
    ///   - No PenaltySystem/PenaltyKickController/PenaltyShootoutManager.
    ///   - `UsePenaltyShootout` is a bool with no invalid authored state; the container
    ///     GetInvalidMatchRulesData boundary remains the single authority and never mutates.
    /// </summary>
    public class PenaltyRulesTests
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

        // ---- 101.1 Two separate concepts ----

        [Test]
        public void PenaltyShootoutAvailabilityExistsAsBool()
        {
            var f = FieldOf(MatchRulesType(), "UsePenaltyShootout");
            Assert.IsNotNull(f, "UsePenaltyShootout must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(bool), f.FieldType, "UsePenaltyShootout must be a bool.");
            Assert.AreEqual(MatchRulesType(), f.DeclaringType,
                "UsePenaltyShootout must be declared on MatchRulesDefinition.");
        }

        [Test]
        public void PenaltyShootoutIsOptInByDefault()
        {
            Assert.IsFalse((bool)GetField(NewMatchRules(), "UsePenaltyShootout"),
                "Penalty shootout must be opt-in (default false).");
        }

        [Test]
        public void PenaltyKickRestartHasNoDedicatedRuleDataHere()
        {
            // Penalty Kick Restart is a restart TYPE owned by Task 106 (restart taxonomy), NOT a
            // penalty-rule field to be invented here. No duplicate penalty-kick restart rule data.
            foreach (var n in new[] { "PenaltyKickRule", "PenaltyKickRestart", "PenaltyKick",
                                      "PenaltyKickEnabled", "PenaltyKickRestartRule" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Penalty Kick Restart rules belong to Task 106; must not create '{n}' here.");
            }
        }

        // ---- 101.2 Shootout structure is POLICY/DEFERRED (not invented) ----

        [Test]
        public void NoShootoutStructureInvented()
        {
            foreach (var n in new[] { "ShootoutRounds", "ShootoutKicks", "ShootoutKicksPerTeam",
                                      "ShootoutSuddenDeath", "ShootoutRoundsPerTeam",
                                      "ShootoutTakePerTeam" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Shootout structure is not established; must not invent '{n}'.");
            }
        }

        // ---- 101.3 Boundaries (extra time / player data) ----

        [Test]
        public void PenaltyShootoutIsIndependentOfExtraTime()
        {
            var r = NewMatchRules();
            SetField(r, "UseExtraTime", true);
            Assert.IsFalse((bool)GetField(r, "UsePenaltyShootout"),
                "Enabling extra time must not auto-enable the penalty shootout.");
        }

        [Test]
        public void PenaltyRulesAreNotPlayerRatingData()
        {
            // Player Data 'Penalties'/'PenaltySaving' ratings remain player-capability data; they
            // are NOT duplicated onto match rules.
            foreach (var n in new[] { "Penalties", "PenaltySaving", "PenaltyRating", "SpotKick" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Penalty rules are not player ratings; must not create '{n}' on match rules.");
            }
        }

        // ---- 101.4 No systems ----

        [Test]
        public void NoPenaltySystemsCreated()
        {
            Assert.IsFalse(HasTypeName("PenaltySystem", "PenaltyKickController", "PenaltyShootoutManager",
                    "PenaltyShootoutSystem", "PenaltyKickSystem"),
                "Task 101 is data rules only; no penalty systems/controllers/managers may be created.");
        }

        // ---- 101.5 Validation ----

        [Test]
        public void DefaultPenaltyConfigIsValid()
        {
            // UsePenaltyShootout is a bool with no invalid authored state; default container is valid.
            Assert.IsEmpty(GetInvalidMatchRulesData(NewMatchRules()));
        }

        [Test]
        public void EnablingShootoutDoesNotInvalidate()
        {
            var r = NewMatchRules();
            SetField(r, "UsePenaltyShootout", true);
            Assert.IsEmpty(GetInvalidMatchRulesData(r),
                "Enabling the penalty shootout is a valid authored configuration.");
        }
    }
}
