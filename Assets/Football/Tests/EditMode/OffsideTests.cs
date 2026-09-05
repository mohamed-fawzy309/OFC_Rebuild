using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 102 - Offside Rule (data rules only).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance).
    ///
    /// Task 102 locks:
    ///   - Offside AVAILABILITY is the single established authored flag `EnforceOffside`
    ///     (bool, default TRUE, on) on MatchRulesDefinition (Header "Offside").
    ///   - Offside MODE (strict/relaxed/offside-trap tolerance/intervention rules/VAR/referee
    ///     discretion distances) is NOT ESTABLISHED -> POLICY/DEFERRED: no invented
    ///     `OffsideMode`/`OffsideTrap`/`OffsideTolerance`/`OffsideVarEnabled` fields, no enum.
    ///   - Offside is DATA RULES only. NO OffsideSystem/OffsideDetection/OffsideTrapController/
    ///     OffsideRuleEvaluator runtime systems are created.
    ///   - The "offside offence -> indirect free kick restart" mapping is a RESTART-type concern
    ///     owned by Task 106 (restart taxonomy), NOT invented as offside rule data here.
    ///   - `EnforceOffside` is a bool with no invalid authored state; the container
    ///     GetInvalidMatchRulesData boundary remains the single authority and never mutates.
    /// </summary>
    public class OffsideTests
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

        // ---- 102.1 Offside availability ----

        [Test]
        public void OffsideAvailabilityExistsAsBool()
        {
            var f = FieldOf(MatchRulesType(), "EnforceOffside");
            Assert.IsNotNull(f, "EnforceOffside must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(bool), f.FieldType, "EnforceOffside must be a bool.");
            Assert.AreEqual(MatchRulesType(), f.DeclaringType,
                "EnforceOffside must be declared on MatchRulesDefinition.");
        }

        [Test]
        public void OffsideIsEnabledByDefault()
        {
            Assert.IsTrue((bool)GetField(NewMatchRules(), "EnforceOffside"),
                "Offside must be enforced by default (true).");
        }

        [Test]
        public void OffsideCanBeDisabled()
        {
            var r = NewMatchRules();
            SetField(r, "EnforceOffside", false);
            Assert.IsFalse((bool)GetField(r, "EnforceOffside"),
                "EnforceOffside must be settable false (authorable offside off).");
        }

        // ---- 102.2 Offside mode is POLICY/DEFERRED (not invented) ----

        [Test]
        public void NoOffsideModeEnumInvented()
        {
            Assert.IsFalse(HasTypeName("OffsideMode", "OffsideRuleMode", "OffsideEnforcement",
                    "OffsideInterventionPolicy", "OffsideIntervention"),
                "Offside mode is not established; must not invent an offside-mode enum/type.");
        }

        [Test]
        public void NoOffsideModeFieldsInvented()
        {
            foreach (var n in new[] { "OffsideMode", "OffsideTrap", "OffsideTolerance",
                                      "OffsideDistance", "OffsideVarEnabled", "OffsideRefereeDiscretion",
                                      "OffsideRule" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Offside mode is not established; must not invent '{n}'.");
            }
        }

        [Test]
        public void NoOffsideInterventionFieldsInvented()
        {
            foreach (var n in new[] { "OffsideThreshold", "OffsideAutoCall", "OffsideTimingWindow",
                                      "OffsideDelayFlags", "OffsideDelayedFlag", "OffsideOverturning" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Offside intervention details are not established; must not invent '{n}'.");
            }
        }

        // ---- 102.3 Boundaries (restart taxonomy / foul rules) ----

        [Test]
        public void OffsideFreeKickMappingIsARestartTypeConcern()
        {
            // "Offside offence -> indirect free kick restart" belongs to the restart taxonomy
            // (Task 106), not to offside rule data. No dedicated offside freeze of restarts here.
            foreach (var n in new[] { "OffsideFreeKickType", "OffsideRestart", "OffsideSanction" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"The offside-free-kick mapping is a restart-type concern (Task 106); must not create '{n}' here.");
            }
        }

        [Test]
        public void OffsideIsIndependentOfPenaltyShootout()
        {
            var r = NewMatchRules();
            SetField(r, "UsePenaltyShootout", true);
            Assert.IsTrue((bool)GetField(r, "EnforceOffside"),
                "Enabling the penalty shootout must not disable offside enforcement.");
        }

        // ---- 102.4 No systems ----

        [Test]
        public void NoOffsideSystemsCreated()
        {
            Assert.IsFalse(HasTypeName("OffsideSystem", "OffsideDetection", "OffsideTrapController",
                    "OffsideRuleEvaluator", "OffsideInterventionSystem"),
                "Task 102 is data rules only; no offside systems/controllers may be created.");
        }

        // ---- 102.5 Validation ----

        [Test]
        public void DefaultOffsideConfigIsValid()
        {
            // EnforceOffside is a bool with no invalid authored state; default container is valid.
            Assert.IsEmpty(GetInvalidMatchRulesData(NewMatchRules()));
        }

        [Test]
        public void DisablingOffsideDoesNotInvalidate()
        {
            var r = NewMatchRules();
            SetField(r, "EnforceOffside", false);
            Assert.IsEmpty(GetInvalidMatchRulesData(r),
                "Disabling offside is a valid authored configuration (an offside-less match).");
        }
    }
}
