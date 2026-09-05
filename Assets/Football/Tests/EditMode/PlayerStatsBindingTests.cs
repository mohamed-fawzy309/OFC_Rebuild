using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 140 — Player Stats Binding.
    ///
    /// Establishes and locks the authoritative, minimal way the runtime Player Entity accesses the
    /// PlayerStats belonging to its PlayerDefinition:
    ///
    ///   PlayerEntity --(Definition)--> PlayerDefinition --(PlayerStats)--> PlayerStats
    ///
    /// Authoritative source: <c>PlayerDefinition.PlayerStats</c> (the ONE authored 35-rating source).
    /// Runtime binding: <c>PlayerEntity.Definition.PlayerStats</c> — a single reference path with NO
    /// duplicated storage, NO copy of the values, NO stats manager/registry/cache/component.
    ///
    /// Existing data-layer coverage (NOT duplicated here):
    ///   - PlayerStatsTests: 7 categories / exact approved attributes / range / defaults / validation.
    ///   - AttributeValidationTests: exactly 35 attributes, single range authority, no validators.
    ///   - PlayerEntityTests (Task 139): PlayerEntity holds exactly one PlayerDefinition field.
    ///
    /// Task 140 adds the BINDING-layer locks only: reachability through Definition (same reference,
    /// not a copy), no second storage location on the entity, no invented stats infrastructure,
    /// no stats component on the prefab, and no runtime-state stats on the runtime entity.
    ///
    /// Authored string PlayerId / event int PlayerId remain distinct and un-reconciled (Task 139).
    /// </summary>
    public class PlayerStatsBindingTests
    {
        private const string PrefabPath = "Assets/Football/Prefabs/Players/Player.prefab";

        private static FieldInfo[] DeclaredFields(System.Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.DeclaringType == type)
                .ToArray();
        }

        // ---- 140.3 Source of truth ----

        [Test]
        public void PlayerDefinition_Exposes_PlayerStats()
        {
            // PlayerDefinition.PlayerStats is the authoritative PlayerStats source (public, owned).
            var field = typeof(Football.Data.PlayerDefinition).GetField(
                "PlayerStats", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(field, "PlayerDefinition must expose a PlayerStats field.");
            Assert.AreEqual(typeof(Football.Data.PlayerStats), field.FieldType,
                "PlayerDefinition.PlayerStats must be of the authoritative PlayerStats type.");
        }

        // ---- 140.3 / 140.5 Runtime binding chain ----

        [Test]
        public void PlayerEntity_Reaches_AuthoritativePlayerStats_ThroughDefinition()
        {
            // The runtime binding is: PlayerEntity.Definition.PlayerStats (SAME reference; no copy).
            var go = new GameObject("StatsBindingTest");
            try
            {
                var entity = go.AddComponent<Football.Players.PlayerEntity>();
                var definition = Football.Data.PlayerDefinition.CreateInstance<Football.Data.PlayerDefinition>();
                try
                {
                    Assert.IsNotNull(definition.PlayerStats,
                        "A freshly-created PlayerDefinition must own its PlayerStats instance.");

                    entity.AssignDefinition(definition);

                    Assert.AreSame(definition, entity.Definition,
                        "PlayerEntity.Definition must return the assigned PlayerDefinition reference.");
                    Assert.AreSame(definition.PlayerStats, entity.Definition.PlayerStats,
                        "PlayerEntity.Definition.PlayerStats must be the SAME authored PlayerStats " +
                        "object (binding, not a copy).");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayerEntity_DoesNotDuplicate_PlayerStatsStorage()
        {
            // The entity must hold ONLY the _definition reference — no PlayerStats/category copy.
            var fields = DeclaredFields(typeof(Football.Players.PlayerEntity));
            Assert.AreEqual(1, fields.Length,
                $"PlayerEntity must declare exactly one field (the _definition reference). " +
                $"Found: {string.Join(", ", fields.Select(f => f.Name))}");
            Assert.AreEqual(typeof(Football.Data.PlayerDefinition), fields[0].FieldType,
                "PlayerEntity's sole field must be a PlayerDefinition reference.");

            var statTypes = new[]
            {
                typeof(Football.Data.PlayerStats),
                typeof(Football.Data.PaceStats),
                typeof(Football.Data.ShootingStats),
                typeof(Football.Data.PassingStats),
                typeof(Football.Data.DribblingStats),
                typeof(Football.Data.DefendingStats),
                typeof(Football.Data.PhysicalStats),
                typeof(Football.Data.GoalkeepingStats)
            };
            Assert.IsFalse(statTypes.Contains(fields[0].FieldType),
                "The sole PlayerEntity field must not be a PlayerStats/category type (no stats copy).");
        }

        [Test]
        public void PlayerEntity_HasNoStatsStorageOrAccessor_ThatIsSecondStorage()
        {
            // Decision (140.5/140.6): the binding stays PlayerEntity.Definition.PlayerStats; no
            // Stats accessor/field that could become a second storage location or second source.
            var members = typeof(Football.Players.PlayerEntity)
                .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.DeclaringType == typeof(Football.Players.PlayerEntity))
                .Select(m => m.Name)
                .ToArray();
            foreach (var forbidden in new[] { "Stats", "PlayerStats", "Ratings", "Rating", "StatSource" })
            {
                Assert.IsFalse(members.Contains(forbidden),
                    $"PlayerEntity must NOT declare a '{forbidden}' member (the binding must stay " +
                    "Definition.PlayerStats; no second storage/accessor was justified).");
            }
        }

        // ---- 140.3 No invented infrastructure ----

        [Test]
        public void NoStatsManager_Registry_Cache_Or_StatsComponent_Created()
        {
            var forbidden = new[]
            {
                "StatsManager", "RuntimeStatsManager", "PlayerStatsManager", "StatsRegistry",
                "StatsCache", "PlayerStatsComponent", "PlayerRatingsComponent", "StatsComponent",
                "StatsBinder", "PlayerStatsBinder"
            };
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    Assert.IsFalse(forbidden.Contains(t.Name),
                        $"No '{t.Name}' stats manager/registry/cache/component may be created.");
                }
            }
        }

        // ---- 140.4 Runtime boundary ----

        [Test]
        public void PlayerEntity_DoesNotOwnRuntimeStatState()
        {
            // Authored ratings stay on PlayerStats; the entity must not own runtime stat state.
            var declaredNames = DeclaredFields(typeof(Football.Players.PlayerEntity))
                .Select(f => f.Name)
                .ToHashSet();
            foreach (var forbidden in new[]
            {
                "CurrentStamina", "RuntimeStamina", "Fatigue", "Form", "MatchRating",
                "EffectiveRating", "ModifiedRating", "BuffedRating", "DebuffedRating",
                "CurrentStats", "RuntimeStats", "Condition", "Fitness"
            })
            {
                Assert.IsFalse(declaredNames.Contains(forbidden),
                    $"PlayerEntity must NOT own runtime stat state '{forbidden}'. Runtime condition/" +
                    "fatigue/form are DEFERRED future runtime-state concepts, and authored ratings " +
                    "remain on PlayerDefinition.PlayerStats.");
            }
        }

        // ---- 140.7 Player prefab integration ----

        [Test]
        public void PlayerPrefab_HasNoStatsComponent_AndKeeps_PlayerEntity()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Player.prefab must be loadable.");

            var entity = prefab.GetComponent<Football.Players.PlayerEntity>();
            Assert.IsNotNull(entity,
                "Player prefab root must keep the PlayerEntity component (Task 139).");
            Assert.AreEqual(1, prefab.GetComponents<Football.Players.PlayerEntity>().Length,
                "Player prefab root must have exactly ONE PlayerEntity (no duplicate identity references).");

            foreach (var mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var name = mb.GetType().Name;
                Assert.IsFalse(name.Contains("Stats") || name.Contains("Rating"),
                    $"'{name}' must NOT exist on the prefab — no stats component may be attached; " +
                    "stats binding stays authoritative on PlayerDefinition.PlayerStats.");
            }
        }
    }
}