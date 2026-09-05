using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 82 — Card Type Rules (PlayerDefinition.Profile.CardType).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 82 (REVISION — approved 8-value set) locks:
    ///   - A strongly typed <c>PlayerCardType</c> enum value as pure classification data on the
    ///     Profile. FINAL APPROVED values (replacing Base/Rare/Special): Basic, Iconic, Legend,
    ///     Ultimate, Prime, Form, Signature, Elite — exactly 8, all distinct, no old values.
    ///   - Default policy: Basic is the enum zero value AND the explicit authored field default.
    ///   - Ownership: CardType lives ONLY on Profile — not PlayerStats/Identity/PhysicalProfile, no
    ///     separate card definition; it is NOT the player's identity and does not replace PlayerId.
    ///   - Validation in the Profile boundary (<c>Profile.GetInvalidProfileData()</c>): an undefined
    ///     <c>PlayerCardType</c> value is DETECTED + REPORTED, never silently reset (no auto-Basic).
    ///   - CardType is classification ONLY: it applies no stat/OverallRating/PlayStyle/Position/
    ///     foot/skill modifiers, contains no runtime state, and controls no gameplay/AI/animation/
    ///     physics. No card system/database/registry/UI/economy/chemistry is created.
    ///   - Goalkeepers and outfield players share the SAME unified Profile.CardType field.
    /// </summary>
    public class CardTypeRulesTests
    {
        private const string Prefix = "Football.Data.";
        private static readonly string[] ApprovedCardTypes =
        {
            "Basic", "Iconic", "Legend", "Ultimate", "Prime", "Form", "Signature", "Elite"
        };

        private static readonly string[] OldUnapprovedCardTypes = { "Base", "Rare", "Special" };

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

        private static Type ProfileType() => FindType(Prefix + "Profile");

        private static Type PlayerStatsType() => FindType(Prefix + "PlayerStats");

        private static Type IdentityType() => FindType(Prefix + "Identity");

        private static Type CardTypeType() => FindType(Prefix + "PlayerCardType");

        private static object NewProfile() => Activator.CreateInstance(ProfileType());

        private static FieldInfo CardTypeField() =>
            ProfileType().GetField("CardType", BindingFlags.Public | BindingFlags.Instance);

        private static List<string> GetInvalidProfileData(object profile)
        {
            return (List<string>)ProfileType().GetMethod("GetInvalidProfileData",
                BindingFlags.Public | BindingFlags.Instance).Invoke(profile, null);
        }

        private static bool ProfileMethodExists(string name)
        {
            return ProfileType().GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null;
        }

        // ---- 82.1 Representation ----

        [Test]
        public void CardTypeExists()
        {
            Assert.IsNotNull(CardTypeField(), "Profile must have CardType.");
        }

        [Test]
        public void CardTypeUsesStronglyTypedEnum()
        {
            var f = CardTypeField();
            Assert.IsTrue(f.FieldType.IsEnum, "CardType must be a strongly typed enum, not string/int.");
            Assert.AreEqual(Prefix + "PlayerCardType", f.FieldType.FullName,
                "CardType must use the PlayerCardType enum.");
        }

        [Test]
        public void AllApprovedCardTypesExist()
        {
            var names = Enum.GetNames(CardTypeType());
            foreach (var c in ApprovedCardTypes)
            {
                Assert.IsTrue(names.Contains(c), $"PlayerCardType must define '{c}'.");
            }
        }

        [Test]
        public void CardTypeValuesAreDistinct()
        {
            Assert.AreEqual(ApprovedCardTypes.Length, Enum.GetNames(CardTypeType()).Length,
                "PlayerCardType must have exactly the approved " + ApprovedCardTypes.Length + " values.");
        }

        // ---- 82.2 Classification (8 approved values) ----

        [Test]
        public void OldBaseRareSpecialValuesAreAbsent()
        {
            var names = Enum.GetNames(CardTypeType()).ToList();
            foreach (var old in OldUnapprovedCardTypes)
            {
                Assert.IsFalse(names.Contains(old),
                    $"Old unapproved CardType '{old}' must NOT remain an enum member.");
            }
        }

        [Test]
        public void BasicDefaultPolicyIsExplicit()
        {
            // Basic is the enum zero value AND the explicit authored field default (a defined card type).
            var p = NewProfile();
            var names = Enum.GetNames(CardTypeType());
            var def = (int)CardTypeField().GetValue(p);
            Assert.IsTrue(def >= 0 && def < names.Length, "Default CardType must be a defined value.");
            Assert.AreEqual("Basic", names[def], "Default CardType must be Basic (explicit default policy).");
        }

        [Test]
        public void CardTypeIsClassificationNotStatModifier()
        {
            foreach (var m in new[] { "ApplyCardModifier", "ApplyCardAttributeBonus", "ApplyBasicBonus", "ApplyLegendBonus", "ApplyPrimeBonus", "ApplyEliteBonus", "ApplySignatureBonus", "ApplyFormBonus", "ApplyUltimateBonus", "ApplyIconicBonus" })
            {
                Assert.IsNull(ProfileType().GetMethod(m,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"CardType must NOT apply stat/rarity bonus '{m}'.");
            }
        }

        // ---- 82.3 Ownership ----

        [Test]
        public void CardTypeBelongsToProfile()
        {
            Assert.IsTrue(CardTypeField().DeclaringType == ProfileType(),
                "CardType must live on the Profile data class.");
        }

        [Test]
        public void CardTypeIsNotPlayerIdentity()
        {
            Assert.IsNull(IdentityType().GetField("CardType", BindingFlags.Public | BindingFlags.Instance),
                "CardType must NOT be part of Identity.");
            Assert.IsNotNull(IdentityType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance),
                "Identity must keep the authoritative PlayerId.");
        }

        [Test]
        public void CardTypeDoesNotReplacePlayerId()
        {
            Assert.IsNull(IdentityType().GetField("CardType", BindingFlags.Public | BindingFlags.Instance),
                "CardType must not replace PlayerId as the player key.");
            Assert.IsNotNull(IdentityType().GetField("PlayerId", BindingFlags.Public | BindingFlags.Instance),
                "PlayerId must remain the stable player identity.");
        }

        [Test]
        public void CardTypeBelongsOnlyToProfile()
        {
            Assert.IsNull(PlayerStatsType().GetField("CardType", BindingFlags.Public | BindingFlags.Instance),
                "CardType must NOT be duplicated into PlayerStats.");
            Assert.IsNull(IdentityType().GetField("CardType", BindingFlags.Public | BindingFlags.Instance),
                "CardType must NOT be duplicated into Identity.");
        }

        [Test]
        public void NoSeparateCardDefinitionsCreated()
        {
            // A player remains ONE PlayerDefinition; CardType is one Profile field. Do NOT create a
            // per-card-type PlayerDefinition variant.
            Assert.IsFalse(HasTypeName("BasicPlayerDefinition", "IconicPlayerDefinition", "LegendPlayerDefinition",
                "UltimatePlayerDefinition", "PrimePlayerDefinition", "FormPlayerDefinition",
                "SignaturePlayerDefinition", "ElitePlayerDefinition", "PlayerBasicCardDefinition",
                "PlayerLegendCardDefinition"),
                "The player must remain one unified PlayerDefinition; no per-card-type definition.");
        }

        // ---- Validation ----

        [Test]
        public void CardTypeValidationUsesProfileBoundary()
        {
            Assert.IsNotNull(ProfileType().GetMethod("GetInvalidProfileData",
                BindingFlags.Public | BindingFlags.Instance),
                "CardType validation must live in the Profile boundary (GetInvalidProfileData).");
        }

        [Test]
        public void InvalidCardTypeEnumValueIsDetected()
        {
            var p = NewProfile();
            CardTypeField().SetValue(p, Enum.ToObject(CardTypeType(), 999));
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("CardType")),
                "GetInvalidProfileData must report an undefined PlayerCardType. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void InvalidCardTypeIsNotSilentlyMutated()
        {
            var p = NewProfile();
            var invalid = Enum.ToObject(CardTypeType(), 4321);
            CardTypeField().SetValue(p, invalid);
            GetInvalidProfileData(p);
            Assert.AreEqual((int)invalid, (int)CardTypeField().GetValue(p),
                "GetInvalidProfileData must NOT silently reset an invalid CardType to Basic.");
        }

        [Test]
        public void CardTypeIsNotValidatedByPlayerStats()
        {
            Assert.IsNull(PlayerStatsType().GetField("CardType", BindingFlags.Public | BindingFlags.Instance),
                "CardType must NOT be a PlayerStats field / 1-99 validated.");
            var ps = Activator.CreateInstance(PlayerStatsType());
            var statsProblems = (List<string>)PlayerStatsType().GetMethod("GetInvalidRatings",
                BindingFlags.Public | BindingFlags.Instance).Invoke(ps, null);
            Assert.IsEmpty(statsProblems, "Default PlayerStats must validate cleanly, independent of CardType.");
        }

        // ---- 82.4 Data-driven protection ----

        [Test]
        public void CardTypeIsSeparateFromOverallRating()
        {
            Assert.IsNull(ProfileType().GetMethod("CalculateOverallRating",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "CardType must not compute/modify OverallRating.");
        }

        [Test]
        public void CardTypeIsSeparateFromPlayStyle()
        {
            Assert.IsNotNull(ProfileType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance),
                "PlayStyle remains an independent Profile field.");
            Assert.AreNotEqual(PlayStyleFieldType(), CardTypeField().FieldType,
                "CardType and PlayStyle must be distinct enum types.");
        }

        private static Type PlayStyleFieldType() =>
            ProfileType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance).FieldType;

        [Test]
        public void CardTypeIsSeparateFromPosition()
        {
            Assert.IsNotNull(ProfileType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance),
                "PrimaryPosition remains an independent Profile field.");
            Assert.IsNull(ProfileType().GetMethod("GetCardTypeForPosition",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "CardType must not be derived from/restricted by position.");
        }

        [Test]
        public void CardTypeIsSeparateFromFootAndSkill()
        {
            Assert.IsNotNull(ProfileType().GetField("WeakFoot", BindingFlags.Public | BindingFlags.Instance),
                "WeakFoot remains independent.");
            Assert.IsNotNull(ProfileType().GetField("SkillRating", BindingFlags.Public | BindingFlags.Instance),
                "SkillRating remains independent.");
        }

        [Test]
        public void CardTypeDoesNotModifyPlayerStatsOrOverallRating()
        {
            Assert.IsFalse(HasTypeName("CardStatModifier", "CardAttributeBonus", "CardMultiplier",
                "BasicBonus", "IconicBonus", "LegendBonus", "UltimateBonus", "PrimeBonus",
                "FormBonus", "SignatureBonus", "EliteBonus"),
                "CardType must not create stat-modifier types.");
        }

        [Test]
        public void CardTypeContainsNoRuntimeState()
        {
            foreach (var n in new[] { "CurrentCardType", "ActiveCardType", "RuntimeCardType" })
            {
                Assert.IsNull(ProfileType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"No runtime card-type field '{n}' may exist in Profile.");
            }
        }

        [Test]
        public void CardTypeContainsNoGameplayLogic()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate", "ExecuteCardType" })
            {
                Assert.IsNull(ProfileType().GetMethod(m,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"CardType must not run '{m}' methods.");
            }
        }

        [Test]
        public void CardTypeDoesNotReferenceUnityRuntimeObjects()
        {
            var unityTypes = AllTypes().Where(t => typeof(UnityEngine.Object).IsAssignableFrom(t)).ToList();
            foreach (var f in ProfileType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(f.FieldType == typeof(UnityEngine.Object) || unityTypes.Contains(f.FieldType),
                    $"Profile field '{f.Name}' must not reference Unity runtime objects.");
            }
        }

        // ---- No systems ----

        [Test]
        public void NoCardSystemCreated()
        {
            Assert.IsFalse(HasTypeName("CardSystem", "CardManager", "CardFactory"),
                "No card system/manager/factory may be created.");
        }

        [Test]
        public void NoCardDatabaseRegistryCreated()
        {
            Assert.IsFalse(HasTypeName("CardDatabase", "CardRegistry"),
                "No card database/registry may be created.");
        }

        [Test]
        public void NoChemistrySystemCreated()
        {
            Assert.IsFalse(HasTypeName("ChemistrySystem", "CardChemistry", "PlayerChemistry"),
                "CardType must not imply chemistry behavior.");
        }

        [Test]
        public void NoCardUICreated()
        {
            Assert.IsFalse(HasTypeName("PlayerCardUI", "CardVisual", "CardArt", "CardRenderer", "CardBadgeUI"),
                "No card UI/presentation may be created in Task 82.");
        }

        [Test]
        public void NoCardEconomyCreated()
        {
            Assert.IsFalse(HasTypeName("PackSystem", "MarketSystem", "CardTrading", "CardPurchaseSystem"),
                "No card economy/trading/pack system may be created.");
        }

        [Test]
        public void NoRandomCardGenerationCreated()
        {
            Assert.IsFalse(HasTypeName("CardGenerator", "RandomCardSystem", "CardPackGenerator"),
                "No random card generation may be created.");
        }

        [Test]
        public void GoalkeepersUseSameCardTypeModel()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperCardType", "GKCardType"),
                "Goalkeepers must use the same unified Profile.CardType; no GK-specific card type.");
        }

        [Test]
        public void OutfieldPlayersUseSameCardTypeModel()
        {
            Assert.IsFalse(HasTypeName("OutfieldCardType"),
                "Outfield players use the same unified Profile.CardType.");
        }

        [Test]
        public void CardTypeRemainsDataDriven()
        {
            var f = CardTypeField();
            Assert.IsTrue(f.DeclaringType == ProfileType(), "CardType must sit under PlayerDefinition → Profile.");
            Assert.IsNull(ProfileType().GetMethod("RunCardType",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "CardType must not be executed as gameplay.");
        }
    }
}
