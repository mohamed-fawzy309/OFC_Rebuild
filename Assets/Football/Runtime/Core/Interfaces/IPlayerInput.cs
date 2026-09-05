using UnityEngine;

namespace Football.Core
{
    public interface IPlayerInput
    {
        Vector2 MoveDirection { get; }
        Vector2 LookDirection { get; }
        bool Sprint { get; }
        bool Pass { get; }
        bool Shoot { get; }
        bool Tackle { get; }
        bool SwitchPlayer { get; }
        Vector2 SwitchDirection { get; }
        bool Skill { get; }
        Vector2 SkillDirection { get; }
        bool Interact { get; }
        bool Cancel { get; }
        bool Pause { get; }
        bool IsEnabled { get; set; }
    }
}
