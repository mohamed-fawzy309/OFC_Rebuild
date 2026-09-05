using UnityEngine;

namespace Football.Core
{
    /// <summary>
    /// Device-neutral snapshot of gameplay-relevant player input for a single
    /// sampling/update point.
    ///
    /// InputFrame is PURE DATA. It must not contain device objects, provider
    /// references, gameplay logic, buffering, history, remapping, sensitivity,
    /// dead-zone, priority, or simultaneous-input policy. Those responsibilities
    /// are owned elsewhere (providers feed data in; later tasks own the policies).
    /// Gameplay must consume input exclusively through this value.
    /// </summary>
    public readonly struct InputFrame
    {
        /// <summary>Continuous directional movement input (analog).</summary>
        public Vector2 MoveDirection { get; }

        /// <summary>Continuous directional look/aim input (analog).</summary>
        public Vector2 LookDirection { get; }

        /// <summary>Continuous held sprint state.</summary>
        public bool Sprint { get; }

        /// <summary>One-shot pass command for this sampling point.</summary>
        public bool Pass { get; }

        /// <summary>One-shot shoot command for this sampling point.</summary>
        public bool Shoot { get; }

        /// <summary>One-shot tackle command for this sampling point.</summary>
        public bool Tackle { get; }

        /// <summary>
        /// One-shot player-switch request. A switch without direction means
        /// "request smart defensive player selection"; with a non-zero
        /// <see cref="SwitchDirection"/> it means "request directional player
        /// selection". This carries the REQUEST only — which player is finally
        /// selected is a separate gameplay responsibility.
        /// </summary>
        public bool SwitchPlayer { get; }

        /// <summary>
        /// Device-neutral directional intent for a player-switch request
        /// (device-independent; Mobile swipe/drag and Controller D-Pad/analog are
        /// translated by the provider into this value). <see cref="Vector2.zero"/>
        /// means no direction / smart defensive selection.
        /// </summary>
        public Vector2 SwitchDirection { get; }

        /// <summary>
        /// One-shot skill request. A skill request together with
        /// <see cref="SkillDirection"/> represents "perform a technical football
        /// skill move in this direction." The direction is player intent only —
        /// it does not identify which specific skill move is executed; that is
        /// a gameplay responsibility.
        /// </summary>
        public bool Skill { get; }

        /// <summary>
        /// Device-neutral directional intent for a skill request
        /// (device-independent; Controller stick/dpad and Mobile gesture are
        /// translated by the provider into this value). <see cref="Vector2.zero"/>
        /// means no directional intent.
        /// </summary>
        public Vector2 SkillDirection { get; }

        /// <summary>One-shot interact command for this sampling point.</summary>
        public bool Interact { get; }

        /// <summary>One-shot cancel command for this sampling point.</summary>
        public bool Cancel { get; }

        /// <summary>One-shot system pause/resume command for this sampling point.</summary>
        public bool Pause { get; }

        public InputFrame(
            Vector2 moveDirection,
            Vector2 lookDirection,
            bool sprint,
            bool pass,
            bool shoot,
            bool tackle,
            bool switchPlayer,
            Vector2 switchDirection,
            bool skill,
            Vector2 skillDirection,
            bool interact,
            bool cancel,
            bool pause)
        {
            MoveDirection = moveDirection;
            LookDirection = lookDirection;
            Sprint = sprint;
            Pass = pass;
            Shoot = shoot;
            Tackle = tackle;
            SwitchPlayer = switchPlayer;
            SwitchDirection = switchDirection;
            Skill = skill;
            SkillDirection = skillDirection;
            Interact = interact;
            Cancel = cancel;
            Pause = pause;
        }
    }
}