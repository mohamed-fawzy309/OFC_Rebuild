namespace Football.Core
{
    /// <summary>
    /// Raised when a player kicks the ball. Published by the ball/action authority.
    /// Direction and force are float components to keep Core Unity-free.
    /// Presentation systems (Camera, VFX) may use these values to derive visual effects.
    /// </summary>
    public readonly struct BallKickedEvent : IGameEvent
    {
        public float Timestamp { get; }
        public int PlayerId { get; }
        public float KickDirectionX { get; }
        public float KickDirectionY { get; }
        public float KickDirectionZ { get; }
        public float KickForce { get; }

        public BallKickedEvent(float timestamp, int playerId, float kickDirectionX, float kickDirectionY, float kickDirectionZ, float kickForce)
        {
            Timestamp = timestamp;
            PlayerId = playerId;
            KickDirectionX = kickDirectionX;
            KickDirectionY = kickDirectionY;
            KickDirectionZ = kickDirectionZ;
            KickForce = kickForce;
        }
    }
}
