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
        public bool Interact { get; private set; }
        public bool Cancel { get; private set; }
        public bool Pause { get; private set; }
        public bool IsEnabled { get; set; } = true;

        private void Update()
        {
            if (!IsEnabled) return;

            MoveDirection = new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));
            Sprint = UnityEngine.Input.GetKey(KeyCode.LeftShift);
            Pass = UnityEngine.Input.GetButtonDown("Fire2");
            Shoot = UnityEngine.Input.GetButtonDown("Fire1");
            Tackle = UnityEngine.Input.GetKeyDown(KeyCode.E);
            Interact = UnityEngine.Input.GetKeyDown(KeyCode.Space);
            Cancel = UnityEngine.Input.GetButtonDown("Cancel");
            Pause = UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        }
    }
}
