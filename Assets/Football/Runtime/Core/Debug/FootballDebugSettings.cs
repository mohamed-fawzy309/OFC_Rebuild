using UnityEngine;

namespace Football.Core
{
    [CreateAssetMenu(fileName = "FootballDebugSettings", menuName = "Football/Settings/Debug Settings")]
    public class FootballDebugSettings : ScriptableObject
    {
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
