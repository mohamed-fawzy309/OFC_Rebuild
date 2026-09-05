using UnityEngine;
using Football.Data;

namespace Football.Players
{
    /// <summary>
    /// The minimal runtime identity binding of a Player Entity.
    ///
    /// Task 139 — Player Identity. A runtime Player GameObject is an "entity": the object that
    /// exists in the scene/match and will later carry gameplay components (movement, state, input,
    /// ball interaction). An entity is NOT the same as the authored PlayerDefinition (data) and is
    /// NOT the same as runtime mutable state.
    ///
    /// This component answers ONLY: "WHICH authored PlayerDefinition does this runtime player
    /// represent?" It holds a single serialized reference to the authoritative
    /// Football.Data.PlayerDefinition (the sole source of authored player identity: PlayerId, Name,
    /// Nationality, ClubReference).
    ///
    /// It deliberately does NOT:
    ///   - duplicate any PlayerDefinition.Identity field (no PlayerId/Name/Nationality copies)
    ///   - create a Player manager / registry / database / global lookup
    ///   - hold or create runtime state (position, velocity, stamina, current state, possession,
    ///     input, AI/replay/network, animation state)
    ///   - implement any gameplay behaviour (no movement, state machine, stats binding,
    ///     CharacterController, ball interaction, animation, input consumption, AI, replay, network)
    ///   - generate any identity at runtime
    ///
    /// The authored string PlayerDefinition.Identity.PlayerId is distinct from the int PlayerId used
    /// by some event structs; this component does not reconcile or bridge the two (documented as a
    /// deferred decision in Architecture.md Task 139).
    /// </summary>
    public class PlayerEntity : MonoBehaviour
    {
        [Tooltip("The authoritative authored player definition this runtime player represents.")]
        [SerializeField] private PlayerDefinition _definition;

        /// <summary>
        /// The authored player definition represented by this runtime player entity, or null when
        /// it has not been assigned yet.
        /// </summary>
        public PlayerDefinition Definition => _definition;

        /// <summary>
        /// Assigns the authored player definition this runtime player represents. This is an
        /// authoring/config-time binding (e.g. instantiating a player for a specific squad slot);
        /// it stores only a reference and performs no gameplay, no duplication of identity data,
        /// and no runtime state creation.
        /// </summary>
        public void AssignDefinition(PlayerDefinition definition)
        {
            _definition = definition;
        }
    }
}
