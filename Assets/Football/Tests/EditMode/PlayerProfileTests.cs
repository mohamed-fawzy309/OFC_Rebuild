using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 75 — Player Profile (PlayerDefinition.Profile).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Profile was introduced in Task 66 as the role/capability/classification group inside
    /// PlayerDefinition. Task 75 confirms and locks its semantics: PrimaryPosition (strongly typed
    /// PlayerPosition, GK signal), SecondaryPositions (same position type), PreferredFoot (Right/
    /// Left enum), WeakFoot and SkillRating (int on their OWN conceptual scale, NOT PlayerStats
    /// 1-99), OverallRating (separate auth/derived-open profile field, no calculation here),
    /// CardType (Base/Rare/Special typed classification, not gameplay authority), and PlayStyle
    /// (single strongly-typed style, data only). It verifies the Profile vs PlayerStats separation,
    /// the goalkeeper profile selection rule (PrimaryPosition == GK → Goalkeeping primary; all
    /// categories retained for every player), no runtime state (no CurrentPosition/CurrentRole/
    /// RuntimePosition/ActivePosition), no gameplay logic, no UI, no Profile systems, no separate
    /// goalkeeper model, and no duplication of Identity/PhysicalProfile data.
    ///
    /// Validation policy: WeakFoot / SkillRating conceptually use their OWN 1-5 scale (NOT the
    /// PlayerStats 1-99 scale) and the position-list rules are recorded as POLICY/DEFERRED — Task 75
    /// documents these boundaries but does NOT hardcode a runtime validation engine (that belongs to
    /// the later dedicated validation tasks).
    /// </summary>
    public class PlayerProfileTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ProfileFields = new[]
        {
            "PrimaryPosition", "SecondaryPositions", "PreferredFoot", "WeakFoot",
            "SkillRating", "OverallRating", "CardType", "PlayStyle"
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

        private static Type ProfileType() => FindType(Prefix + "Profile");

        // ---- 75.0 Category responsibility ----

        [Test]
        public void Profile_Exists_AsSerializableData()
        {
            var t = ProfileType();
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Profile must be a [Serializable] data group inside PlayerDefinition, not a MonoBehaviour system.");
        }

        [Test]
        public void Profile_Contains_ExactlyTheApprovedFields()
        {
            var names = ProfileType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(ProfileFields.OrderBy(x => x).ToArray(), names,
                "Profile must contain exactly the approved fields, nothing more/less (no speculative traits/roles).");
        }

        // ---- 75.1 Primary Position ----

        [Test]
        public void PrimaryPosition_IsStronglyTyped()
        {
            var f = ProfileType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Profile must have PrimaryPosition.");
            Assert.IsTrue(f.FieldType.IsEnum, "PrimaryPosition must be a strongly typed enum, not string/int.");
            Assert.AreEqual(Prefix + "PlayerPosition", f.FieldType.FullName,
                "PrimaryPosition must use PlayerPosition.");
        }

        [Test]
        public void PlayerPosition_Defines_TheAuthoritativePositionSet()
        {
            // Audited existing implementation (Task 66): PlayerPosition enum is the authoritative
            // position representation. Not invented here; confirmed.
            var t = FindType(Prefix + "PlayerPosition");
            var values = Enum.GetNames(t);
            Assert.AreEqual(13, values.Length, "PlayerPosition must have the audited 13 positions.");
            foreach (var v in new[] { "GK", "CB", "LB", "RB", "CDM", "CM", "CAM", "LM", "RM", "LW", "RW", "SS", "ST" })
            {
                Assert.IsTrue(values.Contains(v), $"PlayerPosition must define '{v}'.");
            }
        }

        [Test]
        public void PrimaryPosition_HasAnAuthoredDefault()
        {
            var obj = Activator.CreateInstance(ProfileType());
            var f = ProfileType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);
            var val = (int)f.GetValue(obj);
            var names = Enum.GetNames(f.FieldType);
            Assert.IsTrue(val >= 0 && val < names.Length,
                "PrimaryPosition must have a valid authored default enum value.");
        }

        // ---- 75.2 Secondary Positions ----

        [Test]
        public void SecondaryPositions_UseSamePositionTypeAsPrimary()
        {
            var prim = ProfileType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);
            var sec = ProfileType().GetField("SecondaryPositions", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(sec, "Profile must have SecondaryPositions.");
            Assert.IsTrue(typeof(System.Collections.IList).IsAssignableFrom(sec.FieldType),
                "SecondaryPositions must be an IList collection of positions.");
            Assert.IsTrue(sec.FieldType.IsGenericType && sec.FieldType.GetGenericArguments().Length == 1,
                "SecondaryPositions must be a generic single-element-type collection.");
            Assert.AreEqual(prim.FieldType, sec.FieldType.GetGenericArguments()[0],
                "SecondaryPositions must use the SAME strongly typed PlayerPosition as PrimaryPosition.");
        }

        [Test]
        public void SecondaryPositions_AreStronglyTyped_NotStrings()
        {
            var sec = ProfileType().GetField("SecondaryPositions", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(sec.FieldType.IsGenericType,
                "SecondaryPositions must be a generic collection.");
            Assert.AreEqual(Prefix + "PlayerPosition", sec.FieldType.GetGenericArguments()[0].FullName,
                "SecondaryPositions element type must be PlayerPosition, not string.");
        }

        // ---- 75.3 Preferred Foot ----

        [Test]
        public void PreferredFoot_IsStronglyTyped_RightLeft()
        {
            var f = ProfileType().GetField("PreferredFoot", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Profile must have PreferredFoot.");
            Assert.IsTrue(f.FieldType.IsEnum, "PreferredFoot must be a strongly typed enum, not string/numeric.");
            Assert.AreEqual(Prefix + "PreferredFoot", f.FieldType.FullName);
            var values = Enum.GetNames(f.FieldType);
            Assert.AreEqual(2, values.Length, "PreferredFoot must be exactly Right/Left.");
            CollectionAssert.AreEquivalent(new[] { "Right", "Left" }, values);
        }

        [Test]
        public void PreferredFoot_IsNotA_WeakFoot_Rating()
        {
            Assert.AreNotEqual(typeof(int), ProfileType().GetField("PreferredFoot", BindingFlags.Public | BindingFlags.Instance).FieldType,
                "PreferredFoot must be a foot enum, not a numeric rating.");
        }

        // ---- 75.4 Weak Foot ----

        [Test]
        public void WeakFoot_IsProfileData_Int_OnOwnScale()
        {
            var f = ProfileType().GetField("WeakFoot", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Profile must have WeakFoot.");
            Assert.AreEqual(typeof(int), f.FieldType,
                "WeakFoot must be an int capability rating on its own conceptual scale (e.g. 1-5).");
        }

        [Test]
        public void WeakFoot_IsNotDuplicated_InPlayerStats()
        {
            var stats = new[] { "PaceStats", "ShootingStats", "PassingStats", "DribblingStats",
                "DefendingStats", "PhysicalStats", "GoalkeepingStats" };
            foreach (var s in stats)
            {
                Assert.IsNull(FindType(Prefix + s).GetField("WeakFoot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"WeakFoot must NOT be duplicated in {s}.");
            }
        }

        // ---- 75.5 Skill Rating ----

        [Test]
        public void SkillRating_IsProfileData_Int_OnOwnScale()
        {
            var f = ProfileType().GetField("SkillRating", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Profile must have SkillRating.");
            Assert.AreEqual(typeof(int), f.FieldType,
                "SkillRating must be an int capability rating on its own conceptual scale (e.g. 1-5).");
        }

        [Test]
        public void SkillRating_IsNotRenamed_OrDuplicated()
        {
            // Named exactly SkillRating (not SkillMovesRating etc.), not duplicated into PlayerStats.
            Assert.IsNull(ProfileType().GetField("SkillMovesRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "SkillRating must NOT be renamed SkillMovesRating.");
            foreach (var s in new[] { "DribblingStats", "PaceStats", "ShootingStats", "PassingStats",
                "DefendingStats", "PhysicalStats", "GoalkeepingStats" })
            {
                Assert.IsNull(FindType(Prefix + s).GetField("SkillRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"SkillRating must NOT be duplicated in {s}.");
            }
        }

        [Test]
        public void SkillRating_IsDistinctFrom_DribblingAndBallControl()
        {
            // SkillRating (technical skill-move capability) is conceptually related to but NOT the
            // same as Dribbling/BallControl/TightPossession/Agility (football performance attributes).
            var drib = FindType(Prefix + "DribblingStats");
            foreach (var n in new[] { "Dribbling", "BallControl", "TightPossession", "Agility" })
            {
                Assert.IsNotNull(drib.GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"DribblingStats must keep its own '{n}' field.");
            }
            // They live in different owner objects (Profile vs PlayerStats); no auto-derive.
            Assert.IsNull(ProfileType().GetMethod("CalculateSkillRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "SkillRating must not auto-calculate from Dribbling.");
        }

        // ---- 75.2/75.4/75.5 Validation policy (explicit boundaries, not a runtime engine) ----

        [Test]
        public void WeakFoot_DoesNotUse_PlayerStats199Scale()
        {
            // WeakFoot/SkillRating intentionally use their OWN conceptual scale; the PlayerStats
            // 1-99 constants belong to category ratings only (Task 67 dedicated these to the six
            // outfield + goalkeeping categories).
            Assert.AreEqual(1, (int)FindType(Prefix + "PlayerStats").GetField("RatingMin").GetRawConstantValue());
            Assert.AreEqual(99, (int)FindType(Prefix + "PlayerStats").GetField("RatingMax").GetRawConstantValue());
            // Both are int fields, not forced to be within [1..99] via the stat validator (their
            // validation is documented as POLICY/DEFERRED, not the PlayerStats checker).
            Assert.AreEqual(typeof(int), ProfileType().GetField("WeakFoot", BindingFlags.Public | BindingFlags.Instance).FieldType);
            Assert.AreEqual(typeof(int), ProfileType().GetField("SkillRating", BindingFlags.Public | BindingFlags.Instance).FieldType);
        }

        // ---- 75.0 OverallRating boundary ----

        [Test]
        public void OverallRating_RemainsIn_Profile_NotCalculated()
        {
            Assert.IsNotNull(ProfileType().GetField("OverallRating", BindingFlags.Public | BindingFlags.Instance),
                "OverallRating must remain a Profile field.");
            foreach (var s in new[] { "PlayerStats", "PaceStats", "ShootingStats", "PassingStats",
                "DribblingStats", "DefendingStats", "PhysicalStats", "GoalkeepingStats" })
            {
                Assert.IsNull(FindType(Prefix + s).GetField("OverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"OverallRating must NOT live in {s}.");
            }
            // No calculation / weighting / derivation in Task 75 (authored-vs-derived is a separate
            // deferred decision).
            Assert.IsNull(ProfileType().GetMethod("CalculateOverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Profile must NOT calculate OverallRating in Task 75.");
            Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.Name == "OverallRatingCalculator"),
                "No OverallRatingCalculator may be created.");
        }

        // ---- 75.6 Card Type ----

        [Test]
        public void CardType_IsStronglyTyped_Classification()
        {
            var f = ProfileType().GetField("CardType", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Profile must have CardType.");
            Assert.IsTrue(f.FieldType.IsEnum, "CardType must be a strongly typed enum.");
            Assert.AreEqual(Prefix + "PlayerCardType", f.FieldType.FullName);
            Assert.IsTrue(Enum.GetNames(f.FieldType).Contains("Basic"),
                "CardType must define a Basic value as the default.");
        }

        [Test]
        public void CardType_IsNot_GameplayAuthority()
        {
            // CardType is classification/presentation metadata, NOT a stats modifier or gameplay
            // authority. No card system.
            Assert.IsNull(ProfileType().GetField("CardTypeModifier", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.Name == "CardSystem"),
                "No CardSystem may be created for Task 75.");
        }

        // ---- 75.7 Play Style ----

        [Test]
        public void PlayStyle_IsStronglyTyped_SingleStyle()
        {
            // Audited: Profile.PlayStyle is a SINGLE strongly typed enum value (not a collection).
            var f = ProfileType().GetField("PlayStyle", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Profile must have PlayStyle.");
            Assert.IsTrue(f.FieldType.IsEnum, "PlayStyle must be a strongly typed enum, not a string.");
            Assert.AreEqual(Prefix + "PlayerPlayStyle", f.FieldType.FullName);
            Assert.AreNotEqual(typeof(List<>).MakeGenericType(f.FieldType), f.FieldType,
                "PlayStyle must be a single value, not a multi-style collection (cardinality policy).");
        }

        [Test]
        public void PlayStyle_IsDataOnly_NoBehavior()
        {
            Assert.IsNull(ProfileType().GetMethod("ExecutePlayStyle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "PlayStyle must not execute behavior.");
            foreach (var n in new[] { "PlayStyleSystem", "BehaviorSystem", "DecisionSystem" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.Name == n),
                    $"No '{n}' may be created for Task 75.");
            }
        }

        // ---- Goalkeeper profile selection (role rule, data unchanged) ----

        [Test]
        public void GKRule_PrimaryPositionGK_SelectsGoalkeeperProfile()
        {
            var pos = FindType(Prefix + "PlayerPosition");
            Assert.IsTrue(Enum.IsDefined(pos, "GK"), "PlayerPosition must define GK.");
            var prim = ProfileType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(pos, prim.FieldType, "PrimaryPosition must be PlayerPosition.");
        }

        [Test]
        public void AllPlayerStatsCategories_Remain_ForEveryPlayer()
        {
            // The GK role rule does NOT remove any PlayerStats category; every player retains all
            // seven categories including Goalkeeping.
            var ps = FindType(Prefix + "PlayerStats");
            var fields = ps.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            foreach (var cat in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping" })
            {
                Assert.IsTrue(fields.Contains(cat), $"PlayerStats must retain the '{cat}' category for every player.");
            }
        }

        // ---- Profile vs PlayerStats separation ----

        [Test]
        public void Profile_ContainsNoPlayerStatsCategoryFields()
        {
            var names = ProfileType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            foreach (var forbidden in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping" })
            {
                Assert.IsFalse(names.Contains(forbidden),
                    $"Profile must NOT contain the PlayerStats category '{forbidden}'.");
            }
        }

        [Test]
        public void Profile_DoesNotDuplicate_IdentityOrPhysicalProfile()
        {
            var names = ProfileType().GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            foreach (var forbidden in new[] { "PlayerId", "Name", "Nationality", "ClubReference",
                "Age", "HeightCm", "WeightKg" })
            {
                Assert.IsFalse(names.Contains(forbidden),
                    $"Profile must NOT duplicate Identity/PhysicalProfile data '{forbidden}'.");
            }
        }

        // ---- No runtime state / no gameplay / no Unity objects ----

        [Test]
        public void Profile_ContainsNoRuntimeState()
        {
            var t = ProfileType();
            foreach (var name in new[] { "CurrentPosition", "CurrentRole", "RuntimePosition",
                "ActivePosition", "IsGoalkeeper", "CurrentFormation", "CurrentCardState", "CurrentStyle" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Profile must NOT hold runtime state '{name}'.");
                Assert.IsNull(t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Profile must NOT expose runtime state '{name}'.");
            }
        }

        [Test]
        public void Profile_DoesNotReferenceUnityRuntimeObjects()
        {
            foreach (var f in ProfileType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(Transform), f.FieldType);
                Assert.AreNotEqual(typeof(Rigidbody), f.FieldType);
                Assert.AreNotEqual(typeof(Collider), f.FieldType);
                Assert.AreNotEqual(typeof(GameObject), f.FieldType);
                Assert.AreNotEqual(typeof(MonoBehaviour), f.FieldType);
                Assert.AreNotEqual(typeof(Animator), f.FieldType);
            }
        }

        [Test]
        public void Profile_ContainsNoGameplayLogic()
        {
            var t = ProfileType();
            foreach (var name in new[] { "ExecuteSkill", "ExecutePass", "Shoot", "Tackle",
                "MovePlayer", "ControlPlayer", "SetRuntimePosition", "SwitchPosition",
                "ApplyCardModifier", "ApplyPlayStyle", "CalculateOverallRating" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Profile must NOT implement gameplay via '{name}'.");
            }
        }

        // ---- No Profile systems / separate GK model / no UI ----

        [Test]
        public void Profile_DoesNotCreatePositionRoleOrSkillSystems()
        {
            foreach (var n in new[] { "PositionSystem", "RoleSystem", "SkillMoveSystem",
                "PlayStyleSystem", "CardSystem", "PlayerRoleController", "OverallRatingCalculator" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.Name == n),
                    $"Profile task must NOT create '{n}'.");
            }
        }

        [Test]
        public void Profile_DoesNotCreateUI()
        {
            foreach (var n in new[] { "PlayerCardUI", "PlayerProfileUI", "StatsScreen", "FifaCardUI" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.Name == n),
                    $"Profile task must NOT create UI '{n}'.");
            }
        }

        [Test]
        public void Profile_DoesNotCreateSeparateGoalkeeperModel()
        {
            foreach (var n in new[] { "GoalkeeperDefinition", "GoalkeeperPlayerStats", "GKDefinition" })
            {
                Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .Any(t => t.Name == n && t.Namespace != null && t.Namespace.StartsWith("Football")),
                    $"Profile task must NOT create the separate model '{n}' — the unified PlayerDefinition is used.");
            }
        }

        // ---- No Team implementation ----

        [Test]
        public void Profile_DoesNotImplementTeamData()
        {
            // ClubReference already exists in Identity; Team/Squad/Formation/Tactics are NOT built
            // here (owned by a later Team Data task, Task 89+).
            foreach (var n in new[] { "TeamDefinition", "Squad", "Formation", "Tactics", "TeamRatings" })
            {
                Assert.IsNull(ProfileType().GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Profile must NOT implement '{n}'.");
            }
        }
    }
}
