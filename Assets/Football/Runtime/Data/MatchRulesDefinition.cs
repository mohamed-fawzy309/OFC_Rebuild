using UnityEngine;

namespace Football.Data
{
    [CreateAssetMenu(fileName = "NewMatchRules", menuName = "Football/Data/Match Rules Definition")]
    public class MatchRulesDefinition : ScriptableObject
    {
        [Header("Match Duration")]
        public float HalfDurationSeconds = 2700f;
        public int NumHalves = 2;
        public bool UseExtraTime;
        public float ExtraTimeDurationSeconds = 1800f;
        public bool UsePenaltyShootout;

        /// <summary>
        /// The authoritative MATCH DURATION (regulation, in seconds), DERIVED as
        /// HalfDurationSeconds x NumHalves (Task 98). This is a read-only COMPUTED EXPRESSION over
        /// the authored HalfDurationSeconds and NumHalves fields — it is NOT an authored field, so
        /// there is no separate/duplicated MatchDuration source to keep consistent. It is not
        /// runtime state and not a clock; it is the configured regulation length and is the intended
        /// source for GameClock.RegulationDurationSeconds (injected read-only double, runtime owner).
        /// Returns a double to match that feed boundary. When HalfDurationSeconds or NumHalves are
        /// invalid (non-positive), MatchDurationSeconds is likewise invalid and GetInvalidMatchRulesData
        /// reports it via those two authored fields.
        /// </summary>
        public double MatchDurationSeconds => (double)HalfDurationSeconds * NumHalves;

        [Header("Field")]
        public float FieldLength = 105f;
        public float FieldWidth = 68f;
        public float GoalWidth = 7.32f;
        public float GoalHeight = 2.44f;
        public float GoalDepth = 2f;
        public float CenterCircleRadius = 9.15f;
        public float PenaltyAreaLength = 16.5f;
        public float PenaltyAreaWidth = 40.3f;
        public float GoalAreaLength = 5.5f;
        public float GoalAreaWidth = 18.32f;

        [Header("Substitutions")]
        public int MaxSubstitutions = 5;

        [Header("Offside")]
        public bool EnforceOffside = true;

        [Header("Fouls")]
        public bool EnforceFouls = true;

        [Header("Match Cards")]
        public bool EnforceMatchCards = true;

        /// <summary>
        /// Match Rules validation (Task 97). MatchRulesDefinition is the canonical AUTHORED MATCH
        /// CONFIGURATION owner (match rule data), distinct from GameClock (the runtime elapsed-time
        /// authority) and distinct from runtime match state (current half/score/cards/etc. which must
        /// never live in this class). This validation DETECTS + REPORTS actionable problems in the
        /// authored container-level fields retained by Task 97: the number of halves, half duration,
        /// extra-time duration when extra time is enabled, the substitution limit, and the field
        /// dimensions. Container-rule semantics that are defined by later tasks (explicit match
        /// duration representation 98/99, extra-time periods 100, penalty rules 101, offside mode 102,
        /// foul category/mode taxonomy 103, match cards 104, substitution windows 105, restart types
        /// 106) remain OUTSIDE this method and are deliberately NOT pre-empted here. This is DATA validation only
        /// — it NEVER mutates authored data, never clamps a duration, never resets a boolean, never
        /// fabricates a default, and never writes to any field. Returns an empty list when valid.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidMatchRulesData()
        {
            var problems = new System.Collections.Generic.List<string>();

            if (NumHalves < 1)
            {
                problems.Add("NumHalves must be at least 1 (a match needs at least one half).");
            }

            if (HalfDurationSeconds <= 0f)
            {
                problems.Add("HalfDurationSeconds must be positive (an authored half must have duration > 0).");
            }

            if (UseExtraTime && ExtraTimeDurationSeconds <= 0f)
            {
                problems.Add("UseExtraTime is enabled but ExtraTimeDurationSeconds is not positive (extra-time duration must be > 0 when extra time is used).");
            }

            if (MaxSubstitutions < 0)
            {
                problems.Add("MaxSubstitutions must not be negative (0 is a valid authored 'no substitutions' limit).");
            }

            ValidateFieldDimension(FieldLength, "FieldLength", problems);
            ValidateFieldDimension(FieldWidth, "FieldWidth", problems);
            ValidateFieldDimension(GoalWidth, "GoalWidth", problems);
            ValidateFieldDimension(GoalHeight, "GoalHeight", problems);
            ValidateFieldDimension(GoalDepth, "GoalDepth", problems);
            ValidateFieldDimension(CenterCircleRadius, "CenterCircleRadius", problems);
            ValidateFieldDimension(PenaltyAreaLength, "PenaltyAreaLength", problems);
            ValidateFieldDimension(PenaltyAreaWidth, "PenaltyAreaWidth", problems);
            ValidateFieldDimension(GoalAreaLength, "GoalAreaLength", problems);
            ValidateFieldDimension(GoalAreaWidth, "GoalAreaWidth", problems);

            return problems;
        }

