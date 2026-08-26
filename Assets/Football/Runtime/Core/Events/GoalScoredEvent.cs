namespace Football.Core
{
    /// <summary>
    /// Raised when a goal is scored. Published by the match/goal system.
    /// </summary>
    public readonly struct GoalScoredEvent : IGameEvent
    {
        public float Timestamp { get; }
        public int ScoringTeamId { get; }
        public int ScoringPlayerId { get; }

        public GoalScoredEvent(float timestamp, int scoringTeamId, int scoringPlayerId)
        {
            Timestamp = timestamp;
            ScoringTeamId = scoringTeamId;
            ScoringPlayerId = scoringPlayerId;
        }
    }
}
