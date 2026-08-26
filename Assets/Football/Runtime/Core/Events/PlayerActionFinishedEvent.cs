namespace Football.Core
{
    public struct PlayerActionFinishedEvent : IGameEvent
    {
        public float Timestamp { get; set; }
        public int PlayerId;
        public string ActionName;
        public bool WasSuccessful;
    }
}
