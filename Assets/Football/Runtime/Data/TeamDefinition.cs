using UnityEngine;

namespace Football.Data
{
    [CreateAssetMenu(fileName = "NewTeamDefinition", menuName = "Football/Data/Team Definition")]
    public class TeamDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string TeamName;
        public string ShortName;
        public int TeamId;

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
    }
}
