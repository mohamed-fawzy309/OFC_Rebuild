using UnityEngine;

namespace Football.Data
{
    [CreateAssetMenu(fileName = "NewTeamDefinition", menuName = "Football/Data/Team Definition")]
    public class TeamDefinition : ScriptableObject
    {
        [Header("Identity")]
        // Authored display name (human-readable identity). NOT the stable unique key.
        public string TeamName;
        // Authored short display name (preserved pre-existing team identity data).
        public string ShortName;
        // Stable authored team identity. NOT an array index, NOT a Unity instance ID, NOT a
        // runtime-generated value, and independent of TeamName and runtime object identity.
        public string TeamId;

        [Header("Appearance")]
        public Material HomeKitMaterial;
        public Material AwayKitMaterial;
        public Color PrimaryColor;
        public Color SecondaryColor;

        [Header("Formation")]
        public PlayerDefinition[] Starters;
        public PlayerDefinition[] Substitutes;
        public string Formation = "4-4-2";

        [Header("Tactics")]
        public float Aggression = 50f;
        public float PossessionPreference = 50f;
        public float DefensiveLine = 50f;
        public float Pressing = 50f;

        /// <summary>
        /// Team Identity validation (Task 90). Reports actionable problems for the authored team
        /// identity: empty/whitespace TeamId and empty/whitespace TeamName. This is DATA validation
        /// only — it never mutates authored data, never generates a TeamId, never renames a team,
        /// and never assigns a fake country/identity. Team Identity validation lives here (team-data
        /// integrity owner), NOT in PlayerDefinition and NOT via PlayerStats.GetInvalidRatings().
        /// Returns an empty list when the team identity is valid.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidTeamIdentity()
        {
            var problems = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(TeamId))
            {
                problems.Add(string.IsNullOrEmpty(TeamId)
                    ? "TeamId is empty."
                    : "TeamId is whitespace.");
            }

            if (string.IsNullOrWhiteSpace(TeamName))
            {
                problems.Add(string.IsNullOrEmpty(TeamName)
                    ? "TeamName is empty."
                    : "TeamName is whitespace.");
            }

            return problems;
        }

        /// <summary>
        /// Detects duplicate team identities within the calling boundary (e.g. the project's
        /// team-data catalog / asset set). A minimal explicit utility — no database, no registry,
        /// no runtime manager. It never modifies any definition. Empty/whitespace TeamId values are
        /// excluded (they are reported as per-definition errors by GetInvalidTeamIdentity, not as
        /// duplicates).
        /// </summary>
        public static System.Collections.Generic.List<string> FindDuplicateTeamIds(
            System.Collections.Generic.IEnumerable<TeamDefinition> teams)
        {
            var duplicates = new System.Collections.Generic.List<string>();
            if (teams == null)
            {
                return duplicates;
            }

            var seen = new System.Collections.Generic.HashSet<string>();
            var reported = new System.Collections.Generic.HashSet<string>();
            foreach (var team in teams)
            {
                if (team == null || string.IsNullOrWhiteSpace(team.TeamId))
                {
                    continue; // null / empty / whitespace TeamId are per-definition errors.
                }
                if (!seen.Add(team.TeamId) && reported.Add(team.TeamId))
                {
                    duplicates.Add(team.TeamId);
                }
            }
            return duplicates;
        }

        /// <summary>
        /// Squad membership validation (Task 92). Reports actionable problems for the authored squad:
        /// null PlayerDefinition references, duplicate references within Starters, duplicate references
        /// within Substitutes, and a player appearing in both Starters and Substitutes. This is DATA
        /// validation only — it never mutates the squad, never removes a reference, never fabricates or
        /// copies a PlayerDefinition, and never reorders membership. Squad validation lives here
        /// (team-data integrity owner), NOT in PlayerDefinition / PlayerStats / Profile / Identity.
        /// Returns an empty list when the squad is valid.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidSquadData()
        {
            var problems = new System.Collections.Generic.List<string>();
            DetectSquadListProblems(Starters, "Starters", problems);
            DetectSquadListProblems(Substitutes, "Substitutes", problems);

            // Overlap: a player must not appear in both Starters and Substitutes at the same time.
            if (Starters != null && Substitutes != null)
            {
                foreach (var starter in Starters)
                {
                    if (starter == null)
                    {
                        continue; // null starters are reported per-list above.
                    }
                    if (System.Array.IndexOf(Substitutes, starter) >= 0)
                    {
                        problems.Add($"Player '{PlayerDisplayId(starter)}' appears in both Starters and Substitutes.");
                    }
                }
            }
            return problems;
        }

