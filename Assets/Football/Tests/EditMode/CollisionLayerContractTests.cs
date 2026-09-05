using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 145 — Collision Layers.
    ///
    /// Task 145 establishes the authoritative Collision Layer configuration for the Player Entity
    /// and verifies that physics-layer ownership is correctly separated from gameplay collision
    /// behavior. This fixture locks the LAYER CONTRACT:
    ///   - Every Player prefab GameObject uses the Unity built-in "Default" layer (index 0) — the
    ///     layer the authoritative prefab actually assigns (FACT). No custom player/socket layer
    ///     has been invented.
    ///   - ProjectSettings/TagManager.asset defines only the Unity built-in layers; DynamicsManager
    ///     retains the Unity default all-collide matrix (no custom collision-matrix rule).
    ///   - The Collision child is the physical representation; Visual / Sockets / Gameplay own zero
    ///     physics components.
    ///   - No Rigidbody was introduced; the single Collider-derived component is the
    ///     CharacterController on Collision; no triggers exist.
    ///   - PlayerEntity remains on the root; CharacterController remains on Collision.
    ///   - No runtime LayerManager / CollisionLayerManager / layer-machinery type was created.
    ///
    /// Deeper CharacterController geometry is already locked by CharacterControllerContractTests
    /// (Task 141); this fixture adds the Task 145 layer boundary contract only.
    ///
    /// No player-player / player-ball / tackle / knockback / trigger / ground-layer behaviour is
    /// implemented or asserted (later Tasks; no project evidence for such layers exists).
    /// </summary>
    public class CollisionLayerContractTests
    {
        private const string PrefabPath = "Assets/Football/Prefabs/Players/Player.prefab";

        private static readonly string[] UnityBuiltInLayerNames =
        {
            "Default", "TransparentFX", "Ignore Raycast", "Water", "UI"
        };

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Player.prefab must be loadable.");
            return prefab;
        }

        private static int DefaultLayer()
        {
            var layer = LayerMask.NameToLayer("Default");
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "'Default' must be a defined layer.");
            return layer;
        }

        private static List<Transform> GetAllTransforms(Transform root)
        {
            var result = new List<Transform>();
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (int i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    result.Add(child);
                    queue.Enqueue(child);
                }
            }
            return result;
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null).ToArray(); }
        }

        // ---- 145.1 / 145.2 — the established layer assignment (FACT lock) ----

        [Test]
        [Category("CollisionLayer")]
        public void PlayerPrefab_Exists_AndLoads()
        {
            var prefab = LoadPrefab();
            Assert.AreEqual("Player", prefab.name,
                "Player prefab root must be named 'Player' (established project convention).");
        }

        [Test]
        [Category("CollisionLayer")]
        public void Player_Root_IsOn_DefaultLayer_Only()
        {
            // FACT: every GameObject in the authoritative Player.prefab is assigned the Unity
            // built-in 'Default' layer (Index 0, YAML m_Layer: 0). No custom player layer exists.
            var prefab = LoadPrefab();
            int expected = DefaultLayer();
            Assert.AreEqual(expected, prefab.gameObject.layer,
                "Player root must stay on the Unity built-in 'Default' layer (no invented player layer).");
        }

        [Test]
        [Category("CollisionLayer")]
        public void Every_PlayerGameObject_UsesOnlyTheDefaultLayer_NoSpeculativeLayer()
        {
            // Covers the whole prefab: root, Visual subtree, Collision, Gameplay subtree and Sockets
            // subtree are all on one layer ('Default'). A stray object on a custom layer would signal
            // a speculative layer assignment without project evidence.
            var prefab = LoadPrefab();
            int expected = DefaultLayer();
            var all = new List<Transform> { prefab.transform };
            all.AddRange(GetAllTransforms(prefab.transform));

            foreach (var t in all)
            {
                Assert.AreEqual(expected, t.gameObject.layer,
                    $"'{t.name}' must be on the 'Default' layer; no speculative layer assignment was " +
                    "introduced without project evidence.");
            }
        }

        [Test]
        [Category("CollisionLayer")]
        public void Collision_Child_UsesThe_Same_DefaultLayer()
        {
            // Player root and Collision child deliberately share the established 'Default' layer
            // (PrefabConventions: Collision = physics shell). No split required at this stage.
            var prefab = LoadPrefab();
            var collision = prefab.transform.Find("Collision");
            Assert.IsNotNull(collision, "Collision child must exist.");
            Assert.AreEqual(prefab.gameObject.layer, collision.gameObject.layer,
                "Collision child must share the Player/root layer (no arbitrary re-layering).");
        }

        // ---- 145.3 / 145.4 — the physical representation boundary ----

        [Test]
        [Category("CollisionLayer")]
        public void Collision_Child_RemainsThePhysicalRepresentation()
        {
            // The sole Collider-derived component is the CharacterController on Collision. Layer
            // ownership does not move the physical body; the CC stays where Tasks 138/141 placed it.
            var prefab = LoadPrefab();
            var collision = prefab.transform.Find("Collision");
            Assert.IsNotNull(collision, "Collision child must exist.");

            var cc = collision.GetComponent<CharacterController>();
            Assert.IsNotNull(cc, "Collision child must own the CharacterController (physical body).");

            var all = prefab.GetComponentsInChildren<CharacterController>(true);
            Assert.AreEqual(1, all.Length,
                "The prefab must keep exactly ONE CharacterController (on Collision).");
        }

        [Test]
        [Category("CollisionLayer")]
        public void Visual_OwnsNoPhysicsComponents()
        {
            var prefab = LoadPrefab();
            var visual = prefab.transform.Find("Visual");
            Assert.IsNotNull(visual, "Visual child must exist.");
            var subtree = GetAllTransforms(visual);

            foreach (var t in subtree)
            {
                Assert.IsNull(t.GetComponent<Collider>(),
                    $"Visual object '{t.name}' must own no Collider (Visual = presentation only).");
                Assert.IsNull(t.GetComponent<Rigidbody>(),
                    $"Visual object '{t.name}' must own no Rigidbody.");
            }
        }

        [Test]
        [Category("CollisionLayer")]
        public void Sockets_OwnNoPhysicsComponents()
        {
            var prefab = LoadPrefab();
            var sockets = prefab.transform.Find("Sockets");
            Assert.IsNotNull(sockets, "Sockets child must exist.");
            var subtree = GetAllTransforms(sockets);

            foreach (var t in subtree)
            {
                Assert.IsNull(t.GetComponent<Collider>(),
                    $"Sockets object '{t.name}' must own no Collider (socket = attachment reference).");
                Assert.IsNull(t.GetComponent<Rigidbody>(),
                    $"Sockets object '{t.name}' must own no Rigidbody.");
            }
        }

        [Test]
        [Category("CollisionLayer")]
        public void Gameplay_OwnsNoPhysicsComponents()
        {
            var prefab = LoadPrefab();
            var gameplay = prefab.transform.Find("Gameplay");
            Assert.IsNotNull(gameplay, "Gameplay child must exist.");
            var subtree = GetAllTransforms(gameplay);

            foreach (var t in subtree)
            {
                Assert.IsNull(t.GetComponent<Collider>(),
                    $"Gameplay object '{t.name}' must own no Collider.");
                Assert.IsNull(t.GetComponent<Rigidbody>(),
                    $"Gameplay object '{t.name}' must own no Rigidbody.");
            }
        }

        // ---- 145.5 / 145.6 — no physics containment, no gameplay inside layers ----

        [Test]
        [Category("CollisionLayer")]
        public void PlayerPrefab_HasNoRigidbody()
        {
            var prefab = LoadPrefab();
            var bodies = prefab.GetComponentsInChildren<Rigidbody>(true);
            Assert.AreEqual(0, bodies.Length,
                "No Rigidbody may exist in the Player prefab (Task 145 keeps scaffold physics-less).");
        }

        [Test]
        [Category("CollisionLayer")]
        public void PlayerPrefab_HasExactlyOneCollider_TheCharacterController_NoTriggers()
        {
            // The only Collider-derived component in the prefab is the CharacterController on
            // Collision: no duplicate/incorrect collider and no trigger component were introduced.
            var prefab = LoadPrefab();
            var colliders = prefab.GetComponentsInChildren<Collider>(true);

            Assert.AreEqual(1, colliders.Length,
                $"The prefab must contain exactly ONE Collider-derived component (the " +
                $"CharacterController). Found {colliders.Length}.");

            var cc = colliders[0] as CharacterController;
            Assert.IsNotNull(cc, "The single collider must be the CharacterController.");
            Assert.AreEqual("Collision", cc.transform.name,
                "The CharacterController must remain on the Collision child.");
            Assert.IsFalse(cc.isTrigger,
                "The CharacterController physical body must not be configured as a trigger.");
        }

        [Test]
        [Category("CollisionLayer")]
        public void PlayerEntity_RemainsOnRoot_And_CharacterControllerOnCollision()
        {
            // Layer configuration must not disturb entity/physics ownership boundaries.
            var prefab = LoadPrefab();
            Assert.IsNotNull(prefab.GetComponent<Football.Players.PlayerEntity>(),
                "PlayerEntity must remain on the Player root (Task 139).");
            Assert.IsNotNull(prefab.transform.Find("Collision")?.GetComponent<CharacterController>(),
                "CharacterController must remain on the Collision child (Tasks 138/141).");
        }

        [Test]
        [Category("CollisionLayer")]
        public void NoCustomLayer_Defined_OrLayerManager_Created()
        {
            // ProjectSettings/TagManager.asset must contain ONLY the Unity built-in layers. Adding a
            // custom player/ball/environment layer without project evidence is forbidden (rule 24).
            var names = DefinedLayerNames();
            Assert.IsTrue(
                UnityBuiltInLayerNames.All(builtIn => names.Contains(builtIn)) &&
                names.All(name => UnityBuiltInLayerNames.Contains(name)),
                $"TagManager must define only the Unity built-in layers. Found: " +
                string.Join(", ", names));

            // No runtime layer-management/collision-matrix machinery may exist.
            var forbidden = new[]
            {
                "LayerManager", "CollisionLayerManager", "CollisionLayerConfig",
                "LayerConstants", "LayerDefinitions", "CollisionMatrixConfig",
                "PhysicsLayerManager"
            };
            var assemblies = new[]
            {
                typeof(Football.Players.PlayerEntity).Assembly,
                typeof(Football.Core.FootballDebugSettings).Assembly,
                typeof(Football.Core.IPlayerInput).Assembly
            };
            foreach (var asm in assemblies)
            {
                foreach (var t in SafeGetTypes(asm))
                {
                    Assert.IsFalse(forbidden.Contains(t.Name),
                        $"No collision-layer runtime system ('{t.Name}') may be introduced (the " +
                        "ProjectSettings layer configuration is authoritative and sufficient).");
                }
            }
        }

        private static List<string> DefinedLayerNames()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            Assert.IsNotNull(assets, "TagManager.asset must be loadable.");
            Assert.IsTrue(assets.Length > 0, "TagManager.asset must contain a TagManager asset.");

            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            Assert.IsNotNull(layers, "TagManager must expose the serialized 'layers' property.");

            var names = new List<string>();
            for (int i = 0; i < layers.arraySize; i++)
            {
                var name = layers.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
            return names;
        }
    }
}