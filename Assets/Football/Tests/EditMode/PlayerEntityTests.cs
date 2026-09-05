using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 139 — Player Identity: Runtime Player Entity.
    ///
    /// Establishes and verifies the authoritative way a runtime Player Entity identifies WHICH
    /// authored PlayerDefinition / player-data identity it represents:
    ///
    ///   - Football.Data.PlayerDefinition.Identity remains the SOLE authoritative authored identity
    ///     source (PlayerId : string, Name, Nationality, ClubReference).
    ///   - The runtime Player GameObject carries a minimal Football.Players.PlayerEntity
    ///     MonoBehaviour that holds a single PlayerDefinition reference — it ANSWERS "which player
    ///     data set is this entity?" and duplicates NO identity fields.
    ///   - Identity data is never copied; the runtime entity holds only a reference.
    ///   - Runtime mutable state (movement/state/possession/position/velocity/etc.) is strictly
    ///     separated from identity: PlayerEntity owns none of it.
    ///   - Assembly boundary: Football.Players depends on Football.Core + Football.Data only
    ///     (verified by ArchitectureDependencyTests); no circular dependency is introduced.
    ///   - Authored string PlayerDefinition.Identity.PlayerId and existing event int PlayerId are
    ///     distinct concepts; Task 139 does not reconcile them (documented DEFERRED).
    ///
    /// The PlayerEntity component lives in Football.Players, and PlayerDefinition lives in
    /// Football.Data (made a real assembly so Players can reference it). Both are referenceable
    /// directly from this test assembly.
    /// </summary>
    public class PlayerEntityTests
    {
        private const string PrefabPath = "Assets/Football/Prefabs/Players/Player.prefab";

        // ---- 139.2/139.8 — Authoritative identity source ----

        [Test]
        public void PlayerDefinition_RemainsTheAuthoritativeAuthoredIdentity()
        {
            // PlayerDefinition.Identity is a separate top-level class (Football.Data.Identity),
            // not a nested type — it is the sole authoritative authored identity group.
            var identityType = typeof(Football.Data.Identity);
            Assert.IsNotNull(identityType, "PlayerDefinition must contain an Identity group.");
            var fields = identityType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(
                new[] { "ClubReference", "Name", "Nationality", "PlayerId" }, fields,
                "Identity must be exactly {PlayerId, Name, Nationality, ClubReference}.");
            var pid = identityType.GetField("PlayerId");
            Assert.AreEqual(typeof(string), pid.FieldType,
                "Authored PlayerId must be a string (stable authored identity).");
        }

        // ---- 139.6 — Runtime PlayerEntity component exists and is minimal ----

        [Test]
        public void PlayerEntity_Exists_AsMonoBehaviour()
        {
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(typeof(Football.Players.PlayerEntity)),
                "PlayerEntity must be a MonoBehaviour attached to the runtime Player GameObject.");
        }

        [Test]
        public void PlayerEntity_HoldsOnly_PlayerDefinitionReference()
        {
            // The runtime entity must hold a single PlayerDefinition reference and duplicate no
            // identity/profile/physical/stats fields.
            var fields = typeof(Football.Players.PlayerEntity)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<System.ObsoleteAttribute>() == null)
                .ToArray();
            var declared = fields
                .Where(f => f.DeclaringType == typeof(Football.Players.PlayerEntity))
                .ToArray();
            Assert.AreEqual(1, declared.Length,
                $"PlayerEntity must declare exactly one field (the _definition reference). " +
                $"Found: {string.Join(", ", declared.Select(f => f.Name))}");
            Assert.AreEqual(typeof(Football.Data.PlayerDefinition), declared[0].FieldType,
                "PlayerEntity's sole field must be a PlayerDefinition reference.");
        }

        [Test]
        public void PlayerEntity_DoesNotDuplicateIdentityFields()
        {
            var declaredNames = typeof(Football.Players.PlayerEntity)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.DeclaringType == typeof(Football.Players.PlayerEntity))
                .Select(f => f.Name)
                .ToHashSet();
            foreach (var forbidden in new[] { "PlayerId", "Name", "Nationality", "ClubReference" })
            {
                Assert.IsFalse(declaredNames.Contains(forbidden),
                    $"PlayerEntity must NOT duplicate the identity field '{forbidden}'.");
            }
        }

        [Test]
        public void PlayerEntity_DoesNotOwnRuntimeStateOrGameplay()
        {
            // Use DeclaredOnly semantics to avoid flagging inherited UnityEngine members.
            var declaredNames = typeof(Football.Players.PlayerEntity)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.DeclaringType == typeof(Football.Players.PlayerEntity))
                .Select(f => f.Name)
                .ToHashSet();

            foreach (var forbidden in new[]
            {
                // Runtime state
                "Position", "Rotation", "Velocity", "Stamina", "CurrentState", "CurrentInput",
                "CurrentBallPossession", "CurrentVelocity", "CurrentPosition", "Possession",
                // Roles / ownership IDs that are NOT the authored identity
                "TeamId", "SlotId", "ControllerId", "InputOwner", "NetworkId", "RuntimePlayerId",
                "State", "Score", "Health"
            })
            {
                Assert.IsFalse(declaredNames.Contains(forbidden),
                    $"PlayerEntity must NOT own '{forbidden}' (runtime state or non-identity concept).");
            }
        }

        [Test]
        public void PlayerEntity_HasNoGameplayBehaviour()
        {
            // No lifecycle/behaviour methods — identity only, no gameplay.
            var methods = typeof(Football.Players.PlayerEntity)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.DeclaringType == typeof(Football.Players.PlayerEntity))
                .Select(m => m.Name)
                .ToHashSet();
            foreach (var forbidden in new[]
            {
                "Update", "FixedUpdate", "LateUpdate", "Awake", "Start", "OnEnable", "OnDisable",
                "OnCollisionEnter", "OnTriggerEnter", "Move", "Tick", "LocalUpdate"
            })
            {
                Assert.IsFalse(methods.Contains(forbidden),
                    $"PlayerEntity must NOT implement gameplay lifecycle/behaviour '{forbidden}'.");
            }
        }

        // ---- 139.6 — Reference assignment (authoring, not gameplay) ----

        [Test]
        public void PlayerEntity_CanBind_PlayerDefinitionReference()
        {
            var go = new GameObject("EntityTest");
            try
            {
                var entity = go.AddComponent<Football.Players.PlayerEntity>();
                var definition = Football.Data.PlayerDefinition.CreateInstance<Football.Data.PlayerDefinition>();
                Assert.IsNotNull(definition);

                Assert.IsNull(entity.Definition,
                    "A freshly created (unassigned) PlayerEntity must report a null definition.");
                entity.AssignDefinition(definition);
                Assert.AreSame(definition, entity.Definition,
                    "PlayerEntity.Definition must return the assigned PlayerDefinition reference.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayerEntity_AssignDefinition_IsNotDuplication()
        {
            // AssignDefinition stores a single reference; it does not copy any data into the entity.
            var fields = typeof(Football.Players.PlayerEntity)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.DeclaringType == typeof(Football.Players.PlayerEntity))
                .ToArray();
            Assert.IsTrue(fields.Length == 1 && fields[0].FieldType == typeof(Football.Data.PlayerDefinition),
                "AssignDefinition must bind a reference; no duplicate data fields may exist.");
        }

        // ---- 139.7 — Player prefab integration ----

        [Test]
        public void PlayerPrefab_HasPlayerEntityOnRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Player.prefab must be loadable.");
            var entity = prefab.GetComponent<Football.Players.PlayerEntity>();
            Assert.IsNotNull(entity,
                "Player prefab root must carry the PlayerEntity identity component (Task 139).");
        }

        [Test]
        public void PlayerPrefab_HasOnlyOnePlayerEntity_OnRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            var entities = prefab.GetComponents<Football.Players.PlayerEntity>();
            Assert.AreEqual(1, entities.Length,
                "Player prefab root must have exactly ONE PlayerEntity (no duplicate identity references).");
        }

        [Test]
        public void PlayerPrefab_HasNoPlayerEntity_OnDescendants()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            foreach (Transform child in prefab.transform)
            {
                Assert.IsNull(child.GetComponent<Football.Players.PlayerEntity>(),
                    $"Descendant '{child.name}' must NOT carry a PlayerEntity (identity belongs on the root only).");
            }
        }

        // ---- 139.8 — Assembly boundary ----

        [Test]
        public void PlayersAssembly_DependsOnCoreAndData_NoOtherGameplay()
        {
            // GetReferencedAssemblies() reflects actual IL usage: PlayerEntity uses PlayerDefinition,
            // so Football.Data appears; Football.Core is a declared asmdef dependency but may not be
            // referenced at IL level if no Core type is used. The authoritative invariant is that
            // Players references NO Football.* assembly outside {Core, Data}.
            var allowed = new[] { "Football.Core", "Football.Data" };
            var refs = typeof(Football.Players.PlayerEntity).Assembly.GetReferencedAssemblies()
                .Select(r => r.Name)
                .Where(n => n.StartsWith("Football."))
                .ToArray();
            foreach (var r in refs)
            {
                Assert.IsTrue(allowed.Contains(r),
                    $"Football.Players must not depend on Football assembly '{r}' (only Core/Data allowed).");
            }
            Assert.Contains("Football.Data", refs,
                "Football.Players must reference Football.Data (the PlayerDefinition it binds).");
        }

        [Test]
        public void NoCircularDependency_BetweenPlayersAndData()
        {
            // Football.Data must reference no Football.* assemblies (so there is no way back to
            // Football.Players and no cycle).
            var dataRefs = typeof(Football.Data.PlayerDefinition).Assembly.GetReferencedAssemblies()
                .Select(r => r.Name).Where(n => n.StartsWith("Football.")).ToArray();
            Assert.IsEmpty(dataRefs,
                "Football.Data must reference no Football assemblies (prevents any cycle). Found: " +
                string.Join(", ", dataRefs));

            // Players must reference Data (not vice-versa) — verify Players is not referenced by Data
            // and Players does reference Data.
            var playersName = typeof(Football.Players.PlayerEntity).Assembly.GetName().Name;
            var dataAssemblyName = typeof(Football.Data.PlayerDefinition).Assembly.GetName().Name;
            Assert.AreNotEqual(playersName, dataAssemblyName, "Players and Data must be distinct assemblies.");
            Assert.IsTrue(
                typeof(Football.Players.PlayerEntity).Assembly.GetReferencedAssemblies()
                    .Any(r => r.Name == dataAssemblyName),
                "Football.Players must reference Football.Data.");
        }

        // ---- 139.8 — No Task 140 behavior / no int-string reconciliation ----

        [Test]
        public void PlayerIdString_IsNotConverted_ToInt()
        {
            // The authored string PlayerId and event int PlayerId are distinct concepts; Task 139
            // neither converts nor bridges them. PlayerEntity exposes no int ID conversion.
            var methods = typeof(Football.Players.PlayerEntity)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.DeclaringType == typeof(Football.Players.PlayerEntity))
                .Where(m => m.Name.ToLowerInvariant().Contains("id") || m.Name.Contains("PlayerId"))
                .ToArray();
            Assert.IsEmpty(methods,
                "PlayerEntity must not implement any PlayerId conversion/bridge logic.");
        }

        [Test]
        public void NoPlayerRegistryOrManagerCreated()
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            var forbidden = new[] { "PlayerRegistry", "PlayerDatabase", "GlobalPlayerLookup",
                "IdentityManager", "PlayerIdentityManager", "PlayerCatalogRuntime" };
            foreach (var asm in assemblies)
            {
                System.Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    Assert.IsFalse(forbidden.Contains(t.Name),
                        $"No '{t.Name}' registry/manager may be created for Player Identity.");
                }
            }
        }
    }
}
