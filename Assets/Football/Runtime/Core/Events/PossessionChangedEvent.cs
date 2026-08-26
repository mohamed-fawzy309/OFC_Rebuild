namespace Football.Core
{
    public struct PossessionChangedEvent : IGameEvent
    {
        public float Timestamp { get; set; }
        public int PreviousTeamId;
        public int NewTeamId;
        public int PlayerId;
    }
}
