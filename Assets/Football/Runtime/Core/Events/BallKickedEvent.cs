using UnityEngine;

namespace Football.Core
{
    public struct BallKickedEvent : IGameEvent
    {
        public float Timestamp { get; set; }
        public int PlayerId;
        public Vector3 KickDirection;
        public float KickForce;
    }
}
