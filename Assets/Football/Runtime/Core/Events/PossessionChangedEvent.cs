namespace Football.Core
{
    /// <summary>
    /// Raised when ball possession changes between teams. Published by the possession authority.
    /// </summary>
    public readonly struct PossessionChangedEvent : IGameEvent
    {
        public float Timestamp { get; }
        public int PreviousTeamId { get; }
        public int NewTeamId { get; }
        public int PlayerId { get; }

        public PossessionChangedEvent(float timestamp, int previousTeamId, int newTeamId, int playerId)
        {
            Timestamp = timestamp;
            PreviousTeamId = previousTeamId;
            NewTeamId = newTeamId;
            PlayerId = playerId;
        }
    }
}
