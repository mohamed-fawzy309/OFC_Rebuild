using System.Collections.Generic;
using UnityEngine;

namespace Football.Data
{
    /// <summary>
    /// PlayerDefinition is the ONE unified authored/configuration model for a football player.
    ///
    /// It supports every player role (goalkeeper, defender, midfielder, attacker) through a single
    /// model — there is no separate GoalkeeperDefinition / OutfieldPlayerDefinition.
    ///
    /// PlayerDefinition is DATA, not gameplay. It must NOT:
    ///   - move the player, control input/animation/AI/camera/physics
    ///   - store runtime state (current position, velocity, stamina, state, possession)
    ///
    /// Structure (approved Master Player Data Architecture):
    ///   Identity        (PlayerId, Name, Nationality, ClubReference)
    ///   PhysicalProfile (Age, Height, Weight)
    ///   Profile         (PrimaryPosition, SecondaryPositions, PreferredFoot, WeakFoot,
    ///                    SkillRating, OverallRating, CardType, PlayStyle)
    ///   PlayerStats     (football attribute categories — detailed categories implemented in a
    ///                    later task; PlayerDefinition only carries the reference boundary here)
    ///
    /// A player is treated as a GOALKEEPER when PrimaryPosition == GK. Goalkeeping stats remain
    /// stored for every player because an outfield player may be assigned to goalkeeper later.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPlayerDefinition", menuName = "Football/Data/Player Definition")]
    public class PlayerDefinition : ScriptableObject
    {
        [Header("Identity")]
        public Identity Identity = new Identity();

        [Header("Physical Profile")]
        public PhysicalProfile Physical = new PhysicalProfile();

        [Header("Profile")]
        public Profile Profile = new Profile();

        [Header("Player Stats")]
        // Reference boundary for football attributes. Category detail is implemented in a later
        // Player Data task (PlayerStats); this task defines only the containment boundary.
        public PlayerStats PlayerStats = new PlayerStats();

        /// <summary>
        /// Player Data Integrity (Task 86). Composes the existing validation boundaries into a single
        /// integrity pass for THIS definition asset and returns actionable problem strings.
        ///
        /// Integrity DETECTS and REPORTS; it NEVER mutates (no ID/name/position/foot/skill/card/style
        /// replacement, no rating clamping, no fake references). It is DATA validation only — it does
        /// not move players, change stats, control AI/animation/physics/camera/match, and it does not
        /// create runtime state or a player database/registry.
        ///
        /// It COMPOSES, it does not duplicate:
        ///   - PlayerStats.GetInvalidRatings()      (Task 78, 1-99 scale)
        ///   - Profile.GetInvalidPositions()        (Task 79, position rules)
        ///   - Profile.GetInvalidProfileData()      (Tasks 80-82, foot/skill/PlayStyle/CardType)
        ///
        /// Tasks 86.1-86.4 owned here:
        ///   - Required groups: Identity / PhysicalProfile / Profile / PlayerStats (missing => reported,
        ///     never NullReferenceException).
        ///   - Required value: Identity.PlayerId must be non-empty and non-whitespace (86.3).
        ///   - Reference boundary: Identity.ClubReference must be a TeamDefinition asset reference
        ///     (null is tolerated until Team Data exists — no fake club is invented; enforcement of
        ///     non-null Club is DEFERRED to the Team task). Runtime Unity object references are rejected.
        ///
        /// Cross-definition PlayerId uniqueness is provided by the static
        /// <see cref="FindDuplicatePlayerIds(System.Collections.Generic.IEnumerable{string})"/> helper
        /// (a minimal pure-C# utility; no database/registry is created).
        /// </summary>
        public System.Collections.Generic.List<string> GetIntegrityProblems()
        {
            var problems = new System.Collections.Generic.List<string>();

            // 86.1 Required groups (detect missing data, never crash).
            if (Identity == null)
            {
                problems.Add("Identity is missing.");
            }
            if (Physical == null)
            {
                problems.Add("PhysicalProfile is missing.");
            }
            if (Profile == null)
            {
                problems.Add("Profile is missing.");
            }
            if (PlayerStats == null)
            {
                problems.Add("PlayerStats is missing.");
            }

            // 86.3 PlayerId: authored, stable, non-empty, non-whitespace.
            if (Identity != null)
            {
                if (string.IsNullOrEmpty(Identity.PlayerId))
                {
                    problems.Add("Identity.PlayerId is empty (a non-empty, authored PlayerId is required).");
                }
                else if (string.IsNullOrWhiteSpace(Identity.PlayerId))
                {
                    problems.Add("Identity.PlayerId is whitespace-only (a non-empty, authored PlayerId is required).");
                }
            }

            // 86.2 Reference boundary: ClubReference must be a TeamDefinition asset reference.
            // Null is tolerated (no fake club); a runtime Unity object is never a valid player-data
            // reference for this architecture.
            if (Identity != null && Identity.ClubReference != null && !(Identity.ClubReference is TeamDefinition))
            {
                problems.Add("Identity.ClubReference must reference a TeamDefinition asset, not a runtime Unity object.");
            }

            // 86.4 Composed validation from the existing boundaries.
            if (PlayerStats != null)
            {
                problems.AddRange(PlayerStats.GetInvalidRatings());
            }
            if (Profile != null)
            {
                problems.AddRange(Profile.GetInvalidPositions());
                problems.AddRange(Profile.GetInvalidProfileData());
            }

            return problems;
        }

