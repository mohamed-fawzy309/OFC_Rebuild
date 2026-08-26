namespace Football.Core
{
    /// <summary>
    /// Raised when a player action completes. Published by the player action system.
    /// </summary>
    public readonly struct PlayerActionFinishedEvent : IGameEvent
    {
        public float Timestamp { get; }
        public int PlayerId { get; }
        public string ActionName { get; }
        public bool WasSuccessful { get; }

        public PlayerActionFinishedEvent(float timestamp, int playerId, string actionName, bool wasSuccessful)
        {
            Timestamp = timestamp;
            PlayerId = playerId;
            ActionName = actionName;
            WasSuccessful = wasSuccessful;
        }
    }
}
