using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 85 — Data Asset Architecture.
    ///
    /// Defines the authored ScriptableObject asset boundary for PlayerDefinition:
    ///   - ONE PlayerDefinition ScriptableObject asset = ONE authored player definition.
    ///   - Identity / PhysicalProfile / Profile / PlayerStats live as nested serializable data
    ///     (NOT separate ScriptableObject assets).
    ///   - PlayerStats is owned per-player by its PlayerDefinition; it is not a global shared asset.
    ///   - PlayerDefinition is authored CONFIGURATION, treated read-only at runtime.
    ///   - Runtime state, Unity runtime objects, caches, and gameplay logic do not belong inside it.
    ///   - ClubReference is a reference boundary to TeamDefinition (not embedded team data).
    ///
    /// The data types live in Assembly-CSharp (no asmdef), so tests inspect them by reflection:
    /// real type/field/serialization inspection, not source-text matching.
    /// </summary>
    public class DataAssetArchitectureTests
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

        private static IEnumerable<Type> AllTypes() =>
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes);

        private static bool HasScriptableObjectType(params string[] names)
        {
            var all = AllTypes().Where(t => typeof(ScriptableObject).IsAssignableFrom(t)).Select(t => t.Name).ToList();
            return names.Any(n => all.Contains(n));
        }

        private static bool HasTypeName(params string[] names)
        {
            var all = AllTypes().Select(t => t.Name).ToList();
            return names.Any(n => all.Contains(n));
        }

        private static bool HasAttribute(Type t, string attributeTypeName)
        {
            return t.GetCustomAttributes(false).Any(a => a.GetType().Name == attributeTypeName);
        }

        private static Type PlayerDefType() => FindType(Prefix + "PlayerDefinition");
        private static Type IdentityType() => FindType(Prefix + "Identity");
        private static Type PhysicalType() => FindType(Prefix + "PhysicalProfile");
        private static Type ProfileType() => FindType(Prefix + "Profile");
        private static Type StatsType() => FindType(Prefix + "PlayerStats");
        private static Type TeamDefType() => FindType(Prefix + "TeamDefinition");

        private static IEnumerable<FieldInfo> PublicFields(Type t) =>
            t.GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static IEnumerable<Type> AllDataGroupTypes()
        {
            var names = new[] { "PlayerDefinition", "Identity", "PhysicalProfile", "Profile", "PlayerStats",
                "PaceStats", "ShootingStats", "PassingStats", "DribblingStats", "DefendingStats",
                "PhysicalStats", "GoalkeepingStats" };
            return names.Select(n => FindType(Prefix + n));
        }

        // ---- 85.1 ScriptableObject strategy ----

        [Test]
        public void PlayerDefinitionIsScriptableObject()
        {
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(PlayerDefType()),
                "PlayerDefinition must be a ScriptableObject (an authored asset).");
        }

        [Test]
        public void PlayerDefinitionIsTheCanonicalPlayerAsset()
        {
            // Authored via the CreateAssetMenu menu (an asset, not a transient runtime object),
            // and carries a CreateAssetMenu attribute exposing it in the editor asset pipeline.
            Assert.IsTrue(HasAttribute(PlayerDefType(), "CreateAssetMenuAttribute") ||
                          HasAttribute(PlayerDefType(), "CreateAssetMenu"),
                "PlayerDefinition must be authored via CreateAssetMenu as a data asset.");
            Assert.AreEqual(4, PublicFields(PlayerDefType()).Count(),
                "The canonical player asset exposes exactly the four data groups.");
        }

        [Test]
        public void PlayerDefinitionContainsIdentity()
        {
            Assert.IsNotNull(PlayerDefType().GetField("Identity", BindingFlags.Public | BindingFlags.Instance));
            var t = PlayerDefType().GetField("Identity", BindingFlags.Public | BindingFlags.Instance).FieldType;
            Assert.AreEqual(IdentityType(), t);
        }

        [Test]
        public void PlayerDefinitionContainsPhysicalProfile()
        {
            var f = PlayerDefType().GetField("Physical", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f);
            Assert.AreEqual(PhysicalType(), f.FieldType);
        }

        [Test]
        public void PlayerDefinitionContainsProfile()
        {
            var f = PlayerDefType().GetField("Profile", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f);
            Assert.AreEqual(ProfileType(), f.FieldType);
        }

        [Test]
        public void PlayerDefinitionContainsPlayerStats()
        {
            var f = PlayerDefType().GetField("PlayerStats", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f);
            Assert.AreEqual(StatsType(), f.FieldType);
        }

        [Test]
        public void NestedDataGroupsAreSerializable()
        {
            foreach (var name in new[] { "Identity", "PhysicalProfile", "Profile", "PlayerStats",
                "PaceStats", "ShootingStats", "PassingStats", "DribblingStats", "DefendingStats",
                "PhysicalStats", "GoalkeepingStats" })
            {
                Assert.IsTrue(FindType(Prefix + name).IsDefined(typeof(SerializableAttribute), false),
                    $"Nested data group '{name}' must be [Serializable] to serialize under the asset.");
            }
        }

        [Test]
        public void PlayerStatsIsOwnedByPlayerDefinition()
        {
            // PlayerStats is an instance field on PlayerDefinition, not a global static/shared type.
            var field = PlayerDefType().GetField("PlayerStats", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "PlayerStats must be an instance field owned by PlayerDefinition.");
            Assert.IsFalse(field.IsStatic);
        }

        [Test]
        public void PlayerStatsIsNotASeparateAssetRequirement()
        {
            // PlayerStats is a nested [Serializable] class, NOT its own ScriptableObject asset.
            Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(StatsType()),
                "PlayerStats must be nested serializable data, not a separate asset.");
        }

        [Test]
        public void IdentityIsOwnedByPlayerDefinition()
        {
            var field = PlayerDefType().GetField("Identity", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field);
            Assert.AreEqual(IdentityType(), field.FieldType);
            Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(IdentityType()),
                "Identity must be nested serializable data, not a separate asset.");
        }

        [Test]
        public void PhysicalProfileIsOwnedByPlayerDefinition()
        {
            Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(PhysicalType()),
                "PhysicalProfile must be nested serializable data, not a separate asset.");
        }

        [Test]
        public void ProfileIsOwnedByPlayerDefinition()
        {
            Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(ProfileType()),
                "Profile must be nested serializable data, not a separate asset.");
        }

        [Test]
        public void GoalkeepingRemainsNestedInPlayerStats()
        {
            var f = StatsType().GetField("Goalkeeping", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "Goalkeeping must remain a nested category in PlayerStats.");
            Assert.AreEqual(FindType(Prefix + "GoalkeepingStats"), f.FieldType);
        }

        [Test]
        public void NoSeparateGoalkeeperAsset()
        {
            Assert.IsFalse(HasScriptableObjectType("GoalkeeperDefinition", "GoalkeeperPlayerData", "GKPlayerDefinition"),
                "Goalkeeping must not be split into a separate ScriptableObject asset.");
        }

        [Test]
        public void NoSeparateCardAsset()
        {
            Assert.IsFalse(HasScriptableObjectType("CardAsset", "BaseCardDefinition", "RareCardDefinition", "SpecialCardDefinition"),
                "CardType must remain a Profile field, not a separate Card asset.");
        }

        [Test]
        public void NoSeparatePlayStyleAsset()
        {
            Assert.IsFalse(HasScriptableObjectType("PlayStyleDefinition", "PlayStyleAsset"),
                "PlayStyle must remain a Profile field, not a separate ScriptableObject asset.");
        }

        [Test]
        public void OnePlayerIsOnePlayerDefinition()
        {
            // No Base/Special/Goalkeeper/Outfield variants of PlayerDefinition.
            Assert.IsFalse(HasScriptableObjectType("PlayerBaseDefinition", "PlayerSpecialDefinition",
                "GoalkeeperDefinition", "OutfieldPlayerDefinition"),
                "A player remains exactly one PlayerDefinition; no specialization variants.");
        }

        // ---- 85.2 Asset ownership ----

        [Test]
        public void PlayerDefinitionDoesNotEmbedTeamDefinitionData()
        {
            // TeamDefinition fields must NOT be nested inside Identity/PlayerDefinition.
            var teamFields = PublicFields(TeamDefType()).Select(x => x.Name).ToArray();
            var identity = IdentityType();
            foreach (var n in teamFields)
            {
                Assert.IsNull(identity.GetField(n, BindingFlags.Public | BindingFlags.Instance),
                    $"Identity must not embed team field '{n}'.");
            }
            // No TeamDefinition nested instance field.
            Assert.IsFalse(PublicFields(PlayerDefType()).Any(f => f.FieldType == TeamDefType()),
                "PlayerDefinition must not embed a TeamDefinition instance.");
        }

        [Test]
        public void ClubReferenceIsAnAssetReferenceBoundary()
        {
            var f = IdentityType().GetField("ClubReference", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, "ClubReference must exist.");
            Assert.AreEqual(TeamDefType(), f.FieldType,
                "ClubReference must reference the TeamDefinition asset (not embedded data).");
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(f.FieldType),
                "TeamDefinition is an authored ScriptableObject asset (reference boundary).");
        }

        [Test]
        public void PlayerStatsIsNotGlobalShared()
        {
            // No static/shared mutable state: PlayerStats must not expose mutable static fields
            // (constants/compile-time literals are allowed), must not be abstract, and must not be
            // a Unity ScriptableObject singleton (it is per-asset data).
            var mutableStatics = StatsType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => !f.IsLiteral); // IsLiteral excludes const
            var mutableStaticList = mutableStatics.ToList();
            Assert.IsEmpty(mutableStaticList,
                "PlayerStats must not expose mutable static state (global shared mutable data).");
            Assert.IsFalse(StatsType().IsAbstract, "PlayerStats must be a concrete per-asset class.");
            Assert.IsFalse(typeof(ScriptableObject).IsAssignableFrom(StatsType()),
                "PlayerStats must not be a shared ScriptableObject asset.");
        }

        [Test]
        public void PerPlayerStatsAreOwnedNestedInstances()
        {
            // Each PlayerStats category is an instance field (per-asset serialized), not static
            // or shared mutable state.
            foreach (var cat in new[] { "Pace", "Shooting", "Passing", "Dribbling", "Defending", "Physical", "Goalkeeping" })
            {
                var f = StatsType().GetField(cat, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(f, $"PlayerStats must own instance category '{cat}'.");
                Assert.IsFalse(f.IsStatic,
                    $"Category '{cat}' must be per-asset instance data, not shared static state.");
            }
        }

        // ---- 85.3 Runtime vs configuration ----

        [Test]
        public void PlayerDefinitionIsAuthoredConfiguration()
        {
            // ScriptableObject + CreateAssetMenu => authored configuration asset, not runtime state.
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(PlayerDefType()));
            Assert.IsTrue(HasAttribute(PlayerDefType(), "CreateAssetMenuAttribute") ||
                          HasAttribute(PlayerDefType(), "CreateAssetMenu"));
        }

        [Test]
        public void PlayerDefinitionDoesNotContainCurrentPosition()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentPosition" });
        }

        [Test]
        public void PlayerDefinitionDoesNotContainCurrentRole()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentRole" });
        }

        [Test]
        public void PlayerDefinitionDoesNotContainCurrentStamina()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentStamina" });
        }

        [Test]
        public void PlayerDefinitionDoesNotContainRuntimeVelocity()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentVelocity", "Velocity", "CurrentSpeed", "CurrentAcceleration" });
        }

        [Test]
        public void PlayerDefinitionDoesNotContainRuntimePossession()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentPossession", "Possession" });
        }

        [Test]
        public void PlayerDefinitionDoesNotContainRuntimeAnimationState()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentAnimationState", "AnimationState", "CurrentAnimation" });
        }

        [Test]
        public void PlayerDefinitionDoesNotContainRuntimePhysicsState()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentHealth", "CurrentPhysicsState", "CurrentState" });
        }

        [Test]
        public void PlayerDefinitionKeepsAuthoredRatingsSeparatedFromRuntimeState()
        {
            // Authored ratings (e.g. Stamina in Physical) stay definition data; no Current* stamina
            // runtime mirror exists anywhere in the model.
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentStamina", "CurrentHealth", "CurrentFatigue", "CurrentFitness" });
        }

        [Test]
        public void PlayerDefinitionKeepsProfileDataSeparatedFromRuntimeState()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentPosition", "CurrentRole", "CurrentCardType", "CurrentPlayStyle" });
        }

        [Test]
        public void PlayerDefinitionKeepsIdentitySeparatedFromRuntimeState()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentClub", "CurrentTeamRole", "CurrentTeam" });
        }

        [Test]
        public void PlayerDefinitionKeepsPhysicalProfileSeparatedFromRuntimeState()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentWeight", "CurrentHeight", "CurrentAge" });
        }

        // ---- 85.4 Mutable state prevention ----

        [Test]
        public void PlayerDefinitionContainsNoRuntimeCollections()
        {
            // Mutable runtime collections (inventory/buffs/targets/teammates/actions) are absent.
            AssertNoField(AllDataGroupTypes(), new[] { "CurrentInventory", "CurrentBuffs", "CurrentTargets", "CurrentTeammates", "CurrentActions", "ActiveStatusEffects", "Inventory" });
        }

        [Test]
        public void PlayerDefinitionContainsNoTransientCaches()
        {
            AssertNoField(AllDataGroupTypes(), new[] { "CachedSpeed", "CachedOverall", "CachedPosition", "CachedTeam", "CachedGKState", "CachedAnimation", "CachedRating" });
        }

        [Test]
        public void PlayerDefinitionContainsNoUnityRuntimeObjects()
        {
            // No GameObject / Component subclass runtime references in the player data model, EXCEPT
            // authored asset references (ScriptableObject, e.g. ClubReference -> TeamDefinition).
            var allowedAsset = typeof(ScriptableObject);
            foreach (var t in AllDataGroupTypes())
            {
                foreach (var f in PublicFields(t))
                {
                    var ft = f.FieldType;
                    var isComponent = typeof(Component).IsAssignableFrom(ft);
                    var isAssetRef = typeof(ScriptableObject).IsAssignableFrom(ft);
                    if (isComponent && !isAssetRef)
                    {
                        Assert.Fail($"{t.Name}.{f.Name} holds forbidden Unity runtime object type '{ft.Name}'. " +
                                    "Object-derived refs must be authored asset references only.");
                    }
                    Assert.IsFalse(ft == typeof(GameObject), $"{t.Name}.{f.Name} holds a GameObject reference.");
                }
            }
        }

        [Test]
        public void PlayerDefinitionContainsNoTransformOrPhysicsComponents()
        {
            foreach (var t in AllDataGroupTypes())
            {
                foreach (var f in PublicFields(t))
                {
                    if (f.FieldType == typeof(Transform) || f.FieldType == typeof(Rigidbody) ||
                        f.FieldType == typeof(Collider) ||
                        typeof(MonoBehaviour).IsAssignableFrom(f.FieldType))
                    {
                        Assert.Fail($"{t.Name}.{f.Name} must not reference runtime component '{f.FieldType.Name}'.");
                    }
                }
            }
        }

        [Test]
        public void PlayerDefinitionContainsNoGameplayLogic()
        {
            foreach (var t in AllDataGroupTypes())
            {
                foreach (var m in new[] { "Update", "FixedUpdate", "LateUpdate" })
                {
                    Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"{t.Name} must not expose '{m}'.");
                }
            }
        }

        [Test]
        public void PlayerDefinitionDoesNotCreateRuntimeState()
        {
            foreach (var t in AllDataGroupTypes())
            {
                foreach (var m in new[] { "OnEnable", "OnDisable", "Start", "Awake" })
                {
                    Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"{t.Name} must not run runtime lifecycle behaviour '{m}'.");
                }
            }
        }

        [Test]
        public void PlayerDefinitionDoesNotCreatePlayerFactory()
        {
            Assert.IsFalse(HasTypeName("PlayerDefinitionFactory", "RuntimePlayerFactory", "PlayerFactory"),
                "No PlayerDefinition factory may be introduced for asset instantiation.");
        }

        [Test]
        public void PlayerDefinitionDoesNotCreateAssetImporter()
        {
            Assert.IsFalse(HasTypeName("PlayerDataImporter", "PlayerDataGenerator", "DataImporter"),
                "No player-data import pipeline may be added for this task.");
        }

        [Test]
        public void PlayerDefinitionDoesNotCreateAssetBuilder()
        {
            Assert.IsFalse(HasTypeName("PlayerAssetBuilder", "PlayerDataBuilder", "AssetBuilder"),
                "No player-data builder may be added for this task.");
        }

        [Test]
        public void PlayerDefinitionHasNoRuntimeMutationMethods()
        {
            var forbidden = new[] { "SetCurrentStamina", "SetCurrentPosition", "SetCurrentHealth",
                "ModifyStamina", "UpdatePosition", "ApplyRuntimeEffect", "AddBuff", "SetRuntimeState" };
            foreach (var t in AllDataGroupTypes())
            {
                foreach (var m in forbidden)
                {
                    Assert.IsNull(t.GetMethod(m, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        $"{t.Name} must not expose runtime mutation method '{m}'.");
                }
            }
        }

        private static void AssertNoField(IEnumerable<Type> types, string[] names)
        {
            foreach (var t in types)
            {
                foreach (var n in names)
                {
                    // Check both public and serialized private (nonpublic) instance fields.
                    var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.IsNull(f, $"{t.Name} must not contain runtime field '{n}'.");
                }
            }
        }
    }
}
