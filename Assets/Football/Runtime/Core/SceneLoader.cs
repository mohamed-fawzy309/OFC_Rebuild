using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Football.Core
{
    /// <summary>
    /// A controlled, synchronous scene loader.
    ///
    /// Responsibilities (OWNED):
    /// - validate a requested scene against the runtime-available scene set
    /// - request a scene load through Unity's scene API
    /// - track and expose explicit loading state
    /// - reject conflicting/same-scene requests
    ///
    /// NOT owned:
    /// - service creation/initialization
    /// - gameplay, match, player, ball, UI, camera logic
    /// - application startup decisions (owned by GameBootstrap)
    ///
    /// SceneLoader is pure C# (not a MonoBehaviour), is not a Singleton, does
    /// not use a Service Locator, and contains no per-frame polling. It is
    /// explicitly owned and referenced by the Composition Root.
    ///
    /// The runtime-available scene set is the set of scenes registered in the
    /// Build Settings (a valid source of truth in a built player). By default
    /// this set is discovered once at construction from the active Build Settings.
    /// </summary>
    public class SceneLoader
    {
        private readonly IReadOnlyList<string> _availableScenes;
        private readonly Action<string> _loadScene;

        private string _targetScene;
        private string _currentScene;

        /// <summary>
        /// Creates a SceneLoader. The available scene set defaults to the scenes
        /// registered in the Build Settings. A custom <paramref name="loadScene"/>
        /// may be supplied for tests; the default routes to
        /// <see cref="SceneManager.LoadScene(string, LoadSceneMode)"/>.
        /// </summary>
        public SceneLoader(IReadOnlyList<string> availableScenePaths = null, Action<string> loadScene = null)
        {
            _availableScenes = availableScenePaths ?? DiscoverBuildSettingsScenes();
            _loadScene = loadScene ?? (path => SceneManager.LoadScene(path, LoadSceneMode.Single));
            CurrentState = SceneLoadState.Idle;
        }

        public SceneLoadState CurrentState { get; private set; }

        public string CurrentScene => _currentScene;

        public string TargetScene => _targetScene;

        public bool IsLoading => CurrentState == SceneLoadState.Loading;

        public bool IsLoaded => CurrentState == SceneLoadState.Loaded;

        public bool IsFailed => CurrentState == SceneLoadState.Failed;

        /// <summary>
        /// Loads the scene at <paramref name="scenePath"/> synchronously.
        /// Validates input and membership in the available scene set before loading.
        /// Loading a scene that is already the current scene is rejected.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// If <paramref name="scenePath"/> is null, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">
        /// If a load is already in progress, the scene is not registered as available,
        /// or the requested scene is already the current scene.</exception>
        public void LoadScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
                throw new ArgumentException("Scene path must not be null, empty, or whitespace.", nameof(scenePath));

            if (CurrentState == SceneLoadState.Loading)
                throw new InvalidOperationException(
                    $"A scene load is already in progress (target: {_targetScene}). " +
                    "Concurrent scene loads are not allowed.");

            if (string.Equals(scenePath, _currentScene, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Scene {scenePath} is already the current scene. " +
                    "Reloading the current scene is not allowed.");

            if (!ContainsScene(scenePath))
                throw new InvalidOperationException(
                    $"Scene {scenePath} is not registered in the available scene set. " +
                    "Ensure the scene is added to the Build Settings before loading.");

            _targetScene = scenePath;
            CurrentState = SceneLoadState.Loading;

            try
            {
                _loadScene(scenePath);
            }
            catch (Exception ex)
            {
                CurrentState = SceneLoadState.Failed;
                throw new InvalidOperationException(
                    $"Scene load failed for {scenePath}.", ex);
            }

            _currentScene = scenePath;
            CurrentState = SceneLoadState.Loaded;
        }

        private bool ContainsScene(string scenePath)
        {
            foreach (var candidate in _availableScenes)
            {
                if (string.Equals(candidate, scenePath, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static IReadOnlyList<string> DiscoverBuildSettingsScenes()
        {
            var paths = new List<string>();
            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
                paths.Add(SceneUtility.GetScenePathByBuildIndex(i));
            return paths;
        }
    }

    /// <summary>
    /// Read-only lifecycle state for <see cref="SceneLoader"/>.
    /// </summary>
    public enum SceneLoadState
    {
        Idle,
        Loading,
        Loaded,
        Failed
    }
}
