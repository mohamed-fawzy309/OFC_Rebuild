using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 138 — Player Prefab structure / contract validation.
    ///
    /// Verifies the authoritative Player prefab at the established project location:
    ///   - Exists and is loadable as a valid Unity prefab.
    ///   - Root GameObject is named "Player".
    ///   - Contains the established sub-object hierarchy (Visual, Collision, Gameplay, Sockets).
    ///   - CharacterController exists on the Collision child with documented parameters.
    ///   - No gameplay MonoBehaviours have been prematurely attached by this task.
    ///   - Existing architectural conventions are preserved.
    ///
    /// Validation uses AssetDatabase + PrefabUtility, matching the project's established
    /// test conventions (ArchitectureSmokeTests, ArchitectureDependencyTests).
    /// </summary>
    public class PlayerPrefabStructureTests
    {
        private const string PrefabPath = "Assets/Football/Prefabs/Players/Player.prefab";

        private static GameObject LoadPrefab()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == childName)
                    return parent.GetChild(i);
            }
            return null;
        }

        private static List<Transform> GetAllDescendants(Transform root)
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

        // ---- 138.6a — Prefab existence / loadability ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_ExistsAtPath()
        {
            Assert.IsTrue(File.Exists($"Assets/{PrefabPath.Substring("Assets/".Length)}"),
                $"Player prefab must exist at {PrefabPath}.");
        }

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_IsLoadableAsGameObject()
        {
            var prefab = LoadPrefab();
            Assert.IsNotNull(prefab, "Player.prefab must be loadable as a valid Unity prefab.");
        }

        // ---- 138.6b — Root naming convention ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_RootIsNamedPlayer()
        {
            var prefab = LoadPrefab();
            Assert.IsNotNull(prefab);
            Assert.AreEqual("Player", prefab.name,
                "Player prefab root must be named 'Player' (established project convention).");
        }

        // ---- 138.6c — Established hierarchy sub-objects ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_ContainsVisualChild()
        {
            var prefab = LoadPrefab();
            Assert.IsNotNull(FindChild(prefab.transform, "Visual"),
                "Player prefab must contain a 'Visual' child for future model/animator/audio placement.");
        }

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_ContainsCollisionChild()
        {
            var prefab = LoadPrefab();
            Assert.IsNotNull(FindChild(prefab.transform, "Collision"),
                "Player prefab must contain a 'Collision' child for CharacterController placement.");
        }

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_ContainsGameplayChild()
        {
            var prefab = LoadPrefab();
            Assert.IsNotNull(FindChild(prefab.transform, "Gameplay"),
                "Player prefab must contain a 'Gameplay' child for future gameplay extension points.");
        }

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_ContainsSocketsChild()
        {
            var prefab = LoadPrefab();
            Assert.IsNotNull(FindChild(prefab.transform, "Sockets"),
                "Player prefab must contain a 'Sockets' child for attachment points.");
        }

        // ---- 138.6d — Visual sub-structure ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_VisualContainsModelAnimatorAudioSource()
        {
            var prefab = LoadPrefab();
            var visual = FindChild(prefab.transform, "Visual");
            Assert.IsNotNull(visual, "Visual child must exist.");

            Assert.IsNotNull(FindChild(visual, "Model"),
                "Visual must contain a 'Model' child for future mesh/skinned mesh.");
            Assert.IsNotNull(FindChild(visual, "Animator"),
                "Visual must contain an 'Animator' child for future Animator component.");
            Assert.IsNotNull(FindChild(visual, "AudioSource"),
                "Visual must contain an 'AudioSource' child for future audio source.");
        }

        // ---- 138.6e — Gameplay sub-structure ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_GameplayContainsExpectedChildren()
        {
            var prefab = LoadPrefab();
            var gameplay = FindChild(prefab.transform, "Gameplay");
            Assert.IsNotNull(gameplay, "Gameplay child must exist.");

            string[] expectedChildren = {
                "Input", "Movement", "Rotation", "StateMachine",
                "Actions", "BallInteraction", "Animation"
            };
            foreach (var childName in expectedChildren)
            {
                Assert.IsNotNull(FindChild(gameplay, childName),
                    $"Gameplay must contain '{childName}' child (extension point for future Task).");
            }
        }

        // ---- 138.6f — Sockets sub-structure ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_SocketsContainsExpectedAttachmentPoints()
        {
            var prefab = LoadPrefab();
            var sockets = FindChild(prefab.transform, "Sockets");
            Assert.IsNotNull(sockets, "Sockets child must exist.");

            string[] expectedSockets = { "LeftFoot", "RightFoot", "Head", "BallControlPoint" };
            foreach (var socketName in expectedSockets)
            {
                Assert.IsNotNull(FindChild(sockets, socketName),
                    $"Sockets must contain '{socketName}' attachment point.");
            }
        }

        // ---- 138.6g — CharacterController existence and parameters ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_CollisionHasCharacterController()
        {
            var prefab = LoadPrefab();
            var collision = FindChild(prefab.transform, "Collision");
            Assert.IsNotNull(collision, "Collision child must exist.");

            var cc = collision.GetComponent<CharacterController>();
            Assert.IsNotNull(cc, "Collision child must have a CharacterController component (established architecture).");
        }

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_CharacterControllerParametersMatchEstablishedValues()
        {
            var prefab = LoadPrefab();
            var collision = FindChild(prefab.transform, "Collision");
            var cc = collision.GetComponent<CharacterController>();
            Assert.IsNotNull(cc);

            Assert.AreEqual(1.8f, cc.height, 0.001f,
                "CharacterController height must be 1.8 (established architecture value).");
            Assert.AreEqual(0.3f, cc.radius, 0.001f,
                "CharacterController radius must be 0.3 (established architecture value).");
            Assert.AreEqual(0.9f, cc.center.y, 0.001f,
                "CharacterController center.y must be 0.9 (established architecture value).");
        }

        // ---- 138.6h — No premature gameplay MonoBehaviours on root ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_RootHasNoPrematureGameplayMonoBehaviours()
        {
            var prefab = LoadPrefab();
            var components = prefab.GetComponents<Component>();
            foreach (var comp in components)
            {
                // Task 139 legitimately added the single PlayerEntity identity component to the root;
                // Transform is required on any GameObject. No other (gameplay) MonoBehaviour is allowed.
                Assert.IsTrue(comp is Transform || comp is Football.Players.PlayerEntity,
                    $"Player prefab root must NOT have a premature gameplay MonoBehaviour component: {comp.GetType().Name}. " +
                    "Root should contain only Transform + the PlayerEntity identity component. " +
                    "Gameplay components belong on their designated sub-objects.");
            }
        }

        // ---- 138.6i — Collision child has only Transform + CharacterController ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_CollisionHasOnlyTransformAndCharacterController()
        {
            var prefab = LoadPrefab();
            var collision = FindChild(prefab.transform, "Collision");
            Assert.IsNotNull(collision);

            var components = collision.GetComponents<Component>();
            var componentTypes = components.Select(c => c.GetType()).ToList();

            Assert.IsTrue(componentTypes.Contains(typeof(Transform)),
                "Collision must have Transform.");
            Assert.IsTrue(componentTypes.Contains(typeof(CharacterController)),
                "Collision must have CharacterController.");
            Assert.AreEqual(2, componentTypes.Count,
                $"Collision must have exactly Transform + CharacterController (no premature components). " +
                $"Found: {string.Join(", ", componentTypes.Select(t => t.Name))}");
        }

        // ---- 138.6j — No premature gameplay MonoBehaviours anywhere in hierarchy ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_ContainsNoPrematureGameplayMonoBehaviours()
        {
            var prefab = LoadPrefab();
            var descendants = GetAllDescendants(prefab.transform);
            var forbidden = new HashSet<string>
            {
                "MonoBehaviour",
            };

            // The only MonoBehaviours permitted across the entire prefab are the PlayerEntity
            // identity component on the root (Task 139). No gameplay MonoBehaviour scripts may be
            // attached to any child. CharacterController (on the Collision child) is a Component,
            // not a MonoBehaviour, so it is not counted.
            int monoBehaviourCount = 0;
            string foundOn = null;
            foreach (var desc in descendants)
            {
                var mbs = desc.GetComponents<MonoBehaviour>();
                monoBehaviourCount += mbs.Length;
                if (mbs.Length > 0)
                {
                    foundOn = desc.name;
                }
            }
            // Also check root: only the allowed PlayerEntity identity component.
            var rootMbs = prefab.GetComponents<Football.Players.PlayerEntity>();
            Assert.AreEqual(1, rootMbs.Length,
                "Player prefab root must have exactly one PlayerEntity identity component (Task 139).");

            // Verify: no gameplay MonoBehaviours exist on any descendant.
            Assert.AreEqual(0, monoBehaviourCount,
                $"Player prefab children must contain no gameplay MonoBehaviour scripts. " +
                (foundOn != null ? $"Found on: {foundOn}" : ""));
        }

        // ---- 138.6k — Prefab is a proper prefab asset (not a scene object) ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_IsPrefabAsset()
        {
            var prefab = LoadPrefab();
            Assert.IsNotNull(prefab);
            Assert.IsTrue(PrefabUtility.IsPartOfPrefabAsset(prefab),
                "Player prefab must be a proper prefab asset, not a scene object.");
        }

        // ---- 138.6l — No file creation / no unexpected changes ----

        [Test]
        [Category("PlayerPrefab")]
        public void PlayerPrefab_NoNewPrefabFilesCreatedInPlayersFolder()
        {
            var playersDir = "Assets/Football/Prefabs/Players";
            var files = Directory.GetFiles(playersDir, "*.prefab");
            Assert.AreEqual(1, files.Length,
                $"Players folder must contain exactly 1 prefab (Player.prefab). " +
                $"Found {files.Length}: {string.Join(", ", files.Select(Path.GetFileName))}");
        }
    }
}
