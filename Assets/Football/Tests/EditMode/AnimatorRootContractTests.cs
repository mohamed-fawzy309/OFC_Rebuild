using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 143 — Animator Root.
    ///
    /// Task 143 establishes the authoritative Animator Root boundary INSIDE the Player's Visual
    /// hierarchy: a presentation/animation boundary that must NOT become gameplay logic. The task
    /// is foundation/boundary only — NO animation system, controller, clips, parameters,
    /// transitions, blend trees, Root Motion or gameplay-to-animation communication is implemented.
    ///
    /// CRITICAL DISTINCTION: a GameObject named "Animator" is NOT a UnityEngine.Animator component.
    /// This fixture locks the boundary contract:
    ///   - Exactly ONE object named "Animator": Player/Visual/Animator, a DIRECT child of Visual.
    ///   - It is TRANSFORM-ONLY (exactly one component: Transform) — there is NO Unity Animator
    ///     component anywhere in the Player prefab.
    ///   - It is presentation-only and separate from Collision (CharacterController stays on the
    ///     Collision child) and from Gameplay.
    ///   - No Animator Controller / clips / animation assets exist in the project.
    ///   - No animation runtime manager/controller type was created.
    ///
    /// Full prefab structure stays locked by PlayerPrefabStructureTests (Task 138); no-animation
    /// runtime by AnimationDebugCategoryTests (Task 57); the Visual Root boundary by
    /// VisualRootContractTests (Task 142). This fixture adds ONLY the Task 143 Animator Root
    /// boundary contract.
    /// </summary>
    public class AnimatorRootContractTests
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

        // ---- 143.2 / 143.3 — one authoritative Animator Root, direct child of Visual ----

        [Test]
        [Category("AnimatorRoot")]
        public void PlayerPrefab_ContainsExactlyOne_AnimatorNamedObject()
        {
            var prefab = LoadPrefab();
            var matches = GetAllTransforms(prefab.transform).Where(t => t.name == "Animator").ToList();
            Assert.AreEqual(1, matches.Count,
                $"Player prefab must contain exactly ONE object named 'Animator' (the authoritative " +
                $"Animator Root boundary). Found {matches.Count}.");
        }

        [Test]
        [Category("AnimatorRoot")]
        public void AnimatorRoot_IsDirectChildOfVisual()
        {
            var prefab = LoadPrefab();
            var visual = GetAllTransforms(prefab.transform).Single(t => t.name == "Visual");
            var animator = GetAllTransforms(prefab.transform).Single(t => t.name == "Animator");

            Assert.AreEqual(prefab.transform, visual.parent, "Visual must remain a direct child of Player.");
            Assert.AreEqual(visual, animator.parent,
                "The Animator Root must be a DIRECT child of Visual (presentation hierarchy).");
        }

        // ---- 143.2 — GameObject "Animator" is Transform-only, NOT a Unity Animator component ----

        [Test]
        [Category("AnimatorRoot")]
        public void AnimatorRoot_IsTransformOnly_NotAUnityAnimatorComponent()
        {
            // CRITICAL DISTINCTION: an object named "Animator" is not a UnityEngine.Animator
            // component. At this stage the Animator Root is a Transform-only placeholder.
            var prefab = LoadPrefab();
            var animator = GetAllTransforms(prefab.transform).Single(t => t.name == "Animator");

            var components = animator.GetComponents<Component>();
            Assert.AreEqual(1, components.Length,
                "Animator Root must be a Transform-only boundary (exactly one component). " +
                $"Found: {string.Join(", ", components.Select(c => c.GetType().Name))}");
            Assert.IsInstanceOf<Transform>(components[0]);

            Assert.IsNull(animator.GetComponent<UnityEngine.Animator>(),
                "The 'Animator' object must NOT have a UnityEngine.Animator component (no animation " +
                "system implemented; Task 143 is the boundary only).");
        }

        [Test]
        [Category("AnimatorRoot")]
        public void NoUnityAnimatorComponent_Exists_AnywhereInPlayerPrefab()
        {
            // Player root, Collision and Gameplay must likewise remain free of a Unity Animator
            // component; the whole prefab is animator-component-free.
            var prefab = LoadPrefab();
            var all = new List<Transform> { prefab.transform };
            all.AddRange(GetAllTransforms(prefab.transform));

            foreach (var t in all)
            {
                Assert.IsNull(t.GetComponent<UnityEngine.Animator>(),
                    $"'{t.name}' must not have a UnityEngine.Animator component (no animation system " +
                    "exists; verify: the 'Animator' object is a Transform placeholder).");
            }
        }

        // ---- 143.4 — presentation-only boundary, separate from Collision / Gameplay ----

        [Test]
        [Category("AnimatorRoot")]
        public void AnimatorRoot_IsPresentationOnly_NoGameplayOrPhysics()
        {
            var prefab = LoadPrefab();
            var animator = GetAllTransforms(prefab.transform).Single(t => t.name == "Animator");
            var subtree = GetAllTransforms(animator);
            subtree.Add(animator);

            foreach (var t in subtree)
            {
                Assert.IsNull(t.GetComponent<MonoBehaviour>(),
                    $"'{t.name}' under the Animator Root must not carry gameplay MonoBehaviours.");
                Assert.IsNull(t.GetComponent<Collider>(),
                    $"'{t.name}' under the Animator Root must not own any Collider (physics belongs to Collision).");
                Assert.IsNull(t.GetComponent<Rigidbody>(),
                    $"'{t.name}' under the Animator Root must not own a Rigidbody.");
            }
        }

        [Test]
        [Category("AnimatorRoot")]
        public void AnimatorRoot_IsSeparateFrom_CollisionAndGameplay()
        {
            var prefab = LoadPrefab();
            var animator = GetAllTransforms(prefab.transform).Single(t => t.name == "Animator");
            var collision = GetAllTransforms(prefab.transform).Single(t => t.name == "Collision");
            var gameplay = GetAllTransforms(prefab.transform).Single(t => t.name == "Gameplay");

            var animatorTree = new HashSet<Transform>(GetAllTransforms(animator)) { animator };

            // The CharacterController stays on Collision — never inside the Animator Root.
            var cc = prefab.GetComponentsInChildren<CharacterController>(true);
            Assert.AreEqual(1, cc.Length);
            Assert.IsFalse(animatorTree.Contains(cc[0].transform),
                "The CharacterController must not live under the Animator Root (stays on Collision).");

            Assert.IsFalse(animatorTree.Contains(collision),
                "Collision must not live under the Animator Root (presentation must not own physics).");
            Assert.IsFalse(animatorTree.Contains(gameplay),
                "Gameplay must not live under the Animator Root.");
            Assert.AreEqual(prefab.transform, gameplay.parent,
                "Gameplay must remain a direct child of the Player root.");
            Assert.AreEqual(prefab.transform, collision.parent,
                "Collision must remain a direct child of the Player root.");
        }

        // ---- 143.6 — no premature animation content / runtime introduced ----

        [Test]
        [Category("AnimatorRoot")]
        public void NoAnimatorController_Clips_OrAnimationAssets_Exist()
        {
            // No Animator Controller (.controller), no animation clips (.anim/.clip), no imported
            // humanoid/mesh animation sources (.fbx) anywhere under Assets.
            string[] animationExtensions = { "*.controller", "*.anim", "*.clip", "*.fbx" };
            var found = new List<string>();
            foreach (var ext in animationExtensions)
            {
                found.AddRange(Directory.GetFiles("Assets", ext, SearchOption.AllDirectories)
                    .Where(p => !p.Contains("/Library/")));
            }
            Assert.IsEmpty(found,
                "No animation assets may exist yet (Task 143 is the boundary; no Animator " +
                "Controller/clips/imported animations). Found: " + string.Join(", ", found));
        }

        [Test]
        [Category("AnimatorRoot")]
        public void NoAnimationRuntimeManager_Created()
        {
            // No animation runtime manager/controller/wrapper may be introduced (no consumer, and
            // Task 143 is boundary-only).
            var forbidden = new[]
            {
                "PlayerAnimator", "AnimatorControllerWrapper", "AnimationManager",
                "VisualAnimationManager", "PlayerAnimationController", "AnimationRootController"
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
                        $"No animation runtime system ('{t.Name}') may be introduced (Task 143 keeps " +
                        "the Animator Root as a Transform-only boundary; no consumer exists).");
                }
            }
        }

        [Test]
        [Category("AnimatorRoot")]
        public void PlayerEntity_DeclaresNoAnimatorMember()
        {
            // The entity root must not bind to the Animator Root (no gameplay-to-animation
            // communication yet). Scan only members declared by PlayerEntity, excluding the
            // universal Unity Component plumbing ("transform"/"gameObject").
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                      | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var unityPlumbing = new[] { "transform", "gameObject" };
            var t = typeof(Football.Players.PlayerEntity);

            var animatorField = t.GetFields(flags)
                .FirstOrDefault(f => f.FieldType == typeof(UnityEngine.Animator)
                                  || f.Name.IndexOf("Animator", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsNull(animatorField,
                $"PlayerEntity must not declare an Animator field '{animatorField?.Name}'.");

            var animatorProp = t.GetProperties(flags)
                .Where(p => !unityPlumbing.Contains(p.Name))
                .FirstOrDefault(p => p.PropertyType == typeof(UnityEngine.Animator)
                                  || p.Name.IndexOf("Animator", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsNull(animatorProp,
                $"PlayerEntity must not expose Animator access '{animatorProp?.Name}' (no gameplay-to-animation communication).");

            var animatorReturn = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                              BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !unityPlumbing.Contains(m.Name))
                .FirstOrDefault(m => m.ReturnType == typeof(UnityEngine.Animator)
                                  || m.Name.IndexOf("Animator", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsNull(animatorReturn,
                $"PlayerEntity must not expose an Animator accessor '{animatorReturn?.Name}'.");
        }
    }
}