        /// <summary>
        /// Minimal PlayerId uniqueness utility (Task 86.3). Given the authored PlayerId values of a
        /// set of PlayerDefinition assets, returns the list of IDs that appear MORE than once.
        ///
        /// The uniqueness scope is the calling boundary (e.g. the project's player-data catalog /
        /// asset set). This is a pure C# utility with no database, no registry, and no runtime
        /// manager. It never modifies any definition.
        /// </summary>
        public static System.Collections.Generic.List<string> FindDuplicatePlayerIds(
            System.Collections.Generic.IEnumerable<string> playerIds)
        {
            var duplicates = new System.Collections.Generic.List<string>();
            if (playerIds == null)
            {
                return duplicates;
            }

            var seen = new System.Collections.Generic.HashSet<string>();
            var reported = new System.Collections.Generic.HashSet<string>();
            foreach (var id in playerIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue; // empty/whitespace IDs are per-definition errors, not duplicates.
                }
                if (!seen.Add(id) && reported.Add(id))
                {
                    duplicates.Add(id);
                }
            }
            return duplicates;
        }
    }

    /// <summary>
    /// WHO the player is. Contains no gameplay, physical, or rating data.
    /// </summary>
    [System.Serializable]
    public class Identity
    {
        // Stable authored identity. Not an array index, not a runtime-generated random value.
        public string PlayerId;

        // Authored player name (single source of truth for display identity).
        public string Name;

        // Country representation. Proposed: ISO-alphanumeric country code string (no dedicated
        // Country system exists yet). A typed CountryId/enum is a DEFERRED refinement.
        public string Nationality;

        // Reference to the player's associated club (authored TeamDefinition), not an embedded
        // mutable copy and not a runtime MonoBehaviour. Team data is owned by a later task; this
        // defines the Club reference boundary only.
        public TeamDefinition ClubReference;
    }

    /// <summary>
    /// Physical/biographical measurements using real-world units (not 1-99 ratings).
    /// </summary>
    [System.Serializable]
    public class PhysicalProfile
    {
        public int Age;
        public float HeightCm; // stored in centimetres
        public float WeightKg; // stored in kilograms
    }

    /// <summary>
    /// Role / capability / classification data. These are NOT PlayerStats categories.
    /// </summary>
    [System.Serializable]
    public class Profile
    {
        // PreferredFoot/SkillRating profile-rating scale (Task 80). WeakFoot and SkillRating use
        // their OWN conceptual 1-5 scale — NOT the PlayerStats 1-99 rating scale.
        public const int WeakFootMin = 1;
        public const int WeakFootMax = 5;
        public const int WeakFootDefault = 3;
        public const int SkillRatingMin = 1;
        public const int SkillRatingMax = 5;
        public const int SkillRatingDefault = 3;

        // The player's primary role. PrimaryPosition == GK makes the primary stat profile
        // Goalkeeping; otherwise the primary profile is the outfield categories. Must be authored
        // explicitly per player; the default is an outfield position, never interpreted as
        // authoritative.
        public PlayerPosition PrimaryPosition = PlayerPosition.ST;

        public List<PlayerPosition> SecondaryPositions = new List<PlayerPosition>();

        // Naturally preferred foot (Right/Left) — NOT a numeric rating.
        public PreferredFoot PreferredFoot = PreferredFoot.Right;

        // Capability rating for the non-preferred foot (own scale 1-5). Not an outfield stat.
        // Explicit profile default (3 = neutral mid-capability) so a freshly-created profile is valid.
        public int WeakFoot = WeakFootDefault;

        // General ability to perform technical skill moves (own conceptual scale 1-5).
        // NOT renamed SkillMovesRating; NOT an outfield stat. Explicit profile default (3).
        public int SkillRating = SkillRatingDefault;

        // Overall player rating. Separate from SkillRating. DECISION (audit-pending): authored
        // directly for now; whether it should be derived from PlayerStats is a deferred decision.
        public int OverallRating;

        // Card classification data (presentation/data only; never gameplay authority). Approved set
        // (Task 82 revision) in the PlayerCardType enum. Basic is the explicit authored/default policy.
        public PlayerCardType CardType = PlayerCardType.Basic;

        // Behavioral/technical style reference (PROPOSED: single strongly-typed style; whether a
        // player may hold multiple styles is a deferred profile decision). Data only — never
        // executes gameplay. Approved set (Task 81 revision) in the PlayerPlayStyle enum. The
        // default Playmaker is an explicit authored/default policy so a fresh profile is valid.
        public PlayerPlayStyle PlayStyle = PlayerPlayStyle.Playmaker;

        /// <summary>
        /// Position validation (Task 79). Reports actionable problems for the authored position
        /// configuration: a PrimaryPosition cast to an undefined enum value, SecondaryPositions
        /// entries cast to undefined values, duplicate secondary positions, and a PrimaryPosition
        /// that is also listed in SecondaryPositions. This is DATA validation only — it never
        /// mutates authored data, never creates runtime position state, and does not implement any
        /// tactical/formation/role/AI/team system. It enforces structural rules only: GK may appear
        /// as a secondary position and an outfield position may be secondary to a GK without being
        /// prohibited here (goalkeeper-compatibility policy is a later task). Returns an empty list
        /// when the position configuration is valid.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidPositions()
        {
            var problems = new System.Collections.Generic.List<string>();
            var posType = typeof(PlayerPosition);

            if (!System.Enum.IsDefined(posType, PrimaryPosition))
            {
                problems.Add(string.Format(
                    "PrimaryPosition={0} is not a defined PlayerPosition.", PrimaryPosition));
            }

            if (SecondaryPositions != null)
            {
                var seen = new System.Collections.Generic.HashSet<PlayerPosition>();
                for (int i = 0; i < SecondaryPositions.Count; i++)
                {
                    var p = SecondaryPositions[i];
                    if (!System.Enum.IsDefined(posType, p))
                    {
                        problems.Add(string.Format(
                            "SecondaryPositions[{0}]={1} is not a defined PlayerPosition.", i, p));
                        continue;
                    }
                    if (p == PrimaryPosition)
                    {
                        problems.Add(string.Format(
                            "PrimaryPosition '{0}' must not be duplicated in SecondaryPositions.", p));
                    }
                    if (!seen.Add(p))
                    {
                        problems.Add(string.Format(
                            "Duplicate SecondaryPosition '{0}' appears multiple times.", p));
                    }
                }
            }

            return problems;
        }

        /// <summary>
        /// Profile foot/skill/play-style/card-type validation (Tasks 80, 81, 82). Reports actionable
        /// problems for the authored PreferredFoot / WeakFoot / SkillRating / PlayStyle / CardType
        /// data. PreferredFoot must resolve to a defined <c>PreferredFoot</c> value; WeakFoot and
        /// SkillRating must be within their OWN conceptual range [1..5] (NOT the PlayerStats 1-99
        /// scale); PlayStyle must resolve to a defined <c>PlayerPlayStyle</c> value (single-style,
        /// never silently reset); CardType must resolve to a defined <c>PlayerCardType</c> value
        /// (classification only, never silently reset). This is DATA validation only — it never
        /// mutates authored data, never clamps or replaces invalid values, never modifies PlayerStats
        /// or OverallRating, and implements no gameplay, animation, physics, or AI. Returns an empty
        /// list when the data is valid.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidProfileData()
        {
            var problems = new System.Collections.Generic.List<string>();

            if (!System.Enum.IsDefined(typeof(PreferredFoot), PreferredFoot))
            {
                problems.Add(string.Format(
                    "PreferredFoot={0} is not a defined PreferredFoot.", PreferredFoot));
            }

            if (WeakFoot < WeakFootMin || WeakFoot > WeakFootMax)
            {
                problems.Add(string.Format(
                    "WeakFoot={0} is outside the allowed range [{1}..{2}].", WeakFoot, WeakFootMin, WeakFootMax));
            }

            if (SkillRating < SkillRatingMin || SkillRating > SkillRatingMax)
            {
                problems.Add(string.Format(
                    "SkillRating={0} is outside the allowed range [{1}..{2}].", SkillRating, SkillRatingMin, SkillRatingMax));
            }

            if (!System.Enum.IsDefined(typeof(PlayerPlayStyle), PlayStyle))
            {
                problems.Add(string.Format(
                    "PlayStyle={0} is not a defined PlayerPlayStyle.", PlayStyle));
            }

            if (!System.Enum.IsDefined(typeof(PlayerCardType), CardType))
            {
                problems.Add(string.Format(
                    "CardType={0} is not a defined PlayerCardType.", CardType));
            }

            return problems;
        }
    }

    /// <summary>
    /// Football attribute container held by EVERY player (unified model).
    ///
    /// PlayerStats stores ONLY football rating data. It MUST NOT contain identity, physical,
    /// profile, or runtime state. It is data — it does not move/calculate/control anything.
    ///
    /// The seven strongly-typed categories are:
    ///   Pace / Shooting / Passing / Dribbling / Defending / Physical  (the six outfield categories)
    ///   Goalkeeping                                               (the seventh stored category)
    ///
    /// Goalkeeping data exists for every player (an outfield player may be assigned to GK later);
    /// it is never a reason to split into a separate goalkeeper model.
    ///
    /// Rating scale (audited, proposed): all football attributes use the SAME integer scale
    /// [RatingMin..RatingMax], default RatingDefault. WeakFoot / SkillRating live in Profile on
    /// their own conceptual scale and are NOT part of this scale. OverallRating is authored in
    /// Profile and is NOT derived from PlayerStats here.
    /// </summary>
    [System.Serializable]
    public class PlayerStats
    {
        public const int RatingMin = 1;
        public const int RatingMax = 99;
        public const int RatingDefault = 50;

        public PaceStats Pace = new PaceStats();
        public ShootingStats Shooting = new ShootingStats();
        public PassingStats Passing = new PassingStats();
        public DribblingStats Dribbling = new DribblingStats();
        public DefendingStats Defending = new DefendingStats();
        public PhysicalStats Physical = new PhysicalStats();
        public GoalkeepingStats Goalkeeping = new GoalkeepingStats();

        /// <summary>
        /// Configuration-integrity validation (Task 67.5 policy: detect invalid authored ratings;
        /// do not silently clamp or reject). Returns human-readable problem strings for every
        /// rating currently outside [RatingMin..RatingMax], or an empty list when valid.
        /// Validation only — it never mutates data and never derives gameplay.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidRatings()
        {
            var problems = new System.Collections.Generic.List<string>();
            ValidateCategory(Pace, "Pace", problems);
            ValidateCategory(Shooting, "Shooting", problems);
            ValidateCategory(Passing, "Passing", problems);
            ValidateCategory(Dribbling, "Dribbling", problems);
            ValidateCategory(Defending, "Defending", problems);
            ValidateCategory(Physical, "Physical", problems);
            ValidateCategory(Goalkeeping, "Goalkeeping", problems);
            return problems;
        }

        private static void ValidateCategory(object category, string categoryName,
            System.Collections.Generic.List<string> problems)
        {
            var fields = category.GetType().GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.FieldType != typeof(int))
                {
                    continue;
                }
                int value = (int)f.GetValue(category);
                if (value < RatingMin || value > RatingMax)
                {
                    problems.Add(string.Format(
                        "{0}.{1}={2} is outside the allowed rating range [{3}..{4}].",
                        categoryName, f.Name, value, RatingMin, RatingMax));
                }
            }
        }
    }

    /// <summary>Pace category (one of the six outfield categories).</summary>
    [System.Serializable]
    public class PaceStats
    {
        public int Acceleration = PlayerStats.RatingDefault;
        public int SprintSpeed = PlayerStats.RatingDefault;
    }

    /// <summary>Shooting category (six outfield category).</summary>
    [System.Serializable]
    public class ShootingStats
    {
        public int AttackingAwareness = PlayerStats.RatingDefault;
        public int Finishing = PlayerStats.RatingDefault;
        public int ShotPower = PlayerStats.RatingDefault;
        public int LongShots = PlayerStats.RatingDefault;
        public int Volleys = PlayerStats.RatingDefault;
        public int Penalties = PlayerStats.RatingDefault;
    }

    /// <summary>Passing category (six outfield category). Curve belongs here.</summary>
    [System.Serializable]
    public class PassingStats
    {
        public int Vision = PlayerStats.RatingDefault;
        public int ShortPassing = PlayerStats.RatingDefault;
        public int LongPassing = PlayerStats.RatingDefault;
        public int Crossing = PlayerStats.RatingDefault;
        public int FreeKickAccuracy = PlayerStats.RatingDefault;
        public int Curve = PlayerStats.RatingDefault;
    }

    /// <summary>Dribbling category (six outfield category). TightPossession belongs here.</summary>
    [System.Serializable]
    public class DribblingStats
    {
        public int Dribbling = PlayerStats.RatingDefault;
        public int BallControl = PlayerStats.RatingDefault;
        public int TightPossession = PlayerStats.RatingDefault;
        public int Agility = PlayerStats.RatingDefault;
        public int Balance = PlayerStats.RatingDefault;
        public int Reactions = PlayerStats.RatingDefault;
    }

    /// <summary>Defending category (six outfield category).</summary>
    [System.Serializable]
    public class DefendingStats
    {
        public int DefensiveAwareness = PlayerStats.RatingDefault;
        public int Interceptions = PlayerStats.RatingDefault;
        public int StandingTackle = PlayerStats.RatingDefault;
        public int SlidingTackle = PlayerStats.RatingDefault;
        public int Heading = PlayerStats.RatingDefault;
    }

    /// <summary>Physical category (six outfield category).</summary>
    [System.Serializable]
    public class PhysicalStats
    {
        public int Strength = PlayerStats.RatingDefault;
        public int Stamina = PlayerStats.RatingDefault;
        public int Jumping = PlayerStats.RatingDefault;
        public int Aggression = PlayerStats.RatingDefault;
    }

    /// <summary>Goalkeeping category (seventh stored category for EVERY player).</summary>
    [System.Serializable]
    public class GoalkeepingStats
    {
        public int Diving = PlayerStats.RatingDefault;
        public int Handling = PlayerStats.RatingDefault;
        public int Kicking = PlayerStats.RatingDefault;
        public int Positioning = PlayerStats.RatingDefault;
        public int Reflexes = PlayerStats.RatingDefault;
        public int Parrying = PlayerStats.RatingDefault;
    }

    /// <summary>
    /// Player positions. PROPOSED value set (Subject to audit — rule 37.4) but the unified
    /// goalkeeper signal (GK) is authoritative: PrimaryPosition == GK selects the goalkeeper
    /// profile.
    /// </summary>
    public enum PlayerPosition
    {
        GK,
        CB,
        LB,
        RB,
        CDM,
        CM,
        CAM,
        LM,
        RM,
        LW,
        RW,
        SS,
        ST
    }

    /// <summary>
    /// Naturally preferred foot. Not a numeric rating.
    /// </summary>
    public enum PreferredFoot
    {
        Right,
        Left
    }

    /// <summary>
    /// Card classification (presentation/data only; never gameplay authority).
    ///
    /// APPROVED value set (Task 82 revision — replaces the earlier Base/Rare/Special list) with
    /// EXACTLY 8 classification labels: Basic, Iconic, Legend, Ultimate, Prime, Form, Signature,
    /// Elite. These are classification categories ONLY — they describe no player ability directly
    /// and apply NO automatic stat/OverallRating/PlayStyle/Position/WeakFoot/SkillRating modifiers.
    ///
    /// Default policy (authored, explicit): the Profile field default is PlayerCardType.Basic (the
    /// enum's zero value AND the authored field initializer) — a freshly-created definition is
    /// therefore valid. No other CardType is a runtime/active/current state.
    ///
    /// Ownership: belongs ONLY to PlayerDefinition.Profile.CardType. It is NOT an identifier /
    /// player ID; a player keeps ONE PlayerId regardless of CardType. It is NOT on Identity /
    /// PhysicalProfile / PlayerStats / TeamDefinition. Extensible for future explicit decisions;
    /// no speculative card types are added now.
    /// </summary>
    public enum PlayerCardType
    {
        Basic,
        Iconic,
        Legend,
        Ultimate,
        Prime,
        Form,
        Signature,
        Elite
    }

    /// <summary>
    /// Behavioral/technical PLAYING STYLE / PROFILE (data only; never executes gameplay).
    ///
    /// APPROVED value set (Task 81 revision — replaces the earlier AllRounder/TargetMan/Creative/
    /// BallWinner/Sweeper/Specialist list). Categorized into Attack / Midfield / Defense-Build-up /
    /// Goalkeeper. The enum order is categorical documentation, NOT a priority or ranking.
    ///
    /// Semantics:
    ///   - A PlayStyle is descriptive player data. It does NOT determine PrimaryPosition,
    ///     SecondaryPositions, PlayerStats, OverallRating, CardType, WeakFoot, or SkillRating.
    ///   - NO automatic PlayStyle→Position compatibility rules exist in Task 81.
    ///   - Goalkeepers use the SAME field with the two GK styles below; no separate GK style type.
    ///   - It never drives AI/animation/physics/movement/passing/shooting/dribbling and applies no
    ///     stat modifiers. Future gameplay systems may READ it and interpret it.
    ///
    /// Default policy (authored, explicit): the Profile field default is PlayerPlayStyle.Playmaker
    /// (see Profile.PlayStyle initializer) so a freshly-created definition validates cleanly. The
    /// enum's first / language-zero member (Poacher) is NOT treated as an implied gameplay default;
    /// the authored field initializer is the single source of the default. This enum stays
    /// extensible; new styles are added only through an explicit architecture decision.
    /// </summary>
    public enum PlayerPlayStyle
    {
        // ATTACK
        Poacher,
        False9,
        FreeRoamer,
        Winger,
        BoxStriker,
        VersatileStriker,

        // MIDFIELD
        AnchorMan,
        Destroyer,
        BoxToBox,
        Orchestrator,
        HolePlayer,
        Playmaker,

        // DEFENSE / BUILD-UP
        BuildUp,
        BallWinner,
        ModernDefender,
        AttackingFullBack,
        DefensiveFullBack,
        InsideFullBack,

        // GOALKEEPER
        BallPlayingKeeper,
        ClassicKeeper
    }
}
