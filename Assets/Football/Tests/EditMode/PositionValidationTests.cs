using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 79 — Position Validation (PlayerDefinition.Profile position rules).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source-text matching).
    ///
    /// Task 75 locked the position STRUCTURE (strongly typed PlayerPosition, Single PrimaryPosition,
    /// SecondaryPositions of the same type, GK signal, no runtime role, no position system). Task 79
    /// adds the position VALIDATION authority — <c>Profile.GetInvalidPositions()</c> — and locks its
    /// behavior: all 13 approved positions are valid, one strongly-typed PrimaryPosition, an
    /// SecondaryPositions collection (empty allowed) that rejects duplicates, rejects PrimaryPosition
    /// overlap, and rejects undefined enum casts, all validated DETECT-AND-REPORT without mutating
    /// authored data. GK is a valid position; GK-as-secondary and outfield-secondary-to-GK are NOT
    /// structurally prohibited (goalkeeper-compatibility policy belongs to a later task). Position
    /// validation does NOT create runtime positions, role/formation/AI/UI systems, a separate
    /// goalkeeper model, or modify PlayerStats / OverallRating.
    /// </summary>
    public class PositionValidationTests
    {
        private const string Prefix = "Football.Data.";

        private static readonly string[] ApprovedPositions =
        {
            "GK", "CB", "LB", "RB", "CDM", "CM", "CAM", "LM", "RM", "LW", "RW", "SS", "ST"
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

        private static Type PositionType() => FindType(Prefix + "PlayerPosition");

        private static object NewProfile() => Activator.CreateInstance(ProfileType());

        private static void SetPrimary(object profile, object value)
        {
            GetPrimaryField().SetValue(profile, value);
        }

        private static FieldInfo GetPrimaryField()
        {
            return ProfileType().GetField("PrimaryPosition", BindingFlags.Public | BindingFlags.Instance);
        }

        private static System.Collections.IList GetSecondaryList(object profile)
        {
            return (System.Collections.IList)ProfileType().GetField("SecondaryPositions",
                BindingFlags.Public | BindingFlags.Instance).GetValue(profile);
        }

        private static object EnumValue(string name)
        {
            return Enum.Parse(PositionType(), name);
        }

        private static object UndefinedValue(int n)
        {
            return Enum.ToObject(PositionType(), n);
        }

        private static List<string> GetProblems(object profile)
        {
            return (List<string>)ProfileType().GetMethod("GetInvalidPositions",
                BindingFlags.Public | BindingFlags.Instance).Invoke(profile, null);
        }

        private static object ProfileWithPrimary(string primary)
        {
            var p = NewProfile();
            SetPrimary(p, EnumValue(primary));
            return p;
        }

        // ---- 79.1 Define valid positions ----

        [Test]
        public void GetInvalidPositions_Exists_OnProfile()
        {
            var m = ProfileType().GetMethod("GetInvalidPositions", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(m, "Profile must expose Position validation (GetInvalidPositions).");
            Assert.AreEqual(typeof(List<string>), m.ReturnType);
        }

        [Test]
        public void AllApprovedPositions_AreValid()
        {
            foreach (var name in ApprovedPositions)
            {
                var p = ProfileWithPrimary(name);
                var problems = GetProblems(p);
                Assert.IsFalse(problems.Any(x => x.Contains("PrimaryPosition")),
                    $"'{name}' is an approved position and must be valid as PrimaryPosition. Got: {string.Join("; ", problems)}");
            }
        }

        [Test]
        public void GK_IsAValidPosition()
        {
            var p = ProfileWithPrimary("GK");
            Assert.IsEmpty(GetProblems(p), "GK must be a valid PrimaryPosition with no structural errors.");
        }

        [Test]
        public void PositionType_UsesStronglyTypedEnum_NotString()
        {
            Assert.IsTrue(GetPrimaryField().FieldType.IsEnum, "PrimaryPosition must be a strongly typed enum.");
            Assert.AreEqual(Prefix + "PlayerPosition", GetPrimaryField().FieldType.FullName,
                "PrimaryPosition must use PlayerPosition, not a string.");
        }

        [Test]
        public void PrimaryPosition_IsSingleValue_NotCollection()
        {
            Assert.IsFalse(typeof(System.Collections.IEnumerable).IsAssignableFrom(GetPrimaryField().FieldType),
                "PrimaryPosition must be a single value, not a collection.");
        }

        // ---- 79.2 Primary position rules ----

        [Test]
        public void PrimaryPosition_HasValidAuthoredDefault_NotAnUnsetSentinel()
        {
            // Audited: PrimaryPosition defaults to ST, a VALID authored enum value. There is no
            // dedicated "Unset/None" sentinel value; ST is a real position, so a default Profile is
            // already positionally valid.
            var p = NewProfile();
            var value = GetPrimaryField().GetValue(p);
            Assert.IsTrue(Enum.GetNames(PositionType()).Contains(value.ToString()),
                "PrimaryPosition default (ST) must be a defined authored position.");
            Assert.IsEmpty(GetProblems(p), "The default Profile must be positionally valid (no fake 'unset' state).");
        }

        [Test]
        public void PrimaryPosition_LanguageDefault_IsNotConfusedWithAuthoredDefault()
        {
            // Documented policy: the enum's default (ST) is both the language default and a valid
            // authored value. There is no separate "unset" concept; validation never treats a defined
            // position as "not authored".
            var names = Enum.GetNames(PositionType());
            var defaultVal = GetPrimaryField().GetValue(NewProfile()).ToString();
            Assert.IsTrue(names.Contains(defaultVal), "The default value must be a valid position, not an 'unset' sentinel.");
        }

        // ---- 79.3 Secondary positions ----

        [Test]
        public void SecondaryPositions_UseSameTypeAsPrimary()
        {
            var sec = ProfileType().GetField("SecondaryPositions", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsTrue(sec.FieldType.IsGenericType, "SecondaryPositions must be a generic collection.");
            Assert.AreEqual(GetPrimaryField().FieldType, sec.FieldType.GetGenericArguments()[0],
                "SecondaryPositions must use the SAME strongly typed PlayerPosition.");
        }

        [Test]
        public void SecondaryPositions_AreNotStrings()
        {
            var sec = ProfileType().GetField("SecondaryPositions", BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(Prefix + "PlayerPosition", sec.FieldType.GetGenericArguments()[0].FullName,
                "SecondaryPositions element type must be PlayerPosition, not string.");
        }

        [Test]
        public void EmptySecondaryPositions_AreValid()
        {
            // Policy: SecondaryPositions may be empty (default is an empty list).
            var p = ProfileWithPrimary("CM");
            Assert.IsEmpty(GetSecondaryList(p), "Default SecondaryPositions must be empty.");
            Assert.IsEmpty(GetProblems(p), "An empty SecondaryPositions list must be positionally valid.");
        }

        [Test]
        public void ValidMultiPosition_CMWithCAM_AndCDM_Passes()
        {
            var p = ProfileWithPrimary("CM");
            var list = GetSecondaryList(p);
            list.Add(EnumValue("CAM"));
            list.Add(EnumValue("CDM"));
            Assert.IsEmpty(GetProblems(p),
                "Primary=CM with Secondary=[CAM, CDM] is a valid multi-position configuration.");
        }

        [Test]
        public void DuplicateSecondaryPosition_IsDetected()
        {
            var p = ProfileWithPrimary("CM");
            var list = GetSecondaryList(p);
            list.Add(EnumValue("CAM"));
            list.Add(EnumValue("CAM"));
            var problems = GetProblems(p);
            Assert.IsTrue(problems.Any(x => x.Contains("CAM") && x.Contains("Duplicate")),
                "A duplicated secondary position (CAM twice) must be detected. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void PrimaryPositionDuplicatedInSecondary_IsDetected()
        {
            var p = ProfileWithPrimary("CM");
            var list = GetSecondaryList(p);
            list.Add(EnumValue("CM"));
            list.Add(EnumValue("CDM"));
            var problems = GetProblems(p);
            Assert.IsTrue(problems.Any(x => x.Contains("CM") && x.Contains("PrimaryPosition")),
                "PrimaryPosition (CM) listed in SecondaryPositions must be detected. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void GKCanAppearAsSecondary_IsNotProhibited_Here()
        {
            // Policy: GK as a secondary position is NOT structurally prohibited in Task 79
            // (goalkeeper-compatibility rules belong to a later task).
            var p = ProfileWithPrimary("ST");
            var list = GetSecondaryList(p);
            list.Add(EnumValue("GK"));
            Assert.IsEmpty(GetProblems(p),
                "ST primary with GK secondary must produce no structural error (GK-compat is deferred).");
        }

        [Test]
        public void GKCanHaveOutfieldSecondary_IsNotProhibited_Here()
        {
            var p = ProfileWithPrimary("GK");
            var list = GetSecondaryList(p);
            list.Add(EnumValue("CM"));
            Assert.IsEmpty(GetProblems(p),
                "GK primary with an outfield secondary must produce no structural error (deferred).");
        }

        [Test]
        public void SecondaryPositions_HaveNoMaximumBound()
        {
            // Policy: the project has no bounded maximum for secondary positions; all 12 other
            // positions may be listed without a structural max error.
            var p = ProfileWithPrimary("GK");
            var list = GetSecondaryList(p);
            foreach (var name in ApprovedPositions.Where(n => n != "GK"))
            {
                list.Add(EnumValue(name));
            }
            Assert.IsEmpty(GetProblems(p), "Listing all other positions as secondary must not hit an invented max.");
        }

        // ---- 79.4 Multi-position validation ----

        [Test]
        public void InvalidPrimaryPosition_IsDetected()
        {
            var p = ProfileWithPrimary("CM");
            SetPrimary(p, UndefinedValue(999));
            var problems = GetProblems(p);
            Assert.IsTrue(problems.Any(x => x.Contains("PrimaryPosition") && x.Contains("not a defined")),
                "An undefined PrimaryPosition cast must be detected. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void InvalidSecondaryPosition_IsDetected()
        {
            var p = ProfileWithPrimary("CM");
            var list = GetSecondaryList(p);
            list.Add(UndefinedValue(999));
            var problems = GetProblems(p);
            Assert.IsTrue(problems.Any(x => x.Contains("SecondaryPositions[0]") && x.Contains("not a defined")),
                "An undefined SecondaryPosition cast must be detected with its index. Got: " + string.Join("; ", problems));
        }

        [Test]
        public void Message_IsActionable_IdentifiesPosition()
        {
            var p = ProfileWithPrimary("CM");
            var list = GetSecondaryList(p);
            list.Add(UndefinedValue(999));
            var problems = GetProblems(p);
            var msg = problems.FirstOrDefault(x => x.Contains("not a defined"));
            Assert.IsNotNull(msg, "The diagnostic must be actionable.");
            Assert.IsTrue(msg.Contains("SecondaryPositions[0]"), "The diagnostic must identify the collection index.");
            Assert.IsTrue(msg.Contains("999"), "The diagnostic must include the offending value.");
        }

        // ---- No mutation ----

        [Test]
        public void PositionValidation_DoesNotMutateAuthoredData()
        {
            var p = ProfileWithPrimary("CM");
            var list = GetSecondaryList(p);
            list.Add(EnumValue("CAM"));
            list.Add(EnumValue("CAM"));
            list.Add(EnumValue("CDM"));
            var original = list.Cast<object>().ToArray();

            GetProblems(p);

            CollectionAssert.AreEqual(original, list.Cast<object>().ToArray(),
                "Validation must NOT remove duplicate/reordered positions from the authored list.");
        }

        [Test]
        public void PositionValidation_DoesNotChangePrimaryPosition()
        {
            var p = ProfileWithPrimary("CM");
            SetPrimary(p, EnumValue("CM"));
            GetProblems(p);
            Assert.AreEqual("CM", GetPrimaryField().GetValue(p).ToString(),
                "Validation must NOT change PrimaryPosition.");
        }

        // ---- No runtime / no systems / no gameplay / no separate GK model ----

        [Test]
        public void PositionValidation_DoesNotCreateRuntimePosition()
        {
            var t = ProfileType();
            foreach (var name in new[] { "CurrentPosition", "CurrentRole", "RuntimePosition",
                "ActivePosition", "PositionInField", "NextPosition" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Position validation must NOT add runtime position '{name}'.");
            }
        }

        [Test]
        public void PositionValidation_DoesNotCreateSystems()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "PositionSystem", "RoleSystem", "PlayerRoleSystem",
                "FormationPositionSystem", "FormationSystem", "PositionAI", "DefensivePositioningAI",
                "AttackingPositionAI" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null
                    && t.Namespace.StartsWith("Football")),
                    $"Position validation must NOT create '{name}'.");
            }
        }

        [Test]
        public void PositionValidation_DoesNotCreateUI()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "PositionSelectorUI", "PositionDisplayUI", "PlayerPositionUI" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null
                    && t.Namespace.StartsWith("Football")),
                    $"Position validation must NOT create UI '{name}'.");
            }
        }

        [Test]
        public void PositionValidation_DoesNotCreateSeparateGoalkeeperModel()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "GoalkeeperDefinition", "GKDefinition", "GoalkeeperPlayerPositionProfile" })
            {
                Assert.IsFalse(all.Any(t => t.Name == name && t.Namespace != null
                    && t.Namespace.StartsWith("Football")),
                    $"Position validation must NOT create the separate '{name}' model — GK uses the same model.");
            }
        }

        [Test]
        public void GK_UsesSamePositionModel()
        {
            // GK players use the SAME Profile/PrimaryPosition model; no separate position type.
            Assert.IsTrue(Enum.GetNames(PositionType()).Contains("GK"),
                "GK must be a member of the unified PlayerPosition enum.");
            foreach (var sig in new[] { "GoalkeeperPosition", "GKPosition" })
            {
                Assert.IsNull(AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                    .FirstOrDefault(t => t.Name == sig),
                    $"No separate '{sig}' type may exist.");
            }
        }

        [Test]
        public void PositionValidation_DoesNotModifyPlayerStatsOrOverallRating()
        {
            // GetInvalidPositions must not touch PlayerStats or OverallRating.
            var t = ProfileType();
            foreach (var name in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending",
                "Physical", "Goalkeeping", "PlayerStats" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Profile/position validation must NOT hold/use PlayerStats '{name}'.");
            }
            Assert.IsNull(t.GetMethod("CalculateOverallRating", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Position validation must NOT recalculate OverallRating.");
            // Position validation must not mutate any rating: validated data is unchanged.
            Assert.IsNull(t.GetMethod("SetPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                "Position validation must NOT provide runtime position setters.");
            Assert.IsNull(t.GetMethod("ChangePosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(t.GetMethod("SwitchPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        [Test]
        public void PositionValidation_DoesNotValidateOtherProfileFields()
        {
            // GetInvalidPositions reports POSITION issues only; it must not report a WeakFoot or
            // SkillRating value (those belong to other boundaries).
            var p = ProfileWithPrimary("CM");
            ProfileType().GetField("WeakFoot", BindingFlags.Public | BindingFlags.Instance).SetValue(p, 99);
            ProfileType().GetField("SkillRating", BindingFlags.Public | BindingFlags.Instance).SetValue(p, -5);
            var problems = GetProblems(p);
            Assert.IsFalse(problems.Any(x => x.Contains("WeakFoot") || x.Contains("SkillRating")),
                "GetInvalidPositions must only report position problems, not foot/skill/stats issues. Got: " + string.Join("; ", problems));
        }
    }
}
