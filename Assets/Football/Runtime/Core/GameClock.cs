using System;

namespace Football.Core
{
    /// <summary>
    /// The lifecycle state of the <see cref="GameClock"/>.
    ///
    /// Deliberately minimal: there is no <c>Paused</c> state. Pause-aware time is owned by a
    /// later task (Task 51); for the current foundation a stopped clock is sufficient — a clock
    /// that is not advancing is simply <see cref="Stopped"/>. This keeps the state model small
    /// and does not couple match time to application/menu pause.
    /// </summary>
    public enum GameClockState
    {
        Stopped,
        Running
    }

    /// <summary>
    /// A deterministic clock representing FOOTBALL MATCH TIME.
    ///
    /// It answers "how much match time has elapsed", and is deliberately independent of:
    /// - real wall-clock time (no DateTime / DateTimeOffset / Stopwatch)
    /// - Unity Time (Time.time, Time.deltaTime, Time.realtimeSinceStartup)
    /// - Unity time scale (Time.timeScale is NOT match-clock authority)
    /// - frame count and per-frame lifecycle (no Update/FixedUpdate/LateUpdate)
    ///
    /// The clock advances ONLY when explicitly told to via <see cref="Advance"/>. It does not
    /// source its own delta — an owner (later the simulation-time abstraction) supplies the
    /// elapsed seconds. This keeps the clock deterministic, testable, and replay-compatible.
    ///
    /// The single source of truth for match time is <see cref="ElapsedSeconds"/> (an immutable,
    /// read-only accumulation in double seconds). No derived minutes/seconds/display-string is
    /// stored as authoritative state; formatting is a presentation concern owned later.
    ///
    /// The clock does NOT own score, player state, ball physics, the game state machine, scene
    /// loading, pause UI, input, animation, audio, or player movement. It is a match-time source,
    /// not a gameplay manager. Halves and stoppage time are match-state concerns: the clock can
    /// simply be stopped at a boundary and resumed, without a special second clock.
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>The configured length of regulation time in seconds. Injected configuration, NOT clock state.</summary>
        public double RegulationDurationSeconds { get; }

        /// <summary>The current lifecycle state of the clock.</summary>
        public GameClockState State { get; private set; } = GameClockState.Stopped;

        /// <summary>The authoritative elapsed match time, in seconds. Read-only; never settable externally.</summary>
        public double ElapsedSeconds { get; private set; }

        /// <summary>True while the clock is advancing match time.</summary>
        public bool IsRunning => State == GameClockState.Running;

        /// <summary>
        /// Creates a match clock.
        /// </summary>
        /// <param name="regulationDurationSeconds">The configured regulation length in seconds
        /// (e.g. half-duration x number of halves, supplied by the owner from match rules). The
        /// clock carries this as read-only configuration; it does not auto-stop or enforce it —
        /// higher-level match logic interprets it. Non-negative.</param>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="regulationDurationSeconds"/> is negative.</exception>
        public GameClock(double regulationDurationSeconds = 0)
        {
            if (regulationDurationSeconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(regulationDurationSeconds),
                    "Regulation duration must be non-negative.");

            RegulationDurationSeconds = regulationDurationSeconds;
        }

        /// <summary>
        /// Starts the clock.
        /// Safe no-op when already running, so repeated Starts are deterministic.
        /// Stopped → Running.
        /// </summary>
        public void Start()
        {
            if (State == GameClockState.Running)
                return; // already running: deterministic no-op
            State = GameClockState.Running;
        }

        /// <summary>
        /// Stops the clock (e.g. at a half-time boundary or match stop).
        /// Safe no-op when already stopped, so repeated Stops are deterministic.
        /// Running → Stopped.
        /// </summary>
        public void Stop()
        {
            if (State == GameClockState.Stopped)
                return; // already stopped: deterministic no-op
            State = GameClockState.Stopped;
        }

        /// <summary>
        /// Resets elapsed match time to zero and returns the clock to the Stopped state.
        /// Valid from any state. The clock remains restorable via <see cref="Start"/>.
        /// </summary>
        public void Reset()
        {
            ElapsedSeconds = 0;
            State = GameClockState.Stopped;
        }

        /// <summary>
        /// Advances match time by the given number of seconds. The clock advances ONLY while
        /// <see cref="Running"/> and ONLY for non-negative deltas.
        /// </summary>
        /// <param name="deltaSeconds">Positive elapsed-seconds delta supplied by the owner.
        /// Zero is a no-op. Negative is rejected (no silent backwards-simulation).</param>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="deltaSeconds"/> is negative.</exception>
        public void Advance(double deltaSeconds)
        {
            if (deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds),
                    "Match time cannot advance by a negative delta.");

            if (deltaSeconds == 0 || State == GameClockState.Stopped)
                return; // zero delta no-op, or a stopped clock does not advance

            ElapsedSeconds += deltaSeconds;
        }
    }
}
