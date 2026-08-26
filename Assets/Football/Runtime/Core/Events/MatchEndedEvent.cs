namespace Football.Core
{
    /// <summary>
    /// Raised when a match ends. Published by the match system.
    /// </summary>
    public readonly struct MatchEndedEvent : IGameEvent
    {
        public float Timestamp { get; }
        public int HomeGoals { get; }
        public int AwayGoals { get; }

        public MatchEndedEvent(float timestamp, int homeGoals, int awayGoals)
        {
            Timestamp = timestamp;
            HomeGoals = homeGoals;
            AwayGoals = awayGoals;
        }
    }
}
