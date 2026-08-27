using System;
using System.Collections.Generic;

namespace Football.Core
{
    /// <summary>
    /// Read-only lifecycle state for a single <see cref="SceneTransitionSystem"/> request.
    /// Distinct from <see cref="SceneLoadState"/>: the loader answers "what is happening with
    /// the actual load?", while this answers "what is happening with the requested transition?".
    /// </summary>
    public enum SceneTransitionState
    {
        Idle,
        Loading,
        Completed,
        Failed
    }

    /// <summary>
    /// A pure C# coordinator that controls TRANSITION REQUESTS and TRANSITION LIFECYCLE.
    ///
    /// Ownership:
    /// - GameBootstrap          = application startup orchestration
    /// - SceneTransitionSystem  = transition request/lifecycle orchestration
    /// - SceneLoader            = scene-loading mechanics (the ONLY owner of SceneManager)
    /// - Unity SceneManager     = the actual engine operation
    ///
    /// Responsibilities (OWNED):
    /// - accept a single transition request (source -> target)
    /// - validate the request (target != current scene, not already transitioning)
    /// - prevent conflicting/concurrent/reentrant transitions (no queue, no replacement)
    /// - delegate the actual load to SceneLoader and interpret the result
    /// - expose transition-level state and read-through current-scene knowledge
    ///
    /// NOT owned:
    /// - actual scene-loading mechanics (delegated to SceneLoader)
    /// - service initialization/shutdown
    /// - player spawning, match setup, gameplay rules
    /// - loading UI / progress / fades / audio transitions
    /// - input reading (requests arrive explicitly from callers)
    ///
    /// This class is NOT a MonoBehaviour, is NOT a Singleton (no static instance), does NOT use
    /// DontDestroyOnLoad, does NOT use a Service Locator, and performs no per-frame polling
    /// (no Update/FixedUpdate/LateUpdate). It is owned by the Composition Root and does not
    /// outlive its owner.
    ///
    /// The current loader is SYNCHRONOUS, so a single RequestTransition runs the load to
    /// completion (or failure) within one call. No fake asynchronous polling is introduced;
    /// the design delegates to the loader abstraction so an async loader could replace the
    /// synchronous one later without rewriting this public architecture.
    /// </summary>
    public class SceneTransitionSystem
    {
        private readonly SceneLoader _loader;

        private string _targetScene;

        public SceneTransitionSystem(SceneLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            CurrentState = SceneTransitionState.Idle;
        }

        public SceneTransitionState CurrentState { get; private set; }

        /// <summary>
        /// The scene currently active. Read through to the loader so there is exactly ONE
        /// mutable source of truth for the current scene (no divergent second field).
        /// </summary>
        public string CurrentScene => _loader.CurrentScene;

        public string TargetScene => _targetScene;

        public bool IsTransitioning => CurrentState == SceneTransitionState.Loading;

        /// <summary>
        /// The last failure, if any. Preserves the original exception as InnerException.
        /// </summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// Requests a transition from the current scene to <paramref name="targetScene"/>.
        ///
        /// Validates the request, moves to Loading, delegates the actual scene load to the
        /// injected SceneLoader (synchronous), and completes or fails deterministically.
        /// A conflicting (already transitioning) or same-scene request is rejected without
        /// mutating the active transition. There is no queue, no silent replacement, no
        /// automatic retry, and no fallback.
        /// </summary>
        /// <param name="targetScene">The build-settings scene path to transition to.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="loader"/> was null at construction.</exception>
        /// <exception cref="ArgumentException">If <paramref name="targetScene"/> is null/empty/whitespace.</exception>
        /// <exception cref="InvalidOperationException">
        /// If a transition is already in progress, the target equals the current scene, or the
        /// underlying scene load fails (original preserved as InnerException).</exception>
        public void RequestTransition(string targetScene)
        {
            if (string.IsNullOrWhiteSpace(targetScene))
                throw new ArgumentException(
                    "Transition target must not be null, empty, or whitespace.", nameof(targetScene));

            if (CurrentState == SceneTransitionState.Loading)
                throw new InvalidOperationException(
                    $"A scene transition is already in progress (target: {_targetScene}). " +
                    "Conflicting transitions are not allowed; the first request remains authoritative.");

            if (string.Equals(targetScene, _loader.CurrentScene, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Scene {targetScene} is already the current scene. " +
                    "Reloading the current scene is not a valid transition.");

            _targetScene = targetScene;
            CurrentState = SceneTransitionState.Loading;
            LastError = null;

            try
            {
                // Delegate the actual loading mechanics to the loader (the only SceneManager owner).
                _loader.LoadScene(targetScene);
                CurrentState = SceneTransitionState.Completed;
            }
            catch (Exception ex)
            {
                CurrentState = SceneTransitionState.Failed;
                LastError = ex;
                // Do not hide the root cause: rethrow with the original as InnerException.
                throw new InvalidOperationException(
                    $"Scene transition failed for '{targetScene}'.", ex);
            }
        }
    }
}
