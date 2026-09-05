using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 81 — Play Style Rules (PlayerDefinition.Profile.PlayStyle).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 81 (REVISION — approved categorical PlayStyle set) locks:
    ///   - A SINGLE strongly typed <c>PlayerPlayStyle</c> enum value (never a list/array, no
    ///     secondary/tertiary style). Approved values (replacing AllRounder/TargetMan/Creative/
    ///     BallWinner/Sweeper/Specialist): Poacher, False9, FreeRoamer, Winger, BoxStriker,
    ///     VersatileStriker (Attack); AnchorMan, Destroyer, BoxToBox, Orchestrator, HolePlayer,
    ///     Playmaker (Midfield); BuildUp, BallWinner, ModernDefender, AttackingFullBack,
    ///     DefensiveFullBack, InsideFullBack (Defense/Build-up); BallPlayingKeeper, ClassicKeeper
    ///     (Goalkeeper). No old unapproved values remain.
    ///   - Validation in the Profile boundary (<c>Profile.GetInvalidProfileData()</c>): an undefined
    ///     <c>PlayerPlayStyle</c> enum value is DETECTED + REPORTED, never silently reset.
    ///   - PlayStyle is authored data only — it executes no gameplay, drives no AI/animation/physics,
    ///     creates no stat modifiers or position-compatibility rules, and never touches PlayerStats or
    ///     OverallRating. No style system/database/registry is created.
    ///   - Goalkeepers and outfield players share the SAME unified Profile.PlayStyle field (GK styles
    ///     are just two values of the one enum).
    /// </summary>
    public class PlayStyleRulesTests
    {
        private const string Prefix = "Football.Data.";
        private static readonly string[] ApprovedStyles =
        {
            // Attack
            "Poacher", "False9", "FreeRoamer", "Winger", "BoxStriker", "VersatileStriker",
            // Midfield
            "AnchorMan", "Destroyer", "BoxToBox", "Orchestrator", "HolePlayer", "Playmaker",
            // Defense / Build-up
            "BuildUp", "BallWinner", "ModernDefender", "AttackingFullBack", "DefensiveFullBack", "InsideFullBack",
            // Goalkeeper
            "BallPlayingKeeper", "ClassicKeeper"
        };

        private static readonly string[] OldUnapprovedStyles =
        {
            "AllRounder", "TargetMan", "Creative", "Sweeper", "Specialist",
            "False 9", "Free Roamer", "Box Striker", "Anchor Man", "Box-to-Box", "Hole Player",
            "Build Up", "Modern Defender", "Attacking Full-back", "Defensive Full-back", "Inside Full-back",
            "Ball-Playing Keeper", "Classic Keeper"
        };

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

        private static Type PlayStyleType() => FindType(Prefix + "PlayerPlayStyle");

        private static object NewProfile() => Activator.CreateInstance(ProfileType());

        private static FieldInfo PlayStyleField() =>
            ProfileType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance);

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

        // ---- 81.1 Representation ----

        [Test]
        public void PlayStyleExists()
        {
            Assert.IsNotNull(PlayStyleField(), "Profile must have PlayStyle.");
        }

        [Test]
        public void PlayStyleUsesStronglyTypedEnum()
        {
            var f = PlayStyleField();
            Assert.IsTrue(f.FieldType.IsEnum, "PlayStyle must be a strongly typed enum, not a string/int.");
            Assert.AreEqual(Prefix + "PlayerPlayStyle", f.FieldType.FullName,
                "PlayStyle must use the PlayerPlayStyle enum.");
        }

        [Test]
        public void AllApprovedPlayStylesExist()
        {
            var names = Enum.GetNames(PlayStyleType());
            foreach (var s in ApprovedStyles)
            {
                Assert.IsTrue(names.Contains(s), $"PlayerPlayStyle must define '{s}'.");
            }
        }

        [Test]
        public void PlayStyleValuesAreDistinct()
        {
            var names = Enum.GetNames(PlayStyleType());
            Assert.AreEqual(ApprovedStyles.Length, names.Length,
                "PlayerPlayStyle must have exactly the approved " + ApprovedStyles.Length + " values.");
        }

        [Test]
        public void NoOldUnapprovedPlayStyleValuesRemain()
        {
            // Task 81 REVISION: the previous set (AllRounder/TargetMan/Creative/BallWinner/Sweeper/
            // Specialist) and any space/hyphen display forms are NOT retained as enum members.
            var names = Enum.GetNames(PlayStyleType()).ToList();
            Assert.That(names, Does.Not.Contain("AllRounder"));
            Assert.That(names, Does.Not.Contain("TargetMan"));
            Assert.That(names, Does.Not.Contain("Creative"));
            Assert.That(names, Does.Not.Contain("Sweeper"));
            Assert.That(names, Does.Not.Contain("Specialist"));
            // No member may use the human-readable spaced/hyphenated form; enum members are
            // identifier-safe PascalCase (e.g. False9, Winger, BallPlayingKeeper).
            Assert.IsEmpty(names.Where(n => n.Contains(" ") || n.Contains("-")),
                "Enum members must not use spaces or hyphens.");
        }

        [Test]
        public void GoalkeeperPlayStylesAreValuedMembersOfSingleEnum()
        {
            var names = Enum.GetNames(PlayStyleType()).ToList();
            Assert.That(names, Does.Contain("BallPlayingKeeper"));
            Assert.That(names, Does.Contain("ClassicKeeper"));
            // GK styles are NOT a separate type/field/system.
            Assert.IsFalse(HasTypeName("GoalkeeperPlayStyle", "GoalkeeperStyleSystem", "GoalkeeperPlayStyleDefinition"),
                "GK styles must be two values of the SAME PlayerPlayStyle enum; no separate GK style type.");
        }

        [Test]
        public void PlayStyleRemainsAuthoritativeSingleProfileField()
        {
            // PlayStyle belongs ONLY to PlayerDefinition.Profile.PlayStyle — not PlayerStats/Identity/
            // PhysicalProfile, and not a separate ScriptableObject asset.
            Assert.IsNotNull(PlayStyleField(), "PlayStyle must live on Profile.");
            Assert.IsNull(PlayerStatsType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance),
                "PlayStyle must NOT be on PlayerStats.");
            Assert.IsNull(FindType(Prefix + "Identity").GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance),
                "PlayStyle must NOT be on Identity.");
            Assert.IsNull(FindType(Prefix + "PhysicalProfile").GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance),
                "PlayStyle must NOT be on PhysicalProfile.");
        }

        // ---- 81.2 Single vs Multiple ----

        [Test]
        public void PlayStyleUsesSingleValue()
        {
            var f = PlayStyleField();
            Assert.IsTrue(f.FieldType.IsEnum,
                "PlayStyle must be a single enum value, not a collection.");
        }

        [Test]
        public void PlayStyleIsNotAListOrArray()
        {
            var f = PlayStyleField();
            var t = f.FieldType;
            Assert.IsFalse(t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>),
                "PlayStyle must NOT be a List.");
            Assert.IsFalse(t.IsArray, "PlayStyle must NOT be an array.");
        }

        [Test]
        public void NoSecondaryPlayStyleCreated()
        {
            foreach (var n in new[] { "SecondaryPlayStyle", "TertiaryPlayStyle", "AdditionalPlayStyles" })
            {
                Assert.IsNull(ProfileType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"No secondary/tertiary style field '{n}' may exist.");
            }
        }

        // ---- 81.3 Validation ----

        [Test]
        public void ProfileValidationHandlesPlayStyle()
        {
            var m = ProfileType().GetMethod("GetInvalidProfileData", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(m, "Profile must expose GetInvalidProfileData (PlayStyle validation boundary).");
        }

        [Test]
        public void InvalidPlayStyleEnumValueIsDetected()
        {
            var p = NewProfile();
            PlayStyleField().SetValue(p, Enum.ToObject(PlayStyleType(), 999));
            var problems = GetInvalidProfileData(p);
            Assert.IsTrue(problems.Any(x => x.Contains("PlayStyle")),
                "GetInvalidProfileData must report an undefined PlayerPlayStyle. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void InvalidPlayStyleIsNotSilentlyMutated()
        {
            var p = NewProfile();
            var invalid = Enum.ToObject(PlayStyleType(), 1234);
            PlayStyleField().SetValue(p, invalid);
            GetInvalidProfileData(p);
            Assert.AreEqual((int)invalid, (int)PlayStyleField().GetValue(p),
                "GetInvalidProfileData must NOT silently reset/replace an invalid PlayStyle.");
        }

        [Test]
        public void PlayStyleHasExplicitDefaultPolicy()
        {
            // The authored default is documented as an explicit policy: Profile.PlayStyle defaults to
            // Playmaker so a fresh definition validates. The enum's first/language-zero member
            // (Poacher) is NOT treated as an implied gameplay default.
            var p = NewProfile();
            var names = Enum.GetNames(PlayStyleType());
            var defaultVal = (int)PlayStyleField().GetValue(p);
            Assert.IsTrue(defaultVal >= 0 && defaultVal < names.Length,
                "Default PlayStyle must be a defined enum value.");
            Assert.AreEqual("Playmaker", names[defaultVal],
                "Authored default PlayStyle must be Playmaker (explicit default policy).");
        }

        [Test]
        public void PlayStyleIsNotValidatedByPlayerStats()
        {
            Assert.IsNull(PlayerStatsType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance),
                "PlayStyle must NOT be a PlayerStats field (so it is not 1-99 validated).");
            // Default PlayerStats must validate cleanly via its own authority, independent of PlayStyle.
            var ps = Activator.CreateInstance(PlayerStatsType());
            var statsProblems = (List<string>)PlayerStatsType().GetMethod("GetInvalidRatings",
                BindingFlags.Public | BindingFlags.Instance).Invoke(ps, null);
            Assert.IsEmpty(statsProblems, "Default PlayerStats must validate cleanly.");
        }

        // ---- 81.4 Separation / data-driven ----

        [Test]
        public void PlayStyleIsSeparateFromPrimaryPosition()
        {
            var style = PlayStyleField().FieldType;
            Assert.AreNotEqual(FindType(Prefix + "PlayerPosition"), style,
                "PlayStyle must be independent of PrimaryPosition type.");
            Assert.IsNull(ProfileType().GetMethod("GetPlayStyleForPosition",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayStyle must not be derived from position.");
        }

        [Test]
        public void PlayStyleIsSeparateFromSecondaryPositions()
        {
            Assert.IsFalse(ProfileType().GetField("SecondaryPositions",
                BindingFlags.Public | BindingFlags.Instance).FieldType == PlayStyleType(),
                "PlayStyle must not be coupled to SecondaryPositions.");
        }

        [Test]
        public void PlayStyleIsSeparateFromDribblingAndSkillRating()
        {
            Assert.IsNull(PlayerStatsType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance),
                "PlayStyle must not be a PlayerStats/Dribbling category.");
            Assert.IsNull(ProfileType().GetMethod("CalculatePlayStyle",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayStyle must not be calculated from Dribbling/SkillRating/stats.");
        }

        [Test]
        public void PlayStyleDoesNotModifyPlayerStatsOrOverallRating()
        {
            Assert.IsNull(ProfileType().GetMethod("ApplyPlayStyle",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayStyle must not apply stat modifiers.");
            Assert.IsNull(ProfileType().GetMethod("CalculateOverallRating",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayStyle must not calculate/modify OverallRating.");
        }

        [Test]
        public void PlayStyleContainsNoRuntimeState()
        {
            foreach (var n in new[] { "CurrentPlayStyle", "ActivePlayStyle", "RuntimePlayStyle" })
            {
                Assert.IsNull(ProfileType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"No runtime style field '{n}' may exist in Profile.");
            }
        }

        [Test]
        public void PlayStyleContainsNoGameplayLogic()
        {
            Assert.IsNull(ProfileType().GetMethod("ExecutePlayStyle",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayStyle must not execute behavior.");
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(ProfileType().GetMethod(m,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Profile must not run '{m}' methods from PlayStyle.");
            }
        }

        [Test]
        public void PlayStyleDoesNotReferenceUnityRuntimeObjects()
        {
            var all = AllTypes().Where(t => typeof(UnityEngine.Object).IsAssignableFrom(t)).ToList();
            foreach (var f in ProfileType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.IsFalse(f.FieldType == typeof(UnityEngine.Object) ||
                               all.Contains(f.FieldType),
                    $"Profile field '{f.Name}' must not reference Unity runtime objects.");
            }
        }

        // ---- No systems / models ----

        [Test]
        public void NoPlayStyleSystemCreated()
        {
            Assert.IsFalse(HasTypeName("PlayStyleSystem", "PlayStyleManager", "PlayStyleFactory"),
                "No play-style system/manager/factory may be created.");
        }

        [Test]
        public void NoPlayStyleAISystemCreated()
        {
            Assert.IsFalse(HasTypeName("AIPlayStyleSystem", "PlayStyleDecisionSystem", "RoleBehaviorSystem", "BehaviorTree"),
                "No play-style AI/decision system may be created.");
        }

        [Test]
        public void NoPlayStyleAnimationSystemCreated()
        {
            Assert.IsFalse(HasTypeName("PlayStyleAnimationSystem", "PlayStyleAnimatorBridge"),
                "No play-style animation system may be created.");
        }

        [Test]
        public void NoPlayStylePhysicsSystemCreated()
        {
            Assert.IsFalse(HasTypeName("PlayStylePhysicsSystem", "PlayStyleMovementSystem"),
                "No play-style physics/movement system may be created.");
        }

        [Test]
        public void NoPlayStyleDatabaseRegistryCreated()
        {
            Assert.IsFalse(HasTypeName("PlayStyleDatabase", "PlayStyleRegistry"),
                "No play-style database/registry may be created.");
        }

        [Test]
        public void GoalkeepersUseSamePlayStyleModel()
        {
            Assert.IsFalse(HasTypeName("GoalkeeperPlayStyle", "GKPlayStyle"),
                "Goalkeepers must use the same unified Profile.PlayStyle; no GK-specific style model.");
        }

        [Test]
        public void OutfieldPlayersUseSamePlayStyleModel()
        {
            Assert.IsFalse(HasTypeName("OutfieldPlayStyle", "OutfieldStyleProfile"),
                "Outfield players use the same unified Profile.PlayStyle.");
        }

        [Test]
        public void PlayStyleDoesNotCreateStatModifiers()
        {
            Assert.IsFalse(HasTypeName("PlayStyleStatModifier", "PlayStyleModifiers"),
                "PlayStyle must not create stat modifiers.");
        }

        [Test]
        public void PlayStyleDoesNotCreatePositionCompatibilityRules()
        {
            Assert.IsNull(ProfileType().GetMethod("GetCompatiblePlayStylesForPosition",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayStyle must not create position/style compatibility rules in Task 81.");
        }

        [Test]
        public void PlayStyleRemainsDataDriven()
        {
            // Profile.PlayStyle is a plain authored scalar on the data class; it is independent of
            // PlayerStats and carries no gameplay behavior.
            var f = PlayStyleField();
            Assert.IsTrue(f.DeclaringType == ProfileType(), "PlayStyle must live on the Profile data class.");
            Assert.IsNull(ProfileType().GetMethod("RunPlayStyle",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayStyle must not be executed as gameplay.");
        }
    }
}
