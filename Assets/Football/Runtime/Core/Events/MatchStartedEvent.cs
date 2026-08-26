namespace Football.Core
{
    public struct MatchStartedEvent : IGameEvent
    {
        public float Timestamp { get; set; }
        public int HomeTeamId;
        public int AwayTeamId;
    }
}
