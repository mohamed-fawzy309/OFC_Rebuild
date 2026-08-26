using UnityEngine;

namespace Football.Data
{
    [CreateAssetMenu(fileName = "NewPlayerDefinition", menuName = "Football/Data/Player Definition")]
    public class PlayerDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string PlayerName;
        public int JerseyNumber;
        public string Position;

        [Header("Appearance")]
        public GameObject ModelPrefab;
        public Material KitMaterial;

        [Header("Base Attributes")]
        public float BaseSpeed = 5f;
        public float BaseSprintSpeed = 8f;
        public float BaseAcceleration = 10f;
        public float BaseStamina = 100f;
        public float BaseStrength = 50f;
        public float BaseAgility = 50f;

        [Header("Ball Skills")]
        public float Dribbling = 50f;
        public float Passing = 50f;
        public float Shooting = 50f;
        public float BallControl = 50f;

        [Header("Defensive")]
        public float Tackling = 50f;
        public float Interception = 50f;

        [Header("Goalkeeper")]
        public bool IsGoalkeeper;
        public float GKReflexes;
        public float GKPositioning;
        public float GKDiving;
    }
}
