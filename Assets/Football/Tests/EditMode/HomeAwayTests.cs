using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 96 — Home/Away Configuration (authored presentation/configuration owned by TeamDefinition).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance + the real GetInvalidHomeAwayData API).
    ///
    /// Task 96 locks:
    ///   - The established authored Home/Away configuration is the kit appearance Material asset
    ///     references (HomeKitMaterial / AwayKitMaterial), REUSED from the existing Appearance block.
    ///   - Home/Away is CONFIGURATION, NOT match/venue/current-side runtime state (no IsHome/IsAway/
    ///     CurrentKit/CurrentVenue/CurrentHomeTeam ...).
    ///   - Do NOT duplicate Team Color identity (PrimaryColor/SecondaryColor stay Task 91's); no
    ///     HomePrimaryColor/AwayPrimaryColor etc. (locked deferred by Task 91 tests).
    ///   - No HomeFormation/HomeTactics/AwayTactics/HomeSquad/AwaySquad; no kit/render/match system.
    ///   - Validation (GetInvalidHomeAwayData) DETECTS + REPORTS null Material refs, never fabricates.
    /// </summary>
    public class HomeAwayTests
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

        private static Type TeamDefType() => FindType(Prefix + "TeamDefinition");

        private static Type PlayerDefType() => FindType(Prefix + "PlayerDefinition");

        private static object NewTeam() => ScriptableObject.CreateInstance(TeamDefType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static MethodInfo MethodOf(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              BindingFlags.Static);

        // ---- 96.1 Representation (reuse) ----

        [Test]
        public void HomeAwayMaterialsExistAsAuthoredAssets()
        {
            foreach (var n in new[] { "HomeKitMaterial", "AwayKitMaterial" })
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsNotNull(f, $"'{n}' must exist on TeamDefinition.");
                Assert.AreEqual(typeof(Material), f.FieldType,
                    $"'{n}' must be an authored Material asset reference.");
                Assert.AreEqual(TeamDefType(), f.DeclaringType,
                    $"'{n}' must be declared on TeamDefinition.");
                Assert.IsTrue((f.Attributes & FieldAttributes.Public) != 0,
                    $"'{n}' must be a public serialized authored field.");
            }
        }

        [Test]
        public void HomeAwayOwnershipBelongsToTeamDefinition()
        {
            Assert.IsNotNull(FieldOf(TeamDefType(), "HomeKitMaterial"),
                "TeamDefinition owns the Home configuration.");
            Assert.IsNotNull(FieldOf(TeamDefType(), "AwayKitMaterial"),
                "TeamDefinition owns the Away configuration.");
            Assert.IsNull(FieldOf(PlayerDefType(), "HomeKitMaterial"),
                "PlayerDefinition must not own Home/Away configuration.");
            Assert.IsNull(FieldOf(PlayerDefType(), "AwayKitMaterial"),
                "PlayerDefinition must not own Home/Away configuration.");
        }

        [Test]
        public void HomeAwayDoNotDuplicateTeamColorIdentity()
        {
            // Team Color identity stays Task 91's PrimaryColor/SecondaryColor.
            Assert.IsNotNull(FieldOf(TeamDefType(), "PrimaryColor"), "PrimaryColor stays on TeamDefinition.");
            Assert.IsNotNull(FieldOf(TeamDefType(), "SecondaryColor"), "SecondaryColor stays on TeamDefinition.");
            foreach (var n in new[] { "HomeColor", "AwayColor", "HomePrimaryColor", "HomeSecondaryColor",
                                      "AwayPrimaryColor", "AwaySecondaryColor", "TertiaryColor" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Home/Away must not duplicate Team Color identity via '{n}'.");
            }
        }

        [Test]
        public void NoHomeAwayFormationOrTacticsOrSquad()
        {
            foreach (var n in new[] { "HomeFormation", "AwayFormation", "HomeTactics", "AwayTactics",
                                      "HomeSquad", "AwaySquad", "HomeAggression", "AwayAggression" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Home/Away must not create domain field '{n}'.");
            }
        }

        [Test]
        public void HomeAwayDoNotAffectRatings()
        {
            Assert.IsNull(MethodOf(TeamDefType(), "ModifyTeamRatingFromHomeAway"),
                "Home/Away must not modify Team Ratings.");
            Assert.IsNull(MethodOf(TeamDefType(), "ApplyHomeAwayToRating"),
                "Home/Away must not apply to ratings.");
        }

        // ---- 96.3 Runtime boundary ----

        [Test]
        public void HomeAwayHaveNoRuntimeState()
        {
            foreach (var n in new[] { "IsHome", "IsAway", "CurrentHomeAway", "ActiveSide",
                                      "CurrentKit", "CurrentVenue", "CurrentHomeTeam",
                                      "CurrentAwayTeam", "CurrentHomeKit", "CurrentAwayKit" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not hold Home/Away runtime/match state '{n}'.");
            }
        }

        [Test]
        public void HomeAwayHaveNoRendererOrRuntimeObjects()
        {
            foreach (var n in new[] { "Renderer", "MeshRenderer", "SkinnedMeshRenderer", "GameObject",
                                      "Transform", "Animator", "MaterialPropertyBlock" })
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not hold runtime rendering object '{n}'.");
            }
            Assert.IsFalse(HasTypeName("HomeAwayRenderer", "RuntimeKitRenderer", "TeamKitRenderer",
                    "TeamAppearanceSystem", "KitSelector", "HomeAwayUI", "KitSelectorUI",
                    "TeamAppearanceUI"),
                "Home/Away must not create renderer/kit/UI infrastructure.");
        }

        [Test]
        public void HomeAwayDoNotRunLoopsOrSystems()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate", "ApplyKitToRenderer",
                                      "SelectKit", "SwitchHomeAway" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"TeamDefinition must not run '{m}' from Home/Away config.");
            }
            Assert.IsFalse(HasTypeName("MatchSystem", "MatchConfigurationSystem", "HomeAwayManager",
                    "HomeAwayController", "KitSystem", "KitManager", "KitController", "VenueConfig",
                    "MatchKitSystem"),
                "Home/Away must not create match/kit/manager/controller systems.");
        }

        // ---- 96.4 Validation ----

        [Test]
        public void ValidHomeAwayHasNoProblems()
        {
            // A team with both kit materials assigned reports no Home/Away problems.
            var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            Assert.IsNotNull(shader, "A built-in shader must be available to author the valid test.");
            var mat = new Material(shader);
            var team = NewTeam();
            SetMaterial(team, "HomeKitMaterial", mat);
            SetMaterial(team, "AwayKitMaterial", mat);
            Assert.IsEmpty(GetInvalidHomeAwayData(team));
        }

        [Test]
        public void NullHomeKitIsDetected()
        {
            var team = NewTeam();
            SetMaterial(team, "HomeKitMaterial", null);
            var problems = GetInvalidHomeAwayData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("HomeKitMaterial")),
                "A null HomeKitMaterial must be detected and reported.");
        }

        [Test]
        public void NullAwayKitIsDetected()
        {
            var team = NewTeam();
            SetMaterial(team, "AwayKitMaterial", null);
            var problems = GetInvalidHomeAwayData(team);
            Assert.IsTrue(problems.Any(x => x.Contains("AwayKitMaterial")),
                "A null AwayKitMaterial must be detected and reported.");
        }

        [Test]
        public void InvalidHomeAwayIsNotSilentlyMutated()
        {
            var team = NewTeam();
            SetMaterial(team, "HomeKitMaterial", null);
            GetInvalidHomeAwayData(team);
            Assert.IsNull(FieldOf(TeamDefType(), "HomeKitMaterial").GetValue(team),
                "Validation must not fabricate or replace the invalid Home kit Material.");
        }

        [Test]
        public void HomeAwayValidationBelongsToTeamDefinition()
        {
            Assert.IsNotNull(MethodOf(TeamDefType(), "GetInvalidHomeAwayData"),
                "Home/Away validation must live on TeamDefinition.");
            Assert.IsNull(MethodOf(PlayerDefType(), "GetInvalidHomeAwayData"),
                "PlayerDefinition must not own Home/Away validation.");
        }

        // ---- helpers ----

        private static List<string> GetInvalidHomeAwayData(object team) =>
            (List<string>)MethodOf(TeamDefType(), "GetInvalidHomeAwayData").Invoke(team, null);

        private static void SetMaterial(object team, string field, object value) =>
            FieldOf(TeamDefType(), field).SetValue(team, value);
    }
}
