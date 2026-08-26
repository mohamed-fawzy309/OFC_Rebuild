namespace Football.Core
{
    public struct PlayerActionStartedEvent : IGameEvent
    {
        public float Timestamp { get; set; }
        public int PlayerId;
        public string ActionName;
    }
}