        private void ValidateFieldDimension(float value, string name,
            System.Collections.Generic.List<string> problems)
        {
            if (value <= 0f)
            {
                problems.Add($"{name} must be positive (an authored field dimension must be > 0).");
            }
        }
    }

    /// <summary>
    /// Match disciplinary card type (Task 104). The strongly typed Match CARD representation, owned
    /// by the Match Rules (authored rule data) — NOT player profile card classification.
    ///
    /// APPROVED value set: Yellow, SecondYellow, Red — exactly the three match disciplinary card
    /// types. These are CARD REPRESENTATION values (the disciplinary result of an offence), wholly
    /// separate from <see cref="PlayerCardType"/> (the player PROFILE classification: Basic, Iconic,
    /// Legend, Ultimate, Prime, Form, Signature, Elite — Player Data, Task 82).
    ///
    /// SecondYellow is represented as an INDEPENDENT card type (a distinct authored card) here. The
    /// RUNTIME question of whether two Yellow cards derive a dismissal/red ("second yellow -> red")
    /// is PLAYER/MATCH RUNTIME disciplinary STATE, NOT this rule data — it is out of scope for this
    /// data-only task and is not modelled as runtime state here.
    ///
    /// This type is classification/rule data ONLY. It describes the card; it does NOT assign cards,
    /// escalate them, dismiss players, run referee behavior, detect fouls, play animations, or drive
    /// UI/AI. No CardSystem/CardManager/DisciplinarySystem is created (Task 104 is data only).
    ///
    /// MatchRulesDefinition is the authoritative authored owner (via <c>EnforceMatchCards</c> plus
    /// this card type vocabulary). Current booking/dismissal state (CurrentYellowCards,
    /// CurrentRedCards, PlayerBookings, PlayerDismissed, CardHistory) is runtime match state and is
    /// deliberately NOT placed on MatchRulesDefinition.
    /// </summary>
    public enum MatchCardType
    {
        Yellow,
        SecondYellow,
        Red
    }

    /// <summary>
    /// Match restart type (Task 106). The strongly typed MATCH RESTART taxonomy, owned by the Match
    /// Rules (authored rule data).
    ///
    /// APPROVED value set: KickOff, ThrowIn, GoalKick, CornerKick, FreeKick, PenaltyKick, DropBall —
    /// the seven restart classifications established by the project (see the anticipated restart set
    /// referenced across Tasks 102/103/104 boundary tests). THIS is a restart CLASSIFICATION
    /// (how play resumes after a stoppage / goal / out-of-bounds), distinct from:
    ///   - <c>GameStateId</c> (Football.Core): runtime MATCH/GAME STATE. <c>GameStateId.KickOff</c>
    ///     is a game-state value in the phase flow (Boot → ... → KickOff → Playing → ...); it is
    ///     NOT a restart taxonomy and is neither replaced nor merged. A restart type (e.g.
    ///     <c>MatchRestartType.KickOff</c>) may share a name with a game state, but the
    ///     responsibilities are distinct: game-state = what state the match machine is in;
    ///     restart-type = how play resumes.
    ///   - <c>UsePenaltyShootout</c> (Task 101): a MATCH/COMPETITION rule about what happens after
    ///     regulation/extra time. <c>PenaltyKick</c> here is a RESTART TYPE (a contested free kick
    ///     on goal). These are NOT merged, and shootout duration/count/rules are NOT duplicated here.
    ///   - Player Data: <c>PlayerStats.Passing.FreeKickAccuracy</c> (Task 70) is a PLAYER skill
    ///     rating, not match restart data.
    ///
    /// Task 106 is data architecture ONLY: restart EXECUTION (ball placement, player positioning,
    /// kick taker selection, free-kick/corner/throw-in/penalty-kick/kickoff execution), referee
    /// behavior, foul outcome gameplay, physics (ball velocity/force/position/trajectory), UI and AI
    /// are all OUT OF SCOPE. No RestartSystem/RestartManager/KickoffSystem/ThrowInSystem/GoalKickSystem/
    /// CornerKickSystem/FreeKickSystem/PenaltyKickSystem/DropBallSystem is created. MatchRulesDefinition
    /// holds NO runtime restart state (CurrentRestart/ActiveRestart/PendingRestart/LastRestart/
    /// RestartState/RestartTimer/RestartExecutor/CurrentRestartPlayer/CurrentRestartPosition).
    /// </summary>
    public enum MatchRestartType
    {
        KickOff,
        ThrowIn,
        GoalKick,
        CornerKick,
        FreeKick,
        PenaltyKick,
        DropBall
    }
}
