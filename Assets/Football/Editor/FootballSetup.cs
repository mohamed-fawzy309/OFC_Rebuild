using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;

namespace Football.Editor
{
    public static class FootballSetup
    {
        private const string Root = "Assets/Football";

        [MenuItem("Football/Setup/Create Prefabs", priority = 100)]
        public static void CreatePrefabs()
        {
            CreatePlayerPrefab();
            CreateBallPrefab();
            CreateStadiumPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FootballSetup] Prefabs created.");
        }

        [MenuItem("Football/Setup/Create Scenes", priority = 200)]
        public static void CreateScenes()
        {
            CreateBootstrapScene();
            CreateMatchScene();
            CreateTestScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FootballSetup] Scenes created.");
        }

        [MenuItem("Football/Setup/Create All", priority = 0)]
        public static void CreateAll()
        {
            CreatePrefabs();
            CreateScenes();
            CreateSmokeTest();
            Debug.Log("[FootballSetup] All architecture assets created successfully.");
        }

        [MenuItem("Football/Setup/Verify Architecture", priority = 500)]
        public static void VerifyArchitecture()
        {
            var errors = new List<string>();

            string[] requiredFolders = {
                "Art/Characters/Players", "Art/Characters/Goalkeepers", "Art/Characters/Referee",
                "Art/Characters/Shared", "Art/Balls", "Art/Stadium/Models", "Art/Stadium/Textures",
                "Art/Stadium/Materials", "Art/Stadium/Props", "Art/UI", "Art/VFX", "Art/Generated",
                "Animation/Source/Mixamo", "Animation/Processed/Locomotion", "Animation/Processed/BallControl",
                "Animation/Processed/Actions", "Animation/Processed/Goalkeeper", "Animation/Controllers",
                "Audio/Commentary", "Audio/Crowd", "Audio/Ball", "Audio/Player", "Audio/UI", "Audio/Music",
                "Materials", "Prefabs/Players", "Prefabs/Goalkeepers", "Prefabs/Ball", "Prefabs/Stadium",
                "Prefabs/UI", "Prefabs/Effects", "Scenes/Bootstrap", "Scenes/Menus", "Scenes/Match", "Scenes/Test",
                "Data/Players", "Data/Teams", "Data/Matches", "Data/Ball", "Data/Movement", "Data/Animation", "Data/Rules",
                "Runtime/Core/Interfaces", "Runtime/Core/Events", "Runtime/Core/Debug",
                "Runtime/Input", "Runtime/Players/States", "Runtime/Ball", "Runtime/Actions",
                "Runtime/Match/States", "Runtime/Teams", "Runtime/AI", "Runtime/Camera",
                "Runtime/UI", "Runtime/World", "Editor", "Tests/EditMode", "Tests/PlayMode", "Settings"
            };

            foreach (var folder in requiredFolders)
            {
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder))
                    errors.Add($"Missing folder: {folder}");
            }

            string[] requiredAsmdefs = {
                "Runtime/Core/Football.Core.asmdef",
                "Runtime/Input/Football.Input.asmdef",
                "Runtime/Players/Football.Players.asmdef",
                "Runtime/Ball/Football.Ball.asmdef",
                "Runtime/Actions/Football.Actions.asmdef",
                "Runtime/Match/Football.Match.asmdef",
                "Runtime/Teams/Football.Teams.asmdef",
                "Runtime/AI/Football.AI.asmdef",
                "Runtime/Camera/Football.Camera.asmdef",
                "Runtime/UI/Football.UI.asmdef",
                "Runtime/World/Football.World.asmdef",
                "Editor/Football.Editor.asmdef",
                "Tests/EditMode/Football.Tests.EditMode.asmdef",
                "Tests/PlayMode/Football.Tests.PlayMode.asmdef"
            };

            foreach (var asmdef in requiredAsmdefs)
            {
                if (!File.Exists(Root + "/" + asmdef))
                    errors.Add($"Missing asmdef: {asmdef}");
            }

            string[] requiredPrefabs = {
                "Prefabs/Players/Player.prefab",
                "Prefabs/Ball/SoccerBall.prefab",
                "Prefabs/Stadium/Stadium.prefab"
            };

            foreach (var prefab in requiredPrefabs)
            {
                if (!File.Exists(Root + "/" + prefab))
                    errors.Add($"Missing prefab: {prefab}");
            }

            string[] requiredScenes = {
                "Scenes/Bootstrap/Bootstrap.unity",
                "Scenes/Match/Match.unity",
                "Scenes/Test/PlayerMovementTest.unity",
                "Scenes/Test/BallPhysicsTest.unity",
                "Scenes/Test/BallControlTest.unity",
                "Scenes/Test/AnimationTest.unity",
                "Scenes/Test/MatchRulesTest.unity"
            };

            foreach (var scene in requiredScenes)
            {
                if (!File.Exists(Root + "/" + scene))
                    errors.Add($"Missing scene: {scene}");
            }

            if (errors.Count == 0)
                Debug.Log("[FootballSetup] Architecture verification PASSED - all assets present.");
            else
                Debug.LogError($"[FootballSetup] Architecture verification FAILED:\n{string.Join("\n", errors)}");
        }

        private static void CreatePlayerPrefab()
        {
            string path = Root + "/Prefabs/Players/Player.prefab";
            if (File.Exists(path)) return;

            var root = new GameObject("Player");

            var visual = CreateChild(root, "Visual");
            CreateChild(visual, "Model");
            CreateChild(visual, "Animator");
            CreateChild(visual, "AudioSource");

            var collision = CreateChild(root, "Collision");
            var cc = collision.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.9f, 0);

            var gameplay = CreateChild(root, "Gameplay");
            CreateChild(gameplay, "Input");
            CreateChild(gameplay, "Movement");
            CreateChild(gameplay, "Rotation");
            CreateChild(gameplay, "StateMachine");
            CreateChild(gameplay, "Actions");
            CreateChild(gameplay, "BallInteraction");
            CreateChild(gameplay, "Animation");

            var sockets = CreateChild(root, "Sockets");
            var leftFoot = CreateChild(sockets, "LeftFoot");
            leftFoot.transform.localPosition = new Vector3(-0.1f, 0.05f, 0f);
            var rightFoot = CreateChild(sockets, "RightFoot");
            rightFoot.transform.localPosition = new Vector3(0.1f, 0.05f, 0f);
            var head = CreateChild(sockets, "Head");
            head.transform.localPosition = new Vector3(0, 1.8f, 0);
            var ballControl = CreateChild(sockets, "BallControlPoint");
            ballControl.transform.localPosition = new Vector3(0, 0.3f, 0.4f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateBallPrefab()
        {
            string path = Root + "/Prefabs/Ball/SoccerBall.prefab";
            if (File.Exists(path)) return;

            var root = new GameObject("SoccerBall");

            var visual = CreateChild(root, "Visual");
            var ballMesh = CreateChild(visual, "BallMesh");
            var sphere = ballMesh.AddComponent<SphereCollider>();
            sphere.radius = 0.11f;

            var physics = CreateChild(root, "Physics");
            var rb = physics.AddComponent<Rigidbody>();
            rb.mass = 0.43f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.05f;
            rb.useGravity = true;
            var physCollider = physics.AddComponent<SphereCollider>();
            physCollider.radius = 0.11f;

            var gameplay = CreateChild(root, "Gameplay");
            CreateChild(gameplay, "BallController");

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateStadiumPrefab()
        {
            string path = Root + "/Prefabs/Stadium/Stadium.prefab";
            if (File.Exists(path)) return;

            var root = new GameObject("Stadium");

            var environment = CreateChild(root, "Environment");
            CreateChild(environment, "Pitch");
            CreateChild(environment, "Stands");
            CreateChild(environment, "Roof");
            CreateChild(environment, "Boards");
            CreateChild(environment, "Props");

            var gameplay = CreateChild(root, "Gameplay");
            CreateChild(gameplay, "FieldBounds");
            CreateChild(gameplay, "GoalAreas");
            CreateChild(gameplay, "OutOfBounds");
            CreateChild(gameplay, "SpawnPoints");

            var goals = CreateChild(root, "Goals");

            var homeGoal = CreateChild(goals, "HomeGoal");
            CreateChild(homeGoal, "Frame");
            CreateChild(homeGoal, "Net");
            CreateChild(homeGoal, "GoalSensor");

            var awayGoal = CreateChild(goals, "AwayGoal");
            CreateChild(awayGoal, "Frame");
            CreateChild(awayGoal, "Net");
            CreateChild(awayGoal, "GoalSensor");

            var light = new GameObject("Directional Light");
            light.transform.SetParent(root.transform);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var lightComp = light.AddComponent<Light>();
            lightComp.type = LightType.Directional;
            lightComp.intensity = 1f;
            lightComp.color = new Color(1f, 0.96f, 0.84f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateBootstrapScene()
        {
            string path = Root + "/Scenes/Bootstrap/Bootstrap.unity";
            if (File.Exists(path)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var bootstrap = new GameObject("_Bootstrap");
            CreateChild(bootstrap, "GameBootstrap");
            CreateChild(bootstrap, "ServiceRegistry");
            CreateChild(bootstrap, "Settings");
            CreateChild(bootstrap, "SceneLoader");

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateMatchScene()
        {
            string path = Root + "/Scenes/Match/Match.unity";
            if (File.Exists(path)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var bootstrap = new GameObject("_Bootstrap");

            var systems = CreateChild(null, "_Systems");
            CreateChild(systems, "MatchController");
            CreateChild(systems, "MatchStateMachine");
            CreateChild(systems, "TeamManager");
            CreateChild(systems, "RulesSystem");
            CreateChild(systems, "AudioManager");

            var world = CreateChild(null, "_World");
            CreateChild(world, "Stadium");
            CreateChild(world, "Lighting");
            CreateChild(world, "Environment");
            CreateChild(world, "Boundaries");

            var match = CreateChild(null, "_Match");
            var players = CreateChild(match, "Players");
            CreateChild(players, "HomeTeam");
            CreateChild(players, "AwayTeam");
            var ballContainer = CreateChild(match, "Ball");
            CreateChild(ballContainer, "SoccerBall");
            var officials = CreateChild(match, "Officials");
            CreateChild(officials, "Referee");
            CreateChild(officials, "Assistants");

            var camera = CreateChild(null, "_Camera");
            CreateChild(camera, "MainCamera");
            CreateChild(camera, "GameplayCamera");

            var ui = CreateChild(null, "_UI");
            CreateChild(ui, "HUD");
            CreateChild(ui, "Scoreboard");
            CreateChild(ui, "MatchUI");

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateTestScenes()
        {
            string[] testScenes = {
                "PlayerMovementTest",
                "BallPhysicsTest",
                "BallControlTest",
                "AnimationTest",
                "MatchRulesTest"
            };

            foreach (var name in testScenes)
            {
                string path = Root + $"/Scenes/Test/{name}.unity";
                if (File.Exists(path)) continue;

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                var test = CreateChild(null, "_Test");
                CreateChild(test, "Systems");
                CreateChild(test, "Subject");
                CreateChild(test, "Camera");
                CreateChild(test, "Debug");

                EditorSceneManager.SaveScene(scene, path);
            }
        }

        private static void CreateSmokeTest()
        {
            string testDir = Root + "/Tests/EditMode";
            string testFile = testDir + "/ArchitectureSmokeTests.cs";
            if (File.Exists(testFile)) return;

            string content = @"using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Football.Tests.EditMode
{
    public class ArchitectureSmokeTests
    {
        [Test]
        public void CoreAssemblyExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(""Assets/Football/Runtime/Core/Football.Core.asmdef"");
            Assert.IsNotNull(asset, ""Football.Core.asmdef not found"");
        }

        [Test]
        public void PlayerPrefabExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(""Assets/Football/Prefabs/Players/Player.prefab"");
            Assert.IsNotNull(asset, ""Player.prefab not found"");
        }

        [Test]
        public void BallPrefabExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(""Assets/Football/Prefabs/Ball/SoccerBall.prefab"");
            Assert.IsNotNull(asset, ""SoccerBall.prefab not found"");
        }

        [Test]
        public void BootstrapSceneExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(""Assets/Football/Scenes/Bootstrap/Bootstrap.unity"");
            Assert.IsNotNull(asset, ""Bootstrap.unity not found"");
        }

        [Test]
        public void MatchSceneExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(""Assets/Football/Scenes/Match/Match.unity"");
            Assert.IsNotNull(asset, ""Match.unity not found"");
        }

        [Test]
        public void StadiumPrefabExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(""Assets/Football/Prefabs/Stadium/Stadium.prefab"");
            Assert.IsNotNull(asset, ""Stadium.prefab not found"");
        }
    }
}";
            File.WriteAllText(testFile, content);
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            if (parent != null)
                child.transform.SetParent(parent.transform);
            return child;
        }
    }
}