        private void DetectSquadListProblems(PlayerDefinition[] list, string listName,
            System.Collections.Generic.List<string> problems)
        {
            if (list == null)
            {
                return;
            }
            var seen = new System.Collections.Generic.HashSet<PlayerDefinition>();
            var reported = new System.Collections.Generic.HashSet<PlayerDefinition>();
            for (var i = 0; i < list.Length; i++)
            {
                var player = list[i];
                if (player == null)
                {
                    problems.Add($"{listName} contains a null PlayerDefinition reference at index {i}.");
                    continue;
                }
                if (!seen.Add(player) && reported.Add(player))
                {
                    problems.Add($"Player '{PlayerDisplayId(player)}' appears multiple times in {listName}.");
                }
            }
        }

        private static string PlayerDisplayId(PlayerDefinition player)
        {
            var pid = player != null && player.Identity != null ? player.Identity.PlayerId : null;
            return string.IsNullOrWhiteSpace(pid) ? "(no PlayerId)" : pid;
        }

        /// <summary>
        /// Formation validation (Task 93). Reports an actionable problem when the authored formation
        /// identifier is empty/whitespace (a TeamDefinition that must have a formation). Formation is
        /// an authored TEAM SHAPE identifier; this validation DETECTS + REPORTS and never mutates — it
        /// does not auto-fill a formation, does not generate positions, does not assign players, and
        /// does not modify the Squad or PlayerDefinition position data. Lives on TeamDefinition
        /// (team-data integrity owner). Returns an empty list when the formation is valid.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidFormationData()
        {
            var problems = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(Formation))
            {
                problems.Add(string.IsNullOrEmpty(Formation)
                    ? "Formation is empty (an authored formation identifier is required)."
                    : "Formation is whitespace-only (an authored formation identifier is required).");
            }
            return problems;
        }

        /// <summary>
        /// Tactics validation (Task 94). Tactics are AUTHORED TEAM behavioral-preference data
        /// (Aggression / PossessionPreference / DefensiveLine / Pressing) owned by TeamDefinition.
        /// No established numeric range exists for these fields — they use the established float
        /// representation with the established default of 50f and their ranges are POLICY / DEFERRED
        /// (NOT PlayerStats 1-99, NOT Auto-validated). Because no invalid authored value is currently
        /// established, this boundary DETECTS + REPORTS nothing today and never mutates; it exists to
        /// document the single authoritative validation owner (team-data, NOT PlayerDefinition /
        /// PlayerStats / Player Profile / Formation / Squad / Team Ratings / Home-Away). Returns an
        /// empty list, consistent with the DEFERRED range policy.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidTacticsData()
        {
            // Tactics ranges are POLICY/DEFERRED (no established numeric range), so no authored
            // value is currently invalid. This method is deliberately empty: it owns the validation
            // boundary location without inventing arbitrary numeric ranges. It never mutates tactics.
            return new System.Collections.Generic.List<string>();
        }

        /// <summary>
        /// Team Ratings validation (Task 95). Team Ratings are TEAM-LEVEL authored rating data owned
        /// by TeamDefinition. NO rating representation/range/default is currently established in the
        /// project (audit found no existing team rating fields, and no derivation formula exists), so
        /// it is marked POLICY / DEFERRED rather than invented. In particular Team Ratings are NOT
        /// PlayerStats (1-99) and are NOT derived from the Squad/PlayerStats (DERIVATION = DEFERRED).
        /// Because no authored representation exists, there is no invalid authored value today; this
        /// boundary DETECTS + REPORTS nothing and never mutates. It owns the single authoritative
        /// validation location (team-data, NOT PlayerDefinition / PlayerStats / Squad / Formation /
        /// Tactics / Team Identity / Team Colors / Home-Away). Returns an empty list.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidTeamRatingsData()
        {
            // Team Rating representation is DEFERRED, so no authored value is currently invalid.
            // This method owns the validation boundary location without inventing a rating scale or
            // a derivation formula. It never mutates ratings.
            return new System.Collections.Generic.List<string>();
        }

        /// <summary>
        /// Home/Away configuration validation (Task 96). The authored Home/Away configuration on
        /// TeamDefinition is the kit appearance Material references (HomeKitMaterial / AwayKitMaterial),
        /// reused unchanged from the existing Appearance block. This validation DETECTS + REPORTS null
        /// Material references (a TeamDefinition that must present a home and away kit). It never
        /// fabricates a Material, never auto-creates a kit asset, never replaces an invalid reference,
        /// and never touches Renderer/runtime kit visuals. Returns an empty list when valid.
        /// </summary>
        public System.Collections.Generic.List<string> GetInvalidHomeAwayData()
        {
            var problems = new System.Collections.Generic.List<string>();
            if (HomeKitMaterial == null)
            {
                problems.Add("HomeKitMaterial is not assigned (an authored home kit Material is required).");
            }
            if (AwayKitMaterial == null)
            {
                problems.Add("AwayKitMaterial is not assigned (an authored away kit Material is required).");
            }
            return problems;
        }
    }
}
