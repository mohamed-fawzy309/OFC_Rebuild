namespace Football.Core
{
    public struct MatchEndedEvent : IGameEvent
    {
        public float Timestamp { get; set; }
        public int HomeGoals;
        public int AwayGoals;
    }
}
