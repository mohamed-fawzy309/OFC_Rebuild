using UnityEngine;

namespace Football.Data
{
    [CreateAssetMenu(fileName = "NewMovementConfig", menuName = "Football/Data/Movement Config")]
    public class MovementConfig : ScriptableObject
    {
        [Header("Speeds")]
        public float WalkSpeed = 2f;
        public float RunSpeed = 5f;
        public float SprintSpeed = 8f;
        public float BackpedalSpeed = 2.5f;

        [Header("Acceleration")]
        public float Acceleration = 10f;
        public float Deceleration = 15f;
        public float SprintAcceleration = 12f;

        [Header("Rotation")]
        public float TurnSpeed = 720f;
        public float SprintTurnSpeed = 540f;

        [Header("Physics")]
        public float GroundCheckDistance = 0.1f;
        public LayerMask GroundLayer;

        [Header("Stamina")]
        public float MaxStamina = 100f;
        public float StaminaDrainPerSecondSprint = 15f;
        public float StaminaRegenPerSecondIdle = 20f;
        public float StaminaRegenPerSecondWalk = 10f;
        public float StaminaRegenPerSecondRun = 5f;
        public float MinStaminaForSprint = 10f;
    }
}
