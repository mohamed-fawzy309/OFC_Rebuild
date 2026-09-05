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
    /// Task 144 — Socket System.
    ///
    /// Task 144 establishes the authoritative socket structure and boundary for the Player entity:
    /// stable named attachment/reference points that introduce NO gameplay behavior. The preferred
    /// outcome is a plain Transform-based structure — a SocketSystem/SocketManager must NOT be
    /// created merely because the roadmap says "Socket system" (rule: prefer Transform-based
    /// architecture when it is sufficient).
    ///
    /// This fixture locks the SOCKET CONTRACT:
    ///   - Exactly ONE 'Sockets' container, a direct child of the Player root.
    ///   - Exactly the four established sockets (LeftFoot / RightFoot / Head / BallControlPoint),
    ///     all Transform-only attachment points with the established scaffold positions.
    ///   - Sockets carry no gameplay MonoBehaviours and no physics; they do not own the
    ///     CharacterController (that stays on Collision).
    ///   - Sockets remain separate from Visual / Collision / Gameplay.
    ///   - No speculative socket added; no SocketManager/Registry/System/component was created.
    ///
    /// Presence of the four socket objects is already covered by PlayerPrefabStructureTests
    /// (Task 138, checks containers hold the expected names). This fixture adds the Task 144
    /// boundary/ownership contract only.
    ///
    /// Ball interaction, dribbling/passing/shooting/tackling, foot/head IK, animation, equipment
    /// and movement are deliberately NOT implemented and NOT tested here (later Tasks).
    /// </summary>
    public class SocketContractTests
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

        // ---- 144.2 / 144.3 — one authoritative Sockets container, child of Player ----

        [Test]
        [Category("Socket")]
        public void PlayerPrefab_ContainsExactlyOneSocketsContainer()
        {
            var prefab = LoadPrefab();
            var matches = GetAllTransforms(prefab.transform).Where(t => t.name == "Sockets").ToList();
            Assert.AreEqual(1, matches.Count,
                $"Player prefab must contain exactly ONE 'Sockets' container (the authoritative " +
                $"socket parent). Found {matches.Count}.");
        }

        [Test]
        [Category("Socket")]
        public void Sockets_IsDirectChildOfPlayerRoot()
        {
            var prefab = LoadPrefab();
            var sockets = GetAllTransforms(prefab.transform).Single(t => t.name == "Sockets");
            Assert.AreEqual(prefab.transform, sockets.parent,
                "The Sockets container must be a DIRECT child of the Player root.");
        }

        // ---- 144.2 / 144.6 — the four established Transform sockets ----

        [Test]
        [Category("Socket")]
        public void Sockets_HasExactlyTheFourEstablishedSockets()
        {
            var prefab = LoadPrefab();
            var sockets = GetAllTransforms(prefab.transform).Single(t => t.name == "Sockets");

            Assert.AreEqual(4, sockets.childCount,
                $"Sockets must contain exactly the four established sockets. Found {sockets.childCount}.");

            var names = new List<string>();
            for (int i = 0; i < sockets.childCount; i++) names.Add(sockets.GetChild(i).name);
            Assert.AreEqual(new[] { "LeftFoot", "RightFoot", "Head", "BallControlPoint" }, names.ToArray(),
                "Sockets children must be, in order: LeftFoot, RightFoot, Head, BallControlPoint.");
        }

        [Test]
        [Category("Socket")]
        public void Sockets_AndEachSocket_AreTransformOnlyAttachmentPoints()
        {
            // Sockets are implemented as plain named Transform attachment points — no component is
            // required. Each object must be exactly one component (Transform).
            var prefab = LoadPrefab();
            var sockets = GetAllTransforms(prefab.transform).Single(t => t.name == "Sockets");

            var all = new List<Transform> { sockets };
            for (int i = 0; i < sockets.childCount; i++) all.Add(sockets.GetChild(i));

            foreach (var t in all)
            {
                var components = t.GetComponents<Component>();
                Assert.AreEqual(1, components.Length,
                    $"'{t.name}' must be a Transform-only attachment point (exactly one component). " +
                    $"Found: {string.Join(", ", components.Select(c => c.GetType().Name))}");
                Assert.IsInstanceOf<Transform>(components[0]);
            }
        }

        [Test]
        [Category("Socket")]
        public void Socket_Positions_MatchEstablishedScaffoldValues()
        {
            // The socket positions are authored/established in the Editor builder
            // (FootballSetup.cs) and preserved in the authoritative prefab. They are documented
            // scaffold values, NOT invented "correct football" coordinates.
            var prefab = LoadPrefab();
            var sockets = GetAllTransforms(prefab.transform).Single(t => t.name == "Sockets");

            Assert.AreEqual(Vector3.zero, sockets.localPosition, "Sockets container must sit at origin.");

            var left = sockets.Find("LeftFoot");
            var right = sockets.Find("RightFoot");
            var head = sockets.Find("Head");
            var ball = sockets.Find("BallControlPoint");

            AssertPos(left, new Vector3(-0.1f, 0.05f, 0f), "LeftFoot");
            AssertPos(right, new Vector3(0.1f, 0.05f, 0f), "RightFoot");
            AssertPos(head, new Vector3(0f, 1.8f, 0f), "Head");
            AssertPos(ball, new Vector3(0f, 0.3f, 0.4f), "BallControlPoint");
        }

        private static void AssertPos(Transform t, Vector3 expected, string label)
        {
            Assert.IsNotNull(t, $"{label} socket must exist.");
            Assert.AreEqual(expected.x, t.localPosition.x, 0.0001f, $"{label}.localPosition.x");
            Assert.AreEqual(expected.y, t.localPosition.y, 0.0001f, $"{label}.localPosition.y");
            Assert.AreEqual(expected.z, t.localPosition.z, 0.0001f, $"{label}.localPosition.z");
        }

        // ---- 144.4 — sockets are references, NOT gameplay logic ----

        [Test]
        [Category("Socket")]
        public void Sockets_ContainNoGameplayMonoBehaviour()
        {
            var prefab = LoadPrefab();
            var sockets = GetAllTransforms(prefab.transform).Single(t => t.name == "Sockets");
            var all = new List<Transform> { sockets };
            for (int i = 0; i < sockets.childCount; i++) all.Add(sockets.GetChild(i));

            foreach (var t in all)
            {
                Assert.IsNull(t.GetComponent<MonoBehaviour>(),
                    $"'{t.name}' must not carry gameplay MonoBehaviours (socket = reference/attachment, not logic).");
            }
        }

        [Test]
        [Category("Socket")]
        public void Sockets_DoNotOwn_CharacterControllerOrAnyPhysics()
        {
            var prefab = LoadPrefab();
            var sockets = GetAllTransforms(prefab.transform).Single(t => t.name == "Sockets");
            var socketTree = new HashSet<Transform>(GetAllTransforms(sockets)) { sockets };

            foreach (var t in socketTree)
            {
                Assert.IsNull(t.GetComponent<Collider>(),
                    $"'Sockets' subtree object '{t.name}' must not own any Collider.");
                Assert.IsNull(t.GetComponent<Rigidbody>(),
                    $"'Sockets' subtree object '{t.name}' must not own a Rigidbody.");
            }

            var cc = prefab.GetComponentsInChildren<CharacterController>(true);
            Assert.AreEqual(1, cc.Length);
            Assert.IsFalse(socketTree.Contains(cc[0].transform),
                "The CharacterController must NOT live under Sockets; it stays on Collision.");
        }

        [Test]
        [Category("Socket")]
        public void Sockets_AreSeparateFrom_Visual_Collision_Gameplay()
        {
            var prefab = LoadPrefab();
            var sockets = GetAllTransforms(prefab.transform).Single(t => t.name == "Sockets");
            var visual = GetAllTransforms(prefab.transform).Single(t => t.name == "Visual");
            var collision = GetAllTransforms(prefab.transform).Single(t => t.name == "Collision");
            var gameplay = GetAllTransforms(prefab.transform).Single(t => t.name == "Gameplay");

            var socketTree = new HashSet<Transform>(GetAllTransforms(sockets)) { sockets };

            Assert.IsFalse(socketTree.Contains(visual), "Visual must not live under Sockets.");
            Assert.IsFalse(socketTree.Contains(collision), "Collision must not live under Sockets.");
            Assert.IsFalse(socketTree.Contains(gameplay), "Gameplay must not live under Sockets.");
        }

        // ---- 144.5 / 144.6 — no speculative sockets, no manager/system ----

        [Test]
        [Category("Socket")]
        public void NoSpeculativeSockets_Added()
        {
            // Only the established four socket Transforms exist. Extra speculative sockets are
            // forbidden unless project evidence proves they belong (144.6).
            var forbidden = new[] { "Camera", "Hand", "Chest", "Equipment", "BallKick", "PassTarget" };
            var prefab = LoadPrefab();
            var all = GetAllTransforms(prefab.transform);
            foreach (var forbiddenName in forbidden)
            {
                Assert.IsFalse(all.Any(t => t.name == forbiddenName),
                    $"No speculative socket '{forbiddenName}' may be added (Task 144 keeps the four " +
                    "established sockets only).");
            }
        }

        [Test]
        [Category("Socket")]
        public void NoSocketManager_Registry_OrSystem_Created()
        {
            // A Transform-based socket structure is sufficient; no runtime socket machinery may
            // exist (no consumer, and rule 25 prefers Transform-based architecture).
            var forbidden = new[]
            {
                "SocketSystem", "SocketManager", "SocketRegistry", "SocketDatabase",
                "PlayerSocketController", "DynamicSocketCollection", "SocketPoint",
                "AttachmentPoint"
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
                        $"No socket runtime system ('{t.Name}') may be introduced (socket = named " +
                        "Transform attachment points; no manager is required).");
                }
            }
        }
    }
}