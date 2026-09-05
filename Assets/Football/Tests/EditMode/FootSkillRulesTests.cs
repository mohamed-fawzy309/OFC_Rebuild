using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 80 — Foot &amp; Skill Rules (PlayerDefinition.Profile PreferredFoot / WeakFoot /
    /// SkillRating) plus the approved CF → SS position correction.
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 80 locks foot/skill PROFILE data:
    ///   - PreferredFoot is a strongly typed Right/Left enum (no strings, no numeric rating).
    ///   - WeakFoot and SkillRating are PROFILE ratings on their OWN conceptual 1–5 scale — NOT the
    ///     PlayerStats 1–99 scale — validated by <c>Profile.GetInvalidProfileData()</c>.
    ///   - Validation is DETECT-AND-REPORT: it never clamps, replaces, or inserts values.
    ///   - No foot/skill/animation/AI/gameplay system is created; no OverallRating calculator.
    ///   - Goalkeepers and outfield players share the SAME unified Profile foot/skill fields.
    ///
    /// The approved position correction replaces CF with SS (Shadow Striker) in the PlayerPosition
    /// enum and in the Player Data position tests/documentation. CF is NO LONGER an approved
    /// position, and both CF and SS are never supported together.
    /// </summary>
    public class FootSkillRulesTests
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

        private static Type ProfileType() => FindType(Prefix + "Profile");

        private static Type PlayerStatsType() => FindType(Prefix + "PlayerStats");

        private static Type PreferredFootType() => FindType(Prefix + "PreferredFoot");

        private static Type PositionType() => FindType(Prefix + "PlayerPosition");

        private static object NewProfile() => Activator.CreateInstance(ProfileType());

        private static object NewPlayerStats() => Activator.CreateInstance(PlayerStatsType());

        private static List<string> GetInvalidProfileData(object profile)
        {
            return (List<string>)ProfileType().GetMethod("GetInvalidProfileData",
                BindingFlags.Public | BindingFlags.Instance).Invoke(profile, null);
        }

        private static FieldInfo Field(string name) =>
            ProfileType().GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static bool PlayerStatsHasField(string name) =>
            PlayerStatsType().GetField(name, BindingFlags.Public | BindingFlags.Instance) != null;

        private static bool HasTypeName(params string[] names)
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToList();
            return names.Any(n => all.Any(t => t.Name == n));
        }

        private static int ProfileConstant(string name)
        {
            var c = ProfileType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(c, $"Profile must expose constant '{name}'.");
            Assert.IsTrue(c.IsLiteral && !c.IsInitOnly, $"'{name}' must be a const.");
            return (int)c.GetRawConstantValue();
        }

        // ---- 80.1 Preferred Foot ----

        [Test]
        public void PreferredFootExists()
        {
            var f = Field("PreferredFoot");
            Assert.IsNotNull(f, "Profile must have PreferredFoot.");
            Assert.AreEqual(Prefix + "PreferredFoot", f.FieldType.FullName,
                "PreferredFoot must use the PreferredFoot type.");
        }

        [Test]
        public void PreferredFootIsStronglyTyped()
        {
            var f = Field("PreferredFoot");
            Assert.IsTrue(f.FieldType.IsEnum,
                "PreferredFoot must be a strongly typed enum, not string/int.");
        }

        [Test]
        public void PreferredFootSupportsRightAndLeft()
        {
            var names = Enum.GetNames(PreferredFootType());
            Assert.IsTrue(names.Contains("Right"), "PreferredFoot must support Right.");
            Assert.IsTrue(names.Contains("Left"), "PreferredFoot must support Left.");
        }

        [Test]
        public void PreferredFootDoesNotUseString()
        {
            var f = Field("PreferredFoot");
            Assert.AreNotEqual(typeof(string), f.FieldType,
                "PreferredFoot must NOT be a string.");
            Assert.AreNotEqual(typeof(int), f.FieldType,
                "PreferredFoot must NOT be a numeric rating.");
        }

        [Test]
        public void PreferredFoot_HasValidAuthoredDefault()
        {
            var p = NewProfile();
            var val = (int)Field("PreferredFoot").GetValue(p);
            var names = Enum.GetNames(PreferredFootType());
            Assert.IsTrue(val >= 0 && val < names.Length,
                "Default PreferredFoot must be a defined enum value.");
        }

        [Test]
        public void PreferredFoot_UndefinedValue_IsReported()
        {
            var p = NewProfile();
            Field("PreferredFoot").SetValue(p, Enum.ToObject(PreferredFootType(), 999));
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("PreferredFoot")),
                "GetInvalidProfileData must report an undefined PreferredFoot. Got: " + string.Join("; ", problems));
        }

        // ---- 80.2 Weak Foot ----

        [Test]
        public void WeakFootExists()
        {
            Assert.IsNotNull(Field("WeakFoot"), "Profile must have WeakFoot.");
        }

        [Test]
        public void WeakFootIsSeparateFromPlayerStats()
        {
            Assert.IsFalse(PlayerStatsHasField("WeakFoot"),
                "WeakFoot must NOT be a PlayerStats field; it belongs in Profile only.");
        }

        [Test]
        public void WeakFootUsesIntRepresentation()
        {
            Assert.AreEqual(typeof(int), Field("WeakFoot").FieldType,
                "WeakFoot must be an int rating.");
        }

        [Test]
        public void WeakFootUses1To5Range()
        {
            Assert.AreEqual(1, ProfileConstant("WeakFootMin"), "WeakFoot min must be 1.");
            Assert.AreEqual(5, ProfileConstant("WeakFootMax"), "WeakFoot max must be 5.");
        }

        [Test]
        public void WeakFoot_HasValidAuthoredDefault()
        {
            var p = NewProfile();
            var val = (int)Field("WeakFoot").GetValue(p);
            Assert.IsTrue(val >= 1 && val <= 5,
                "Default WeakFoot must be within [1..5], got " + val + ".");
        }

        [Test]
        public void WeakFootMinimumBoundaryIsValid()
        {
            var p = NewProfile();
            Field("WeakFoot").SetValue(p, 1);
            var problems = GetInvalidProfileData(p);
            Assert.IsFalse(problems.Any(x => x.Contains("WeakFoot")),
                "WeakFoot=1 must be valid. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void WeakFootMaximumBoundaryIsValid()
        {
            var p = NewProfile();
            Field("WeakFoot").SetValue(p, 5);
            var problems = GetInvalidProfileData(p);
            Assert.IsFalse(problems.Any(x => x.Contains("WeakFoot")),
                "WeakFoot=5 must be valid. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void WeakFootBelowMinimumIsInvalid()
        {
            var p = NewProfile();
            Field("WeakFoot").SetValue(p, 0);
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("WeakFoot")),
                "WeakFoot=0 must be reported as invalid. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void WeakFootAboveMaximumIsInvalid()
        {
            var p = NewProfile();
            Field("WeakFoot").SetValue(p, 6);
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("WeakFoot")),
                "WeakFoot=6 must be reported as invalid. Got: " + string.Join("; ", problems));
        }

        // ---- 80.3 Skill Rating ----

        [Test]
        public void SkillRatingExists()
        {
            Assert.IsNotNull(Field("SkillRating"), "Profile must have SkillRating.");
        }

        [Test]
        public void SkillRatingIsSeparateFromDribbling()
        {
            Assert.IsTrue(PlayerStatsHasField("Dribbling"),
                "Dribbling must remain a PlayerStats category.");
            Assert.IsFalse(PlayerStatsHasField("SkillRating"),
                "SkillRating must NOT be a PlayerStats field; it belongs in Profile only.");
            Assert.IsNull(ProfileType().GetMethod("CalculateSkillRating",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "SkillRating must NOT be calculated from Dribbling.");
        }

        [Test]
        public void SkillRatingUsesIntRepresentation()
        {
            Assert.AreEqual(typeof(int), Field("SkillRating").FieldType,
                "SkillRating must be an int rating.");
        }

        [Test]
        public void SkillRatingUses1To5Range()
        {
            Assert.AreEqual(1, ProfileConstant("SkillRatingMin"), "SkillRating min must be 1.");
            Assert.AreEqual(5, ProfileConstant("SkillRatingMax"), "SkillRating max must be 5.");
        }

        [Test]
        public void SkillRating_HasValidAuthoredDefault()
        {
            var p = NewProfile();
            var val = (int)Field("SkillRating").GetValue(p);
            Assert.IsTrue(val >= 1 && val <= 5,
                "Default SkillRating must be within [1..5], got " + val + ".");
        }

        [Test]
        public void SkillRatingMinimumBoundaryIsValid()
        {
            var p = NewProfile();
            Field("SkillRating").SetValue(p, 1);
            var problems = GetInvalidProfileData(p);
            Assert.IsFalse(problems.Any(x => x.Contains("SkillRating")),
                "SkillRating=1 must be valid. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void SkillRatingMaximumBoundaryIsValid()
        {
            var p = NewProfile();
            Field("SkillRating").SetValue(p, 5);
            var problems = GetInvalidProfileData(p);
            Assert.IsFalse(problems.Any(x => x.Contains("SkillRating")),
                "SkillRating=5 must be valid. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void SkillRatingBelowMinimumIsInvalid()
        {
            var p = NewProfile();
            Field("SkillRating").SetValue(p, 0);
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("SkillRating")),
                "SkillRating=0 must be reported as invalid. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void SkillRatingAboveMaximumIsInvalid()
        {
            var p = NewProfile();
            Field("SkillRating").SetValue(p, 6);
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("SkillRating")),
                "SkillRating=6 must be reported as invalid. Got: " + string.Join("; ", problems));
        }

        // ---- 80.4 Supported Ranges / validation behavior ----

        [Test]
        public void ProfileValidationDoesNotUsePlayerStats99Range()
        {
            // WeakFoot=50 is valid on a PlayerStats 1-99 scale but INVALID on the profile 1-5 scale.
            var p = NewProfile();
            Field("WeakFoot").SetValue(p, 50);
            Field("SkillRating").SetValue(p, 50);
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("WeakFoot")),
                "WeakFoot=50 must be invalid on the 1-5 profile scale. Got: " + string.Join("; ", problems));
            Assert.IsTrue(problems.Any(x => x.Contains("SkillRating")),
                "SkillRating=50 must be invalid on the 1-5 profile scale. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void InvalidProfileValuesAreReported()
        {
            var p = NewProfile();
            Field("PreferredFoot").SetValue(p, Enum.ToObject(PreferredFootType(), 999));
            Field("WeakFoot").SetValue(p, -1);
            Field("SkillRating").SetValue(p, 6);
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("PreferredFoot")), "Expected PreferredFoot problem.");
            Assert.IsTrue(problems.Any(x => x.Contains("WeakFoot")), "Expected WeakFoot problem.");
            Assert.IsTrue(problems.Any(x => x.Contains("SkillRating")), "Expected SkillRating problem.");
        }

        [Test]
        public void InvalidProfileValuesAreNotSilentlyMutated()
        {
            var p = NewProfile();
            Field("WeakFoot").SetValue(p, -3);
            Field("SkillRating").SetValue(p, 9);
            var beforeWeak = (int)Field("WeakFoot").GetValue(p);
            var beforeSkill = (int)Field("SkillRating").GetValue(p);
            GetInvalidProfileData(p);
            Assert.AreEqual(beforeWeak, (int)Field("WeakFoot").GetValue(p),
                "GetInvalidProfileData must NOT clamp/replace WeakFoot.");
            Assert.AreEqual(beforeSkill, (int)Field("SkillRating").GetValue(p),
                "GetInvalidProfileData must NOT clamp/replace SkillRating.");
        }

        [Test]
        public void DefaultProfile_IsFootSkillValid()
        {
            var p = NewProfile();
            var problems = GetInvalidProfileData(p);
            Assert.IsFalse(problems.Any(x =>
                x.Contains("PreferredFoot") || x.Contains("WeakFoot") || x.Contains("SkillRating")),
                "A default Profile must be foot/skill valid. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void GoalkeepersUseSameFootAndSkillProfile()
        {
            foreach (var sig in new[] { "GoalkeeperProfile", "GoalkeeperFootSkill", "GKFootData" })
            {
                Assert.IsFalse(HasTypeName(sig),
                    "No separate goalkeeper foot/skill type may exist; GK uses the unified Profile.");
            }
        }

        [Test]
        public void OutfieldPlayersUseSameFootAndSkillProfile()
        {
            foreach (var sig in new[] { "OutfieldProfile", "OutfieldFootSkill" })
            {
                Assert.IsFalse(HasTypeName(sig),
                    "No separate outfield foot/skill type may exist; all players use the unified Profile.");
            }
        }

        [Test]
        public void NoOverallRatingCalculatorCreated()
        {
            Assert.IsFalse(HasTypeName("OverallRatingCalculator"), "No OverallRatingCalculator may exist.");
            Assert.IsNull(ProfileType().GetMethod("CalculateOverallRating",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Profile must NOT calculate OverallRating from foot/skill.");
        }

        // ---- No gameplay / animation / AI systems ----

        [Test]
        public void PreferredFootDoesNotBecomeGameplayLogic()
        {
            // Foot data must not spawn foot/gameplay systems.
            Assert.IsFalse(HasTypeName("FootSystem", "PreferredFootSystem", "FootController"),
                "No foot gameplay system may be created from PreferredFoot.");
        }

        [Test]
        public void WeakFootDoesNotBecomeGameplayLogic()
        {
            Assert.IsFalse(HasTypeName("WeakFootGameplay", "FootAccuracySystem"),
                "No weak-foot gameplay system may be created.");
        }

        [Test]
        public void SkillRatingDoesNotBecomeGameplayLogic()
        {
            Assert.IsFalse(HasTypeName("SkillMoveSystem", "SkillMoveController", "FootControlSystem"),
                "No skill-move gameplay system may be created from SkillRating.");
        }

        [Test]
        public void NoFootSystemCreated()
        {
            Assert.IsFalse(HasTypeName("FootSystem", "PreferredFootSystem", "FootController"),
                "No foot system may exist.");
        }

        [Test]
        public void NoSkillMoveSystemCreated()
        {
            Assert.IsFalse(HasTypeName("SkillMoveSystem", "SkillMoveController"),
                "No skill-move system may exist.");
        }

        [Test]
        public void NoAnimationSystemCreated()
        {
            Assert.IsFalse(HasTypeName("SkillMoveAnimation", "SkillAnimatorBridge", "FootAnimationSystem"),
                "SkillRating/foot data must NOT connect to animation.");
        }

        [Test]
        public void NoAISystemCreated()
        {
            Assert.IsFalse(HasTypeName("SkillRatingAI", "PreferredFootAI", "WeakFootAI"),
                "SkillRating/PreferredFoot/WeakFoot must NOT drive AI behavior.");
        }

        // ---- Position update: CF → SS ----

        [Test]
        public void CFIsNoLongerAnApprovedPosition()
        {
            var names = Enum.GetNames(PositionType());
            Assert.IsFalse(names.Contains("CF"),
                "CF must NO LONGER be an approved PlayerPosition.");
        }

        [Test]
        public void SSIsAnApprovedPosition()
        {
            var names = Enum.GetNames(PositionType());
            Assert.IsTrue(names.Contains("SS"), "SS (Shadow Striker) must be an approved PlayerPosition.");
        }

        [Test]
        public void SSIsStronglyTyped()
        {
            var names = Enum.GetNames(PositionType());
            Assert.IsTrue(names.Contains("SS"),
                "SS must be a strongly typed PlayerPosition enum member.");
        }

        [Test]
        public void CFAndSSAreNotBothSupported()
        {
            var names = Enum.GetNames(PositionType());
            Assert.IsFalse(names.Contains("CF") && names.Contains("SS"),
                "CF and SS must never both be supported.");
        }

        [Test]
        public void PositionCountRemainsThirteen()
        {
            Assert.AreEqual(13, Enum.GetNames(PositionType()).Length,
                "PlayerPosition must keep exactly 13 approved positions.");
        }
    }
}
