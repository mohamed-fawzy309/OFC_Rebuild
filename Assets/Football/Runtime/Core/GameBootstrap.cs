using System;
using System.Collections.Generic;
using UnityEngine;

namespace Football.Core
{
    public enum BootstrapState
    {
        Boot,
        Initializing,
        Ready,
        Failed,
        ShuttingDown,
        Stopped
    }

    /// <summary>
    /// Tracks the internal startup pipeline position while startup is running.
    ///
    /// This is deliberately separate from <see cref="BootstrapState"/>. The top-level
    /// lifecycle state collapses the whole pipeline into a single coarsely-grained
    /// "Initializing" value, which cannot express the deterministic phase boundaries
    /// that the startup sequence must expose. StartupPhase is the fine-grained
    /// counterpart: it is only meaningful while a startup sequence is in progress.
    /// </summary>
    public enum StartupPhase
    {
        NotStarted,
        StartupBegan,
        InfrastructureReady,
        ServicesRegistered,
        ServicesInitialized,
        InitialSceneLoaded,
        Complete,
        Failed
    }

    /// <summary>
    /// The application's single startup orchestrator.
    ///
    /// GameBootstrap is the one entry point that drives the project's deterministic
    /// startup sequence. It COORDINATES phase boundaries only; it does not absorb the
    /// responsibilities of the systems it coordinates:
    /// - ServiceRegistry still stores/looks up services; it is never asked to initialize them.
    /// - SceneLoader still performs the actual scene load; it is never asked to start the app.
    /// - The detailed ordering of individual services is owned by the service-initialization
    ///   system, not by GameBootstrap.
    ///
    /// The phase work itself is supplied by the Composition Root through <see cref="Configure"/>.
    /// This keeps GameBootstrap decoupled from concrete registries/scene loaders and makes the
    /// sequence fully testable with seams.
    ///
    /// Ordering policy (the startup sequence is event-driven and deterministic; there is NO
    /// per-frame polling):
    ///  1. Bootstrap entry
    ///  2. Core/infrastructure setup
    ///  3. Service registration
    ///  4. Service initialization
    ///  5. Initial scene load
    ///  6. Startup complete
    ///
    /// A failed prerequisite halts all later phases and preserves the Failed state; startup
    /// never proceeds past the failing phase.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private static readonly Dictionary<BootstrapState, HashSet<BootstrapState>> AllowedTransitions = new()
        {
            { BootstrapState.Boot,           new HashSet<BootstrapState> { BootstrapState.Initializing } },
            { BootstrapState.Initializing,   new HashSet<BootstrapState> { BootstrapState.Ready, BootstrapState.Failed } },
            { BootstrapState.Ready,          new HashSet<BootstrapState> { BootstrapState.Initializing, BootstrapState.ShuttingDown, BootstrapState.Failed } },
            { BootstrapState.Failed,         new HashSet<BootstrapState>() },
            { BootstrapState.ShuttingDown,   new HashSet<BootstrapState> { BootstrapState.Stopped } },
            { BootstrapState.Stopped,        new HashSet<BootstrapState>() }
        };

        public BootstrapState CurrentState { get; private set; } = BootstrapState.Boot;

        public bool IsReady => CurrentState == BootstrapState.Ready;

        public bool IsFailed => CurrentState == BootstrapState.Failed;

        public bool IsStopped => CurrentState == BootstrapState.Stopped;

        public bool HasFailed => CurrentState == BootstrapState.Failed;

        /// <summary>
        /// The immutable, read-only snapshot of the most recent (and only) bootstrap failure.
        /// Null until startup fails. There is no setter: the bootstrap owns its error state and
        /// callers cannot mutate or clear the stored failure context.
        /// </summary>
        public BootstrapError CurrentError { get; private set; }

        /// <summary>
        /// The category of the current failure, or null while not failed. Read-through to
        /// <see cref="CurrentError"/> so category and error context can never diverge.
        /// </summary>
        public BootstrapErrorCategory? FailureCategory => CurrentError?.Category;

        /// <summary>
        /// The current position in the startup pipeline. Meaningful once startup has begun;
        /// <see cref="StartupPhase.NotStarted"/> before that. Becomes
        /// <see cref="StartupPhase.Failed"/> if startup aborts.
        /// </summary>
        public StartupPhase CurrentPhase { get; private set; } = StartupPhase.NotStarted;

        /// <summary>
        /// Chronological trace of the startup sequence (one entry per phase boundary).
        /// Used to verify that phases execute in the exact required order.
        /// </summary>
        public IReadOnlyList<string> StartupTrace => _trace;

        private Action _prepareInfrastructure;
        private Action _registerServices;
        private Action _initializeServices;
        private Func<string, bool> _loadScene;
        private string _initialScenePath;
        private Action<BootstrapError> _reportError;

        private readonly List<string> _trace = new();

