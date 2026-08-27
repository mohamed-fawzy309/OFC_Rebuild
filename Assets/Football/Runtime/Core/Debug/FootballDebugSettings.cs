using UnityEngine;

namespace Football.Core
{
    /// <summary>
    /// CONFIGURATION for DEVELOPMENT/DEBUG observability (logging enablement, diagnostics,
    /// visualization, future debug overlay).
    ///
    /// This is OBSERVABILITY CONFIGURATION, NOT AUTHORITY. It must never control gameplay truth:
    /// - Gameplay produces state; Debug observes state.
    /// - No gameplay system may gate its behavior on these flags
    ///   (forbidden: <c>if (settings.EnableMovement) player.Move()</c>).
    /// - No authoritative game state (score, possession, scene, match time, player state, ball
    ///   state) is stored here. This is a ScriptableObject used as configuration, not a mutable
    ///   global runtime state container.
    ///
    /// SINGLETON (technical debt, documented):
    /// The static <see cref="Instance"/> is the project's ONE tolerated Singleton, retained as a
    /// DEBUG-ONLY technical exception. It is NOT approved for gameplay or runtime service
    /// architecture. It is the sole entry recognized by ArchitectureDependencyTests.KnownSingletonFiles
    /// and never controls gameplay, services, scene loading, match state, time, or input.
    ///
    /// Development vs release (Task 54 policy): the master switch <see cref="DebugEnabled"/> is
    /// OFF by default (safe in all contexts). <see cref="IsDevelopmentContext"/> reports whether the
    /// current build context is a development context (editor / development build). Debug settings
    /// are NOT a security boundary and are not stripped in release; categories are simply observed
    /// by debug infrastructure only in development contexts.
    /// </summary>
    [CreateAssetMenu(fileName = "FootballDebugSettings", menuName = "Football/Settings/Debug Settings")]
    public class FootballDebugSettings : ScriptableObject
    {
        [Header("Master")]
        [Tooltip("Master switch. When OFF, Debug infrastructure performs no work (no logging, no " +
                 "telemetry capture, no visualization, no expensive diagnostics). Safe default: OFF.")]
        public bool DebugEnabled;

        [Header("Debug Categories")]
        public bool EnableMovementDebug;
        public bool EnableBallDebug;
        public bool EnableAnimationDebug;
        public bool EnableMatchDebug;
        public bool EnableAIDebug;
        public bool EnableCameraDebug;
        public bool EnablePerformanceDebug;

        [Header("Visualization")]
        public bool ShowPlayerIDs;
        public bool ShowBallTrail;
        public bool ShowFieldBounds;
        public bool ShowPlayerStateLabels;

        /// <summary>
        /// True when the current build context is a development context (Unity Editor or a
        /// development build). Debug behaviour is relevant only here; it is never a security
        /// boundary. This is the single build-context directive in the module.
        /// </summary>
        public bool IsDevelopmentContext =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        /// <summary>
        /// Debug-only technical exception (see class docs). Not for use by gameplay/runtime service
        /// architecture. Isolated to debug infrastructure.
        /// </summary>
        public static FootballDebugSettings Instance { get; private set; }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
