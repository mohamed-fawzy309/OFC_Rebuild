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

        [Header("Ball")]
        public float BallRadius = 0.11f;
        public float BallMass = 0.43f;
        public float GravityMultiplier = 1f;

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
    }
}