        /// <summary>
        /// Supplies the phase work for the startup sequence. Must be called before
        /// <see cref="Initialize"/> runs (i.e. while still in <see cref="BootstrapState.Boot"/>).
        /// Null hooks are treated as no-op phases (used when no work exists yet, e.g. tests or a
        /// not-yet-wired placeholder bootstrap). When <paramref name="loadScene"/> and
        /// <paramref name="initialScenePath"/> are both provided, the initial scene must load
        /// successfully before startup may complete; otherwise startup completes without a scene.
        /// </summary>
        public GameBootstrap Configure(
            Action prepareInfrastructure = null,
            Action registerServices = null,
            Action initializeServices = null,
            Func<string, bool> loadScene = null,
            string initialScenePath = null,
            Action<BootstrapError> reportError = null)
        {
            if (CurrentState != BootstrapState.Boot || CurrentPhase != StartupPhase.NotStarted)
                throw new InvalidOperationException(
                    "Bootstrap: cannot configure startup after it has begun.");

            _prepareInfrastructure = prepareInfrastructure;
            _registerServices = registerServices;
            _initializeServices = initializeServices;
            _loadScene = loadScene;
            _initialScenePath = initialScenePath;
            _reportError = reportError;
            return this;
        }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (CurrentState == BootstrapState.Failed || CurrentState == BootstrapState.Stopped)
            {
                throw new InvalidOperationException(
                    $"Bootstrap: cannot initialize from terminal state {CurrentState}.");
            }

            if (CurrentState == BootstrapState.Initializing)
            {
                throw new InvalidOperationException(
                    "Bootstrap: startup is already in progress. Re-entrant initialization is rejected.");
            }

            if (CurrentState == BootstrapState.Ready)
            {
                // Startup already completed. Reuse the completed startup as a safe no-op rather
                // than executing the sequence a second time.
                return;
            }

            ChangeState(BootstrapState.Initializing);
            BeginPhase(StartupPhase.StartupBegan, "Bootstrap");

            try
            {
                BeginPhase(StartupPhase.InfrastructureReady, "Infrastructure");
                _prepareInfrastructure?.Invoke();

                BeginPhase(StartupPhase.ServicesRegistered, "RegisterServices");
                _registerServices?.Invoke();

                BeginPhase(StartupPhase.ServicesInitialized, "InitializeServices");
                _initializeServices?.Invoke();

                if (_loadScene != null && _initialScenePath != null)
                {
                    BeginPhase(StartupPhase.InitialSceneLoaded, "LoadInitialScene");
                    bool loaded = _loadScene(_initialScenePath);
                    if (!loaded)
                    {
                        throw new InvalidOperationException(
                            $"Bootstrap: initial scene load failed for '{_initialScenePath}'.");
                    }
                }

                BeginPhase(StartupPhase.Complete, "StartupComplete");
                ChangeState(BootstrapState.Ready);
            }
            catch (Exception ex)
            {
                // A failed prerequisite halts all later phases and preserves the Failed state.
                // Capture the failing phase (still the phase that threw) BEFORE overwriting with
                // Failed, so the stored error identifies exactly where startup aborted.
                var failingPhase = CurrentPhase;
                BeginPhase(StartupPhase.Failed, "Failed");
                ChangeState(BootstrapState.Failed);
                CurrentError = BuildError(failingPhase, ex);
                // The authoritative bootstrap-level report fires exactly ONCE, at this boundary.
                // The default is a no-op so production emits no duplicate log; the Composition Root
                // may wire this to a logger. The original exception is still rethrown to propagate.
                _reportError?.Invoke(CurrentError);
                throw;
            }
        }

        public void Shutdown()
        {
            ChangeState(BootstrapState.ShuttingDown);
            ChangeState(BootstrapState.Stopped);
        }

        public void MarkFailed()
        {
            ChangeState(BootstrapState.Failed);
        }

        private void BeginPhase(StartupPhase phase, string traceKey)
        {
            CurrentPhase = phase;
            _trace.Add(traceKey);
        }

        /// <summary>
        /// Builds the immutable <see cref="BootstrapError"/> for a failed startup. The category
        /// and source system are derived deterministically from the pipeline phase that failed,
        /// never by sniffing exception types. This keeps Task 46 boundary semantics local to the
        /// bootstrap and avoids replacing the detailed lower-level exceptions (ServiceRegistry,
        /// ServiceInitializer, SceneLoader, SceneTransitionSystem) with generic errors.
        /// </summary>
        private static BootstrapError BuildError(StartupPhase phase, Exception exception)
        {
            switch (phase)
            {
                case StartupPhase.InfrastructureReady:
                    return new BootstrapError(BootstrapErrorCategory.Configuration, phase, "Infrastructure", exception);
                case StartupPhase.ServicesRegistered:
                    return new BootstrapError(BootstrapErrorCategory.Service, phase, "RegisterServices", exception);
                case StartupPhase.ServicesInitialized:
                    return new BootstrapError(BootstrapErrorCategory.Service, phase, "InitializeServices", exception);
                case StartupPhase.InitialSceneLoaded:
                    return new BootstrapError(BootstrapErrorCategory.SceneLoading, phase, "LoadInitialScene", exception);
                default:
                    return new BootstrapError(BootstrapErrorCategory.Unexpected, phase, "GameBootstrap", exception);
            }
        }

        private void ChangeState(BootstrapState newState)
        {
            if (CurrentState == newState)
                throw new InvalidOperationException($"Bootstrap: duplicate state {newState}.");

            if (!AllowedTransitions.TryGetValue(CurrentState, out var allowed) ||
                !allowed.Contains(newState))
            {
                throw new InvalidOperationException(
                    $"Bootstrap: invalid transition {CurrentState} → {newState}.");
            }

            CurrentState = newState;
        }
    }
}
