using System;
using System.Collections.Generic;
using System.Reflection;
using Football.Core;
using NUnit.Framework;

namespace Football.Tests.EditMode
{
    public class SceneLoaderTests
    {
        private const string MatchScene = "Assets/Football/Scenes/Match/Match.unity";
        private const string MenuScene = "Assets/Football/Scenes/Bootstrap/Bootstrap.unity";

        private readonly List<string> _availableScenes = new() { MatchScene, MenuScene };
        private int _loadCalls;

        [SetUp]
        public void SetUp()
        {
            _loadCalls = 0;
        }

        private SceneLoader CreateLoader(Action<string> onLoad = null)
        {
            return new SceneLoader(
                _availableScenes,
                path =>
                {
                    _loadCalls++;
                    onLoad?.Invoke(path);
                });
        }

        [Test]
        public void SceneLoaderCanBeCreated()
        {
            var loader = new SceneLoader(_availableScenes);
            Assert.IsNotNull(loader);
        }

        [Test]
        public void InitialStateIsIdle()
        {
            var loader = CreateLoader();
            Assert.AreEqual(SceneLoadState.Idle, loader.CurrentState);
            Assert.IsFalse(loader.IsLoading);
        }

        [Test]
        public void NullSceneInputFails()
        {
            var loader = CreateLoader();
            Assert.Throws<ArgumentException>(() => loader.LoadScene(null));
        }

        [Test]
        public void EmptySceneInputFails()
        {
            var loader = CreateLoader();
            Assert.Throws<ArgumentException>(() => loader.LoadScene(string.Empty));
        }

        [Test]
        public void WhitespaceSceneInputFails()
        {
            var loader = CreateLoader();
            Assert.Throws<ArgumentException>(() => loader.LoadScene("   "));
        }

        [Test]
        public void MissingSceneFails()
        {
            var loader = CreateLoader();
            Assert.Throws<InvalidOperationException>(() => loader.LoadScene("Assets/Unknown/Unknown.unity"));
        }

        [Test]
        public void LoadScene_Transitions_ToLoadingThenLoaded()
        {
            SceneLoadState observed = SceneLoadState.Idle;
            bool loadingWasTrue = false;
            SceneLoader holder = null;
            var loader = new SceneLoader(
                _availableScenes,
                _ =>
                {
                    _loadCalls++;
                    loadingWasTrue = holder.IsLoading;
                    observed = holder.CurrentState;
                });
            holder = loader;

            loader.LoadScene(MatchScene);

            Assert.IsTrue(loadingWasTrue);
            Assert.AreEqual(SceneLoadState.Loading, observed);
            Assert.AreEqual(SceneLoadState.Loaded, loader.CurrentState);
            Assert.AreEqual(MatchScene, loader.CurrentScene);
            Assert.AreEqual(MatchScene, loader.TargetScene);
            Assert.IsTrue(loader.IsLoaded);
            Assert.AreEqual(1, _loadCalls);
        }

        [Test]
        public void LoadingStateIsReadOnly()
        {
            var property = typeof(SceneLoader).GetProperty(nameof(SceneLoader.CurrentState));
            Assert.IsNull(property.GetSetMethod());
        }

        [Test]
        public void DuplicateLoadIsRejected()
        {
            SceneLoader holder = null;
            var loader = new SceneLoader(
                _availableScenes,
                _ =>
                {
                    _loadCalls++;
                    Assert.Throws<InvalidOperationException>(() => holder.LoadScene(MenuScene));
                });
            holder = loader;

            loader.LoadScene(MatchScene);
            Assert.AreEqual(1, _loadCalls);
        }

        [Test]
        public void SameSceneLoadIsRejected()
        {
            var loader = CreateLoader();
            loader.LoadScene(MatchScene);
            Assert.Throws<InvalidOperationException>(() => loader.LoadScene(MatchScene));
        }

        [Test]
        public void LoadingAnotherScene_AfterCompletion_IsAllowed()
        {
            var loader = CreateLoader();
            loader.LoadScene(MatchScene);
            loader.LoadScene(MenuScene);
            Assert.AreEqual(2, _loadCalls);
            Assert.AreEqual(MenuScene, loader.CurrentScene);
        }

        [Test]
        public void NoSingletonRequirement()
        {
            Assert.IsNull(typeof(SceneLoader).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static));
            Assert.AreEqual(typeof(object), typeof(SceneLoader).BaseType);
            Assert.IsFalse(typeof(SceneLoader).IsSubclassOf(typeof(UnityEngine.Object)));
        }

        [Test]
        public void NoServiceLocatorDependency()
        {
            var fields = typeof(SceneLoader).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                Assert.IsFalse(field.FieldType.FullName?.Contains("ServiceRegistry") ?? false,
                    $"SceneLoader must not depend on ServiceRegistry via field {field.Name}.");
                Assert.IsFalse(field.FieldType.FullName?.Contains("ServiceLocator") ?? false,
                    $"SceneLoader must not depend on a Service Locator via field {field.Name}.");
            }
        }

        [Test]
        public void NoGameplayDependency()
        {
            var fields = typeof(SceneLoader).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                Assert.IsFalse(field.FieldType.FullName?.Contains("Football.Match") ?? false,
                    $"SceneLoader must not depend on match types via field {field.Name}.");
                Assert.IsFalse(field.FieldType.FullName?.Contains("Football.Players") ?? false,
                    $"SceneLoader must not depend on player types via field {field.Name}.");
            }
        }

        [Test]
        public void NoPerFramePolling()
        {
            Assert.IsNull(typeof(SceneLoader).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(typeof(SceneLoader).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.IsNull(typeof(SceneLoader).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        [Test]
        public void SceneLoaderDoesNotOwnServiceInitialization()
        {
            var loader = CreateLoader();
            loader.LoadScene(MatchScene);

            var type = typeof(SceneLoader);
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                Assert.IsFalse(method.Name == "Initialize",
                    "SceneLoader must not expose service-style Initialize.");
                Assert.IsFalse(method.Name == "Shutdown",
                    "SceneLoader must not expose service-style Shutdown.");
            }
        }
    }
}
