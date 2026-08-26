using UnityEngine;

namespace Football.Core
{
    public struct GoalScoredEvent : IGameEvent
    {
        public float Timestamp { get; set; }
        public int ScoringTeamId;
        public int ScoringPlayerId;
        public Vector3 BallPosition;
    }
}
