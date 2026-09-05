using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 103 - Foul Rules (data rules only).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance).
    ///
    /// Task 103 locks:
    ///   - Foul AVAILABILITY is the single established authored flag `EnforceFouls`
    ///     (bool, default TRUE, on) on MatchRulesDefinition (Header "Fouls"), mirroring the
    ///     established EnforceOffside/UsePenaltyShootout single-boolean rule pattern.
    ///   - Foul CATEGORY/SEVERITY taxonomy (standard/serious/professional, advantage-policy) is
    ///     NOT ESTABLISHED -> POLICY/DEFERRED: no invented `FoulType`/`FoulSeverity`/`FoulCategory`
    ///     enum, no `AdvantageRule`/`FoulRule` fields.
    ///   - Foul DATA and MATCH CARDS are SEPARATE. Fouls do NOT own card state
    ///     (CurrentYellowCards/CurrentRedCards/current booking counts are runtime state AND match
    ///     card data owned by Task 104). Task 103 creates no card consequence fields.
    ///   - Foul is RULE DATA only. NO FoulSystem/FoulDetector/FoulManager/ChallengeSystem/
    ///     ContactSystem/AdvantageSystem runtime systems or runtime foul state
    ///     (CurrentFoul/FoulEvent/ActiveFoul/LastFoul) are created.
    ///   - A foul offence's consequent restart (e.g. free kick) is a RESTART-type concern owned by
    ///     Task 106, NOT invented as foul rule data here.
    ///   - `EnforceFouls` is a bool with no invalid authored state; the container
    ///     GetInvalidMatchRulesData boundary remains the single authority and never mutates. No
    ///     independent FoulValidator is created (Task 97 boundary is coherent).
    /// </summary>
    public class FoulRulesTests
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

        // ---- 103.1 Foul representation ----

        [Test]
        public void FoulAvailabilityExistsAsBool()
        {
            var f = FieldOf(MatchRulesType(), "EnforceFouls");
            Assert.IsNotNull(f, "EnforceFouls must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(bool), f.FieldType, "EnforceFouls must be a bool.");
            Assert.AreEqual(MatchRulesType(), f.DeclaringType,
                "EnforceFouls must be declared on MatchRulesDefinition.");
        }

        [Test]
        public void FoulsAreEnabledByDefault()
        {
            Assert.IsTrue((bool)GetField(NewMatchRules(), "EnforceFouls"),
                "Fouls must be enforced by default (true).");
        }

        [Test]
        public void FoulsCanBeDisabled()
        {
            var r = NewMatchRules();
            SetField(r, "EnforceFouls", false);
            Assert.IsFalse((bool)GetField(r, "EnforceFouls"),
                "EnforceFouls must be settable false (authorable fouls off).");
        }

        // ---- 103.2 Foul categories are POLICY/DEFERRED (not invented) ----

        [Test]
        public void NoFoulCategoryEnumInvented()
        {
            Assert.IsFalse(HasTypeName("FoulType", "FoulSeverity", "FoulCategory",
                    "FoulClassification", "FoulKind"),
                "Foul category/severity taxonomy is not established; must not invent an enum/type.");
        }

        [Test]
        public void NoFoulCategoryFieldsInvented()
        {
            foreach (var n in new[] { "FoulType", "FoulSeverity", "FoulCategory", "FoulKind",
                                      "FoulRule", "FoulRules", "AdvantageRule", "AdvantagePlayOn" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Foul categories/rules are not established; must not invent '{n}'.");
            }
        }

        // ---- 103.3 Disciplinary boundary (fouls vs match cards) ----

        [Test]
        public void FoulsDoNotOwnCardState()
        {
            // Match disciplinary cards and any current booking counts belong to match card data
            // (Task 104) / runtime state, NOT to foul rule data.
            foreach (var n in new[] { "CurrentYellowCards", "CurrentRedCards", "YellowCardRule",
                                      "RedCardRule", "CardConsequence", "BookingCount",
                                      "SecondYellow" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Foul rules must not own card data/state; must not create '{n}'.");
            }
        }

        [Test]
        public void FoulDataIsSeparateFromMatchCardData()
        {
            // Fouls describe the offence; match cards describe the disciplinary result (Task 104).
            // No foul field may conflate the two.
            foreach (var n in new[] { "FoulCardMapping", "FoulCardType", "FoulSanction",
                                      "FoulDiscipline" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Foul rule data must stay separate from match card data; must not create '{n}'.");
            }
        }

        // ---- 103.4 No foul gameplay / runtime state ----

        [Test]
        public void NoRuntimeFoulStateOnMatchRules()
        {
            foreach (var n in new[] { "CurrentFoul", "FoulEvent", "ActiveFoul", "LastFoul",
                                      "PendingFoul", "FoulCount", "ConsecutiveFouls",
                                      "FoulHistory", "ActiveAdvantage" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition is authored config; must not hold runtime foul state '{n}'.");
            }
        }

        [Test]
        public void NoFoulSystemsCreated()
        {
            Assert.IsFalse(HasTypeName("FoulSystem", "FoulDetector", "FoulManager",
                    "ChallengeSystem", "ContactSystem", "AdvantageSystem", "FoulRulesSystem"),
                "Task 103 is data rules only; no foul systems/detectors/managers may be created.");
        }

        [Test]
        public void NoFoulEventsOrContractsCreated()
        {
            Assert.IsFalse(HasTypeName("FoulEvent", "FoulCommittedEvent", "FoulDetectedEvent",
                    "FoulEventArgs", "AdvantagePlayedEvent"),
                "Task 103 adds no runtime foul events/contracts.");
        }

        // ---- 103.5 Boundaries (restart taxonomy / player data) ----

        [Test]
        public void FoulRestartMappingIsARestartTypeConcern()
        {
            // The "foul offence -> free kick / penalty kick restart" mapping belongs to the restart
            // taxonomy (Task 106), not to foul rule data invented here.
            foreach (var n in new[] { "FoulFreeKickType", "FoulRestart", "FoulFreeKick",
                                      "FoulPenaltyMapping" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"The foul-restart mapping is a restart-type concern (Task 106); must not create '{n}' here.");
            }
        }

        [Test]
        public void FoulsAreNotPlayerFoulRatingData()
        {
            // Player foul-related skill ratings remain player-capability data (PlayerDefinition/
            // PlayerStats); they are NOT duplicated onto match rules.
            foreach (var n in new[] { "Aggression", "Tackling", "Discipline", "FoulProneness",
                                      "SelfControl", "Rashness" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Foul rules are not player skill ratings; must not create '{n}' on match rules.");
            }
        }

        // ---- 103.5 Validation ----

        [Test]
        public void DefaultFoulConfigIsValid()
        {
            // EnforceFouls is a bool with no invalid authored state; default container is valid.
            Assert.IsEmpty(GetInvalidMatchRulesData(NewMatchRules()));
        }

        [Test]
        public void DisablingFoulsDoesNotInvalidate()
        {
            var r = NewMatchRules();
            SetField(r, "EnforceFouls", false);
            Assert.IsEmpty(GetInvalidMatchRulesData(r),
                "Disabling fouls is a valid authored configuration (a foul-less match).");
        }

        [Test]
        public void NoIndependentFoulValidatorCreated()
        {
            Assert.IsFalse(HasTypeName("FoulValidator", "FoulRulesValidator", "FoulConfigValidator"),
                "Foul validation must stay within the Task 97 GetInvalidMatchRulesData boundary; no independent FoulValidator.");
        }
    }
}
