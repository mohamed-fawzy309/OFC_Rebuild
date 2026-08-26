namespace Football.Core
{
    /// <summary>
    /// Marker contract for all gameplay events.
    /// Events represent meaningful occurrences that have already happened.
    /// Timestamp is simulation time (caller-provided), not wall-clock time.
    /// </summary>
    public interface IGameEvent
    {
        /// <summary>
        /// Simulation time when this event occurred, in seconds.
        /// Set by the publisher at event creation time.
        /// </summary>
        float Timestamp { get; }
    }
}
