using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 104 - Match disciplinary cards (data only).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance).
    ///
    /// Task 104 locks:
    ///   - A strongly typed Match CARD representation exists: <c>MatchCardType</c> enum with the
    ///     APPROVED value set Yellow, SecondYellow, Red. This describes the match disciplinary card
    ///     TYPE (rule/representation data), wholly SEPARATE from the PlayerCardType enum
    ///     (player PROFILE classification Basic/Iconic/Legend/Ultimate/Prime/Form/Signature/Elite,
    ///     Player Data, Task 82). NO Player Data contamination.
    ///   - SecondYellow is an INDEPENDENT card type here (a distinct authored card). The RUNTIME
    ///     question of whether two yellows derive a dismissal/red is PLAYER/MATCH RUNTIME
    ///     disciplinary STATE, out of scope for this data-only task, NOT runtime state here.
    ///   - Card AVAILABILITY is the single established authored flag <c>EnforceMatchCards</c>
    ///     (bool, default TRUE, on) on MatchRulesDefinition (Header "Match Cards"), mirroring the
    ///     established EnforceOffside/EnforceFouls single-boolean rule pattern.
    ///   - Ownership: MatchRulesDefinition is the authoritative authored owner. Runtime card state
    ///     (CurrentYellowCards/CurrentRedCards/PlayerBookings/PlayerDismissed/CardHistory) must NOT
    ///     live on MatchRulesDefinition.
    ///   - Task 104 is DATA ONLY: no CardSystem/CardManager/DisciplinarySystem/BookingSystem/
    ///     DismissalSystem, no assigning/escalating/dismissing/referee behavior, no foul detection,
    ///     no UI/AI/animation. Fouls (Task 103), penalty shootout (Task 101), and restart types
    ///     (Task 106) are separate and are NOT implemented here.
    ///   - <c>EnforceMatchCards</c> is a bool with no invalid authored state; the container
    ///     GetInvalidMatchRulesData boundary remains the single authority and never mutates. No
    ///     independent CardValidator is created.
    /// </summary>
    public class MatchCardRulesTests
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

        private static Type MatchCardTypeType() => FindType(Prefix + "MatchCardType");

        private static Type PlayerCardTypeType() => FindType(Prefix + "PlayerCardType");

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

        // ---- 104.1 Representation ----

        [Test]
        public void MatchCardRepresentationExists()
        {
            var t = MatchCardTypeType();
            Assert.IsTrue(t.IsEnum, "MatchCardType must be an enum (strongly typed representation).");
            Assert.AreEqual(3, Enum.GetNames(t).Length,
                "MatchCardType must define exactly the three approved match card types.");
        }

        [Test]
        public void MatchCardTypeIsStronglyTyped()
        {
            // The card type vocabulary is an enum, never a loose string / magic integer.
            var t = MatchCardTypeType();
            Assert.IsTrue(t.IsEnum, "MatchCardType must be an enum (strongly typed), not a string/int.");
            Assert.IsFalse(t == typeof(string) || t == typeof(int), "No string/int card representation.");
        }

        // ---- 104.2 Yellow / Second Yellow / Red ----

        [Test]
        public void YellowExists()
        {
            var names = Enum.GetNames(MatchCardTypeType());
            Assert.IsTrue(names.Contains("Yellow"), "MatchCardType must define Yellow.");
        }

        [Test]
        public void SecondYellowExistsAsIndependentType()
        {
            var names = Enum.GetNames(MatchCardTypeType());
            Assert.IsTrue(names.Contains("SecondYellow"),
                "SecondYellow is represented as an independent card type (rule data); must be defined.");
        }

        [Test]
        public void RedExists()
        {
            var names = Enum.GetNames(MatchCardTypeType());
            Assert.IsTrue(names.Contains("Red"), "MatchCardType must define Red.");
        }

        [Test]
        public void CardTypesAreDistinct()
        {
            var names = Enum.GetNames(MatchCardTypeType());
            Assert.AreEqual(3, names.Distinct().Count(),
                "Yellow/SecondYellow/Red must be three distinct card types.");
        }

        // ---- 104.3 Ownership ----

        [Test]
        public void EnforceMatchCardsExistsAsBool()
        {
            var f = FieldOf(MatchRulesType(), "EnforceMatchCards");
            Assert.IsNotNull(f, "EnforceMatchCards must exist on MatchRulesDefinition.");
            Assert.AreEqual(typeof(bool), f.FieldType, "EnforceMatchCards must be a bool.");
            Assert.AreEqual(MatchRulesType(), f.DeclaringType,
                "EnforceMatchCards must be declared on MatchRulesDefinition.");
        }

        [Test]
        public void MatchCardsEnabledByDefault()
        {
            Assert.IsTrue((bool)GetField(NewMatchRules(), "EnforceMatchCards"),
                "Match disciplinary cards must be enforced by default (true).");
        }

        [Test]
        public void MatchCardsCanBeDisabled()
        {
            var r = NewMatchRules();
            SetField(r, "EnforceMatchCards", false);
            Assert.IsFalse((bool)GetField(r, "EnforceMatchCards"),
                "EnforceMatchCards must be settable false (authorable cards off).");
        }

        [Test]
        public void NoDuplicateCardAuthority()
        {
            // No extra card-rule type/field shadows MatchCardType + EnforceMatchCards as the single
            // card rule owner.
            Assert.IsFalse(HasTypeName("CardRule", "CardRules", "MatchCardDefinition",
                    "DisciplinaryCardType", "DisciplinaryCard"),
                "No duplicate match-card authority type may be created.");
            foreach (var n in new[] { "CardRule", "CardRules", "MatchCardTypeRule", "CardTypeRule" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition holds EnforceMatchCards only; must not create '{n}'.");
            }
        }

        // ---- 104.3 Player Card Type boundary ----

        [Test]
        public void MatchCardsAreSeparateFromPlayerCardType()
        {
            var match = MatchCardTypeType();
            var player = PlayerCardTypeType();
            Assert.AreNotEqual(match, player,
                "MatchCardType (match disciplinary cards) must be a distinct enum from PlayerCardType.");
            Assert.AreNotEqual(match.FullName, player.FullName,
                "MatchCardType and PlayerCardType must have distinct fully-qualified names.");
            Assert.IsTrue(Enum.GetNames(player).Contains("Basic"),
                "PlayerCardType must remain the approved Player Data classification set (Basic present).");
        }

        [Test]
        public void NoPlayerCardTypeContaminationOnMatchRules()
        {
            // Match rules must not host the Player Data classification field or route disciplinary
            // behavior through it.
            Assert.IsNull(FieldOf(MatchRulesType(), "CardType"),
                "MatchRulesDefinition must not hold the Player CardType field.");
            foreach (var n in new[] { "PlayerCardType", "ProfileCardType", "MatchCardFromPlayerCard" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Match disciplinary cards must stay separate from PlayerCardType; must not create '{n}'.");
            }
        }

        // ---- 104.4 No runtime card state / no gameplay / no systems ----

        [Test]
        public void NoRuntimeCardStateOnMatchRules()
        {
            foreach (var n in new[] { "CurrentYellowCards", "CurrentRedCards", "CurrentSecondYellow",
                                      "PlayerBookings", "PlayerDismissed", "CurrentDisciplinaryState",
                                      "CurrentCardCount", "DismissedPlayers", "CardHistory" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition is authored config; must not hold runtime card state '{n}'.");
            }
        }

        [Test]
        public void NoCardSystemsCreated()
        {
            Assert.IsFalse(HasTypeName("CardSystem", "CardManager", "DisciplinarySystem",
                    "BookingSystem", "DismissalSystem", "MatchCardManager", "CardGameplaySystem",
                    "RefereeCardSystem", "FoulToCardEngine", "CardAssignmentFromFoulSystem"),
                "Task 104 is data only; no card/referee gameplay systems may be created.");
        }

        [Test]
        public void NoCardGameplayLogic()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate", "AssignCard",
                                      "EscalateCard", "DismissPlayer", "IssueCard", "HandleCard" })
            {
                Assert.IsNull(MethodOf(MatchRulesType(), m),
                    $"MatchRulesDefinition holds no gameplay logic; must not define '{m}'.");
            }
        }

        // ---- 104.4 Foul / Penalty / Restart boundaries ----

        [Test]
        public void NoFoulDuplicationInCardModel()
        {
            // Fouls (Task 103) are separate. The card model must not re-own foul detection or the
            // EnforceFouls flag.
            Assert.IsNull(FieldOf(MatchRulesType(), "CardFoulMapping"),
                "Card data must not duplicate foul rule responsibility.");
            Assert.IsFalse(HasTypeName("CardFoulMapping", "FoulCardRule", "RefereeFoulEngine"),
                "No foul-to-card engine may be created by card data.");
        }

        [Test]
        public void NoPenaltyOrRestartDuplication()
        {
            // Penalty shootout config (Task 101) + PenaltyKick/FreeKick/etc. restarts (Task 106)
            // are not implemented here.
            Assert.IsNull(FieldOf(MatchRulesType(), "CardPenaltyShootout"),
                "Card data must not duplicate penalty shootout configuration (Task 101).");
            Assert.IsFalse(HasTypeName("KickOffRule", "ThrowInRule", "GoalKickRule", "CornerKickRule",
                    "FreeKickRule", "PenaltyKickRule", "DropBallRule"),
                "Restart types (Task 106) must not be implemented as part of Task 104.");
        }

        // ---- 104.4 Validation ----

        [Test]
        public void DefaultCardConfigIsValid()
        {
            // EnforceMatchCards is a bool with no invalid authored state; default container is valid.
            Assert.IsEmpty(GetInvalidMatchRulesData(NewMatchRules()));
        }

        [Test]
        public void DisablingCardsDoesNotInvalidate()
        {
            var r = NewMatchRules();
            SetField(r, "EnforceMatchCards", false);
            Assert.IsEmpty(GetInvalidMatchRulesData(r),
                "Disabling match cards is a valid authored configuration.");
        }

        [Test]
        public void NoIndependentCardValidatorCreated()
        {
            Assert.IsFalse(HasTypeName("CardValidator", "MatchCardValidator", "DisciplinaryValidator"),
                "Card validation must stay within the Task 97 GetInvalidMatchRulesData boundary; no independent CardValidator.");
        }
    }
}
