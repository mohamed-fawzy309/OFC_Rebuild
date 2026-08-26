using UnityEngine;

namespace Football.Data
{
    [CreateAssetMenu(fileName = "NewDribbleConfig", menuName = "Football/Data/Dribble Config")]
    public class DribbleConfig : ScriptableObject
    {
        [Header("Control")]
        public float BallStickDistance = 0.5f;
        public float BallStickHeight = 0.15f;
        public float BallControlRadius = 0.8f;

        [Header("Dribble Speed")]
        public float MaxDribbleSpeed = 6f;
        public float DribbleAcceleration = 8f;
        public float SpeedPenaltyAtHighDribble = 0.7f;

        [Header("Foot Alternation")]
        public float FootSwitchInterval = 0.3f;
        public float FootSwitchAngle = 30f;

        [Header("Ball Touch")]
        public float TouchForce = 2f;
        public float TouchInterval = 0.15f;
    }
}
