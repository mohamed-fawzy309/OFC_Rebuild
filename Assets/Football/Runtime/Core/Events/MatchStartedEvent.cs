namespace Football.Core
{
    /// <summary>
    /// Raised when a match begins. Published by the match system.
    /// </summary>
    public readonly struct MatchStartedEvent : IGameEvent
    {
        public float Timestamp { get; }
        public int HomeTeamId { get; }
        public int AwayTeamId { get; }

        public MatchStartedEvent(float timestamp, int homeTeamId, int awayTeamId)
        {
            Timestamp = timestamp;
            HomeTeamId = homeTeamId;
            AwayTeamId = awayTeamId;
        }
    }
}
