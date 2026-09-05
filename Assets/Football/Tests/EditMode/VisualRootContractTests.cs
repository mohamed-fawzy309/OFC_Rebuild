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
    /// Task 142 — Visual Root.
    ///
    /// Task 142 establishes the authoritative Visual Root of the Player Entity: a clean boundary
    /// between entity/gameplay logic and the Player's visual representation. The Visual Root must
    /// be presentation-only and must NOT own gameplay logic.
    ///
    /// This fixture locks the VISUAL ROOT ENTITY CONTRACT:
    ///   - Exactly ONE authoritative Visual boundary: Player/Visual, a direct child of the Player
    ///     root, transform-only (no visual runtime system was attached).
    ///   - Its established child structure (Model / Animator / AudioSource) is preserved as
    ///     TRANSFORM-ONLY PLACEHOLDERS (no renderer, no Animator component, no AudioSource
    ///     component, no mesh attached — the hierarchy is scaffold-only).
    ///   - Visual is separate from Collision (the CharacterController lives on Collision, never
    ///     under Visual) and from Gameplay.
    ///   - No parallel visual root was created, and no visual manager/controller runtime exists.
    ///   - PlayerEntity (identity on Player root) declares no visual ownership.
    ///
    /// Full prefab structure locks remain in PlayerPrefabStructureTests (Task 138); the absence of
    /// any animation runtime is locked by AnimationDebugCategoryTests (Task 57). This fixture adds
    /// ONLY the Visual Root boundary contract.
    ///
    /// Model implementation, materials/kits/skins, Animator behavior (Task 143), sockets behavior
    /// (Task 144) and collision-layer configuration (Task 145) are deliberately NOT implemented or
    /// tested here.
    /// </summary>
    public class VisualRootContractTests
    {
        private const string PrefabPath = "Assets/Football/Prefabs/Players/Player.prefab";

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Player.prefab must be loadable.");
            return prefab;
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

        // ---- 142.3 — one authoritative Visual Root, direct child of Player ----

        [Test]
        [Category("VisualRoot")]
        public void PlayerPrefab_ContainsExactlyOneVisualObject()
        {
            var prefab = LoadPrefab();
            var visuals = new List<Transform>();
            if (prefab.name == "Visual") visuals.Add(prefab.transform);
            visuals.AddRange(GetAllTransforms(prefab.transform).Where(t => t.name == "Visual"));

            Assert.AreEqual(1, visuals.Count,
                $"Player prefab must contain exactly ONE object named 'Visual' (the authoritative " +
                $"Visual Root). Found {visuals.Count}.");
        }

        [Test]
        [Category("VisualRoot")]
        public void Visual_IsDirectChildOfPlayerRoot()
        {
            var prefab = LoadPrefab();
            var visuals = prefab.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Visual").ToList();
            Assert.AreEqual(1, visuals.Count);

            Assert.AreEqual(prefab.transform, visuals[0].parent,
                "The Visual Root must be a DIRECT child of the Player root (entity root).");
        }

        [Test]
        [Category("VisualRoot")]
        public void PlayerPrefab_HasNoParallelVisualRoot()
        {
            // Guard against a second/p parallel visual boundary being introduced.
            var forbiddenNames = new[] { "VisualRoot", "PlayerVisual", "Visuals", "View", "ModelRoot", "RenderRoot" };
            var prefab = LoadPrefab();
            var all = GetAllTransforms(prefab.transform);
            foreach (var t in all)
            {
                Assert.IsFalse(forbiddenNames.Contains(t.name),
                    $"No parallel Visual Root ('{t.name}') may be introduced: the authoritative Visual " +
                    "Root is the single 'Visual' child (Task 142).");
            }
        }

        // ---- 142.2 / 142.6 — Visual is a transform-only scaffold boundary ----

        [Test]
        [Category("VisualRoot")]
        public void Visual_IsTransformOnlyBoundary()
        {
            // The Visual Root itself must be a plain Transform boundary: exactly one component,
            // the Transform — no visual/gameplay MonoBehaviour attached by this task.
            var prefab = LoadPrefab();
            var visual = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Visual");
            var components = visual.GetComponents<Component>();
            Assert.AreEqual(1, components.Length,
                "Visual Root must be a Transform-only boundary (exactly one component). " +
                $"Found: {string.Join(", ", components.Select(c => c.GetType().Name))}");
            Assert.IsInstanceOf<Transform>(components[0]);
        }

        [Test]
        [Category("VisualRoot")]
        public void Visual_HasExactlyTheEstablishedChildStructure()
        {
            var prefab = LoadPrefab();
            var visual = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Visual");

            Assert.AreEqual(3, visual.childCount,
                "Visual must contain exactly the established child structure (Model/Animator/AudioSource).");

            var names = new List<string>();
            for (int i = 0; i < visual.childCount; i++) names.Add(visual.GetChild(i).name);
            Assert.AreEqual(new[] { "Model", "Animator", "AudioSource" }, names.ToArray(),
                "Visual children must be, in order: Model, Animator, AudioSource (Task 138 scaffold).");
        }

        [Test]
        [Category("VisualRoot")]
        public void Visual_AndItsChildren_AreTransformOnlyPlaceholders()
        {
            // The scaffold objects must remain pure Transforms. Even a debug-only component or a
            // placeholder mesh would violate the "scaffold-only" boundary.
            var prefab = LoadPrefab();
            var visual = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Visual");
            var objects = new List<Transform> { visual };
            for (int i = 0; i < visual.childCount; i++) objects.Add(visual.GetChild(i));

            foreach (var obj in objects)
            {
                var components = obj.GetComponents<Component>();
                Assert.AreEqual(1, components.Length,
                    $"'{obj.name}' must be a transform-only placeholder (exactly one component). " +
                    $"Found: {string.Join(", ", components.Select(c => c.GetType().Name))}");
                Assert.IsInstanceOf<Transform>(components[0]);
            }
        }

        [Test]
        [Category("VisualRoot")]
        public void Visual_SubtreeContainsNoPresentationOrPhysicsComponents()
        {
            // No rendered model exists: no MeshRenderer/SkinnedMeshRenderer/Renderer, no Animator
            // component, no AudioSource component. No physics on the visual side either.
            var prefab = LoadPrefab();
            var visual = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Visual");

            var visualSelfPlusDesc = new List<Transform> { visual };
            visualSelfPlusDesc.AddRange(GetAllTransforms(visual));

            foreach (var obj in visualSelfPlusDesc)
            {
                Assert.IsNull(obj.GetComponent<MeshRenderer>(),
                    $"'Visual' subtree object '{obj.name}' must not have a MeshRenderer (no model implemented).");
                Assert.IsNull(obj.GetComponent<SkinnedMeshRenderer>(),
                    $"'Visual' subtree object '{obj.name}' must not have a SkinnedMeshRenderer (no model implemented).");
                Assert.IsNull(obj.GetComponent<Animator>(),
                    $"'Visual' subtree object '{obj.name}' must not have an Animator component (Task 143 owns animation).");
                Assert.IsNull(obj.GetComponent<AudioSource>(),
                    $"'Visual' subtree object '{obj.name}' must not have an AudioSource component.");
                Assert.IsNull(obj.GetComponent<Collider>(),
                    $"'Visual' subtree object '{obj.name}' must not own any Collider (physics belongs to Collision).");
            }
        }

        // ---- 142.4 / 142.7 — Visual separation from Collision / Gameplay ----

        [Test]
        [Category("VisualRoot")]
        public void CharacterController_IsNotOwnedBy_Visual()
        {
            var prefab = LoadPrefab();
            var visual = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Visual");
            var cc = prefab.GetComponentsInChildren<CharacterController>(true);
            Assert.AreEqual(1, cc.Length);

            var visualTree = new HashSet<Transform>(GetAllTransforms(visual)) { visual };
            Assert.IsFalse(visualTree.Contains(cc[0].transform),
                "The CharacterController must NOT be owned by the Visual Root; it stays on the " +
                "Collision child (physical representation).");
        }

        [Test]
        [Category("VisualRoot")]
        public void Visual_IsSeparateFrom_CollisionAndGameplay()
        {
            var prefab = LoadPrefab();
            var visual = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Visual");
            var collision = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Collision");
            var gameplay = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Gameplay");

            var visualTree = new HashSet<Transform>(GetAllTransforms(visual)) { visual };

            Assert.IsFalse(visualTree.Contains(collision),
                "Collision must not live under Visual (Visual is presentation-only).");
            Assert.IsFalse(visualTree.Contains(gameplay),
                "Gameplay must not live under Visual (Visual is presentation-only).");
            Assert.AreEqual(prefab.transform, collision.parent,
                "Collision must remain a direct child of the Player root.");
            Assert.AreEqual(prefab.transform, gameplay.parent,
                "Gameplay must remain a direct child of the Player root.");
        }

        // ---- 142.5 — no visual runtime system / no visual ownership on the entity ----

        [Test]
        [Category("VisualRoot")]
        public void NoVisualRuntimeManagement_Created()
        {
            var forbidden = new[]
            {
                "VisualController", "PlayerVisual", "VisualRoot", "VisualManager",
                "PlayerVisualManager", "SkinManager", "KitManager", "ModelController",
                "AnimationController", "RendererController", "VisualBridge"
            };
            var assemblyNames = new[]
            {
                typeof(Football.Players.PlayerEntity).Assembly,
                typeof(Football.Core.FootballDebugSettings).Assembly,
                typeof(Football.Core.IPlayerInput).Assembly
            };
            foreach (var asm in assemblyNames)
            {
                foreach (var t in SafeGetTypes(asm))
                {
                    Assert.IsFalse(forbidden.Contains(t.Name),
                        $"No visual runtime system ('{t.Name}') may be introduced (Task 142 keeps the " +
                        "Visual Root as a plain Transform boundary; no public visual API has a consumer).");
                }
            }
        }

        [Test]
        [Category("VisualRoot")]
        public void PlayerEntity_DeclaresNoVisualOwnership()
        {
            // The entity root must not own the visual representation. PlayerEntity may only hold
            // its single authored PlayerDefinition reference (identity binding). The universal Unity
            // runtime plumbing accessors Component.transform / Component.gameObject are inherent on
            // every MonoBehaviour (not visual ownership) and are excluded by name; everything else
            // that is or looks like visual ownership is forbidden.
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                      | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var t = typeof(Football.Players.PlayerEntity);

            var unityPlumbing = new[] { "transform", "gameObject" };

            var visualField = t.GetFields(flags)
                .FirstOrDefault(f => f.FieldType == typeof(Transform)
                                  || f.FieldType == typeof(GameObject)
                                  || f.Name.IndexOf("Visual", System.StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsNull(visualField,
                $"PlayerEntity must not declare a visual/reference-holding field '{visualField?.Name}' " +
                "(entity does not own the Visual Root).");

            var visualProp = t.GetProperties(flags)
                .Where(p => !unityPlumbing.Contains(p.Name))
                .FirstOrDefault(p => p.PropertyType == typeof(Transform)
                                  || p.PropertyType == typeof(GameObject)
                                  || p.Name.IndexOf("Visual", System.StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsNull(visualProp,
                $"PlayerEntity must not expose visual access '{visualProp?.Name}' (no public visual API " +
                "when no consumer exists).");

            var visualReturn = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !unityPlumbing.Contains(m.Name))
                .FirstOrDefault(m => m.ReturnType == typeof(Transform) || m.ReturnType == typeof(GameObject));
            Assert.IsNull(visualReturn,
                $"PlayerEntity must not expose a Transform/GameObject accessor '{visualReturn?.Name}'.");
        }
    }
}