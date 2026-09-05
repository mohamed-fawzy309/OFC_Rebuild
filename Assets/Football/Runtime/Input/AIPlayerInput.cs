using UnityEngine;

namespace Football.Input
{
    public class AIPlayerInput : MonoBehaviour, Core.IPlayerInput
    {
        public Vector2 MoveDirection { get; set; }
        public Vector2 LookDirection { get; set; }
        public bool Sprint { get; set; }
        public bool Pass { get; set; }
        public bool Shoot { get; set; }
        public bool Tackle { get; set; }
        public bool SwitchPlayer { get; set; }
        public Vector2 SwitchDirection { get; set; }
        public bool Skill { get; set; }
        public Vector2 SkillDirection { get; set; }
        public bool Interact { get; set; }
        public bool Cancel { get; set; }
        public bool Pause { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
