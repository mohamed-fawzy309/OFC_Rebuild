using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 91 — Team Colors (owned by TeamDefinition).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance).
    ///
    /// Task 91 locks:
    ///   - Team colors are AUTHORED TEAM DATA using UnityEngine.Color
    ///     (PrimaryColor / SecondaryColor on TeamDefinition) — never hex/int/string.
    ///   - Canonical ownership is TeamDefinition; PlayerDefinition / PlayerStats / Identity / Squad
    ///     do NOT own team colors.
    ///   - Colors are authored/serialized configuration, not runtime state; no Current*/Runtime*/
    ///     Applied color fields, no Renderer/scene-object references, no material/shader mutation.
    ///   - Kit appearance (HomeKitMaterial / AwayKitMaterial) is SEPARATE authored asset data and
    ///     is preserved, distinct from team color identity.
    ///   - No color system/controller/material generator/shader system/UI/hex parser/color database.
    ///   - Home/Away color rules (Task 96) and speculative colors stay DEFERRED.
    /// </summary>
    public class TeamColorTests
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

        private static Type IdentityType() => FindType(Prefix + "Identity");

        private static Type PlayerStatsType() => FindType(Prefix + "PlayerStats");

        private static object NewTeam() => ScriptableObject.CreateInstance(TeamDefType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static IEnumerable<string> FieldNames(Type t)
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name);
        }

        private static bool IsColorType(Type ft) => ft == typeof(Color) || ft.FullName == "UnityEngine.Color";

        private static readonly string[] RuntimeColorNames =
            { "CurrentPrimaryColor", "CurrentSecondaryColor", "RuntimePrimaryColor",
              "RuntimeSecondaryColor", "AppliedColor" };

        private static readonly string[] DeferredColorNames =
            { "TertiaryColor", "AccentColor", "GoalkeeperColor", "HomeColor", "AwayColor",
              "HomePrimaryColor", "HomeSecondaryColor", "AwayPrimaryColor", "AwaySecondaryColor" };

        private static readonly string[] RuntimeSceneTypes =
            { "GameObject", "Transform", "Renderer", "SpriteRenderer", "MeshRenderer",
              "SkinnedMeshRenderer", "MaterialPropertyBlock" };

        // ---- 91.1 Representation ----

        [Test]
        public void TeamHasPrimaryColor()
        {
            Assert.IsNotNull(FieldOf(TeamDefType(), "PrimaryColor"),
                "TeamDefinition must have a PrimaryColor field.");
        }

        [Test]
        public void TeamHasSecondaryColor()
        {
            Assert.IsNotNull(FieldOf(TeamDefType(), "SecondaryColor"),
                "TeamDefinition must have a SecondaryColor field.");
        }

        [Test]
        public void PrimaryColorUsesUnityColor()
        {
            Assert.IsTrue(IsColorType(FieldOf(TeamDefType(), "PrimaryColor").FieldType),
                "PrimaryColor must be UnityEngine.Color, not a hex string / int / custom struct.");
        }

        [Test]
        public void SecondaryColorUsesUnityColor()
        {
            Assert.IsTrue(IsColorType(FieldOf(TeamDefType(), "SecondaryColor").FieldType),
                "SecondaryColor must be UnityEngine.Color, not a hex string / int / custom struct.");
        }

        [Test]
        public void PrimaryAndSecondaryAreDistinctAuthoredFields()
        {
            var prim = FieldOf(TeamDefType(), "PrimaryColor");
            var sec = FieldOf(TeamDefType(), "SecondaryColor");
            Assert.AreNotEqual(prim.Name, sec.Name,
                "PrimaryColor and SecondaryColor must be separate fields.");
            Assert.AreNotEqual(prim, sec,
                "PrimaryColor and SecondaryColor must not be the same field instance.");
            Assert.IsTrue(IsColorType(prim.FieldType) && IsColorType(sec.FieldType),
                "Both colors must be UnityEngine.Color.");
        }

        // ---- 91.2 Primary / Secondary ----

        [Test]
        public void PrimaryColorDoesNotAutomaticallyDeriveSecondary()
        {
            // SecondaryColor is authored independently; there is no derivation method/field.
            Assert.IsNull(MethodByName(TeamDefType(), "DeriveSecondaryColor"),
                "SecondaryColor must not be derived from PrimaryColor via a routine.");
            Assert.IsNull(FieldOf(TeamDefType(), "SecondaryColorDerivedFromPrimary"),
                "SecondaryColor must not have a derivation-flag field.");
        }

        [Test]
        public void EqualPrimaryAndSecondaryAreNotAutomaticallyInvalid()
        {
            // Two equal colors are a valid authored state; no rule forces them to differ.
            Assert.IsNull(MethodByName(TeamDefType(), "ValidateColorsDistinct"),
                "No validation may force primary and secondary to be different.");
            Assert.IsNull(MethodByName(TeamDefType(), "GetInvalidColors"),
                "No over-engineered color validation may be added to TeamDefinition.");
        }

        // ---- 91.3 Ownership ----

        [Test]
        public void ColorsAreTeamOwned()
        {
            foreach (var n in new[] { "PrimaryColor", "SecondaryColor" })
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsNotNull(f, $"{n} must exist.");
                Assert.AreEqual(TeamDefType(), f.DeclaringType,
                    $"{n} must be declared on TeamDefinition.");
            }
        }

        [Test]
        public void ColorsAreNotPlayerOwned()
        {
            foreach (var t in new[] { PlayerDefType(), IdentityType() })
            {
                Assert.IsNull(FieldOf(t, "PrimaryColor"),
                    $"{t.Name} must not own PrimaryColor.");
                Assert.IsNull(FieldOf(t, "SecondaryColor"),
                    $"{t.Name} must not own SecondaryColor.");
                Assert.IsNull(FieldOf(t, "ClubColors"),
                    $"{t.Name} must not own ClubColors.");
            }
        }

        [Test]
        public void ColorsAreNotPlayerStats()
        {
            Assert.IsNull(FieldOf(PlayerStatsType(), "PrimaryColor"),
                "PlayerStats must not carry TeamColor fields.");
            Assert.IsNull(FieldOf(PlayerStatsType(), "SecondaryColor"),
                "PlayerStats must not carry TeamColor fields.");
        }

        [Test]
        public void ColorsAreNotIdentityFields()
        {
            // Identity stays TeamId/TeamName/ShortName; colors are separate appearance/identity data.
            foreach (var idField in new[] { "TeamId", "TeamName", "ShortName" })
            {
                Assert.IsNotNull(FieldOf(TeamDefType(), idField),
                    $"Identity field '{idField}' must remain on TeamDefinition.");
            }
            foreach (var t in new[] { TeamDefType() })
            {
                Assert.IsNotNull(FieldOf(t, "TeamId"), "TeamId must remain the identity key.");
            }
        }

        [Test]
        public void ColorsAreNotSquadFields()
        {
            // Starters/Substitutes stay PlayerDefinition[] references (no color data embedded in squad).
            foreach (var n in new[] { "Starters", "Substitutes" })
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsNotNull(f, $"{n} must remain on TeamDefinition.");
                Assert.IsTrue(f.FieldType.IsArray, $"{n} must remain an array.");
                Assert.AreEqual(PlayerDefType(), f.FieldType.GetElementType(),
                    $"{n} element type must remain PlayerDefinition (not a color/kit carrier).");
            }
            // Squad members (PlayerDefinition) must not own team colors.
            Assert.IsNull(FieldOf(PlayerDefType(), "PrimaryColor"),
                "Squad members must not own team colors.");
            Assert.IsNull(FieldOf(PlayerDefType(), "SecondaryColor"),
                "Squad members must not own team colors.");
        }

        // ---- 91.4 Runtime vs Configuration ----

        [Test]
        public void ColorsAreSerializedData()
        {
            // Colors are public serialized fields on an authored ScriptableObject asset.
            var sob = typeof(ScriptableObject).IsAssignableFrom(TeamDefType());
            Assert.IsTrue(sob, "TeamDefinition must remain a ScriptableObject.");
            foreach (var n in new[] { "PrimaryColor", "SecondaryColor" })
            {
                var f = FieldOf(TeamDefType(), n);
                Assert.IsTrue((f.Attributes & FieldAttributes.Public) != 0,
                    $"{n} must be a serialized public field.");
                Assert.IsTrue((f.Attributes & FieldAttributes.Static) == 0,
                    $"{n} must be instance (authored per-team) data.");
            }
        }

        [Test]
        public void ColorsAreAuthoredData_NotGenerated()
        {
            // No runtime generation (Random/Guid/HSV/hash) is used to produce team colors.
            Assert.IsNull(MethodByName(TeamDefType(), "GenerateColors"),
                "TeamDefinition must not generate colors at runtime.");
            Assert.IsNull(MethodByName(TeamDefType(), "RandomizeColors"),
                "TeamDefinition must not randomize team colors.");
            Assert.IsNull(FieldOf(TeamDefType(), "ColorSeed"),
                "TeamDefinition must not hold a color-generation seed.");
        }

        [Test]
        public void NoRuntimeColorState()
        {
            foreach (var n in RuntimeColorNames)
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"TeamDefinition must not hold runtime color state '{n}'.");
            }
        }

        [Test]
        public void NoRendererOrSceneObjectReferences()
        {
            foreach (var f in TeamDefType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.IsFalse(RuntimeSceneTypes.Contains(f.FieldType.Name),
                    $"TeamDefinition field '{f.Name}' must not be a runtime scene/renderer ref " +
                    $"({f.FieldType.Name}).");
            }
        }

        [Test]
        public void ColorsDoNotMutateMaterialsOrShaders()
        {
            Assert.IsNull(MethodByName(TeamDefType(), "ApplyToRenderer"),
                "TeamDefinition must not apply colors to a Renderer.");
            Assert.IsNull(MethodByName(TeamDefType(), "SetMaterialColor"),
                "TeamDefinition must not mutate materials.");
            Assert.IsNull(MethodByName(TeamDefType(), "SetShaderProperty"),
                "TeamDefinition must not set shader properties.");
            Assert.IsNull(MethodByName(TeamDefType(), "InstantiateKitMaterial"),
                "TeamDefinition must not instantiate materials.");
        }

        [Test]
        public void TeamDefinitionHasNoRenderingLoop()
        {
            foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
            {
                Assert.IsNull(MethodOf(TeamDefType(), m),
                    $"TeamDefinition must not run '{m}' — rendering is a runtime concern.");
            }
        }

        [Test]
        public void ExistingKitMaterialsRemainSeparate()
        {
            // Kit appearance assets and team color identity may legitimately coexist.
            Assert.IsNotNull(FieldOf(TeamDefType(), "HomeKitMaterial"),
                "HomeKitMaterial must remain an authored appearance asset.");
            Assert.IsNotNull(FieldOf(TeamDefType(), "AwayKitMaterial"),
                "AwayKitMaterial must remain an authored appearance asset.");
        }

        [Test]
        public void HomeAwayColorRulesRemainDeferred()
        {
            // Task 96 owns Home/Away; speculative extra colors stay DEFERRED.
            foreach (var n in DeferredColorNames)
            {
                Assert.IsNull(FieldOf(TeamDefType(), n),
                    $"Speculative/Home-Away color '{n}' must stay deferred (Task 96).");
            }
        }

        [Test]
        public void NoColorSystemOrInfrastructure()
        {
            Assert.IsFalse(HasTypeName(
                    "TeamColorController", "TeamColorSystem", "RuntimeTeamColorSystem",
                    "TeamMaterialSystem", "KitMaterialGenerator", "MaterialColorSystem",
                    "TeamColorShader", "KitShader", "ColorShaderSystem",
                    "TeamColorUI", "TeamColorPicker", "TeamColorPreview",
                    "ColorParser", "HexColorParser", "TeamColorDatabase"),
                "Team Colors must not require a color/material/shader/UI/database system.");
        }

        [Test]
        public void ColorsDoNotAffectRatingsOrStats()
        {
            // Colors have no stat-modifier surface on the team or any player object.
            Assert.IsNull(MethodByName(TeamDefType(), "ModifyTeamRating"),
                "TeamDefinition must not create stat modifiers from colors.");
            Assert.IsNull(MethodByName(TeamDefType(), "ApplyColorToRating"),
                "TeamDefinition must not apply colors to team ratings.");
        }

        // ---- Helpers ----

        private static MethodInfo MethodByName(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              BindingFlags.Static);

        private static MethodInfo MethodOf(Type t, string name) =>
            t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                              BindingFlags.Static);
    }
}
