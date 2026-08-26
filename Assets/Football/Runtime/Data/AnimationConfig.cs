using UnityEngine;

namespace Football.Data
{
    [CreateAssetMenu(fileName = "NewAnimationConfig", menuName = "Football/Data/Animation Config")]
    public class AnimationConfig : ScriptableObject
    {
        [Header("Locomotion")]
        public float LocomotionBlendTime = 0.15f;
        public float TurnThreshold = 15f;

        [Header("Ball Control")]
        public float DribbleBlendTime = 0.1f;
        public float KickBlendTime = 0.1f;

        [Header("Actions")]
        public float TackleBlendTime = 0.1f;
        public float CelebrationBlendTime = 0.2f;

        [Header("Match")]
        public float TransitionSpeed = 10f;
    }
}
