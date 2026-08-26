using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Football.Tests.EditMode
{
    public class ArchitectureSmokeTests
    {
        [Test]
        public void CoreAssemblyExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>("Assets/Football/Runtime/Core/Football.Core.asmdef");
            Assert.IsNotNull(asset, "Football.Core.asmdef not found");
        }

        [Test]
        public void PlayerPrefabExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Football/Prefabs/Players/Player.prefab");
            Assert.IsNotNull(asset, "Player.prefab not found");
        }

        [Test]
        public void BallPrefabExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Football/Prefabs/Ball/SoccerBall.prefab");
            Assert.IsNotNull(asset, "SoccerBall.prefab not found");
        }

        [Test]
        public void BootstrapSceneExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>("Assets/Football/Scenes/Bootstrap/Bootstrap.unity");
            Assert.IsNotNull(asset, "Bootstrap.unity not found");
        }

        [Test]
        public void MatchSceneExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>("Assets/Football/Scenes/Match/Match.unity");
            Assert.IsNotNull(asset, "Match.unity not found");
        }

        [Test]
        public void StadiumPrefabExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Football/Prefabs/Stadium/Stadium.prefab");
            Assert.IsNotNull(asset, "Stadium.prefab not found");
        }
    }
}