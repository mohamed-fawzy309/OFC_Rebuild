using UnityEngine;

namespace Football.Input
{
    public class HumanPlayerInput : MonoBehaviour, Core.IPlayerInput
    {
        [SerializeField] private int _playerIndex;

        public Vector2 MoveDirection { get; private set; }
        public Vector2 LookDirection { get; private set; }
        public bool Sprint { get; private set; }
        public bool Pass { get; private set; }
        public bool Shoot { get; private set; }
        public bool Tackle { get; private set; }
        public bool SwitchPlayer { get; private set; }
        public Vector2 SwitchDirection { get; private set; }
        public bool Skill { get; private set; }
        public Vector2 SkillDirection { get; private set; }
        public bool Interact { get; private set; }
        public bool Cancel { get; private set; }
        public bool Pause { get; private set; }
        public bool IsEnabled { get; set; } = true;

        private void Update()
        {
            if (!IsEnabled) return;

            // Task 127 (Controller Support): all actions are read through the legacy
            // Input Manager named axes, so each action carries BOTH its keyboard binding
            // (positiveButton) and its generic gamepad/joystick binding (altPositiveButton)
            // defined in ProjectSettings/InputManager.asset. This keeps the legacy
            // UnityEngine.Input framework authoritative for keyboard AND controller
            // without introducing a second input framework.
            MoveDirection = new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));
            Sprint = UnityEngine.Input.GetButton("Sprint");
            Pass = UnityEngine.Input.GetButtonDown("Fire2");
            Shoot = UnityEngine.Input.GetButtonDown("Fire1");
            Tackle = UnityEngine.Input.GetButtonDown("Tackle");
            Interact = UnityEngine.Input.GetButtonDown("Interact");
            Cancel = UnityEngine.Input.GetButtonDown("Cancel");
            Pause = UnityEngine.Input.GetButtonDown("Pause");
        }
    }
}
