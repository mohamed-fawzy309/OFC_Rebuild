namespace Football.Core
{
    /// <summary>
    /// Raised when a player begins an action (pass, shoot, tackle, etc.).
    /// Published by the player action system.
    /// </summary>
    public readonly struct PlayerActionStartedEvent : IGameEvent
    {
        public float Timestamp { get; }
        public int PlayerId { get; }
        public string ActionName { get; }

        public PlayerActionStartedEvent(float timestamp, int playerId, string actionName)
        {
            Timestamp = timestamp;
            PlayerId = playerId;
            ActionName = actionName;
        }
    }
}
