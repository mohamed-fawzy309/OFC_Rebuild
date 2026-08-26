using UnityEngine;

namespace Football.Data
{
    [CreateAssetMenu(fileName = "NewBallConfig", menuName = "Football/Data/Ball Config")]
    public class BallConfig : ScriptableObject
    {
        [Header("Physics")]
        public float Mass = 0.43f;
        public float Radius = 0.11f;
        public float Drag = 0.1f;
        public float AngularDrag = 0.05f;
        public float Bounciness = 0.6f;

        [Header("Movement")]
        public float MaxSpeed = 35f;
        public float Friction = 0.4f;
        public float RollingFriction = 0.02f;
        public float AirResistance = 0.1f;

        [Header("Interaction")]
        public float KickForce = 20f;
        public float PassForce = 12f;
        public float ChipForce = 15f;
        public float DribbleStickDistance = 0.5f;
    }
}
