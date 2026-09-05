# Architecture — Input System

## Task 117 — InputFrame

Status: IMPLEMENTED (PASS).

Task 117 established `Football.Core.InputFrame` as the **device-neutral gameplay
input snapshot** — a pure-data value representing gameplay-relevant input for a
single sampling/update point.

### Location

- `Football.Core.InputFrame` — `Assets/Football/Runtime/Core/InputFrame.cs`
  (namespace `Football.Core`, assembly `Football.Core`)

### Ownership boundary

| Layer | Responsibility |
|---|---|
| Device | Reads raw/device-specific state only (legacy `UnityEngine.Input`/`KeyCode` in `HumanPlayerInput`). |
| Input Provider | Converts device or external source into the gameplay input contract; owns sampling cadence and the `IsEnabled` enable gate. |
| IPlayerInput | Living read-side input contract (device-neutral); remains the authoritative contract. |
| InputFrame | Device-neutral snapshot **value** of gameplay-relevant input for one sampling point; pure data; immutable. |
| Gameplay | Consumes device-neutral input only (`InputFrame` / `IPlayerInput`), never devices. |

### Relationship with IPlayerInput

- `IPlayerInput` stays the authoritative contract (unchanged). `InputFrame` is the
  value representation of the same gameplay input subset.
- `InputFrame` contains exactly the gameplay-relevant contract values:
  `MoveDirection`, `LookDirection` (continuous/analog), `Sprint` (held), and the
  one-shot commands `Pass`, `Shoot`, `Tackle`, `Interact`, `Cancel`, `Pause`.
- `IsEnabled` is provider/lifecycle state and is intentionally NOT in `InputFrame`.

### Device-neutrality rule

`InputFrame` contains only value data (`Vector2`, `bool`). It must never contain
device objects, the Input System (no `Keyboard`/`Gamepad`/`Mouse`/`InputAction`/
`InputControl`), provider references, or gameplay logic.

### Provider responsibility

Providers own converting devices/external sources into the contract values.
Task 117 did NOT rewrite providers (no gameplay consumer exists yet) — each
provider's existing values already map 1:1 onto `InputFrame`.

### Gameplay responsibility

Gameplay consumes device-neutral input only. No gameplay consumer exists yet
(FACT from audit); none was manufactured by Task 117.

### What Task 117 intentionally does NOT handle

Buffering, remapping, dead-zone/sensitivity processing, input priority,
simultaneous-input policy, controller/keyboard implementation, AI/replay/network
runtimes, and gameplay action systems.

### Deferred future responsibilities

- Button buffering — Task 132
- Input priority — Task 133
- Simultaneous-input handling — Task 134
- Remapping / sensitivity / dead zones — later input tasks
- Provider → InputFrame wiring when a consumer is introduced

### Tests / verification

- Focused: `Football.Tests.EditMode.InputFrameTests` (9 tests) — PASS.
- Full EditMode suite: 1564/1564 PASS.
- Old project (`D:\Projects\unity\ofc`) untouched. No commit/push/tag performed.

---

## Task 118 — Move Input

Status: VERIFIED (KEEP) — PASS.

Task 118 established (by audit + verification, NOT by new infrastructure) the
device-independent representation and handling of MOVEMENT INPUT, up to but not
including movement gameplay.

### Movement input authority

- **Authoritative provider contract:** `IPlayerInput.MoveDirection`
  (`UnityEngine.Vector2 { get; }`, `Football.Core`).
- **Authoritative snapshot representation:** `InputFrame.MoveDirection`
  (`Vector2 { get; }`, `Football.Core`) — identical type and meaning.
- **Single authority rule:** there is exactly ONE movement-input vector and ONE
  meaning. No `MoveInput*` / `MovementVector` / `MoveDirectionSource` /
  `MovementInput*` abstraction exists; Task 118 verified and locked this with
  tests (`MoveInputContractTests`).

### MoveDirection semantics

- **Type:** `UnityEngine.Vector2`. (FACT)
- **Meaning:** axis pair <X = Horizontal, Y = Vertical> from the provider (raw
  analog direction intent). Per-axis raw range `[-1,1]` (raw axis values); NOT
  normalized — diagonal raw magnitude may exceed 1. (FACT/INFERENCE)
- **Source:** `HumanPlayerInput.Update()` produces it from legacy
  `UnityEngine.Input.GetAxisRaw("Horizontal"/"Vertical")`; AI/Network/Replay
  providers expose a settable `MoveDirection` (externally injected). (FACT)
- **Coordinate space:** NOT world/local/camera-relative in the input contract —
  no transform exists at the input layer. Interpreting the direction into world
  space is the future gameplay layer's responsibility. (FACT/UNKNOWN)
- **Processing:** none — raw passthrough. No normalization, dead zone,
  sensitivity, smoothing, filtering, remapping, buffering, or priority exists at
  the input layer (verified by test). (FACT)

### Relationship to IPlayerInput / InputFrame

```
Device / external source
      ↓
Provider (Human reads device; AI/Network/Replay inject)
      ↓
IPlayerInput.MoveDirection        (authoritative provider contract)
      ↓  (value, same type)
InputFrame.MoveDirection          (authoritative device-neutral snapshot)
      ↓
Future movement gameplay consumer (not yet implemented)
```

- `IPlayerInput` remains the authoritative provider contract (unchanged).
- `InputFrame.MoveDirection` is the device-neutral snapshot of the same value
  (unchanged from Task 117).
- Provider → `InputFrame` snapshot wiring remains deferred until a gameplay
  consumer exists (Task 117 rationale unchanged).

### Ownership matrix

| Owner | Responsibility |
|---|---|
| Device | Raw device reads only (`HumanPlayerInput`). |
| Provider | Produce/convert movement input into contract semantics; own `IsEnabled`. |
| IPlayerInput | Authoritative read-side movement contract. |
| InputFrame | Device-neutral movement snapshot value. |
| Gameplay (future) | Consume device-neutral `MoveDirection`; interpret to world space; apply `MovementConfig` tuning. |

### Device-neutrality boundary

Movement input is represented as player intent (a `Vector2`), never as device
objects. `IPlayerInput`/`InputFrame` contain no `Keyboard`/`Gamepad`/`Mouse`/
`InputAction`/`InputControl`/`KeyCode`/`UnityEngine.Input`. Device reads are
legitimate ONLY inside `HumanPlayerInput` (provider layer) — verified by
`MoveInputContractTests.MoveInputContract_ContainsNoDeviceDependencies`.

### What Task 118 does NOT implement

- Movement gameplay: speed, acceleration, deceleration, turning, sprint
  mechanics, stamina, locomotion, player movement controller, physics movement,
  animation/root motion, movement state machine.
- Sprint input — **deferred to Task 119** (verified by test).
- Controller/keyboard support, dead zones, sensitivity, remapping, buffering,
  input priority, simultaneous-input resolution.
- No duplicate movement-input authority, no fake gameplay consumer, no
  speculative infrastructure.

### Future movement gameplay boundary

The eventual gameplay layer consumes only the device-neutral contract
(`IPlayerInput.MoveDirection` / `InputFrame.MoveDirection`) and must remain
device-independent. MovementConfig (Task 107) is the gameplay tuning owner and
stays distinct from the input contract.

### Tests / verification

- Focused: `Football.Tests.EditMode.MoveInputContractTests` (8 tests) — PASS
  (single authority, contract coherence, provider parity, device-neutrality, no
  processing abstractions, Sprint deferred, no movement gameplay).
- InputFrame regression: `Football.Tests.EditMode.InputFrameTests` (9 tests) — PASS.
- Full EditMode suite: 1572/1572 PASS.
- Old project (`D:\Projects\unity\ofc`) untouched. No commit/push/tag performed.

---

## Task 119 — Sprint Input

Status: VERIFIED (KEEP) — PASS.

Task 119 established (by audit + verification, NOT by new infrastructure) the
device-independent representation and ownership of SPRINT INPUT as a gameplay
input contract — the intent/state signal only. Sprint MECHANICS are out of scope.

### Sprint input authority

- **Authoritative provider contract:** `IPlayerInput.Sprint`
  (`bool { get; }`, `Football.Core`).
- **Authoritative snapshot representation:** `InputFrame.Sprint`
  (`bool { get; }`, `Football.Core`) — identical type and meaning.
- **Single authority rule:** exactly ONE Sprint input vector-of-meaning exists
  (the `bool` intent signal). No `SprintInput*` / `SprintCommand` /
  `SprintReader` / `SprintSource` abstraction exists; verified and locked by
  tests (`SprintInputContractTests`).

### Sprint input semantics

- **Type:** `bool`. (FACT)
- **Meaning:** continuous HELD player intent — "player is holding sprint".
  NOT one-shot, NOT edge-triggered. (FACT: produced with `Input.GetKey`, not
  `GetKeyDown`.)
- **Source:** `HumanPlayerInput.Update()` reads it every frame from legacy
  `UnityEngine.Input.GetKey(KeyCode.LeftShift)`; AI/Network/Replay inject
  externally. (FACT)
- **Default:** `false`. No filtering/normalization/transformation. (FACT)
- **Intent vs gameplay state:** the contract carries INPUT INTENT only; it does
  NOT represent gameplay state such as "player is currently sprinting"
  (`IsSprinting`/stamina/speed are excluded from the contract). (FACT)

### Device mapping

The `LeftShift` mapping is provider-level code inside `HumanPlayerInput`
(legitimate — the provider owns device reading). The value that flows through
`IPlayerInput`/`InputFrame` is a device-neutral `bool`. No controller/keyboard
mapping system exists or was added. (FACT)

### Relationship to IPlayerInput / InputFrame

```
Device / External Source
      ↓
Provider (Human: GetKey(LeftShift); AI/Network/Replay inject)
      ↓
IPlayerInput.Sprint          (authoritative provider contract, bool)
      ↓  (value)
InputFrame.Sprint            (authoritative device-neutral snapshot field)
      ↓
Future Sprint/Movement gameplay consumer (not yet implemented)
```

### Ownership matrix

| Owner | Responsibility |
|---|---|
| Device | Raw device reads only (`HumanPlayerInput`). |
| Provider | Produce/convert Sprint as held intent into the contract; own `IsEnabled`. |
| IPlayerInput | Authoritative read-side Sprint contract. |
| InputFrame | Device-neutral Sprint snapshot field. |
| Gameplay (future) | Consume the `bool` intent and apply `MovementConfig` sprint/stamina TUNING (mechanics). |

`IsEnabled` remains provider/lifecycle state (gates provider sampling), excluded
from `InputFrame`.

### Sprint input vs sprint gameplay boundary

Sprint INPUT = "player is holding sprint" (`bool`, exists now).
Sprint GAMEPLAY = speed/acceleration/stamina consumption/regeneration/movement
state (does NOT exist; belongs to future gameplay tasks). `MovementConfig`
sprint/stamina floats are authored gameplay tuning, not input, and were not moved.

### What Task 119 intentionally does NOT implement

- Sprint mechanics: sprint speed, sprint acceleration, stamina consumption,
  stamina regeneration, movement state changes, locomotion, animation, physics,
  speed multipliers, cooldowns, sprint gameplay rules.
- Sprint gameplay system (none exists; verified by test).
- Controller/keyboard support, dead zones, sensitivity, remapping, buffering
  (Task 132), priority (Task 133), simultaneous-input resolution (Task 134).
- Pass Input — **deferred to Task 120** (not implemented).
- No duplicate Sprint authority, no fake gameplay consumer, no speculative
  infrastructure.

### Tests / verification

- Focused: `Football.Tests.EditMode.SprintInputContractTests` (7 tests) — PASS
  (contract shape, coherence, provider parity + default false, single authority,
  no buffering/edge/history, no gameplay metadata, no gameplay system).
- Input race regression: `InputFrameTests` 9/9, `MoveInputContractTests` 8/8 — PASS.
- Full EditMode suite: 1579/1579 PASS.
- Old project (`D:\Projects\unity\ofc`) untouched. No commit/push/tag performed.

### Remaining deferrals (unchanged)

- Provider → InputFrame snapshot wiring (until a gameplay consumer exists).
- Sprint gameplay mechanics; Pass Input (Task 120); buffering (132); priority
  (133); simultaneous-input (134); remapping/sensitivity/dead zones.

---

## Task 120 — Pass Input

Status: VERIFIED (KEEP) — PASS.

Task 120 established (by audit + verification, NOT by new infrastructure) the
correct representation, semantics, ownership, and device-neutral contract of
PASS INPUT — the transient INPUT COMMAND only. The passing gameplay mechanic is
out of scope and does not exist.

### Pass input authority

- **Authoritative provider contract:** `IPlayerInput.Pass`
  (`bool { get; }`, `Football.Core`).
- **Authoritative snapshot representation:** `InputFrame.Pass`
  (`bool { get; }`, `Football.Core`) — identical type and meaning.
- **Single authority rule:** exactly ONE Pass input meaning exists (the one-shot
  command pulse). No `PassInput*` / `PassCommand` / `PassRequest` / `PassReader`
  abstraction exists; verified and locked by tests (`PassInputContractTests`).

### Pass semantics

- **Type:** `bool`. (FACT)
- **Meaning:** TRANSIENT one-shot command pulse — "player requested a pass".
  NOT held state, NOT continuous. A pulse is expected on the frame the command
  is issued, then `false`. (FACT: produced with `Input.GetButtonDown`, the
  button-down edge. `GetButton`/`GetKey` would be held — not used.)
- **Source:** `HumanPlayerInput.Update()` reads it every frame via legacy
  `UnityEngine.Input.GetButtonDown("Fire2")`; AI/Network/Replay inject
  externally. (FACT)
- **Default:** `false`. No filtering/normalization/transformation. (FACT)
- **Intent vs gameplay:** the contract carries INPUT INTENT only. It NEVER
  represents "ball is being passed"/"pass succeeded"/target/force/type/state/
  animation/receiver — no such metadata exists in the contract. (FACT)
- **Provider note:** all four providers are STRUCTURALLY compatible (same `bool`
  on one interface, default false). Non-Human providers expose a settable `bool`;
  whether an external writer issues one-shot pulses is not provable from code
  (UNKNOWN — temporal behavior depends on the writer).

### Device mapping

Pass is read via a legacy Unity button (`"Fire2"`) inside `HumanPlayerInput`
(provider layer — legitimate). The value flowing through `IPlayerInput`/
`InputFrame` is a device-neutral `bool`. No controller/keyboard mapping system
exists or was added. (FACT; exact physical binding = Unity Input Manager default,
UNKNOWN.)

### Relationship to IPlayerInput / InputFrame

```
Device / External Source
      ↓
Provider (Human: GetButtonDown("Fire2"); AI/Network/Replay inject)
      ↓
IPlayerInput.Pass             (authoritative provider contract, transient bool)
      ↓  (value)
InputFrame.Pass               (authoritative device-neutral snapshot field)
      ↓
Future gameplay consumer (not yet implemented)
```

### Input vs gameplay boundary

PASS INPUT = "player requested a pass" (`bool`, exists now).
PASS GAMEPLAY = force/power, targeting, direction calculation, accuracy, type
selection, animation, ball launch, ball physics, receiver selection, assisted/
through-/lobbed-/ground-pass mechanics, cooldown/recovery (does NOT exist;
verified by `PassingTests` Task 70 audit). `PassingStats` (player capability
ratings) is player data, distinct from input.

### Ownership matrix

| Owner | Responsibility |
|---|---|
| Device | Raw device reads only (`HumanPlayerInput`). |
| Provider | Produce/convert Pass as a one-shot intent into the contract; own `IsEnabled`. |
| IPlayerInput | Authoritative read-side Pass contract. |
| InputFrame | Device-neutral Pass snapshot field. |
| Gameplay (future) | Consume the one-shot intent when implementing passing mechanics. |

`IsEnabled` remains provider/lifecycle state (gates provider sampling), excluded
from `InputFrame`.

### What Task 120 intentionally does NOT implement

- Passing gameplay: force, power, targeting, direction, accuracy, type
  selection, animation, ball launch, ball physics, receiver selection, assisted
  passing, through-ball, lobbed-pass, ground-pass, cooldown, recovery, gameplay
  state.
- No pass gameplay system (none exists; verified by test).
- Shoot Input — **deferred to Task 121** (not implemented).
- Controller/keyboard support, dead zones, sensitivity, remapping, buffering
  (Task 132), priority (Task 133), simultaneous-input resolution (Task 134).
- No duplicate Pass authority, no fake gameplay consumer, no speculative
  infrastructure.

### Tests / verification

- Focused: `Football.Tests.EditMode.PassInputContractTests` (7 tests) — PASS
  (contract shape, coherence, provider parity + default false, single authority,
  transient-pulse/no-buffering/priority machinery, no passing-gameplay metadata,
  no passing gameplay runtime).
- Input race regression: `InputFrameTests` 9/9, `MoveInputContractTests` 8/8,
  `SprintInputContractTests` 7/7, `PassingTests` 14/14 — PASS.
- Full EditMode suite: 1586/1586 PASS.
- Old project (`D:\Projects\unity\ofc`) untouched. No commit/push/tag performed.

### Remaining deferrals (unchanged after Task 120)

- Provider → InputFrame snapshot wiring (until a gameplay consumer exists).
- Passing gameplay mechanics; buffering (132); priority
  (133); simultaneous-input (134); remapping/sensitivity/dead zones.

---

## Task 121 — Shoot Input

Status: VERIFIED (KEEP) — PASS.

Task 121 established (by audit + verification, NOT by new infrastructure) the
correct representation, semantics, ownership, and device-neutral contract of
SHOOT INPUT — the transient INPUT COMMAND only. The shooting gameplay mechanic
is out of scope and does not exist.

### Shoot input authority

- **Authoritative provider contract:** `IPlayerInput.Shoot`
  (`bool { get; }`, `Football.Core`).
- **Authoritative snapshot representation:** `InputFrame.Shoot`
  (`bool { get; }`, `Football.Core`) — identical type and meaning.
- **Single authority rule:** exactly ONE Shoot input meaning exists (the one-shot
  command pulse). No `ShootInput*` / `ShootCommand` / `ShotInput` /
  `ShotRequest` / `KickInput` abstraction exists; verified and locked by tests
  (`ShootInputContractTests`).

### Shoot semantics

- **Type:** `bool`. (FACT)
- **Meaning:** TRANSIENT one-shot command pulse — "player requested a shot".
  NOT held state, NOT continuous. A pulse is expected on the frame the command
  is issued, then `false`. (FACT: produced with `Input.GetButtonDown`, the
  button-down edge. `GetButton`/`GetKey` would be held — not used.)
- **Source:** `HumanPlayerInput.Update()` reads it every frame via legacy
  `UnityEngine.Input.GetButtonDown("Fire1")`; AI/Network/Replay inject
  externally. (FACT)
- **Default:** `false`. No filtering/normalization/transformation. (FACT)
- **Intent vs gameplay:** the contract carries INPUT INTENT only. It NEVER
  represents "ball was kicked"/"player is shooting"/power/force/target/
  direction/type/trajectory/animation/charge/state/result — no such metadata
  exists in the contract. (FACT)
- **Provider note:** all four providers are STRUCTURALLY compatible (same `bool`
  on one interface, default false). Non-Human providers expose a settable `bool`;
  whether an external writer issues one-shot pulses is not provable from code
  (UNKNOWN — temporal behavior depends on the writer).

### Player data / config boundaries

- `PlayerStats.Shooting` (`ShootingStats`: AttackingAwareness, Finishing,
  ShotPower, LongShots, Volleys, Penalties) is **PLAYER DATA** — player
  capability ratings, distinct from input. No duplication into the input
  contract. (FACT; Task 69 verified.)
- `BallConfig.KickForce` (float, 20f default) is **GAMEPLAY TUNING** — ball
  physics interaction force, distinct from input. (FACT)
- `PlayerStateId.Shooting` is a gameplay-state enum, not input. (FACT)

### Device mapping

Shoot is read via a legacy Unity button (`"Fire1"`) inside `HumanPlayerInput`
(provider layer — legitimate). The value flowing through `IPlayerInput`/
`InputFrame` is a device-neutral `bool`. No controller/keyboard mapping system
exists or was added. (FACT; exact physical binding = Unity Input Manager default,
UNKNOWN.)

### Relationship to IPlayerInput / InputFrame

```
Device / External Source
      ↓
Provider (Human: GetButtonDown("Fire1"); AI/Network/Replay inject)
      ↓
IPlayerInput.Shoot            (authoritative provider contract, transient bool)
      ↓  (value)
InputFrame.Shoot              (authoritative device-neutral snapshot field)
      ↓
Future shooting gameplay consumer (not yet implemented)
```

### Input vs gameplay boundary

SHOOT INPUT = "player requested a shot" (`bool`, exists now).
SHOOT GAMEPLAY = shot power, shot force, shot target, shot direction, shot
type, shot accuracy, aim system, charge mechanics, ball launch, kick physics,
ball trajectory, shot animation, goalkeeper reaction, shot result, shooting
cooldown, shooting stamina, player shooting state (does NOT exist; verified by
`ShootingTests` Task 69 audit and `ShootInputContractTests` Task 121).

### Ownership matrix

| Owner | Responsibility |
|---|---|
| Device | Raw device reads only (`HumanPlayerInput`). |
| Provider | Produce/convert Shoot as a one-shot intent into the contract; own `IsEnabled`. |
| IPlayerInput | Authoritative read-side Shoot contract. |
| InputFrame | Device-neutral Shoot snapshot field. |
| Gameplay (future) | Consume the one-shot intent when implementing shooting mechanics. |

`IsEnabled` remains provider/lifecycle state (gates provider sampling), excluded
from `InputFrame`.

### What Task 121 intentionally does NOT implement

- Shooting gameplay: power, force, direction, targeting, aim, accuracy, shot
  type, charge mechanics, charge duration, ball launch, kick physics, ball
  trajectory, shot animation, shot cooldown, shot stamina consumption, shot
  result handling, goalkeeper interaction.
- No shooting gameplay system (none exists; verified by test).
- Tackle Input — **deferred to Task 122** (not implemented).
- Controller/keyboard support, dead zones, sensitivity, remapping, buffering
  (Task 132), priority (Task 133), simultaneous-input resolution (Task 134).
- No duplicate Shoot authority, no fake gameplay consumer, no speculative
  infrastructure.

### Tests / verification

- Focused: `Football.Tests.EditMode.ShootInputContractTests` (7 tests) — PASS
  (contract shape, coherence, provider parity + default false, single authority,
  transient-pulse/no-buffering/priority machinery, no shooting-gameplay metadata,
  no shooting gameplay runtime).
- Related fixtures: `InputFrameTests` 9/9, `MoveInputContractTests` 8/8,
  `SprintInputContractTests` 7/7, `PassInputContractTests` 7/7,
  `PassingTests` 14/14, `ShootingTests` 14/14 — all PASS.
- Full EditMode suite: 1593/1593 PASS.
- Old project (`D:\Projects\unity\ofc`) untouched. No commit/push/tag performed.

### Remaining deferrals (unchanged after Task 121)

- Provider → InputFrame snapshot wiring (until a gameplay consumer exists).
- Shooting gameplay mechanics; buffering (132); priority (133);
  simultaneous-input (134); remapping/sensitivity/dead zones.

---

## Task 122 — Tackle Input

Status: VERIFIED (KEEP) — PASS.

Task 122 established (by audit + verification, NOT by new infrastructure) the
correct representation, semantics, ownership, and device-neutral contract of
TACKLE INPUT — the transient INPUT COMMAND only. Tackle gameplay, mechanics,
collision, player-state transitions, foul logic, and ball-recovery behavior are
out of scope and do not exist.

### Tackle input authority

- **Authoritative provider contract:** `IPlayerInput.Tackle`
  (`bool { get; }`, `Football.Core`).
- **Authoritative snapshot representation:** `InputFrame.Tackle`
  (`bool { get; }`, `Football.Core`) — identical type and meaning.
- **Single authority rule:** exactly ONE Tackle input meaning exists (the
  one-shot command pulse). No `TackleInput*` / `TackleCommand` /
  `TackleRequest` / `SlideInput` abstraction exists; verified and locked by
  tests (`TackleInputContractTests`).

### Tackle semantics

- **Type:** `bool`. (FACT)
- **Meaning:** TRANSIENT one-shot command pulse — "player requested a tackle".
  NOT held state, NOT continuous. A pulse is expected on the frame the command
  is issued, then `false`. (FACT: produced with `Input.GetKeyDown`, the
  key-press edge. `GetKey`/`GetButton` would be held — not used.)
- **Source:** `HumanPlayerInput.Update()` reads it every frame via legacy
  `UnityEngine.Input.GetKeyDown(KeyCode.E)`; AI/Network/Replay inject
  externally. (FACT)
- **Default:** `false`. No filtering/normalization/transformation. (FACT)
- **Intent vs gameplay:** the contract carries INPUT INTENT only. It NEVER
  represents "currently tackling"/"tackle connected"/"tackle succeeded"/"ball
  recovered"/"possession changed"/"foul occurred"/card result/target/force/
  direction/animation/cooldown/state — no such metadata exists in the contract.
  (FACT)
- **Provider note:** all four providers are STRUCTURALLY compatible (same `bool`
  on one interface, default false). Non-Human providers expose a settable `bool`;
  whether an external writer issues one-shot pulses is not provable from code
  (UNKNOWN — temporal behavior depends on the writer).

### Domain boundaries

- `PlayerStats.Defending` (incl. `StandingTackle`/`SlidingTackle`) is
  **PLAYER DATA** — player capability ratings, distinct from input. (FACT;
  Task 72 verified.)
- `AnimationConfig.TackleBlendTime` is **GAMEPLAY TUNING** — animation blend,
  distinct from input. (FACT)
- `PlayerStateId.Tackling` is a gameplay-state enum, not input. (FACT)
- `MatchRulesDefinition.EnforceFouls` is a match/competition RULE, distinct from
  input and from foul game logic. (FACT; Task 103 verified.)
- No `TackleConfig` exists and no evidence justifies creating one. (FACT)

### Device mapping

Tackle is read via a legacy key (`KeyCode.E`) inside `HumanPlayerInput`
(provider layer — legitimate). The value flowing through `IPlayerInput`/
`InputFrame` is a device-neutral `bool`. No controller/keyboard mapping system
exists or was added. (FACT; exact binding provider-owned.)

### Relationship to IPlayerInput / InputFrame

```
Device / External Source
      ↓
Provider (Human: GetKeyDown(KeyCode.E); AI/Network/Replay inject)
      ↓
IPlayerInput.Tackle           (authoritative provider contract, transient bool)
      ↓  (value)
InputFrame.Tackle             (authoritative device-neutral snapshot field)
      ↓
Future tackle gameplay consumer (not yet implemented)
```

### Input vs gameplay boundary

TACKLE INPUT = "player requested a tackle" (`bool`, exists now).
TACKLE GAMEPLAY = tackle movement, animation, collision, range, timing,
success/failure, ball recovery, sliding/standing tackle mechanics, targeting,
direction resolution, force, cooldown, recovery, stamina, lockout, foul
detection, referee/card logic, player-state transitions, possession changes,
ball interaction, tackle physics, invulnerability windows, tackle result/state
(does NOT exist; verified by `DefendingTests` Task 72 audit and
`TackleInputContractTests` Task 122).

### Ownership matrix

| Owner | Responsibility |
|---|---|
| Device | Raw device reads only (`HumanPlayerInput`). |
| Provider | Produce/convert Tackle as a one-shot intent into the contract; own `IsEnabled`. |
| IPlayerInput | Authoritative read-side Tackle contract. |
| InputFrame | Device-neutral Tackle snapshot field. |
| Gameplay (future) | Consume the one-shot intent when implementing tackle mechanics. |

`IsEnabled` remains provider/lifecycle state (gates provider sampling), excluded
from `InputFrame`.

### What Task 122 intentionally does NOT implement

- Tackle gameplay: movement, animation, collision, range, timing, success/
  failure, ball recovery, sliding/standing mechanics, targeting, direction
  resolution, force, cooldown, recovery, foul detection, referee/card logic,
  player-state transitions, possession changes, ball interaction, tackle
  physics, invulnerability windows, tackle stamina, tackle lockout, tackle
  result/state.
- No tackle gameplay system (none exists; verified by test).
- Switch Player Input — **deferred to Task 123** (not implemented).
- Controller/keyboard support, dead zones, sensitivity, remapping, buffering
  (Task 132), priority (Task 133), simultaneous-input resolution (Task 134).
- No duplicate Tackle authority, no fake gameplay consumer, no speculative
  infrastructure.

### Tests / verification

- Focused: `Football.Tests.EditMode.TackleInputContractTests` (7 tests) — PASS
  (contract shape, coherence, provider parity + default false, single authority,
  transient-pulse/no-buffering/priority machinery, no tackle-gameplay metadata,
  no tackle gameplay runtime).
- Related fixtures: `InputFrameTests` 9/9, `MoveInputContractTests` 8/8,
  `SprintInputContractTests` 7/7, `PassInputContractTests` 7/7,
  `ShootInputContractTests` 7/7, `ShootingTests` 14/14, `DefendingTests` — all
  PASS.
- Full EditMode suite: 1600/1600 PASS.
- Old project (`D:\Projects\unity\ofc`) untouched. No commit/push/tag performed.

### Remaining deferrals (unchanged)

- Provider → InputFrame snapshot wiring (until a gameplay consumer exists).
- Tackle gameplay mechanics; Switch Player Input (Task 123); buffering (132);
  priority (133); simultaneous-input (134); remapping/sensitivity/dead zones.

## Task 123 — Switch-Player Input

Task 123 established (by audit + verification + a minimal contract extension, at the
explicit product requirement) a **device-neutral PLAYER-SWITCH INPUT** on the existing
`IPlayerInput` / `InputFrame` contract. It carries only a **switch REQUEST** and an
**optional directional INTENT**; it does NOT select which player.

### Switch input authority

The authoritative switch input contract is `IPlayerInput` (read-only to consumers) +
`InputFrame` (immutable snapshot). Two new members were added:

- `bool SwitchPlayer` — **one-shot switch REQUEST** pulse for a sampling point.
- `Vector2 SwitchDirection` — **device-neutral directional INTENT**; `Vector2.zero`
  means "no direction".

All four providers (`HumanPlayerInput`, `AIPlayerInput`, `ReplayPlayerInput`,
`NetworkPlayerInput`) implement the same two members with a single, consistent default
(`false` / `Vector2.zero`).

### Switch semantics

Per the authoritative product requirement:

- **Switch without a direction** = "request smart defensive player selection".
- **Switch with a direction** = "request directional player selection".

The input layer carries only the REQUEST and the INTENT. Which player is finally
selected — smart-defensive scoring, directional targeting, nearest-player selection,
ball-aware selection — is a **separate game PLAY responsibility** and **must not exist
in the input layer** (enforced by contract tests).

### Device mapping boundary

Exact device bindings are **deferred to later device tasks** (Controller → Task 127,
Keyboard → Task 128, Mobile swipe/drag → forthcoming mobile task). For Task 123,
`HumanPlayerInput` exposes the two members with `private set` but no device binding
(they simply default to `false` / `Vector2.zero`). Mobile swipe/drag and Controller
D-Pad/analog must be translated by the provider into `SwitchDirection` so that all
devices converge on ONE device-neutral intent. No keyboard/controller/mobile framework
was added; no dead zone/sensitivity/remapping/buffering/priority/simultaneous-input
(Tasks 132/133/134) was added.

### Boundary / what Task 123 intentionally does NOT implement

- Which player is selected / the selection algorithm (smart-defensive or directional)
  — game PLAY responsibility, not input.
- Device bindings for Controller / Keyboard / Mobile — deferred to device tasks.
- Buffering (132), priority (133), simultaneous-input resolution (134).
- Dead zones, sensitivity, remapping, gesture recognition, history.
- No `SelectedPlayer`/`ControlledPlayer`/`TargetPlayer`/`NearestPlayer`/
  `BestDefender`/`SelectionResult`/`PlayerIndex` state on the contract.
- No duplicate switch abstraction (single authority via `IPlayerInput`/`InputFrame`).

### Tests / verification

- Focused: `Football.Tests.EditMode.SwitchInputContractTests` (10 tests) — PASS
  (contract shape/coherence, provider parity + default false/zero, zero-means-no
  direction, non-zero device-neutral direction representation, single authority, no
  buffering/priority/queue machinery, no player-selection state on the contract, no
  selection algorithm / device types in the contract).
- Updated: `InputFrameTests` 9/9 — PASS (defaults, extended 11-arg constructor, member
  list now includes `SwitchPlayer` + `SwitchDirection`, copy value-semantics).
- Full EditMode suite: **1610/1610 PASS** (0 failed / 0 inconclusive / 0 skipped).
- Old project (`D:\Projects\unity\ofc`) untouched. No commit/push/tag performed.

## Task 124 — Skill Input

**STATUS: IMPLEMENTED — SKILL BUTTON + DIRECTION**

Task 124 defines and implements a device-neutral **SKILL INPUT** for
technical football skill / dribble moves (Step Over, Body Feint, Ball Roll,
Roulette, Elastico, Rainbow Flick, etc.), per the explicit user requirement.

### 124.1 Audit / Discovery (what was found)

Every Skill-related term in the repository was searched: `Skill*Input*`,
`SkillCommand`, `SkillRequest`, `SkillAction`, `SkillMove*`, `SkillButton`,
`SkillDirection`, `SkillType`, `SkillId`, `SkillIndex`, `SkillSlot`,
`SpecialMove`, `Trick`, `Feint`, `Flick`, `Rainbow`, `Elastico`, `StepOver`,
`Roulette`, `BallRoll`, `Ability`, `Gesture`, `Combo`, `Cooldown` (in a
skill-input context), plus all existing provider/contract/runtime code.

**What existed (FACT, before Task 124):**
- `SkillRating` — `PlayerDefinition.Profile` field (int, 1–5, default 3,
  validated by Task 80). Player DATA/capability only. NOT input.
- `FootSkillRulesTests` — asserts `SkillMoveSystem`, `SkillMoveController`,
  `SkillMoveAnimation`, `SkillAnimatorBridge`, `FootControlSystem` must NOT
  exist (FACT; enforced as build-time assertion).
- `PlayerDataArchitectureRules.md` — "SkillRating does NOT list individual
  skills. Do not create a SkillMove system in Player Data. The exact gameplay
  meaning of each value will be defined later." (FACT).
- `IPlayerInput` — had MoveDirection, LookDirection, Sprint, Pass, Shoot,
  Tackle, SwitchPlayer, SwitchDirection, Interact, Cancel, Pause, IsEnabled.
  **No Skill member.**
- `InputFrame` — same set; immutable; 11-arg constructor. **No Skill member.**
- All four providers (Human/AI/Replay/Network) — **no Skill member.**
- `HumanPlayerInput.Update()` — device bindings: Horizontal/Vertical axis,
  LeftShift, Fire2, Fire1, KeyCode.E, Space, Cancel, Escape. **No skill
  binding.**
- `PlayerStateId` — Idle, Walk, Run, Sprint, Backpedal, Dribbling, Passing,
  Shooting, Tackling, Stunned. **No Skill state.**
- `DribbleConfig` — dribble tuning only (ball stick distance/height, control
  radius, speed, foot alternation, touch).
- `AnimationConfig` — `DribbleBlendTime` (dribble blend tuning). **No skill
  animation blend.**
- `PlayerPlayStyle` — descriptive classification (Poacher, Playmaker, etc.).
  No input semantics.
- `Animation/Processed` folders — contain only folder `.meta` files; no
  committed `.anim`/`.controller` assets.

### 124.2 Existing Skill Input Analysis

Classification: **D — Completely absent.** No Skill input abstraction, stub,
placeholder, deprecated member, or speculative API existed. `SkillRating`
is player DATA only and is explicitly forbidden from becoming a skill-move
system/animation bridge. There was no authoritative "what does the player
press to request a Skill?" definition.

### 124.4 Design resolution (user requirement)

The user defined Skill Input explicitly (unblocking implementation):

> "Skill" = technical football skill/dribble moves (Step Over, Body Feint,
> Ball Roll, Roulette, Elastico, Rainbow Flick, etc.). Input = **Skill Button +
> Direction**. Direction = player intent (not a specific animation/skill).
> Gameplay interprets and executes.

**Resolved design:**
- **Skill** — one-shot (transient) boolean skill request.
- **SkillDirection** — device-neutral `Vector2` directional intent; zero = no
  direction. It is PLAYER INTENT ONLY and does not identify a specific skill,
  animation, or gameplay result.
- **No identifier/index/slot** — a generic "request a skill" is sufficient;
  which skill executes is gameplay, not input.
- **No device binding** — deferred to Controller (127) / Keyboard (128) /
  Mobile tasks. `HumanPlayerInput.Skill`/`SkillDirection` are exposed but never
  read by any device in Task 124.

### 124.5 Implementation

Six production files updated in OFC_Rebuild (none in `D:\Projects\unity\ofc`):

1. `Runtime/Core/Interfaces/IPlayerInput.cs` — added `bool Skill` +
   `Vector2 SkillDirection` (14 members total). Read-only from consumer side.
2. `Runtime/Core/InputFrame.cs` — added `Skill` + `SkillDirection` (13 fields);
   constructor extended to **13 arguments** in the order:
   `moveDir, lookDir, sprint, pass, shoot, tackle, switchPlayer,
   switchDirection, skill, skillDirection, interact, cancel, pause`.
3. `Runtime/Input/HumanPlayerInput.cs` — added `Skill` (private set) +
   `SkillDirection` (private set); **no device binding**.
4. `Runtime/Input/AIPlayerInput.cs` — added `Skill` (set) + `SkillDirection`
   (set) so AI can request a skill. No internal AI logic — request only.
5. `Runtime/Input/ReplayPlayerInput.cs` — added `Skill` (set) + `SkillDirection`
   (set) so recorded play can carry a skill request.
6. `Runtime/Input/NetworkPlayerInput.cs` — added `Skill` (set) +
   `SkillDirection` (set) so netstreaming can carry a skill request.

Tests updated/added:
- `Tests/EditMode/InputFrameTests.cs` — defaults (Skill false, SkillDirection
  zero), constructor call now 13 args, member list (+Skill/+SkillDirection),
  copy test asserts SkillDirection value semantics.
- `Tests/EditMode/SwitchInputContractTests.cs` — its reflection-based
  constructor invocation updated to the 13-arg signature (Task 123 regression).
- `Tests/EditMode/SkillInputContractTests.cs` (**new**, +10 tests, Task 124
  deliverable) — verifies: Skill/SkillDirection on IPlayerInput (read-only);
  InputFrame matches contract (read-only); all four providers expose both on
  the same contract with correct defaults; zero direction = no direction;
  non-zero directions representable; single authority (no duplicate skill
  abstraction); transient one-shot (no buffering/queue/priority machinery);
  skill execution state excluded; and **no** `SkillMoveSystem`,
  `SkillMoveController`, `SkillMoveAnimation`, `SkillAnimatorBridge`.

### 124.6 Integrity

- `Football.Core.asmdef` — zero refs, unchanged. `Football.Input.asmdef` —
  refs Core only, unchanged.
- No forbidden type created (`SkillMoveSystem`, `SkillMoveController`,
  `SkillMoveAnimation`, `SkillAnimatorBridge` all absent — enforced by
  FootSkillRulesTests and `SkillInputContractTests`).
- `SkillRating` remains player data only, untouched.
- Old project (`D:\Projects\unity\ofc`): **0 diffs** against
  `ofc_baseline.txt` (117 non-meta files, same timestamps and sizes).

### 124.8 Tests

- `SkillInputContractTests` (10) — PASS.
- `InputFrameTests` (9) — PASS.
- Full EditMode regression: **1620/1620 PASS** (1610 baseline + 10 new
  SkillInputContractTests).

### 124.9 Conflict review

Skill + X behavior remains **deferred**: Task 124 introduces the Skill request
semantics only. Simultaneous-input / conflict resolution stays owned by
Task 133 (Input Priority) and Task 134 (Simultaneous-Input).

### 124.12 Scope boundary

Task 124 implemented the INPUT CONTRACT only. It did NOT implement specific
skill moves, skill selection, skill animation, skill execution, cooldown,
stamina, success/failure, physics, or UI — those are gameplay
responsibilities outside the input contract.

## Task 125 — Pause Input

**STATUS: PASS — NO PRODUCTION CHANGE REQUIRED**

Task 125 verified (rather than newly built) the Pause input contract. The
audit proved that Pause was already correctly and fully established in the
input architecture (from earlier Phase 3 / Task 51 work). **No production
file was modified** for Task 125. A dedicated `PauseInputContractTests`
fixture was added to prove the contract.

### 125.1 Audit / Discovery

Search scope: Pause, PauseInput, PauseCommand, PauseRequest, Resume,
GamePaused, IsPaused, PauseState, GameState, MatchPaused, Time.timeScale,
timeScale, Input.GetKeyDown/GetButtonDown, Escape, Cancel, menu, pause menu.

**Existing Pause contract (FACT):**
- `IPlayerInput.Pause` — `bool`, read-only provider contract
  (`Runtime/Core/Interfaces/IPlayerInput.cs:19`).
- `InputFrame.Pause` — `bool`, immutable snapshot; included in the 13-arg
  constructor (`Runtime/Core/InputFrame.cs:76,105`).
- All four providers expose `Pause`:
  - `HumanPlayerInput.Pause` — `bool` with private setter
    (`Runtime/Input/HumanPlayerInput.cs:21`); sampled in `Update()` from
    `Input.GetKeyDown(KeyCode.Escape)` — a TRANSIENT one-shot.
  - `AIPlayerInput` / `ReplayPlayerInput` / `NetworkPlayerInput` — settable
    `bool` (no internal logic; they merely carry Pause on the same contract).
- **No runtime consumer** of `IPlayerInput` exists (only the four providers
  implement it). Pause consumption by game-flow/UI is DEFERRED.
- `GameStateId.Pause` — the ONE authoritative paused-state marker, owned by
  gameplay/game-flow (`Runtime/Match/States/GameStateId.cs:14`), NOT input.
- `GameClock` (Task 51) — the time owner; Pause → `GameClock.Stop()`, Resume →
  `GameClock.Start()`. No `Paused` flag on GameClock.
- **Zero `Time.timeScale` usage** in Runtime (verified by
  `PauseTimeTests.TimeScale_IsNotGameplayAuthority` / `PauseDoesNotRequireUnityTime`).
- Existing pause tests: `PauseTimeTests` (Task 51, 15 tests),
  `FixedTimestepPolicyTests` (Task 52, incl. `PauseDoesNotModifyFixedTimestep`),
  and `InputFrameTests` (Pause in defaults/ctor/member-list).
- **No dedicated per-action `PauseInputContractTests`** existed (all other
  actions — Move/Sprint/Pass/Shoot/Tackle/Switch/Skill — had one; Pause did not).

**Gaps/Unknowns:** No gap in the production contract. The only gap was the
missing dedicated Pause contract test fixture.

### 125.2 Existing Pause Handling Analysis

- **Representation:** `bool Pause` (transient one-shot request).
- **Timing semantics:** Transient one-shot — matches every other one-shot action
  (Pass `GetButtonDown`, Shoot `GetButtonDown`, Tackle `GetKeyDown`, Switch,
  Skill, Cancel). Default `false`.
- **Request-vs-state:** `Pause` is a REQUEST ("the player requested Pause").
  The PAUSED state is `GameStateId.Pause`, owned by game-flow. They are NOT the
  same. Task 125 keeps this separation.
- **Provider behavior:** Human samples a device; AI/Replay/Network are settable
  carriers. No device-specific Pause decision lives in the contract.
- **Consumer behavior:** None yet — deferred.
- **Cancel distinction:** `Pause` and `Cancel` are distinct contract members
  and distinct providers' values (Pause = Escape; Cancel = Input.GetButtonDown
  "Cancel"). They are not merged.
- **Required changes:** None to production.

### 125.3 Pause Ownership Decision

| Layer | Ownership |
|---|---|
| Device | Detects the physical pause action (e.g. Escape) |
| Provider | Converts it to device-neutral `Pause` intent (one-shot bool) |
| Input Contract | Represents the user's Pause REQUEST (`IPlayerInput.Pause` → `InputFrame.Pause`) |
| Gameplay / Game-flow / UI | Consumes the request and decides what Pause means in the current state (`GameStateId.Pause`, `GameClock.Stop/Start`) |

- Pause is a **one-shot request**, not a held input; held/modifier semantics are
  not supported by evidence (follows the transient one-shot convention).
- Pause means "request pause" — "toggle/resume" interpretation is a game-flow
  concern, not a second input (`TogglePause`/`ResumeInput` are NOT added).
- `Cancel` is separate.
- When `IsEnabled=false`: `HumanPlayerInput.Update()` returns early (existing
  behavior); pause gating remains a game-flow policy, not added here.
- AI/Replay/Network can carry Pause (structural compatibility on one contract).
- `InputFrame` simply snapshots `Pause`.
- No direction, no identifier — none invented.

### 125.4 API / Data Design

- **API design:** The existing `bool Pause` on `IPlayerInput` and `InputFrame`
  is already correct and sufficient. **No API change.**
- **Why keep:** Task 125 explicitly prefers KEEP over redesign for symmetry.
  Adding a `PauseCommand` struct / `PauseMode` enum / `PauseState` enum /
  `Resume` bool / `TogglePause` bool / `PauseRequest` object is not justified by
  repository evidence and would violate single-authority.
- **Type/Default/Timing/Mutability:** bool, default false, transient one-shot,
  read-only on `IPlayerInput`/`InputFrame`.
- **Provider impact:** none (already carried).
- **InputFrame impact:** none (already carried).
- **Compatibility:** fully preserved.
- **Explicit non-goals:** no gameplay state, no resume/toggle input, no menu.

### 125.5 Implementation

**Outcome A — NO PRODUCTION CHANGE REQUIRED.** No production file was modified.
A new test fixture `Tests/EditMode/PauseInputContractTests.cs` (+11 tests, with
`.meta`) was added to prove the existing contract.

### 125.6 Integration

Architecture already in place: `Device → Provider → IPlayerInput → InputFrame
→ Future Game Flow/UI`. Providers separately sample/carry `Pause`; it flows to
the snapshot. **No current consumer exists — Pause consumption is deferred.**
No fake consumer was created. Providers coherence, device-neutrality, and
Pause-vs-Cancel separation are verified by the new fixture.

### 125.7 Gameplay / System Boundary Review

| Concept | Owner | In Input? |
|---|---|---|
| IsPaused | Gameplay/game-flow | No |
| GameStateId.Pause | Gameplay/game-flow (single authority) | No (enum, not input) |
| MatchPaused | Game-flow | No |
| PauseMenuOpen | UI | No |
| Time.timeScale | Engine; not used as authority | No (zero Runtime usage) |
| MatchClockStopped | Time owner (GameClock.Stop) | No |
| PhysicsPaused / AnimationPaused | Respective systems | No |

`Pause INPUT (request) ≠ PAUSED game state`. Cancel remains a distinct intent.
No gameplay system was altered.

### 125.8 Focused Tests

- `PauseInputContractTests` — **11/11 PASS** (new; proves IPlayerInput/InputFrame
  contract type + read-only, all four providers, single authority, transient
  one-shot with no Resume/Toggle/buffering, no paused-state leakage, game-state
  authority separate, Pause-vs-Cancel distinct, no input-layer pause
  menu/gameplay/timeScale runtime).
- `PauseTimeTests` — **15/15 PASS** (existing Task 51, unchanged).

### 125.9 Full Regression / Integrity Audit

- Full EditMode regression: **1631/1631 PASS** (1620 baseline + 11 new
  PauseInputContractTests). Recorded below.
- `D:\Projects\unity\ofc` untouched (all 117 baseline files unchanged).
- asmdefs unchanged; no duplicate types; forbidden systems absent.
- `.meta` for `PauseInputContractTests.cs.meta` valid (minimal 2-line form);
  GUID unique.

### 125.10 Architecture Documentation

This section (Task 125) appended without altering Tasks 117–124.

### 125.11 Final Verification / Gate

- **PASS** — existing Pause contract satisfies Task 125; no production change.
- Semantics supported by evidence; no unsupported Pause behavior invented.
- Boundary verified (paused state / UI / Time.timeScale all outside input).
- Scope: no pause menu, UI, Time.timeScale, state transitions, match/physics/
  animation/network/replay pause, dead zones, sensitivity, remapping, buffering,
  priority, or simultaneous-input resolution implemented.

### 125.12 Scope boundary / deferred

Device bindings (which concrete button/key/gesture triggers Pause) are
deferred to the device-specific tasks (Controller / Keyboard / Mobile). Input
priority ownership remains Task 133; simultaneous-input remains Task 134.
Pause input has no current runtime consumer; gameplay/game-flow consumption is
deferred.

## Task 126 — Human Input Provider

**STATUS: PASS — NO PRODUCTION CHANGE REQUIRED**

Task 126 defined/verified the HUMAN INPUT PROVIDER boundary. The audit proved
`HumanPlayerInput` already correctly implements the provider responsibility —
the translation boundary between human/device input and the device-neutral
`IPlayerInput` contract. **No production file was modified.** A dedicated
`HumanInputProviderTests` fixture (+12 tests) was added to prove the boundary.

### 126.1 Audit / Discovery

Search scope: HumanPlayerInput, Human Input Provider, HumanInput, PlayerInput,
input provider, GetAxis/GetAxisRaw/GetButton/GetKey/GetKeyDown, KeyCode,
Input.Get*, UnityEngine.Input, InputAction/InputControl, Gamepad/Keyboard/
Touch/Mouse/Joystick, device, provider.

**Findings (FACT):**
- All 46 `UnityEngine.Input` / device-type matches in the repository are either
  (a) the sole production device-read block in `HumanPlayerInput.Update()`, or
  (b) negative test assertions verifying device reads do NOT occur elsewhere
  (DebugCategoryTests, InputFrameTests, MoveInputContractTests,
  SkillInputContractTests, SwitchInputContractTests), or (c) config-string
  false positives (e.g. `DribbleConfigTests` "Joystick" string target).
- `HumanPlayerInput` is the **sole human provider** — it implements `IPlayerInput`
  directly, is a `MonoBehaviour`, and samples in `Update()`.
- `HumanPlayerInput.Update()` is the ONLY Update in the Runtime (Task 53
  ownership, enforced by `LifecycleOwnershipTests`).
- `IsEnabled` (default `true`) gates sampling: `if (!IsEnabled) return;`.
- Football.Core asmdef has **zero** UnityEngine references → Core structurally
  CANNOT read device APIs (compile-time guarantee).
- Provider currently binds: MoveDirection (GetAxisRaw Horizontal/Vertical),
  Sprint (GetKey LeftShift), Pass (GetButtonDown Fire2), Shoot (GetButtonDown
  Fire1), Tackle (GetKeyDown E), Interact (GetKeyDown Space), Cancel
  (GetButtonDown Cancel), Pause (GetKeyDown Escape).
- **Unbound contract members:** LookDirection, SwitchPlayer, SwitchDirection,
  Skill, SkillDirection — structurally supported, physically unbound (default
  false/zero). Device bindings deferred to Controller/Keyboard/Mobile tasks.
- No dedicated `HumanInputProviderTests` fixture existed (only per-action
  InputContractTests checking the four providers for one field each, and
  category tests naming the provider).

### 126.2 Existing HumanPlayerInput Analysis

Per-member: MoveDirection (continuous, bound), LookDirection (continuous, unbound),
Sprint (continuous/held, bound), Pass/Shoot/Tackle/Interact/Cancel/Pause
(transient one-shot, bound), SwitchPlayer/SwitchDirection/Skill/SkillDirection
(transient contract members, unbound, safe defaults). Sampling model: `Update()`
gated by `IsEnabled`. Temporal semantics correct: continuous stay continuous
(GetKey/GetAxisRaw), one-shot transient (GetKeyDown/GetButtonDown). No stale
transient persistence (bools naturally drop to false). No defects requiring
change.

### 126.3 Provider vs Device Responsibility Decision

| Layer | Responsibility |
|---|---|
| Device | Detects physical action |
| Human Provider (`HumanPlayerInput`) | Samples device input, translates to logical input, exposes `IPlayerInput` |
| Core (`IPlayerInput`/`InputFrame`) | Device-neutral contract; MUST NOT read `KeyCode`/`UnityEngine.Input` |
| Gameplay | MUST NOT bypass `IPlayerInput` for human input; consumes later |

Current architecture already satisfies this (KEEP). No boundary violation: all
device reads are in the provider; Core is UnityEngine-free by construction;
gameplay has no direct device reads. No corrections required.

### 126.4 Provider API Design

Keep the existing API — it is already the minimal correct design:
`HumanPlayerInput : MonoBehaviour, Core.IPlayerInput` with private-set
properties, `Update()` sampling gated by `IsEnabled`. No `KeyboardInputProvider`
/ `ControllerInputProvider` / `TouchInputProvider` / `DeviceInputManager`
created (later tasks / future architecture).

### 126.5 Implementation

**Outcome A — NO PRODUCTION CHANGE.** No production file was modified. Only a
new test fixture `Tests/EditMode/HumanInputProviderTests.cs` (+12 tests, with
`.meta`) was added.

### 126.6 InputFrame Integration

`HumanPlayerInput` exposes `IPlayerInput`; it does NOT produce `InputFrame` at
runtime, and no runtime consumer exists. All 13 `InputFrame` fields map 1:1 to
`IPlayerInput` members (verified by `HumanInputProviderTests` + `InputFrameTests`).
`IsEnabled` is the provider enable gate and is deliberately excluded from the
snapshot. No `InputRouter`/`InputPipeline`/`GameplayInputSystem`/fake consumer
was introduced — runtime `InputFrame` capture is documented future work.

### 126.7 Gameplay Independence Review

**Verdict: PASS.** `HumanPlayerInput` references only `UnityEngine`
(`MonoBehaviour`, `Vector2`, `KeyCode`, `Input`) and `Core.IPlayerInput`. No
PlayerStateId/PlayerController/MatchRuntime/Ball/Team/PlayerDefinition/
PlayerStats/GameState/GameClock/Camera/Animator/Rigidbody/CharacterController/
Physics/UI/Scene/Match references (grep-verified; reflection-verified). No
gameplay leakage. Core is device-free by asmdef. No corrections required.

### 126.8 Focused Tests

- `HumanInputProviderTests` — **12/12 PASS** (new). Proves: HumanPlayerInput is
  the sole human provider implementing IPlayerInput; all 14 contract members
  implemented with matching types; MonoBehaviour + Update() sampling; IsEnabled
  default true; safe defaults for all members; continuous vs transient contract
  semantics; IsEnabled-gated Update; no gameplay/player-state members; no
  gameplay system references; no gameplay-execution methods; no parallel
  human/device-input abstraction; Core has no Unity-input dependency; InputFrame
  compatibility (no field lost).

### 126.9 Full Regression / Integrity Audit

- Full EditMode regression: **1643/1643 PASS** (1631 baseline + 12 new
  HumanInputProviderTests). Recorded below.
- `D:\Projects\unity\ofc` untouched (all 117 baseline files unchanged).
- asmdefs unchanged (Core zero refs; Input refs Core only).
- No duplicate Human provider, no parallel abstraction, no Core Unity-input
  dependency, no gameplay input bypass.
- `.meta` for `HumanInputProviderTests.cs.meta` valid (minimal 2-line form);
  GUID unique.

### 126.10 Architecture Documentation

This section (Task 126) appended without altering Tasks 117–125.

### 126.11 Final Verification / Gate

- **PASS** — existing HumanPlayerInput satisfies Task 126; no production change.
- Human Input Provider = translation boundary between human/device input and
  IPlayerInput (established). Human Input Provider ≠ gameplay (established).
- PROVIDER / DEVICE BOUNDARY / CONTRACT / TEMPORAL / INPUTFRAME / SCOPE /
  TESTS / INTEGRITY gates all verified.
- Deferred device bindings for LookDirection/SwitchPlayer/Skill etc. → later
  device tasks. Controller → Task 127, Keyboard → Task 128, Dead Zones → 129,
  Sensitivity → 130, Remapping → 131, Buffering → 132, Priority → 133,
  Simultaneous-Input → 134.

### 126.12 Scope boundary

No controller support, no new keyboard framework, no mobile framework, no dead
zones, no sensitivity, no remapping, no buffering, no priority, no
simultaneous-input resolution, and no gameplay implementation. No Task 127 work.
---

# Task 127 - Controller Support

## 127.1 Audit / Discovery

Task 127 adds CONTROLLER (generic gamepad/joystick) support to the input layer.

FRAMEWORK CONTRADICTION FOUND (resolved by user decision):
- `ProjectSettings/ProjectSettings.asset` had `activeInputHandler: 1`
  (new Input System ONLY). With that value the legacy `UnityEngine.Input` reads
  in `HumanPlayerInput.Update()` would THROW `InvalidOperationException` at
  runtime, because `activeInputHandler: 1` disables the legacy Input Manager.
- Every line of production input code (`HumanPlayerInput`, Tasks 117-125) and
  all prior Architecture documentation uses the LEGACY `UnityEngine.Input` API
  (GetAxisRaw/GetButton/GetButtonDown + named axes), backed by the configured
  `ProjectSettings/InputManager.asset`.
- `com.unity.inputsystem` 1.20.0 is present in `Packages/manifest.json` and
  `packages-lock.json`, but NO production code uses the Input System.
- No `Keyboard`/`Gamepad`/`Mouse`/`InputAction`/`InputActionAsset` usage exists
  in Runtime.

DECISION (user-confirmed): KEEP the legacy Input Manager as the authoritative
input framework. Controller support is expressed as legacy Input Manager named
axes with generic (non-vendor) gamepad/joystick bindings, folded into the single
existing provider `HumanPlayerInput`. No second input framework and no parallel
controller abstraction is introduced.

## 127.2 Existing Controller/Gamepad Support

The standard default `InputManager.asset` already provides partial gamepad
support:
- MoveDirection: joystick `Horizontal` (axis 0) / `Vertical` (axis 1, invert 1)
  map the LEFT STICK to `GetAxisRaw("Horizontal"/"Vertical")`.
- Shoot: joystick `Fire1` = `joystick button 0` (A).
- Pass: joystick `Fire2` = `joystick button 1` (B).
- Cancel: axis `altPositiveButton: joystick button 1` (B - documented collision
  with Pass, resolution deferred to Tasks 133/134).
- Fire3 (button 2) and Jump (button 3) exist but are bound to axes unused by
  the provider contract.

## 127.3 Supported Device Scope

- Framework: LEGACY UnityEngine.Input (authoritative, user-confirmed).
- Device scope: GENERIC gamepad/joystick via the legacy Input Manager's generic
  joystick button/axis scheme. No vendor-specific (Xbox/PlayStation) naming.
- Keyboard and mouse bindings preserved unchanged.
- Config fix: `activeInputHandler` set 1 -> 2 (Both), so the legacy framework
  (keyboard AND controller) functions at runtime while the Input System package
  remains installed. This is a configuration correction, NOT a framework
  migration.

## 127.4 Device-to-Input Mapping Design

| Action       | Legacy axis  | Keyboard          | Generic gamepad     |
|--------------|--------------|-------------------|---------------------|
| MoveDirection| Horizontal/Vertical | left/right/a/d, down/up/s | Left Stick (axis 0/1) |
| Sprint       | Sprint       | Left Shift        | Joystick button 4 (LB)|
| Tackle       | Tackle       | E                 | Joystick button 2 (X) |
| Interact     | Interact     | Space             | Joystick button 3 (Y) |
| Pause        | Pause        | Escape            | Joystick button 7 (Start)|
| Shoot        | Fire1        | Left Ctrl / mouse 0 | Joystick button 0 (A) |
| Pass         | Fire2        | Left Alt / mouse 1 | Joystick button 1 (B) |
| Cancel       | Cancel       | Escape            | Joystick button 1 (B) |

INTENTIONALLY UNBOUND (no invented physical binding; documented, deferred -
consistent with Tasks 123/124/125): LookDirection, SwitchPlayer,
SwitchDirection, Skill, SkillDirection. These remain at their safe default.

## 127.5 Implementation

- `HumanPlayerInput.Update()` now reads Sprint/Tackle/Interact/Pause through
  legacy named axes (`GetButton("Sprint")` / `GetButtonDown("Tackle"/"Interact"/
  "Pause")`) instead of raw KeyCode, so each action honors BOTH its keyboard and
  its generic gamepad binding from `InputManager.asset`. MoveDirection/Pass/
  Shoot/Cancel already used named axes and are unchanged.
- `ProjectSettings/InputManager.asset`: added 4 legacy axes (Sprint, Tackle,
  Interact, Pause), each `type: 0` with the established keyboard
  `positiveButton` preserved and a generic `altPositiveButton` joystick binding.
- `ProjectSettings/ProjectSettings.asset`: `activeInputHandler` 1 -> 2 (Both).

## 127.6 Human Provider Integration

Controller support integrates into the single authoritative Human Input Provider
`HumanPlayerInput` (the `NoParallelHumanInputAbstraction_Exists` boundary keeps
it that way). No new provider class, no ControllerInputManager/GamepadManager/
ControllerSystem/DeviceManager type was created.

## 127.7 Device Abstraction Review

- No new input framework and no parallel controller abstraction.
- `Football.Core` asmdef has zero UnityEngine references; Core stays
  device-neutral (cannot read Unity input APIs).
- All device reads remain confined to `HumanPlayerInput.Update()`.
- `activeInputHandler: 2` makes the legacy framework functional at runtime; the
  Input System package remains installed but unused by production code.

## 127.8 Focused Tests

Added `ControllerInputContractTests.cs` (12 tests, GUID
`bfc6f4e8f0a24c7d9e1f3a5c7e9b0d2f`) proving the legacy Input Manager is
authoritative, `activeInputHandler` is 2, every controller axis carries its
established keyboard binding plus its generic joystick binding, existing
gamepad bindings (A/B/Cancel) are preserved, no parallel controller abstraction
exists, HumanPlayerInput implements the full 14-member contract, all device
reads are confined to the single provider, Core has no Unity-input dependency,
and the intentionally unbound actions remain at safe defaults. Focused run:
12/12 PASS.

## 127.9 Full Regression / Integrity Audit

- Full EditMode regression: **1655/1655 PASS** (1643 baseline + 12 new
  ControllerInputContractTests).
- `D:\Projects\unity\ofc` untouched (all 117 baseline files unchanged).
- asmdefs unchanged (Core zero refs; Input refs Core only).
- No parallel controller abstraction, no Core Unity-input dependency, no gameplay
  input bypass.
- `.meta` for `ControllerInputContractTests.cs.meta` valid (minimal 2-line form);
  GUID unique.

## 127.10 Architecture Documentation

This section (Task 127) appended without altering Tasks 117-126.

## 127.11 Final Verification / Gate

- **PASS** - controller support delivered on the kept legacy Input Manager
  framework (user-confirmed), with the `activeInputHandler` config corrected so
  the legacy framework functions at runtime.
- Framework / device-scope / mapping / implementation / provider-boundary /
  abstraction / focused-tests / full-regression / integrity gates all verified.
- Deferred: Keyboard -> Task 128, Dead Zones -> 129, Sensitivity -> 130,
  Remapping -> 131, Buffering -> 132, Priority -> 133 (incl. Cancel/Pass button
  collision), Simultaneous-Input -> 134.

## 127.12 Scope boundary

No new keyboard framework, no mobile framework, no dead zones, no sensitivity,
no remapping, no buffering, no priority, no simultaneous-input resolution
(including the documented Cancel/Pass same-button collision), no Input System
migration, and no gameplay implementation. No Task 128 work performed.

---

# Task 128 — Keyboard Support

## 128.1 Audit / Discovery

Task 128 establishes and verifies KEYBOARD INPUT support at the device boundary
and its mapping into the device-neutral IPlayerInput/InputFrame architecture.

FRAMEWORK (FACT): Legacy `UnityEngine.Input` / Input Manager is the authoritative
keyboard input framework (confirmed, user-decided in Task 127).

AUDIT FINDINGS (all FACT):

- All device reads are confined to `HumanPlayerInput.Update()` (lines 34-41).
  No keyboard reads exist anywhere else in Runtime.
- Other providers (AIPlayerInput, ReplayPlayerInput, NetworkPlayerInput) are
  pure data-driven — no device reads.
- `activeInputHandler: 2` (Both) is compatible with legacy keyboard behavior.
- Core (Football.Core.asmdef `references: []`) has zero Unity references;
  structurally cannot read input APIs.
- InputFrame is pure data, device-neutral.
- No `Keyboard`/`InputSystem`/`InputAction`/`InputControl`/`KeyCode` type
  references exist anywhere in Runtime outside comments.

## 128.2 Existing Keyboard Input Analysis

ALL 8 supported actions already have correct keyboard bindings:

| Action       | Axis       | Keyboard binding(s)         | API call              | Temporal   |
|--------------|------------|-----------------------------|-----------------------|------------|
| MoveDirection| Horizontal | right/left/a/d              | GetAxisRaw            | continuous |
| MoveDirection| Vertical   | up/down/w/s                 | GetAxisRaw            | continuous |
| Sprint       | Sprint     | left shift                  | GetButton             | continuous |
| Pass         | Fire2      | left alt / mouse 1          | GetButtonDown         | transient  |
| Shoot        | Fire1      | left ctrl / mouse 0         | GetButtonDown         | transient  |
| Tackle       | Tackle     | e                           | GetButtonDown         | transient  |
| Interact     | Interact   | space                       | GetButtonDown         | transient  |
| Cancel       | Cancel     | escape                      | GetButtonDown         | transient  |
| Pause        | Pause      | escape                      | GetButtonDown         | transient  |

Actions intentionally UNBOUND (deferred, consistent with Tasks 123/124/125):
LookDirection, SwitchPlayer, SwitchDirection, Skill, SkillDirection.

No duplicate keyboard abstractions found. No keyboard reads bypass
HumanPlayerInput. No keyboard types in Core/InputFrame.

## 128.3 Keyboard Mapping Decision

**Outcome A: NO PRODUCTION CHANGE REQUIRED.**

The audit proved keyboard support already fully exists. The authoritative
keyboard mapping is the existing one established by Tasks 117-126 (legacy
InputManager axes) and enhanced in Task 127 (which added controller bindings
alongside the existing keyboard bindings, without modifying them).

## 128.4 Device-to-Input Mapping Design

The mapping is already implemented:

    W/A/S/D or Arrow Keys
       |
       v
    Horizontal/Vertical axes (InputManager.asset)
       |
       v
    GetAxisRaw → HumanPlayerInput.MoveDirection

    Left Shift → "Sprint" axis → GetButton → Sprint (held)
    E          → "Tackle" axis → GetButtonDown → Tackle (one-shot)
    Space      → "Interact" axis → GetButtonDown → Interact (one-shot)
    Left Alt   → "Fire2" axis → GetButtonDown → Pass (one-shot)
    Left Ctrl  → "Fire1" axis → GetButtonDown → Shoot (one-shot)
    Escape     → "Cancel" axis → GetButtonDown → Cancel (one-shot)
    Escape     → "Pause" axis → GetButtonDown → Pause (one-shot)

Keyboard + controller map to the SAME logical provider member for each action.
No separate KeyboardSprint/ControllerSprint states exist.

## 128.5 Implementation

**No production changes were required for Task 128.** Keyboard support already
correctly exists in HumanPlayerInput + InputManager.asset. The implementation
path is: physical key → legacy InputManager named axis → HumanPlayerInput →
IPlayerInput → InputFrame → Future Gameplay.

## 128.6 Human Provider Integration

Keyboard input enters through HumanPlayerInput (the single human input
provider) and exits as device-neutral IPlayerInput values. The provider serves
as the translation boundary for BOTH keyboard and controller input. The same
`GetButton("Sprint")` call receives `left shift` (keyboard) and `joystick
button 4` (controller) through the same legacy InputManager axis — no
separate device paths.

## 128.7 Gameplay Device-Independence Review

- Core boundary: Football.Core.asmdef has zero Unity references. Clean.
- Gameplay boundary: no gameplay code reads keyboard input. Clean.
- Provider boundary: all device reads in HumanPlayerInput. Clean.
- Device leaks: none.
- No pre-existing violations found in this scope.

## 128.8 Focused Tests

Added `KeyboardInputContractTests.cs` (19 tests, GUID
`c3e5f7a9b1d24e6f8a0c2d4e6f8b0a2c`) proving:
- Legacy Input Manager is the authoritative keyboard framework.
- `activeInputHandler: 2` is compatible with legacy keyboard behavior.
- MoveDirection has WASD + arrow key bindings in InputManager.asset.
- Sprint/Tackle/Interact/Pause/Shoot/Fire2/Cancel have correct keyboard
  bindings verified against InputManager.asset.
- HumanPlayerInput reads keyboard through named legacy axes.
- No KeyboardInputProvider/KeyboardInputManager/etc. exists.
- Core/InputFrame contain no keyboard/device types.
- Temporal semantics (continuous vs transient) preserved.
- Unbound actions (SwitchPlayer/SwitchDirection/Skill/SkillDirection/
  LookDirection) have no named axes in InputManager.asset.
- HumanPlayerInput implements full IPlayerInput contract.
- All device reads confined to HumanPlayerInput.

Focused run: **19/19 PASS.**

## 128.9 Full Regression / Integrity Audit

- Full EditMode regression: **1674/1674 PASS** (1655 baseline + 19 new
  KeyboardInputContractTests).
- `D:\Projects\unity\ofc` untouched (all 117 baseline files unchanged).
- asmdefs unchanged (Core zero refs; Input refs Core only).
- No keyboard types in Core/InputFrame, no duplicate keyboard abstraction,
  no gameplay keyboard reads, no device leakage.
- `.meta` for `KeyboardInputContractTests.cs.meta` valid (minimal 2-line
  form); GUID unique.

## 128.10 Architecture Documentation

This section (Task 128) appended without altering Tasks 117-127.

## 128.11 Final Verification / Gate

- **PASS — NO PRODUCTION CHANGE REQUIRED.** Keyboard support already fully
  exists, established by Tasks 117-126 and preserved by Task 127.
- Legacy Input Manager remains authoritative keyboard framework.
- No New Input System migration occurred.
- All 8 supported keyboard actions correctly mapped through named legacy axes.
- 5 unbound actions intentionally deferred (SwitchPlayer/SwitchDirection/Skill/
  SkillDirection/LookDirection).
- Keyboard + controller converge at HumanPlayerInput for every supported action.
- Gameplay/ Core device-independence verified.
- Deferred: Dead Zones -> 129, Sensitivity -> 130, Remapping -> 131,
  Buffering -> 132, Priority -> 133, Simultaneous Input -> 134.

## 128.12 Scope boundary

No controller support changes (Task 127 scope), no mobile support, no dead
zones, no sensitivity, no remapping, no buffering, no priority, no
simultaneous-input handling, no Input System migration, no gameplay
implementation. No Task 129 work performed.

---

# Task 129 — Dead Zones

## 129.1 Audit / Discovery

Task 129 concerns DEAD-ZONE PROCESSING for analog/directional input — the
input-level responsibility of ignoring insignificant analog stick/axis
movement while preserving meaningful input.

AUDIT FINDINGS (all FACT):

- The Legacy Input Manager's `dead` field in `InputManager.asset` IS the
  dead-zone authority. No custom dead-zone code exists in Runtime.
- Joystick Horizontal (type 2, left stick X): `dead: 0.19`
- Joystick Vertical (type 2, left stick Y): `dead: 0.19`
- All keyboard/button axes (type 0): `dead: 0.001` (negligible noise
  suppression — does not affect digital input).
- Mouse axes (type 1): `dead: 0` (no dead zone).
- `HumanPlayerInput` uses `GetAxisRaw()`, which passes through the InputManager
  dead zone before returning the value.
- No custom threshold, magnitude check, or ClampMagnitude call exists in Runtime.
- `Core`/`InputFrame` perform no dead-zone filtering.
- `LookDirection`, `SwitchDirection`, `SkillDirection` are unbound (Vector2.zero
  default) — no analog source, so no dead-zone processing applies to them.
- No duplicate dead-zone authority exists.

## 129.2 Existing Axis / Stick Processing Analysis

| Directional Input | Source               | Analog/Digital | Dead-Zone Config    |
|-------------------|----------------------|----------------|---------------------|
| MoveDirection     | Horizontal/Vertical  | Analog+Digital | `dead: 0.19` (joy)  |
| LookDirection     | Unbound              | N/A            | N/A                 |
| SwitchDirection   | Unbound              | N/A            | N/A                 |
| SkillDirection    | Unbound              | N/A            | N/A                 |

MoveDirection is the ONLY directional input currently receiving analog data.
The joystick axes (type 2) have `dead: 0.19`; the keyboard axes (type 0)
have `dead: 0.001`.

`GetAxisRaw()` returns the raw input value AFTER the dead zone is applied
but BEFORE smoothing. This is the established Task 118 semantics.

## 129.3 Dead-Zone Ownership Decision

**The Legacy Input Manager IS the single authoritative dead-zone owner.**

The `dead: 0.19` value on the joystick Horizontal/Vertical axes is Unity's
standard default for left-stick analog dead zones. It is an axial dead zone
(each axis independently clamped below threshold). `GetAxisRaw()` passes
through this value.

No custom dead-zone processing layer is needed. The InputManager config
IS the mechanism.

## 129.4 Processing API Design

**NO API CHANGE REQUIRED.**

The existing InputManager `dead: 0.19` on the joystick axes IS the
dead-zone mechanism. `GetAxisRaw()` passes through it. No custom API,
no new utility, no new configuration object is required.

## 129.5 Implementation

**No production changes were required for Task 129.**

The Legacy Input Manager's `dead` field is the dead-zone authority. The
configuration is already correct (0.19 on joystick axes, negligible on
keyboard/button axes). No custom code exists or is needed.

## 129.6 Provider Integration

    Controller Analog (left stick)
       |
       v
    InputManager dead zone (dead: 0.19)
       |
       v
    GetAxisRaw("Horizontal"/"Vertical")
       |
       v
    HumanPlayerInput.MoveDirection
       |
       v
    IPlayerInput / InputFrame
       |
       v
    Future Gameplay

Single authority (InputManager). No duplicate processing. Keyboard input
(digital) passes through with negligible dead zone (0.001).

## 129.7 Input Consistency / Edge-Case Review

- Analog below dead zone (|value| < 0.19): GetAxisRaw returns 0.
- Analog above threshold: raw value preserved.
- Keyboard input: effectively no dead zone (0.001). Digital keys produce
  full -1/0/1 via GetAxisRaw.
- No mixing of dead zone with sensitivity (Task 130) or remapping (Task 131).
- Transient actions (Pass/Shoot/etc.) use GetButtonDown — unaffected by dead
  zones entirely.
- MoveDirection remains semantically compatible with Task 118 (raw passthrough).

## 129.8 Focused Tests

Added `DeadZoneInputContractTests.cs` (12 tests, GUID
`d4a6e8c0f2b34d7e9a1c3e5f7b9d0a2c`) proving:
- Joystick Horizontal/Vertical have positive dead zone configured.
- Dead zone is the standard Unity default (0.19).
- Keyboard/button axes have negligible dead zone (0.001).
- activeInputHandler = 2 (Both) allows legacy dead-zone mechanism.
- No custom dead-zone processing code exists in Runtime.
- HumanPlayerInput does not apply a second dead zone.
- No duplicate dead-zone authority (no DeadZoneManager/etc.).
- Core has no dead-zone processing.
- MoveDirection uses GetAxisRaw (passes through InputManager dead zone).
- No sensitivity/remapping logic introduced (Tasks 130/131 scope).
- Unbound directional inputs have no dead zone processing.
- Button axes unaffected by dead zone.

Focused run: **12/12 PASS.**

## 129.9 Full Regression / Integrity Audit

- Full EditMode regression: **1686/1686 PASS** (1674 baseline + 12 new
  DeadZoneInputContractTests).
- `D:\Projects\unity\ofc` untouched (all 117 baseline files unchanged).
- asmdefs unchanged. Core remains device-neutral.
- No dead-zone code introduced, no sensitivity/remapping leaked.
- `.meta` for `DeadZoneInputContractTests.cs.meta` valid; GUID unique.

## 129.10 Architecture Documentation

This section (Task 129) appended without altering Tasks 117-128.

## 129.11 Final Verification / Gate

- **PASS — NO PRODUCTION CHANGE REQUIRED.** The Legacy Input Manager's
  `dead: 0.19` on the joystick axes IS the dead-zone authority.
- Single dead-zone authority: InputManager.asset.
- No duplicate filtering, no custom dead-zone code, no new abstractions.
- Keyboard/digital input unaffected. MoveDirection compatible with Task 118.
- Deferred: Sensitivity -> 130, Remapping -> 131, Buffering -> 132,
  Priority -> 133, Simultaneous Input -> 134.

## 129.12 Scope boundary

No sensitivity, no remapping, no buffering, no priority, no
simultaneous-input handling, no framework migration, no gameplay
implementation. No Task 130 work performed.

# Task 130 — Sensitivity

## 130.1 Audit / Discovery

- The ONLY sensitivity mechanism in the repository is the Legacy Input
  Manager's per-axis `sensitivity:` field in
  `ProjectSettings/InputManager.asset`. No custom sensitivity code exists
  anywhere in Runtime.
- `HumanPlayerInput` reads MoveDirection via
  `Input.GetAxisRaw("Horizontal"/"Vertical")` (raw passthrough) and all
  other actions via `GetButton`/`GetButtonDown` on named axes.
- Per legacy Input Manager semantics, `GetAxisRaw` applies only the axis
  dead zone; it does NOT apply sensitivity or gravity smoothing
  (`GetAxis` would apply those). ProjectSettings `activeInputHandler: 2`
  (Both) keeps the legacy framework authoritative.
- Actual per-axis configuration (name | type | sensitivity | gravity | dead):
  - Horizontal | type 0 (keyboard) | sensitivity 3 | gravity 3 | dead 0.001
  - Vertical | type 0 (keyboard) | sensitivity 3 | gravity 3 | dead 0.001
  - Horizontal | type 2 (joystick) | sensitivity 1 | gravity 0 | dead 0.19
  - Vertical | type 2 (joystick) | sensitivity 1 | gravity 0 | dead 0.19
  - Sprint / Tackle / Interact / Pause and other button axes (type 0):
    sensitivity 1000 | gravity 1000 | dead 0.001 (digital stock defaults)
  - Mouse axes (type 1): sensitivity 0.1 | gravity 0 | dead 0 (unused by this
    project's logical contract).
- No documentation or test in the repository defines an intended sensitivity
  value or range. The configured values are Unity stock defaults, not
  project-specific product tuning.
- Core (`Football.Core`) / `InputFrame` contain no sensitivity state; Core has
  no device dependencies (asmdef `references: []`).

FACT / INFERENCE / UNKNOWN:
- FACT: InputManager `sensitivity:` is the only sensitivity mechanism.
- FACT: `GetAxisRaw` is used for MoveDirection (raw passthrough), so
  sensitivity/gravity smoothing is not applied to the consumed value.
- FACT: No sensitivity value/range product requirement exists in the repo.
- INFERENCE: `GetAxisRaw` bypasses sensitivity/gravity per legacy Input
  Manager semantics.
- UNKNOWN: Intended product sensitivity tuning (value + range) for gameplay.
- DEFERRED: Any sensitivity product tuning.

## 130.2 Existing Sensitivity Handling Analysis

- Sensitivity is structurally owned by the Input Manager (`sensitivity:`
  field) but is currently INACTIVE for the consumed MoveDirection path,
  because the provider reads via `GetAxisRaw` (which does not apply
  sensitivity/gravity). This is the deliberate Task 118 "raw passthrough"
  contract, enforced by `MoveInputContractTests`.
- InputManager can apply sensitivity only through `GetAxis`, which this
  project deliberately does not use for MoveDirection.
- Dead zone and gravity are separate per-axis InputManager concepts and are
  NOT modified by sensitivity work.
- Required changes (Task 130): NONE to production. No value is invented
  because no product tuning requirement exists.

## 130.3 Sensitivity Ownership Decision

- **Owner: Legacy Input Manager (`ProjectSettings/InputManager.asset`
  `sensitivity:` field).** This is the single authoritative sensitivity
  mechanism, consistent with Tasks 127-129 keeping the legacy framework
  authoritative.
- Configuration responsibility: Input Manager (per-axis `sensitivity`).
- Runtime responsibility: `HumanPlayerInput`, which delegates to the Input
  Manager; no custom processing is added.
- Product tuning status: UNDEFINED. No intended value/range is defined
  anywhere; the current values are Unity defaults.
- Deferred: any sensitivity product tuning until a product requirement
  defines a value/range.

Status decision: **PASS WITH DEFERRED — MECHANISM EXISTS BUT PRODUCT TUNING
IS UNDEFINED.**

## 130.4 Processing / Configuration Design

- Final design: NO API CHANGE, NO configuration change. The Input Manager
  `sensitivity:` field remains the authority.
- No `SensitivityConfig`, `SensitivityManager`, or custom response curves are
  introduced because no repository evidence requires them, and they would
  violate the Task 118 raw-passthrough contract.
- Sensitivity and dead zone remain separate InputManager per-axis fields.
- Sensitivity and gravity remain separate InputManager per-axis fields.
- Normalization is NOT applied to MoveDirection (raw passthrough preserved).

## 130.5 Implementation

- **NO PRODUCTION CHANGE REQUIRED for Task 130.**
- No production/configuration file was modified. Actual configured values
  (sensitivity 3/1/1000/0.1, gravity 3/0/1000/0, dead 0.19/0.001/0) were
  verified but intentionally not changed.
- No custom sensitivity code added. Dead zone remains the Task 129 authority.
- Task 118 MoveDirection semantics (raw, non-normalized) preserved.

## 130.6 Provider Integration

- Flow:
  Device -> InputManager (sensitivity/gravity/dead config)
  -> HumanPlayerInput (GetAxisRaw) -> IPlayerInput -> InputFrame.
- Sensitivity lives in the Input Manager configuration layer, NOT in
  `HumanPlayerInput`, `IPlayerInput`, or `InputFrame`.
- No `Sensitivity` field exists in `InputFrame` or Core. Sensitivity is
  processing/configuration, not gameplay state.
- Unbound directional inputs (LookDirection, SwitchDirection, SkillDirection)
  remain unbound; NO sensitivity behavior was fabricated for them.

## 130.7 Value / Range / Edge-Case Review

- sensitivity = 0 / very low / 1 / high: because MoveDirection is read via
  `GetAxisRaw`, the configured sensitivity is currently not applied to it.
  No product tuning requirement exists to choose these values.
- Keyboard/digital input: button axes use the digital stock default
  (sensitivity 1000) so a held key registers immediately (via GetButton
  semantics, which differ from axis smoothing). Not treated as analog.
- Controller analog input: joystick Horizontal/Vertical (type 2) have
  sensitivity 1, gravity 0 and pass through the Task 129 dead zone (0.19).
- Stick center / near center / near full / reversal: handled by Input Manager
  dead zone + raw passthrough; sensitivity smoothing is not active on the
  consumed path. No velocity/acceleration smoothing exists.
- Interaction with GetAxisRaw: sensitivity/gravity NOT applied (raw).
- TUNING UNDEFINED: any ideal sensitivity value/range is not established by
  any repository or user requirement and is NOT invented here.

Sensitivity vs Dead Zone vs Gravity vs Normalization (separation):
- Sensitivity = input responsiveness/processing responsibility (Task 130).
- Dead Zone = separate input filtering responsibility (Task 129).
- Gravity = separate InputManager return-smoothing concept (not applied via
  GetAxisRaw).
- Normalization = separate concern; MoveDirection is raw and non-normalized.

## 130.8 Focused Tests

Added `SensitivityInputContractTests.cs` (9 tests, GUID
`7f838746941a7aaae25e69422d37b107`) proving:
- Directional axes (keyboard + joystick) have an InputManager sensitivity
  field.
- Sensitivity and dead zone are separate configured fields.
- Sensitivity and gravity are separate configured fields and joystick gravity
  is 0.
- No custom sensitivity/response types exist in Runtime.
- MoveDirection is read via `GetAxisRaw` (not `GetAxis`), preserving the raw
  passthrough contract.
- No Sensitivity field/property/parameter leaks into Core/InputFrame.
- Exactly one keyboard and one joystick movement axis exist (distinct).
- Button axes use the digital stock default (sensitivity >= 1000).
- activeInputHandler = 2 (Both) allows the legacy sensitivity mechanism.

Focused run: **9/9 PASS.**

## 130.9 Full Regression / Integrity Audit

- Full EditMode regression: **1695/1695 PASS** (1686 baseline + 9 new
  SensitivityInputContractTests).
- No compile errors; no Task-130-caused warnings.
- `D:\Projects\unity\ofc` untouched: all 117 baseline files verified 0 missing
  / 0 changed.
- asmdefs unchanged; Core remains device-neutral.
- No New Input System migration; no parallel sensitivity framework; no
  duplicate sensitivity authority.
- No remapping/buffering/priority/simultaneous logic introduced.
- `.meta` for `SensitivityInputContractTests.cs.meta` valid (`fileFormatVersion:
  2`, 32-hex GUID); GUID unique in Assets.

## 130.10 Architecture Documentation

This section (Task 130) appended without altering Tasks 117-129.

## 130.11 Final Verification / Gate

- **PASS WITH DEFERRED — MECHANISM EXISTS BUT PRODUCT TUNING IS UNDEFINED.**
- "No production changes were required for Task 130."
- Single sensitivity authority: Legacy Input Manager `sensitivity:` field.
- No duplicate sensitivity processing; no speculative sensitivity layer.
- Sensitivity is separate from dead zone, gravity, and normalization.
- Keyboard input not processed as analog sensitivity; controller analog
  behavior correct (dead zone passthrough).
- MoveDirection remains compatible with Task 118.
- Unbound inputs were not fabricated.
- Legacy Input Manager remains authoritative; no New Input System, no
  parallel framework.
- Sensitivity is NOT gameplay state; no Sensitivity field in Core/InputFrame.
- Deferred: Remapping -> 131, Buffering -> 132, Priority -> 133,
  Simultaneous Input -> 134.
- No Task 131 work performed.

## 130.12 Scope boundary

No remapping, no buffering, no input priority, no simultaneous-input
handling, no framework migration, no gameplay implementation, no sensitivity
product tuning invented. No Task 131 work performed.

# Task 131 — Remapping

## 131.1 Audit / Discovery

- Task 131 concerns the INPUT BINDING / CONTROL CONFIGURATION layer:
  - **Mapping** = default physical control → logical action.
  - **Remapping** = changing the physical control assigned to an existing
    logical action, WITHOUT changing the logical action's meaning.
- The audit found NO runtime/user-configurable remapping system anywhere in
  the repository:
  - No PlayerPrefs usage in any `.cs`.
  - No JSON binding storage / JsonUtility binding persistence (the only
    JsonUtility hit is a test reading asmdef metadata, unrelated).
  - No ScriptableObject binding model (existing ScriptableObjects are data
    configs, none store key bindings).
  - No InputActionRebinding, no remapping UI, no persistence, no
    reset-to-default API, no conflict handling.
  - No `RemappingManager` / `KeyBindingManager` / `ControlMapper` /
    `BindingSystem` / `KeyConfigSystem`.
- The ONLY binding mechanism is the **static/default** Legacy Input Manager
  mapping (`InputManager.asset` `positiveButton` / `altPositiveButton` /
  `axis`), consumed by `HumanPlayerInput` via named axes and
  `GetButton`/`GetButtonDown`. This is configuration-time/static default
  mapping, NOT runtime player remapping.
- Only `HumanPlayerInput.Update()` touches `UnityEngine.Input` in all
  Runtime; no `KeyCode`, `Keyboard`, `Gamepad`, `InputAction`, or
  `InputControl` anywhere in Runtime. Core (`Football.Core`, asmdef
  `references: []`) structurally cannot read input APIs.

## 131.2 Existing Remapping Analysis (action-by-action)

Every logical action has a stable default/static mapping but NO runtime
remapping mechanism. `remappable = false` for all (no runtime rebinding):

| Logical action | Default/static binding (FACT) | Runtime remappable |
|---|---|---|
| MoveDirection.x | Horizontal: right/d + alt a, neg left/a; joy axis 0 | No |
| MoveDirection.y | Vertical: up/w + alt s, neg down/s; joy axis 1 | No |
| Sprint | Sprint: left shift / joy button 4 | No |
| Tackle | Tackle: e / joy button 2 | No |
| Interact | Interact: space / joy button 3 | No |
| Pause | Pause: escape / joy button 7 | No |
| Shoot | Fire1: left ctrl | No |
| Pass | Fire2: left alt | No |
| Cancel | Cancel: escape / joy button 1 | No |
| LookDirection / SwitchPlayer / SwitchDirection / Skill / SkillDirection | Unbound | N/A (no mapping) |

- Persistence: none. Reset-to-default: none (nothing to reset). Conflict
  handling: none. Static vs runtime: static only.

## 131.3 Remapping Decision

- **Interpretation**: Task 131's safe meaning in this project is
  `PASS WITH DEFERRED` — DEFAULT BINDINGS EXIST AND ARE VALID, but
  player-facing runtime remapping is NOT YET DEFINED/IMPLEMENTED.
- Ownership: Legacy Input Manager (static/default bindings) is the sole
  existing binding authority.
- Required implementation scope: NONE. No rebinding API, no binding data
  model, no runtime override, no persistence, no reset, no conflict
  handling, no UI — none are semantically defined by the repository or a
  product requirement, so none are invented.
- Deferred: any runtime/user remapping product (save format, persistence,
  UI, conflict resolution) remains future work pending a product decision.

## 131.4 Device-to-Input Mapping Design

No runtime remapping design is justified. The target relationship remains:
Physical Control → Mapping (InputManager) → HumanPlayerInput → IPlayerInput
→ InputFrame → Gameplay. Binding configuration stays OUTSIDE InputFrame and
IPlayerInput (which carry input VALUES, not control configuration). No
generic binding framework, no New Input System, no IPlayerInput/InputFrame
semantic change.

## 131.5 Implementation

- **NO PRODUCTION CHANGE REQUIRED for Task 131.**
- "Runtime/user remapping remains deferred."
- No speculative remapping manager, UI, persistence schema, conflict
  resolver, or rebinding workflow was created.
- Logical action semantics unchanged; default mappings unchanged; no
  duplicate binding authority introduced; no future-task functionality
  leaked in.

## 131.6 Human Provider Integration

- `HumanPlayerInput` remains the Human Input Provider, consuming the
  static/default InputManager bindings via named axes/buttons. There is no
  active runtime binding configuration for the provider to consume because
  runtime remapping does not exist.
- No device-specific logical fields (KeyboardPass/ControllerPass/etc.)
  introduced; logical actions remain Pass/Shoot/Sprint/etc.

## 131.7 Gameplay Device-Independence Review

- No gameplay code reads physical controls directly; the only
  `UnityEngine.Input` usage is `HumanPlayerInput.Update()`.
- Gameplay depends on logical actions only; remapping would not require
  gameplay changes. Core and InputFrame are device-neutral.
- Binding configuration is configuration, not gameplay state.
- No pre-existing, Task-131-caused, or newly introduced device bypass.

## 131.8 Focused Tests

Added `RemappingInputContractTests.cs` (10 tests, GUID
`ba636fb38563caee201eb832bcfd0381`) proving:
- Keyboard and controller default bindings exist and are stable.
- Logical actions (Sprint/Tackle/Interact/Pause/Shoot/Pass/Cancel) keep
  their default mappings.
- Controller default bindings (joystick buttons) are intact.
- No runtime remapping system / speculative remapping types exist.
- HumanPlayerInput does not use hardcoded KeyCodes (no device bypass).
- InputFrame and IPlayerInput expose no binding configuration.
- InputFrame carries input values, not physical control configuration
  (device-neutral).
- Core/IPlayerInput/InputFrame have no device-type dependencies.
- Single binding authority: InputManager only (no code-side binding type).
- activeInputHandler = 2 (Both) keeps legacy bindings functional.

Focused run: **10/10 PASS.**

## 131.9 Full Regression / Integrity Audit

- Full EditMode regression: **1705/1705 PASS** (1695 baseline + 10 new
  RemappingInputContractTests).
- No compile errors; no Task-131-caused warnings (expected pre-existing
  CS0219 in FixedTimestepPolicyTests.cs untouched).
- `D:\Projects\unity\ofc` untouched: all 117 baseline files verified
  0 missing / 0 changed.
- asmdefs unchanged; Core remains device-neutral; no New Input System
  migration; no duplicate binding authority; no remapping framework.
- `.meta` for `RemappingInputContractTests.cs.meta` valid
  (`fileFormatVersion: 2`, 32-hex GUID); GUID unique in Assets.

## 131.10 Architecture Documentation

This section (Task 131) appended without altering Tasks 117-130.

## 131.11 Final Verification / Gate

- **PASS WITH DEFERRED** — DEFAULT BINDINGS EXIST AND ARE VALID; PLAYER-FACING
  RUNTIME REMAPPING IS NOT YET DEFINED/IMPLEMENTED.
- Mapping vs Remapping distinction is clear; existing capability audited.
- Default bindings correct; logical action semantics unchanged; physical
  controls are configuration, not gameplay state.
- InputFrame/IPlayerInput contain no binding configuration; both remain
  device-neutral.
- Runtime remapping NOT implemented → explicitly deferred (not claimed).
- Keyboard/controller behavior correct; no New Input System; no duplicate
  binding framework.
- HumanPlayerInput remains the provider boundary; gameplay does not know
  physical controls; Core device-neutral.
- Deferred: Remapping runtime → future product decision; Buffering -> 132,
  Priority -> 133, Simultaneous Input -> 134.
- No Task 132 work performed.

## 131.12 Scope boundary

Runtime/user remapping deferred; no buffering, no input priority, no
simultaneous-input handling, no framework migration, no gameplay
implementation, no speculative remapping infrastructure. No Task 132 work
performed.

# Task 132 — Button Buffering

## 132.1 Audit / Discovery

- Task 132 concerns Button Buffering:
  - **Button Buffering** = temporary retention of transient input intent (a
    short bounded window after the press so the request can be consumed when
    the gameplay action becomes available).
  - Buffering is NOT input detection, NOT Input Priority (133), NOT
    Simultaneous Input handling (134), and NOT gameplay action execution.
- The audit found NO input buffering anywhere in the repository:
  - No CommandQueue / ActionQueue / InputBuffer / BufferedInput / pending-input
    retention code.
  - No buffer duration, queue depth, ordering, duplicate, expiration, or
    consumption policy is defined in any source, data, or doc.
  - The only `timestamp`/`Time` usage in Runtime is gameplay match-clock
    event data (`GameClock`, `BallKickedEvent`, `GoalScoredEvent`, etc.) — a
    Core match simulation concern, unrelated to input buffering.
- The established architecture treats every one-shot transient action as an
  UNBUFFERED one-frame pulse:
  - `InputFrame` is documented as a PURE-DATA SNAPSHOT that must NOT contain
    buffering/history/priority/simultaneous-input policy (`InputFrame.cs:9-12`).
  - Existing action contract tests enforce
    "transient one-shot pulse: no buffering/queue/priority machinery"
    (e.g. `PassInput_IsTransientPulse_NoBufferingQueueOrPriorityMachinery`
    forbids `PassBuffer`/`PassQueue`/`PassHistory`/etc.).
- No gameplay consumer of input exists yet (IPlayerInput is not consumed by
  any production code), so no buffered-input consumption path exists.

## 132.2 Existing Input Timing / Buffering Analysis

Per-action timing matrix (FACT): every one-shot action (Pass / Shoot /
Tackle / SwitchPlayer / Skill / Interact / Cancel / Pause) is produced as a
one-frame boolean pulse via the provider boundary (HumanPlayerInput uses
`GetButtonDown`; AI/Replay/Network providers assign a value per frame). Each
is: one-shot, visible for one sampling point, NOT stored, NOT expired, NOT
consumable later, and immediately lost if not consumed. No timestamps, no
frame numbers, no retention.

- One-frame input sampling is intentionally NOT "buffering"; no request is
  retained beyond its sampling moment. This is verified behavior, not a gap.
- `IsEnabled=false` short-circuits `HumanPlayerInput.Update()` (no sample),
  which is unrelated to buffering.

## 132.3 Buffer Ownership Decision

The established architecture dictates the buffering boundary:
- Capture/storage/expiration/consumption of buffered intent would be owned at
  the transient-intent retention boundary (a future input-processing layer),
  NOT by Core and NOT inside `InputFrame`.
- `InputFrame` remains a pure-data snapshot (no buffer state).
- Gameplay eligibility and execution remain gameplay-owned.
- The audit found NO existing owner component and NO defined buffering
  semantics, so ownership cannot be finalized to a concrete class without
  inventing behavior. Ownership is therefore **deferred** (architecturally
  located, not implemented).

## 132.4 Buffer Data / API Design

No buffer data/API is defined because the repository defines no duration,
queue depth, ordering, duplicate, expiration, or consumption semantics, and
no gameplay consumer exists. Per the task's no-invent rule, NO duration
(e.g. 50/100/150/200/500 ms), queue depth, ordering, duplicate, expiration,
or consumption policy was invented. Design outcome: build nothing;
semantics are UNDEFINED/DEFERRED pending product decisions.

## 132.5 Implementation

- **NO PRODUCTION CHANGE REQUIRED for Task 132.**
- No `BufferManager`, `InputBufferManager`, `CommandQueue`, `ActionQueue`,
  `BufferedInputSystem`, or any buffering type/constant was created.
- No duration/queue/ordering/duplicate/expiration policy invented.
- No priority (133) or simultaneous-input (134) logic introduced.
- Existing transient one-shot semantics and InputFrame pure-data boundary
  preserved.

## 132.6 Gameplay Integration

- No gameplay consumer of buffered input exists (IPlayerInput is not consumed
  by any production code). "No current gameplay consumer exists;
  buffered-input consumption is deferred."
- No fake gameplay consumer was created; no pass/shoot/tackle/skill/
  switch execution implemented; no gameplay state moved into any buffer.

## 132.7 Timing / Duplicate / Expiration Review

- Request creation: provider one-frame pulse. Buffer insertion: N/A (no
  buffer). Timestamp capture: N/A. Expiration: N/A. Consumption: N/A.
- Duplicate/repeated/different-action/same-frame/cross-frame buffering
  behavior: UNDEFINED and DEFERRED (no buffer; would be Task 132 product work).
- Update vs FixedUpdate / Time sources / pause / enable / reset: only the
  existing non-buffering provider behavior exists; no buffering time source
  is defined.
- TUNING UNDEFINED: buffer duration, queue depth, ordering, duplicate policy,
  expiration policy, consumption policy are all undefined and NOT invented.
- Ordering/duplicate semantics that would overlap priority / simultaneous
  handling: DEFERRED to Tasks 133/134 as appropriate.

## 132.8 Focused Tests

Added `BufferingInputContractTests.cs` (9 tests, GUID
`4b060866bd4de7e0497343f7376c63b1`) proving the DOCUMENTED EXISTING BEHAVIOR:
- No buffering system / buffer manager / queue type exists.
- One-shot actions (Pass/Shoot/Tackle/SwitchPlayer/Skill/Interact/Cancel/
  Pause) have no buffer/queue/retention/history types.
- One-shot actions are boolean one-shot signals on IPlayerInput/InputFrame.
- One-shot actions carry no timestamp/lifetime/frame members on the contract.
- InputFrame is a pure-data snapshot with no buffer/queue/history state.
- No buffering type is exposed by Core or the Input provider assemblies.
- No priority or simultaneous-input logic exists (Tasks 133/134 scope).
- No buffer duration/queue-policy tuning constants were invented.
- HumanPlayerInput does not retain transient requests (no buffering member).

Focused run: **9/9 PASS.**

## 132.9 Full Regression / Integrity Audit

- Full EditMode regression: **1714/1714 PASS** (1705 baseline + 9 new
  BufferingInputContractTests).
- No compile errors; no Task-132-caused warnings (expected pre-existing
  CS0219 in FixedTimestepPolicyTests.cs untouched).
- `D:\Projects\unity\ofc` untouched: all 117 baseline files verified 0
  missing / 0 changed.
- asmdefs unchanged; Core remains device-neutral; no new input framework; no
  duplicate buffer/queue authority; no gameplay queue masquerading as an
  input buffer.
- `.meta` for `BufferingInputContractTests.cs.meta` valid
  (`fileFormatVersion: 2`, 32-hex GUID); GUID unique in Assets.

## 132.10 Architecture Documentation

This section (Task 132) appended without altering Tasks 117-131.

## 132.11 Final Verification / Gate

- **PASS WITH DEFERRED.** No buffering is implemented (it is NOT claimed to
  exist); the buffering boundary is identified and all
  semantics/tuning (duration, queue depth, ordering, duplicate, expiration,
  consumption) are UNDEFINED and DEFERRED.
- No speculative buffer created; no ordinary one-frame sampling was called
  buffering.
- Single buffering authority conceptually identified (transient-intent
  boundary); no owner class built (would require inventing semantics).
- InputFrame remains a snapshot; dead zone (129), sensitivity (130),
  remapping (131), priority (133), simultaneous (134) boundaries unchanged.
- No gameplay execution, eligibility, UI, animation, physics, stamina, or
  cooldown introduced; no Task 133/134 work.
- Tests pass; full regression passes; compile clean; `.meta` valid; GUID
  unique; no unexpected files; ofc untouched.

## 132.12 Scope boundary

No buffering implemented (deferred); no input priority (133) and no
simultaneous-input handling (134) introduced; no framework migration; no
gameplay action execution; no invented durations/queue policies. No Task 133
work performed at that time.

# Task 133 — Input Priority

## 133.1 Audit / Discovery

- Task 133 concerns Input Priority / Precedence:
  - **Input Priority** = precedence between competing input intents (which
    logical input command takes precedence when multiple intents conflict or
    cannot be processed together).
  - Priority is NOT buffering (132), NOT simultaneous-input handling (134),
    and NOT gameplay execution.
- The audit found NO priority / precedence / arbitration / conflict-resolution
  logic anywhere in the repository:
  - No `PriorityManager`, `InputPriorityManager`, `CommandPrioritySystem`,
    `InputArbitrator`, `ActionArbitrator`, resolver, or precedence table.
  - No priority enum, no numeric priority constants (no 100/90/80...).
  - All grep matches are either test contracts that assert the ABSENCE of
    priority (deferred to Task 133) or unrelated `override` keyword / Unity
    MenuItem `priority` / debug-category strings.
- `IPlayerInput` exposes 14 raw logical intents with zero priority metadata.
- `InputFrame` is documented as PURE DATA that must NOT contain
  priority/simultaneous-input/resolution policy (InputFrame.cs:9-12).
- `InputFrame` has NO consumer anywhere in Runtime (never read by production
  code), so no precedence is ever applied.
- `GameStateId` / `PlayerStateId` enums enumerate future gameplay/match/player
  STATES; they encode no input precedence (they are gameplay-owned, not an
  input priority table).

## 133.2 Existing Command Conflicts Analysis

Pair-by-pair (all are raw logical intents that co-exist in the frame; NONE are
resolved anywhere):

- COMPATIBLE (naturally co-exist; not conflicts): Move+Sprint; continuous
  (Move/Look) + any one-shot; Sprint + any one-shot; action + its directional
  intent (`SwitchPlayer`+`SwitchDirection`, `Skill`+`SkillDirection`). No
  suppression is introduced for these.
- PRIORITY CONFLICT candidates (competing one-shot actions for the same
  gameplay resource — ball/action slot): Pass+Shoot, Pass+Tackle, Pass+Skill,
  Pass+Switch, Shoot+Tackle, Shoot+Skill, Shoot+Switch, Tackle+Skill,
  Tackle+Switch, Skill+Switch. No authoritative precedence rule exists for ANY
  of these → UNKNOWN/DEFERRED.
- SIMULTANEOUS-INPUT cases (the mechanics of "both present, which is
  processed") → Task 134-owned.
- GAMEPLAY-owned conflicts: action legality (based on state/possession),
  player-switch selection (documented in InputFrame.cs as a gameplay
  responsibility), Pause (game-state gate), Cancel (contextual), Interact
  (contextual).

## 133.3 Priority Rules Decision

- Does the repository define any authoritative priority rules? **NO** (FACT).
- Can a minimal priority contract safely be defined? **NO** — no precedence
  pair, no consumer, and none may be invented (no-arbitrary-numbers rule, no
  common-football-game-convention assumption such as
  "Pause > Skill > Tackle > Shoot > Pass").
- Decision: **PASS WITH DEFERRED — no global priority ordering is defined;
  precedence policy is product-owned and DEFERRED.** No production change.
- Authoritative priority rules: NONE. Explicit precedence relations: NONE.
- Non-priority combinations remain compatible; Task 134 owns simultaneous
  mechanics; gameplay owns legality/selection/context; all competing
  transient-action precedence is UNDEFINED/DEFERRED.

## 133.4 Priority Model / API Design

- No new API is justified: product priority policy is undefined and no
  consumer exists → NO API / NO new type.
- The architectural boundary is the same transient-intent layer already
  identified in Task 132: precedence would live in a future
  processing/coordination layer BETWEEN `InputFrame` (raw intents) and
  Gameplay — NOT inside `IPlayerInput` and NOT inside `InputFrame`.
- `InputFrame` stays "logical input values"; unresolved conflicts remain
  visible as co-present raw intents (nothing dropped/suppressed).
- Non-goals: no `Priority:int` on inputs, no `InputPriority` enum, no global
  numeric ranking, no total ordering, no suppression, no Task 134 resolver,
  no gameplay execution.

## 133.5 Implementation

- **NO PRODUCTION CHANGE REQUIRED for Task 133.**
- No `PriorityManager` / `InputPriorityManager` / `CommandPrioritySystem` /
  `InputArbitrator` / `ActionArbitrator` or any priority type/constant created.
- No numeric priorities, no total ordering, no suppression, no conflict
  resolution, no Task 134 logic, no gameplay execution.
- `IPlayerInput` and `InputFrame` semantics unchanged (raw logical intents /
  pure-data snapshot).

## 133.6 Integration

- **"No current priority consumer exists; resolution is deferred."**
- Providers output raw logical intent and are unchanged (all four).
- InputFrame is NOT overloaded with priority state; AI/Replay/Network semantics
  remain coherent (all share `IPlayerInput`/`InputFrame`); no device-specific
  data enters any priority layer (none exists). No fake consumer created.

## 133.7 Conflict / Precedence Review

Second rigorous review (post-decision): Move+Sprint → compatible; every
continuous+one-shot and action+direction pair → compatible; each competing
transient pair (Pass/Shoot/Tackle/Skill/Switch combinations) → deferred
(product policy; simultaneous mechanics → Task 134; legality/selection →
gameplay); Pause / Cancel / Interact × anything → gameplay/state-owned.
Verified: no priority numbers invented, no total ordering invented, no hidden
suppression, no input-semantics changed. Final conflict verdict: PASS WITH
DEFERRED.

## 133.8 Focused Tests

Added `PriorityInputContractTests.cs` (10 tests, GUID
`51b8d49d69e7404b92ca800f273b3153`) proving the DOCUMENTED EXISTING BEHAVIOR
and the ABSENCE of any speculative priority system:
- No priority/precedence/arbitration/resolver type exists in Core/Input.
- No numeric priority / precedence metadata on IPlayerInput or InputFrame.
- No priority enum or constant exists.
- Providers do not resolve or suppress inputs.
- InputFrame is a pure-data snapshot with no priority/resolution state.
- Compatible inputs coexist (continuous + one-shot); none is dropped.
- No Task 134 (simultaneous) or Task 132 (buffering) type leaked in.
- No gameplay-execution / processing/coordinator layer created.
- No invented precedence policy (no last-wins/first-wins/override/suppression).
- Logical action semantics unchanged / raw intents preserved.

Focused run: **10/10 PASS.**

## 133.9 Full Regression / Integrity Audit

- Full EditMode regression: **1724/1724 PASS** (1714 baseline + 10 new
  PriorityInputContractTests).
- No compile errors; no Task-133-caused warnings (CS0219 in
  FixedTimestepPolicyTests.cs untouched, pre-existing).
- `D:\Projects\unity\ofc` untouched: all 117 baseline files verified 0
  missing / 0 changed.
- asmdefs unchanged; Core remains device-neutral; no device-specific priority
  API in Core; no duplicate priority authority; no Task 134 / buffering /
  remapping / sensitivity / dead-zone modifications.
- `.meta` for `PriorityInputContractTests.cs.meta` valid
  (`fileFormatVersion: 2`, 32-hex GUID); GUID unique in Assets.

## 133.10 Architecture Documentation

This section (Task 133) appended without altering Tasks 117-132 (only the
Task 132.12 boundary line's trailing clause was rephrased to note that Task
133 had not been performed at that time).

## 133.11 Final Verification / Gate

- **PASS WITH DEFERRED.** No global input-priority ordering is defined; that
  is intentional and safe because precedence policy is product-owned and no
  input consumer exists yet. No priority semantics were invented.
- Exactly one conceptual priority authority identified (the future
  processing/coordination layer), identical to the Task 132 transient-intent
  boundary; no owner class built (would require inventing policy).
- Provider acquires only; InputFrame remains input values/snapshot; gameplay
  owns gameplay outcomes; Task 134 owns simultaneous handling.
- Compatible inputs remain compatible; gameplay-owned conflicts remain
  gameplay-owned; unknown rules explicitly deferred.
- No dead-zone/sensitivity/remapping/buffering/simultaneous/gameplay changes;
  no Task 134 work; no arbitrary numeric rankings; no unsupported total
  ordering.
- Tests pass; full regression passes; compile clean; `.meta` valid; GUID
  unique; no unexpected files; ofc untouched.

## 133.12 Scope boundary

No global priority ordering defined (deferred); no simultaneous-input handling
(134) implemented; no buffering (132); no remapping (131); no sensitivity
(130); no dead-zone changes (129); no gameplay execution; no invented numeric
priority values. No Task 134 work performed at that time.

# Task 134 — Simultaneous-Input Handling

## 134.1 Audit / Discovery

- Task 134 concerns Simultaneous-Input Handling:
  - **Simultaneous Input** = multiple input intents being present at the same
    time / input-sample moment.
  - Simultaneous handling is NOT buffering (132) and NOT priority (133).
- The audit found NO simultaneous-input resolution system and NO runtime
  consumer:
  - No `SimultaneousInputManager`, `InputConflictManager`,
    `SimultaneousInputResolver`, `InputCombinationManager`, `MultiInputResolver`,
    or `InputArbitrator`; no combination / coexistence / suppression / gating /
    resolution code.
  - The only matches are the `InputFrame.cs:11` doc comment (explicitly
    excluding simultaneous-input policy) and scene-load conflict rejection in
    `SceneLoader`/`SceneTransitionSystem` (scene-transition state management,
    unrelated to input intents).
  - `IPlayerInput` exposes 14 independent logical values; `InputFrame` is a
    pure-data snapshot (InputFrame.cs:9-12) with independent fields; no input
    is silently erased.
  - **`InputFrame` has no runtime consumer**, so no resolution / gating /
    consumption / legality is applied anywhere.
- Existing SwitchInputContractTests / SkillInputContractTests already prove
  that each action + its directional intent are independently represented
  (zero direction = no direction); Task 134 does not duplicate those, it tests
  the general cross-action coexistence guarantee.

## 134.2 Existing Simultaneous Input Behavior Analysis

- INPUT REPRESENTATION (FACT): `InputFrame` is a readonly struct whose fields
  are independent and assigned unconditionally; every combination (Move+Sprint,
  continuous+one-shot, multiple one-shots, action+direction, Pause/Cancel/
  Interact + any) is REPRESENTABLE simultaneously with NO if/else, early return,
  "handled" flag, gating, or consumption.
- REPRESENTATION vs EXECUTION (FACT): coexistence in InputFrame does NOT prove
  gameplay executes (e.g.) Pass+Shoot together. No runtime consumer exists, so
  whether multiple valid intents execute is GAMEPLAY-owned and undefined.
- No suppression, no priority rule, no gameplay-legality field, no hidden
  resolution. Move+Sprint = coexist; multiple one-shots = coexist (raw intents);
  associated direction pairs = independent; Pause/Cancel/Interact + others =
  coexist.

## 134.3 Supported Combinations Decision

Based on repository evidence (independent snapshot fields, no resolver, no
consumer), the authoritative combination policy is INPUT-LEVEL COEXISTENCE:

1. ALWAYS COEXIST as independent intents (input representation): Move+Sprint;
   movement + any one-shot; Sprint + any one-shot; Switch+SwitchDirection;
   Skill+SkillDirection; Pause + gameplay inputs; Cancel + other inputs;
   Interact + other inputs.
2. MUTUALLY EXCLUSIVE at the input layer: **NONE** (the input layer never makes
   any pair exclusive).
3. COEXIST in InputFrame but GAMEPLAY decides execution: multiple one-shot
   actions present together (Pass+Shoot, Pass+Tackle, Shoot+Skill, etc.) —
   execution/legality is gameplay-owned, NOT input-owned.
4. Priority-owned (Task 133): **NONE currently** (no precedence rule exists).
5. Gameplay-owned: legality (state/possession), player-switch selection, Pause
   (state/game-flow), Cancel/Interact (context).
6. Undefined/deferred: simultaneous EXECUTION of multiple one-shots (gameplay);
   future resolution if precedence ever defined (133); repeated-command handling
   (132, deferred).

Rationale: the supported combinations are exactly those the existing raw
snapshot contract already represents independently; nothing is suppressed or
made exclusive by the input layer; the un-defined part is gameplay execution
policy, correctly owned by future gameplay.

## 134.4 Resolution Rules / API Design

- A resolution mechanism is NOT required: the architecture already represents
  simultaneous inputs safely (independent fields, no erasure, no device
  coupling) and correctly leaves execution/legality to the downstream gameplay
  layer (not yet implemented).
- Outcome: **NO NEW API REQUIRED.** No `bool CanCombine`, `InputCombination`
  enum, `SimultaneousInputPolicy`, `ResolveInputs(...)`, `InputConflictResolver`,
  or any resolver type is justified.
- Data flow: Device → Provider → IPlayerInput → InputFrame (all raw intents
  preserved) → future Gameplay. No simultaneous-processing layer inserted.
- Combination semantics: coexistence at representation; execution policy
  undefined/deferred. Deterministic (pure struct). Device-neutral. Core remains
  neutral (no device types).
- Non-goals: no resolver, no `InputFrame`→command-queue, no gameplay state in
  InputFrame, no priority metadata, no buffering, no suppression/ordering/
  cancellation/first-wins/last-wins.

## 134.5 Implementation

- **NO PRODUCTION CHANGE REQUIRED for Task 134.**
- No `SimultaneousInputManager` / `InputConflictManager` /
  `SimultaneousInputResolver` / `InputCombinationManager` / `MultiInputResolver`
  / `InputArbitrator` created.
- No first-wins / last-wins / all-wins / suppression / cancellation / ordering /
  ranking invented; no gameplay modification; no fake consumers.
- `IPlayerInput` / `InputFrame` / providers unchanged (raw logical intents /
  pure-data snapshot); no priority / buffering / gameplay / device coupling
  leaked in; no unrelated modifications.

## 134.6 Integration

- "No current runtime consumer defines simultaneous-input execution;
  representation remains available through the input contract and execution
  policy is deferred."
- Multiple logical values coexist where appropriate (confirmed); no input
  silently erased (confirmed); no device-specific behavior in Core (confirmed);
  AI/Replay/Network structurally coherent (all implement `IPlayerInput`);
  InputFrame remains a snapshot (confirmed); gameplay remains responsible for
  execution.

## 134.7 Conflict / Priority / Ordering Review

Cross-task ownership matrix:
- Repeated Pass inputs → Buffering (132) / future gameplay (deferred).
- Pass+Shoot both present → simultaneous coexistence at input; precedence =
  Priority (133) if ever defined; legality = gameplay.
- Move+Sprint → simultaneous coexistence.
- Skill+SkillDirection → associated intent pair (same action).
- Switch+SwitchDirection → associated intent pair (same action).
- Pause+gameplay action → state/game-flow (gameplay-owned).
- Cancel/Interact context → gameplay/UI-owned.

Checks: no priority rule accidentally introduced; no buffering rule introduced;
no ordering rule invented; no action suppression introduced; no gameplay
legality embedded in InputFrame; no device-specific rule. **No cross-task
leakage.** Final verdict: PASS WITH DEFERRED.

## 134.8 Focused Tests

Added `SimultaneousInputContractTests.cs` (13 tests, GUID
`3ecca215f47b45768fa9ab4e53bb0bfe`) proving the DOCUMENTED EXISTING BEHAVIOR
and the ABSENCE of any speculative simultaneous-input system:
- Independent inputs coexist on both IPlayerInput and InputFrame.
- InputFrame can represent simultaneous one-shots (e.g. Pass+Shoot+Tackle true).
- InputFrame can represent Move+Sprint+one-shot together.
- Associated direction pairs (Switch+SwitchDirection, Skill+SkillDirection)
  remain independently coherent.
- No global simultaneous resolver / combination policy type exists.
- No arbitrary first-wins/last-wins/suppression/ordering policy exists.
- No priority (133) or buffering (132) type leaked into simultaneous.
- InputFrame is a pure-data snapshot (no resolution/selection/gating state).
- No gameplay state (legal/executable/blocked/consumed) in InputFrame.
- Core stays device-neutral (no keyboard/controller/touch type leaks).
- No runtime consumer / action-executor layer exists.
- Providers do not suppress/gate/resolve inputs.
- Logical action semantics unchanged.

Focused run: **13/13 PASS.**

## 134.9 Full Regression / Integrity Audit

- Full EditMode regression: **1737/1737 PASS** (1724 baseline + 13 new
  SimultaneousInputContractTests).
- No compile errors; no Task-134-caused warnings (CS0219 in
  FixedTimestepPolicyTests.cs untouched, pre-existing).
- `D:\Projects\unity\ofc` untouched: all 117 baseline files verified 0
  missing / 0 changed.
- asmdefs unchanged; Core remains device-neutral; no duplicate simultaneous-input
  authority; no priority (133) or buffering (132) system added; no device-specific
  simultaneous logic in Core; no gameplay execution.
- `.meta` for `SimultaneousInputContractTests.cs.meta` valid
  (`fileFormatVersion: 2`, 32-hex GUID); GUID unique in Assets.

## 134.10 Architecture Documentation

This section (Task 134) appended without altering Tasks 117-133 (only the
Task 133.12 boundary line's trailing clause was rephrased to note that Task 134
had not been performed at that time).

## 134.11 Final Verification / Gate

- **PASS WITH DEFERRED.** No global simultaneous-input resolution policy is
  defined (intentional, product/gameplay-owned); the architecture safely
  preserves simultaneous intents at the input representation level; gameplay
  execution/legality policy is deferred. No policy was invented.
- Representation: multiple logical values coexist (independent InputFrame
  fields); no silent discard; IPlayerInput device-neutral; associated direction
  pairs coherent; no gameplay state in InputFrame.
- Resolution: no arbitrary first-wins/last-wins/suppression/ordering/
  cancellation; undefined combinations explicitly deferred; gameplay-owned
  legality outside the input layer.
- Cross-task: Task 132 owns buffering, Task 133 owns priority, Task 134 owns
  simultaneous handling; no cross-task leakage.
- Device independence: no device-specific simultaneous rules in Core; no
  keyboard/controller logic in InputFrame; no device-specific gameplay logic.
- Scope: no buffering/priority/remapping/sensitivity/dead-zone/gameplay
  changes; no Task 135 work.
- Tests pass; full regression passes; compile clean; `.meta` valid; GUID
  unique; no unexpected files; ofc untouched.

## 134.12 Scope boundary

No simultaneous-input resolution policy defined (deferred, gameplay-owned);
no buffering (132); no priority (133); no remapping (131); no sensitivity
(130); no dead-zone changes (129); no gameplay execution; no invented
first-wins/last-wins/suppression/ordering rules. No Task 135 work performed
at that time.

# Task 135 — AI Input Contract

## 135.1 Audit / Discovery

- Task 135 concerns the AI INPUT CONTRACT — the AI provider's role in the
  logical input architecture. It does NOT concern AI gameplay/runtime
  behavior.
- FACTS established:
  - `AIPlayerInput` (`Football.Input`, `Runtime/Input/AIPlayerInput.cs`) is
    `public class AIPlayerInput : MonoBehaviour, Core.IPlayerInput`, exposing
    the 14 `IPlayerInput` members as settable auto-properties.
  - `Runtime/AI` (`Football.AI` asmdef) is EMPTY — only the asmdef + `.meta`
    exist; no scripts; the AI assembly is NOT compiled. Its references (Core,
    Players, Ball, Teams) are reserved for a future AI runtime but unused.
  - `Football.Input.asmdef` references only `Football.Core`; `Football.Core`
    references `[]` (device-neutral).
  - No AI controller / decision loop / manager / target selector / pathfinding
    / tactics / perception / navigation exists.
  - No production consumer of `IPlayerInput` / `InputFrame` exists.
- `AIDebugCategoryTests` (Task 59) already establishes AIPlayerInput as a
  plain input abstraction with no production consumer, not AI gameplay.

## 135.2 Existing AIPlayerInput Analysis

Full analysis of `AIPlayerInput` (all FACT):
- Inheritance/lifecycle: `MonoBehaviour` + `Core.IPlayerInput`; NO Update /
  FixedUpdate / LateUpdate / Awake / Start / OnEnable / OnDisable / OnDestroy.
- State owned: exactly the 14 contract auto-properties (logical input values);
  no hidden state, no cache, no buffers.
- Samples/produces input: NO — it does not sample or produce; it is a passive
  holder populated by an external (future) AI system.
- Contains AI decision logic: NO.
- Passive or active: PASSIVE.
- Uses device APIs: NO (no `UnityEngine.Input.*`, no device references).
- Conforms to IPlayerInput: YES (structurally implements every member).
- Properties/semantics: 14 members mirroring IPlayerInput, `IsEnabled` default
  `true`; auto-properties are `{ get; set; }` (settable) in contrast to
  HumanPlayerInput's `{ get; private set; }` (device-sampled).

Conclusion: `AIPlayerInput` ALREADY satisfies an AI provider contract — a
passive, device-neutral, decision-free `IPlayerInput` provider. No gap exists.

## 135.3 AI Provider vs AI Runtime Boundary

- AI INPUT PROVIDER = `AIPlayerInput` (Football.Input): device-neutral
  `IPlayerInput` implementation exposing AI-generated LOGICAL INPUT (settable
  properties). Must NOT contain decision logic.
- AI RUNTIME / DECISION-MAKING = does NOT exist yet (Football.AI empty). When
  built (future AI task) it would be a separate assembly (Football.AI) that
  produces the logical intents pushed into the contract. NOT part of the
  input provider.
- Verified: AIPlayerInput cannot become a decision engine (no Update, no
  methods, only contract properties). Behavior selection / tactics /
  navigation / target selection / possession / passing / shooting / defensive
  reasoning → OUTSIDE the provider (future Football.AI). No device assumptions
  leak into the AI provider.
- No AI runtime exists → recorded as UNKNOWN/DEFERRED; none invented.

## 135.4 Contract Ownership

- AIPlayerInput is the AI INPUT PROVIDER (furnishes AI logical input).
- IPlayerInput remains the single consumer-facing contract.
- InputFrame remains the pure-data transport snapshot.
- A separate AI-specific input contract is NOT necessary: IPlayerInput already
  carries every logical input an AI would express. DECISION: CONSOLIDATE on
  IPlayerInput — AIPlayerInput is its AI implementation. No parallel AI input
  contract.

## 135.5 API / Data Design

Audit of existing model against AI input (all logical intents are
AI-producible):
- MoveDirection, LookDirection (Vector2 directional intent) — AI-producible.
- Sprint (held intent) — AI-producible.
- Pass, Shoot, Tackle, SwitchPlayer, Skill, Interact, Cancel, Pause (one-shot
  requests) — AI-producible.
- SwitchDirection, SkillDirection (associated directional intent; zero = no
  direction) — AI-producible.
- IsEnabled (lifecycle gate, default true) — AI-producible.
- Context-dependent: Pause, Cancel, Interact (meaning depends on
  game/gameplay context — gameplay-owned); direction values (zero = no
  direction).
- No new fields. Explicitly NOT added (no evidence, gameplay/decision
  semantics): ActionId, DecisionState, TargetPlayerId, TargetPosition,
  TacticalIntent, PassTarget, ShotType, SkillId, etc. InputFrame remains a
  pure-data snapshot (NOT a behavior/decision object).
- Conclusion: the existing contract fully supports AI input; NO new
  API/fields.

## 135.6 Implementation / Consolidation

- NO production change required (no contract gap). Outcome: KEEP / NOT
  REQUIRED.
- `AIPlayerInput` already correctly implements IPlayerInput as a passive,
  device-neutral provider → CONSOLIDATE/DOCUMENT rather than redesign.
- Production runtime files changed: NONE. No gameplay implementation.

## 135.7 Gameplay Independence

- AIPlayerInput can be consumed without knowing how AI decisions are made
  (exposes only logical IPlayerInput intents).
- Gameplay/runtime would consume IPLayerInput/InputFrame, not AI
  implementation details.
- Human + AI providers feed the same device-neutral contract.
- No gameplay system depends on AIPlayerInput-specific implementation details
  (no consumer exists; provider exposes only the contract).
- No device API leaks into Core through AI input (Core device-neutral).
- No hidden coupling to tactics / pathfinding / animation / physics.
- No gameplay consumer exists yet — recorded as current architecture state
  (UNKNOWN/DEFERRED). Contract-level independence is fully satisfied.

## 135.8 Focused Tests

Added `AIPlayerInputContractTests.cs` (13 tests, GUID
`76da88bb4f1b469a9f060857fa9970e6`). One initial assertion
(`Surface_MatchesHumanProvider`) was over-strict (compared the whole public
surface of a MonoBehaviour, which includes inherited Unity engine properties)
and was corrected to compare only directly-declared (DeclaredOnly) members.
Tests verify:
- AIPlayerInput implements IPlayerInput and is a MonoBehaviour.
- It exposes all 14 contract members (settable auto-properties).
- Member types match the contract.
- It is device-neutral (no UnityEngine.Input sampling, no device type leaks
  into Core).
- No AI-runtime/gameplay members (Decision/Target/Tactic/Intent/etc.).
- No speculative AI brain / runtime system exists.
- No Update/lifecycle/sampling methods (passive provider).
- IsEnabled defaults true.
- InputFrame remains pure data (no AI decision/behavior state).
- No parallel AI input contract exists.
- Declared surface matches the contract and the sibling providers (Human/
  Replay/Network) — no added/dropped logical-input members.

Focused run: **13/13 PASS.**

## 135.9 Full Regression / Integrity Audit

- Full EditMode regression: **1750/1750 PASS** (1737 baseline + 13 new
  AIPlayerInputContractTests).
- No compile errors; no Task-135-caused warnings (CS0219 in
  FixedTimestepPolicyTests.cs untouched, pre-existing).
- `D:\Projects\unity\ofc` untouched: all 117 baseline files verified 0
  missing / 0 changed.
- asmdefs unchanged; Core remains device-neutral; no AI runtime/system added;
  no gameplay implementation; no duplicate AI contract.
- `.meta` for `AIPlayerInputContractTests.cs.meta` valid
  (`fileFormatVersion: 2`, 32-hex GUID); GUID unique in Assets.

## 135.10 Architecture Documentation

This section (Task 135) appended without altering Tasks 117-134 (only the
Task 134.12 boundary line's trailing clause was rephrased to note that Task
135 had not been performed at that time).

## 135.11 Final Verification / Gate

- **PASS WITH DEFERRED.**
- Gate: [1] AIPlayerInput matches the verified IPlayerInput contract (13/13
  contract tests pass). [2] IPlayerInput remains device-neutral. [3] InputFrame
  remains pure data. [4] AI decision-making remains outside the provider.
  [5] No gameplay logic added. [6] No speculative AI fields/systems added.
  [7] Focused tests pass (13/13). [8] Full regression passes (1750/1750).
  [9] Old project unchanged (117/117). [10] No git operations. [11]
  Architecture docs match the implementation. [12] Deferred items documented
  (AI runtime/decision-making = UNKNOWN/DEFERRED; no AI gameplay consumer).
- Deferred item is NOT upgraded to PASS: AI runtime / decision-making does not
  exist and its semantics are not defined — recorded as deferred.

## 135.12 Scope boundary

AI input CONTRACT verified/consolidated on the single IPlayerInput; no AI
runtime / decision system / brain / tactics / target selection / pathfinding /
navigation implemented (Football.AI remains empty, deferred); no separate AI
input contract; no gameplay implementation; no buffering (132) / priority
(133) / simultaneous (134) changes; no device API leaks; no git operations.
Wait for the next user instruction. (At this time Task 136 had not yet been
performed.)

# Task 136 — Replay Input Contract

## 136.1 Audit / Discovery

- Task 136 concerns the REPLAY INPUT CONTRACT — the Replay provider's role in
  the logical input architecture. It does NOT concern replay recording /
  playback implementation.
- FACTS established:
  - `ReplayPlayerInput` EXISTS (`Football.Input`, `Runtime/Input/ReplayPlayerInput.cs`):
    `public class ReplayPlayerInput : MonoBehaviour, Core.IPlayerInput`, exposing
    the 14 `IPlayerInput` members as settable auto-properties.
  - **NO replay recording / playback / serialization infrastructure exists**
    anywhere: no ReplayManager / ReplayController / ReplayRecorder / ReplayPlayer /
    ReplayPlayback / ReplaySerializer / ReplayRecording / ReplayTimeline /
    InputSerializer / SerializedInput / ReplayClock / PlaybackController /
    InputCapture / ReplaySystem. No replay/save/load/demo/ghost assets exist.
  - No deterministic-replay framework (no checksum / frame-hash / rollback /
    fixed-tick replay / replay-state / replay-seed / replay-archive).
  - The only "replay" tokens in `Runtime` are the passive `ReplayPlayerInput`
    provider and a `GameClock.cs:30` doc comment noting the clock is
    "deterministic, testable, and replay-compatible" — design intent only.
  - `Football.Input.asmdef` references only `Football.Core`; `Football.Core`
    references `[]` (device-neutral).
  - No production consumer of `IPlayerInput` / `InputFrame` exists.
- Repeated audit (Tasks 55/64) confirms ReplayPlayerInput is an *input source*,
  NOT a replay recorder.

## 136.2 Existing ReplayPlayerInput Analysis

Full analysis (all FACT):
- Inheritance/lifecycle: `MonoBehaviour` + `Core.IPlayerInput`; NO Update /
  FixedUpdate / LateUpdate / Awake / Start / OnEnable / OnDisable / OnDestroy.
- State owned: exactly the 14 contract auto-properties (logical input values);
  no hidden state, no cache, no buffers.
- Source of input data: EXTERNAL INJECTION — nothing samples (settable);
  an external system would set the properties.
- Reads devices: NO (no `UnityEngine.Input.*`, no device references).
- Writes/records data: NO. Controls time/ticks: NO. Performs playback: NO.
  Performs gameplay logic: NO.
- Passive or active: PASSIVE.
- Conforms to IPlayerInput: YES (structurally implements every member; `IsEnabled`
  defaults `true`; settable `{ get; set; }` like AI/Network).

Conclusion: `ReplayPlayerInput` ALREADY satisfies a Replay INPUT PROVIDER
contract — a passive, device-neutral, replay-data-injected `IPlayerInput`
implementation ("Replay inject"). No contract gap exists.

## 136.3 Replay vs Gameplay Boundary

- REPLAY INPUT SOURCE = `ReplayPlayerInput` (Football.Input): passively exposes
  previously-captured LOGICAL INPUT via `IPlayerInput`. It does NOT record, does
  NOT store, does NOT control playback timing, does NOT execute gameplay.
- REPLAY RECORDING / STORAGE = does NOT exist (UNKNOWN/DEFERRED; no recorder, no
  serialization, no assets).
- PLAYBACK CONTROL = does NOT exist (UNKNOWN/DEFERRED; no playback
  controller/timeline/speed/seek).
- GAMEPLAY / SIMULATION = does NOT exist as a consumer (no IPlayerInput consumer).
- Verified: ReplayPlayerInput does NOT contain tactical/gameplay logic, is NOT an
  AI system, does NOT execute gameplay actions, exposes logical (not device)
  input, and is NOT a playback driver. No new layering model invented.

## 136.4 Replay Input Contract Decision

- Should ReplayPlayerInput implement IPlayerInput? YES — it already does; replay
  exposes logical input on the single contract.
- Should replay playback feed the same InputFrame contract as Human/AI? YES — all
  providers converge on one device-neutral contract.
- Separate ReplayInput contract necessary? NO. `IPlayerInput` is the single
  logical input contract; replay is just another source. DECISION: CONSOLIDATE on
  IPlayerInput — ReplayPlayerInput is its replay implementation. No parallel
  replay contract.
- Should ReplayPlayerInput be passive / external-data-driven? YES — it already is
  (settable properties).
- Replay metadata/control (recording, playback control, timestamps, speed, seek,
  frame indexing) belongs to future replay *infrastructure*, NOT the logical input
  contract — consistent with InputFrame remaining pure data.

## 136.5 API / Data Design

Audit of existing model against replay use (all logical input is replayable):
- MoveDirection, LookDirection (continuous/analog) — replayable.
- Sprint (held) — replayable.
- Pass, Shoot, Tackle, SwitchPlayer, Skill, Interact, Cancel, Pause (one-shot
  signals) — replayable.
- SwitchDirection, SkillDirection (directional intent; zero = none) — replayable.
- IsEnabled (lifecycle gate, default true; provider/lifecycle state, intentionally
  NOT in InputFrame) — control, not recorded gameplay input.
- No new fields. Explicitly NOT added (no evidence; replay-infrastructure metadata,
  not logical input): ReplayTick, ReplayFrame, ReplayTimestamp, TargetPlayerId,
  ReplayActionId, DecisionState, TacticalIntent, SerializedGameplayState,
  checksum/CRC, random seed, authoritative world state, playback speed, seek state.
  InputFrame remains a pure-data snapshot.
- Conclusion: the existing contract fully supports replay as an input source; NO
  new API/fields.

## 136.6 Implementation / Consolidation

- NO production change required (no contract gap). Outcome: KEEP / NOT REQUIRED.
- `ReplayPlayerInput` already correctly implements IPlayerInput as a passive,
  device-neutral provider → CONSOLIDATE/DOCUMENT rather than redesign.
- No replay recording/playback system built (speculative creation prohibited).
- Production runtime files changed: NONE. No gameplay implementation.

## 136.7 Determinism / Read-Only / Playback Boundary

- Read-only (as an input source): replayed logical input is consumed as data;
  ReplayPlayerInput does not mutate a recorded source (no source exists).
- Recorded logical input consumed without device reads: YES (no device reads).
- Externally driven per tick/frame: settable and documented "Replay inject";
  whether actual per-tick driving is wired anywhere is UNKNOWN/DEFERRED (no
  playback driver exists).
- Provider mutates recorded source data: NO (no recorded source exists).
- Playback controls belong to future replay infrastructure, NOT IPlayerInput: YES.
- Determinism guarantees: `GameClock` is "replay-compatible" (design intent), but
  NO deterministic-replay guarantee is implemented (no fixed-tick replay, no
  checksums, no rollback, no authoritative state, no random-state capture).
  Deterministic replay = UNKNOWN/DEFERRED — NOT claimed, NOT invented.

## 136.8 Focused Tests

Added `ReplayPlayerInputContractTests.cs` (14 tests, GUID
`aedbda6aa27045d1bac321cad32a0052`), mirroring the AI/other contract-test pattern.
Two initial assertions were over-broad false positives and were corrected:
- 'Player' substring collided with the legit `SwitchPlayer` member (dropped from
  the recorder/playback keyword list).
- 'ReplayPlayer' substring collided with the legit `ReplayPlayerInput` provider
  (the type-name scan now excludes the known provider/contract types).
Tests verify:
- ReplayPlayerInput implements IPlayerInput and is a MonoBehaviour.
- It exposes all 14 contract members (settable auto-properties).
- Member types match the contract.
- It is device-neutral (no UnityEngine.Input sampling).
- No Update/lifecycle/sampling methods (passive provider).
- IsEnabled defaults true.
- No recorder/player/serialization members (Record/Playback/Serialize/Save/Load/
  Capture/etc.).
- No replay metadata / gameplay intent members (ReplayTick/ReplayFrame/Target/
  DecisionState/etc.).
- No speculative replay runtime/recorder/serializer infrastructure exists.
- No deterministic-replay framework exists.
- No replay metadata on IPlayerInput / InputFrame.
- InputFrame remains pure data.
- No parallel replay-specific input contract (single IPlayerInput consolidation).
- Declared surface matches the contract and sibling providers (Human/AI/Network)
  via DeclaredOnly — no added/dropped logical-input members.

Focused run: **14/14 PASS.**

## 136.9 Full Regression / Integrity Audit

- Full EditMode regression: **1764/1764 PASS** (1750 baseline + 14 new
  ReplayPlayerInputContractTests).
- No compile errors.
- `D:\Projects\unity\ofc` untouched: all 117 baseline files verified 0 missing /
  0 changed (the additional 111 files present are pre-existing Unity-generated
  `.meta` files plus ofc's own `.git/`, all dated on/before 08/26 — none touched
  by this or any task; 0 files modified on/after our session).
- GUID uniqueness: 267 `.meta` files in Assets, 267 unique GUIDs, 0 duplicates;
  new GUID `aedbda6aa27045d1bac321cad32a0052` is unique.
- asmdefs unchanged; Core remains device-neutral; no replay recording/playback
  system added; no gameplay implementation; no duplicate replay contract.

## 136.10 Architecture Documentation

This section (Task 136) appended without altering Tasks 117-135 (only the Task
135.12 boundary line's trailing clause was rephrased to note that Task 136 had
not been performed at that time).

## 136.11 Final Verification / Gate

- **PASS WITH DEFERRED.**
- Gate: [1] ReplayPlayerInput matches the verified IPlayerInput contract (14/14
  contract tests pass). [2] IPlayerInput remains device-neutral. [3] InputFrame
  remains pure data. [4] Replay input source is passive and external-data-driven.
  [5] No gameplay logic added. [6] No speculative replay fields/systems added.
  [7] Focused tests pass (14/14). [8] Full regression passes (1764/1764).
  [9] Old project unchanged (117/117, 0 missing/0 changed). [10] No git
  operations. [11] Architecture docs match the implementation. [12] Deferred
  items documented (replay recording/storage/playback-control and deterministic-
  replay = UNKNOWN/DEFERRED; no IPlayerInput consumer).
- Deferred item is NOT upgraded to PASS: replay recording / storage / playback
  control / deterministic replay do not exist and their semantics are not defined
  — recorded as deferred.

## 136.12 Scope boundary

Replay input CONTRACT verified/consolidated on the single IPlayerInput; no replay
recording / storage / playback / serialization / deterministic-replay system
implemented (deferred); no playback controller / timeline / save-load / ghost /
demo; no deterministic-replay guarantee claimed; no separate replay input
contract; no gameplay implementation; no buffering (132) / priority (133) /
simultaneous (134) / AI (135) changes; no device API leaks; no git operations.
Wait for the next user instruction. (At this time Task 137 had not yet been
performed.)

# Task 137 — Future Network Input Contract

## 137.1 Audit / Discovery

- Task 137 concerns the FUTURE NETWORK INPUT CONTRACT — the (future-)Network
  provider's role in the logical input architecture. It is a source/provider
  contract decision, NOT a networking implementation.
- FACTS established:
  - `NetworkPlayerInput` EXISTS (`Football.Input`, `Runtime/Input/NetworkPlayerInput.cs`):
    `public class NetworkPlayerInput : MonoBehaviour, Core.IPlayerInput`, exposing
    the 14 `IPlayerInput` members as settable auto-properties.
  - **NO networking runtime / transport / package is installed.** `Packages/manifest.json`
    has NO NGO / Mirror / FishNet / Photon / Steam / Unity Transport package.
    `com.unity.multiplayer.center` (1.0.1) is a Unity wizard/UI/template hub, NOT a
    netcode runtime. `com.unity.inputsystem` is present but the framework direction
    (Task 122, activeInputHandler=2) is the legacy Input Manager.
  - **Zero** Runtime references to Transport / Socket / Udp / Tcp / RPC /
    Replication / Prediction / Rollback / Reconciliation / authority / netcode /
    packet.
  - No serialization framework (zero Serialize/Deserialize references) and no
    deterministic-networking framework exists.
  - `Football.Input.asmdef` references only `Football.Core`; `Football.Core`
    references `[]` (fully device/source agnostic).
  - No production consumer of `IPlayerInput` / `InputFrame` exists.
  - Existing contract tests already treat NetworkPlayerInput as one of the four
    providers (Move/Pass/Pause/AI/Replay contract tests).

## 137.2 Existing NetworkPlayerInput

- `NetworkPlayerInput` **EXISTS** (FACT).
- Analysis (all FACT):
  - Inheritance/interface: `MonoBehaviour` + `Core.IPlayerInput` (Football.Input).
  - Fields/properties/methods: exactly the 14 `IPlayerInput` members as settable
    auto-properties; `IsEnabled` default `true`; no other fields/methods.
  - Implements IPlayerInput: YES (structurally). Passive: YES (no Update/FixedUpdate/
    LateUpdate/Awake/Start/OnEnable/OnDisable/OnDestroy, no sampling loop).
  - Reads physical devices: NO (no `UnityEngine.Input.*`).
  - Receives externally supplied logical input: YES (settable).
  - Serializes/deserializes: NO. Knows transport: NO. Owns ticks/sequences: NO.
  - Predicts/reconciles/rolls back: NO. Gameplay logic: NO. Mutates source: NO.
  - Network callbacks: NO.
- Conclusion: NetworkPlayerInput is ALREADY the correct future-network provider
  shape: a passive, device/source-neutral, network-data-injected IPlayerInput
  implementation (the "Network inject" member of the Human/AI/Replay/Network
  family). It does NOT need to be created, and it does NOT implement networking.

## 137.3 Network vs Gameplay Boundary

- NETWORK TRANSPORT (sockets, packets, UDP/TCP, reliability, ordering): does NOT
  exist; future; OUTSIDE logical contract.
- NETWORK SERIALIZATION/DECODING (packet format, compression, CRC): does NOT
  exist; future; OUTSIDE.
- NETWORK INPUT SOURCE/PROVIDER = `NetworkPlayerInput` (Football.Input): consumes
  externally-supplied logical input and exposes it via `IPlayerInput`; must NOT
  know transport/sockets/packets/authority/roles/replication/prediction/reconcile.
- GAMEPLAY / SIMULATION: no consumer exists yet.
- Verified (FACT): the logical input contract does NOT know transport protocols,
  sockets, packets, network authority, client/server roles, replication,
  prediction/reconciliation, or gameplay decisions.
- **Network runtime does not exist — stated explicitly.** Future network input
  delivers the SAME logical contract as Human/AI/Replay (source-neutral).

## 137.4 Future Network Contract Decision

- NetworkPlayerInput should implement IPlayerInput: YES — it already does.
- Network-originated logical input should use InputFrame: YES (single
  device-neutral transport).
- Separate NetworkInput interface necessary: NO — CONSOLIDATE on IPlayerInput;
  NetworkPlayerInput is its network implementation. No parallel contract.
- Network metadata belongs outside the logical contract: YES (already the case).
- Passive / external-data-driven: YES (already settable, no sampling).
- Future provider is a source adapter only: YES (matching Replay/AI "inject").
- Reserve anything now in the API: NO — no premature network API.

## 137.5 API / Data Design

Audit of every ACTUAL current member as a potential network logical-input field:
- Network-source-crossable unchanged (all logical input): MoveDirection (cont.),
  LookDirection (cont.), Sprint (held), Pass/Shoot/Tackle/SwitchPlayer/Skill/
  Interact/Cancel/Pause (one-shot), SwitchDirection/SkillDirection
  (directional, zero=none).
- IsEnabled: lifecycle/provider gate (control), not sampled input; intentionally
  NOT in InputFrame; not recorded gameplay input.
- Network metadata ownership: NONE of NetworkTick/SequenceNumber/InputCommandId/
  ClientId/PlayerId/Timestamp/ServerTime/Authority/PredictionKey/Ack/Reliable/
  PacketId/CRC/CompressionFlags/SnapshotId/RollbackFrame is part of the contract.
  The audit proves none is established → these belong to (absent) future network
  infrastructure, NOT IPlayerInput/InputFrame.
- Transient/continuous: contract already distinguishes continuous vs one-shot
  transient — sufficient for a network provider to deliver per-sampling-point
  logical input unchanged.
- Explicitly NOT added: target IDs, world positions, action IDs, tactical/AI
  decisions, gameplay state.
- Conclusion: existing contract fully supports network as a logical input source;
  NO new API/fields.

## 137.6 Minimal Contract Implementation

- `NetworkPlayerInput` exists and is correct → **KEEP / NOT REQUIRED**.
- No concrete contract-level deficiency → **PRODUCTION CHANGE = NONE**. No runtime
  implementation created; no stub needed; no speculative network runtime.

## 137.7 Network-Independent Gameplay

- Future gameplay consumes logical input without caring whether the source is
  Human/AI/Replay/Network (all four are passive IPlayerInput providers on the same
  source-neutral InputFrame/contract).
- Gameplay does not depend on NetworkPlayerInput-specific properties, does not
  inspect transport/network state through IPlayerInput; IPlayerInput is the
  abstraction boundary; InputFrame is source-neutral; no network authority hidden
  in the contract.
- No gameplay consumer exists — recorded as current architecture (UNKNOWN/DEFERRED);
  contract-level independence fully satisfied.

## 137.8 Serialization / Determinism

- InputFrame is structurally suitable as logical input data (pure-data readonly
  struct, source-neutral). (FACT)
- Semantics distinguish one-shot vs continuous. (FACT)
- Fixed tick/frame boundary: expressed as "a single sampling/update point"
  (InputFrame doc); GameClock exists but is not wired into production. **No fixed
  network tick defined.** (UNKNOWN/DEFERRED)
- Serialization currently defined: **NO** serializer exists. (UNKNOWN/DEFERRED)
- Deterministic simulation guarantees: **NO** deterministic-networking framework
  (rollback/checksum/fixed-tick) exists. Do NOT claim network determinism merely
  because InputFrame is pure data. (UNKNOWN/DEFERRED)
- Network input ordering/frequency semantics: **NO** (no packet ordering / input
  resend / loss handling / command sequence). (UNKNOWN/DEFERRED)
- CRITICAL — not claimed: deterministic networking, serialization compatibility,
  fixed network tick, command sequence rules, client prediction, server
  reconciliation, rollback, input resend, packet ordering, loss handling,
  compression, hashing, checksums. All UNKNOWN/DEFERRED.

## 137.9 Focused Tests

Added `NetworkPlayerInputContractTests.cs` (13 tests, GUID
`c1bad5afcb544edaa806c8671469c401`). One initial assertion
(`NoNetworkMetadata_InInputFrame_OrIPlayerInput`) was a false positive: the
lowercase "ack" substring collided with the legit `Tackle` member, so the test now
uses case-sensitive (Ordinal) keyword matching. Tests verify:
- NetworkPlayerInput implements IPlayerInput and is a MonoBehaviour.
- It exposes all 14 contract members (settable auto-properties).
- Member types match the contract.
- It is source-neutral (no UnityEngine.Input; no transport/socket/packet/serialize
  tokens; no netcode dependency).
- No Update/lifecycle/network-callback methods (passive source adapter).
- IsEnabled defaults true.
- No network metadata / runtime members (Tick/Sequence/ClientId/Timestamp/
  Authority/Prediction/Rollback/etc.).
- No speculative network runtime / transport / netcode infrastructure exists.
- No deterministic-networking framework exists.
- No network metadata on IPlayerInput / InputFrame.
- InputFrame remains pure data.
- No parallel network-specific input contract (single IPlayerInput consolidation).
- Declared surface matches the contract and sibling providers (Human/AI/Replay)
  via DeclaredOnly — no added/dropped logical-input members.

Focused run: **13/13 PASS.**

## 137.10 Full Regression / Integrity Audit

- Focused: **13/13 PASS**.
- Full EditMode regression: **1777/1777 PASS** (1764 baseline + 13 new
  NetworkPlayerInputContractTests). 0 failed / 0 inconclusive / 0 skipped.
- No compile errors (CS0219 in FixedTimestepPolicyTests.cs pre-existing/unchanged;
  does not surface in the batch test log).
- `D:\Projects\unity\ofc` untouched: exact 117/117 files, 0 missing / 0 changed /
  0 extra (using Task 137 exclusions: .meta, UserSettings, Library, Temp, obj, .git).
- GUID integrity: 268 `.meta` files in Assets, 268 unique GUIDs, 0 duplicates /
  0 missing; new GUID `c1bad5afcb544edaa806c8671469c401` is unique.
- No unexpected Runtime files: the only network-named Runtime file remains the
  pre-existing passive `NetworkPlayerInput.cs`; no networking runtime added.
- asmdefs unchanged; Core remains device/source agnostic.

## 137.11 Architecture Documentation

- Files changed: `Docs\Architecture.md` (Task 137 section appended; Task 136.12
  trailing clause rephrased to note Task 137 had not been performed at that time).
- Summary: documents NetworkPlayerInput existence, future ownership decision
  (consolidate on IPlayerInput/InputFrame, source adapter), network vs gameplay
  boundary, transport/serialization metadata ownership (outside contract),
  determinism status (UNKNOWN/DEFERRED), implementation status (PRODUCTION CHANGE
  = NONE), and deferred networking runtime.
- Tasks 117-136 preserved; no unrelated sections rewritten.

## 137.12 Final Verification / Gate

- **PASS WITH DEFERRED.**
- [1] Future Network input contract matches actual architecture: PASS.
- [2] Network source device/source-neutral at logical layer: PASS.
- [3] IPlayerInput remains the single logical input abstraction: PASS.
- [4] InputFrame remains pure data: PASS.
- [5] No network transport metadata leaked into the logical contract: PASS.
- [6] No gameplay logic in the provider: PASS.
- [7] No speculative network runtime implemented: PASS.
- [8] Serialization claims match reality: PASS (none claimed; none exists).
- [9] Determinism claims match reality: PASS (none claimed; UNKNOWN/DEFERRED).
- [10] Focused tests pass: PASS (13/13).
- [11] Full regression passes: PASS (1777/1777).
- [12] Old project unchanged: PASS (117/117).
- [13] Documentation matches implementation: PASS.
- [14] Deferred/unknown networking work explicit: PASS (networking runtime,
  transport, serialization, deterministic-networking, tick semantics, ordering —
  all UNKNOWN/DEFERRED; no IPlayerInput consumer).

## 137.13 Phase 3 Final Verification

Aggregate verification of the entire Phase 3 Input System, Tasks 117-137:
- InputFrame: pure-data readonly struct, source/device-neutral, stable.
- Move/Look (continuous), Sprint (held), Pass/Shoot/Tackle/SwitchPlayer/Skill/
  Interact/Cancel/Pause (one-shot), SwitchDirection/SkillDirection, IsEnabled
  (lifecycle gate) — stable contract on IPlayerInput.
- Human provider (Controller + Keyboard): device-backed but contract-neutral via
  legacy Input Manager (activeInputHandler=2).
- Dead zones: a provider-sampling concern; no dead-zone filtering in Core/InputFrame.
- Sensitivity: configuration concern, not embedded in the logical contract.
- Remapping: configuration concern, deferred where not defined.
- Buffering (132), Priority (133), Simultaneous (134): contract-verified; execution
  policies remain explicitly deferred where gameplay semantics are undefined.
- AI provider (135): passive IPlayerInput provider; no AI runtime.
- Replay provider (136): passive IPlayerInput provider; no replay runtime.
- Future Network contract (137): NetworkPlayerInput is a passive source adapter;
  no networking runtime.
- Confirmed: IPlayerInput is the stable logical source contract; InputFrame pure;
  Human/AI/Replay/Future Network are compatible source/provider concepts;
  device/network concerns remain outside Core; no gameplay implementation leaked
  into Phase 3; no speculative systems introduced to satisfy roadmap wording.
- 137.13 status: **PASS WITH DEFERRED** (runtime systems — AI/Replay/Network —
  intentionally outside Phase 3; input CONTRACT complete).

## 137.14 Phase 3 Exit Criteria

1. Logical input contracts stable and device/source neutral: PASS.
2. IPlayerInput stable and coherent: PASS.
3. InputFrame stable and pure: PASS.
4. Human input provider device-backed but contract-neutral: PASS.
5. Controller and keyboard supported through the selected legacy Input Manager
   path: PASS.
6. AI and Replay providers respect the same logical contract: PASS.
7. Future Network input has a documented contract decision without speculative
   network implementation: PASS.
8. Buffering/priority/simultaneous execution policies explicitly deferred where
   gameplay semantics are not defined: PASS.
9. No gameplay dependency on a physical input device: PASS.
10. No gameplay dependency on AI/Replay/Network implementation details: PASS.
11. Full regression passes: PASS (1777/1777).
12. Old project integrity remains clean: PASS (117/117).
13. Architecture documentation reflects the final Phase 3 state: PASS.
- Final Phase 3 status: **PASS WITH DEFERRED** — the input CONTRACT is complete;
  future runtime systems (AI / Replay / Network / buffering-priority-simultaneous
  execution) are intentionally outside Phase 3 and remain DEFERRED.

## 137.15 Hard Stop

Final report produced; hard stop. No Task 138 or later work performed.

---

## Task 138 - Player Prefab Structure

Status: IMPLEMENTED (PASS).

Task 138 established `Assets/Football/Prefabs/Players/Player.prefab` as authoritative for the
runtime Player prefab structure. `PlayerPrefabStructureTests.cs` locks the prefab contract: the root
carries the identity components and NO premature gameplay MonoBehaviour (no movement /
CharacterController / state machine / animator-driven / ball-interaction / input components) may be
attached to the prefab until the corresponding gameplay runtime exists.

- Player prefab path: `Assets/Football/Prefabs/Players/Player.prefab`.
- Locked by `Tests/EditMode/PlayerPrefabStructureTests.cs`.
- Task 139 subsequently added the single `PlayerEntity` identity component to the prefab root (an
  allowed, identity-only addition; the root component allowlist updated accordingly).

## Task 139 - Player Identity

Status: IMPLEMENTED (PASS WITH DEFERRED).

- Authoritative authored identity: `PlayerDefinition.Identity` (`Football.Data.Identity` — a
  top-level type with `PlayerId` (string) / `Name` / `Nationality` / `ClubReference`). Single source;
  never duplicated.
- Runtime identity representation: `Football.Players.PlayerEntity` MonoBehaviour holds a single
  serialized `PlayerDefinition _definition` reference (exposes `Definition` and authoring-only
  `AssignDefinition`). No identity fields are copied; no gameplay logic.
- Assembly decision: `Football.Data` asmdef created in `Runtime/Data` (references nothing);
  `Football.Players` references `Football.Core` + `Football.Data`; `PlayerDefinition` was not moved
  and not duplicated. No circular dependency.
- Authored string `Identity.PlayerId` vs event `int PlayerId`: distinct concepts; reconciliation
  DEFERRED.
- Full detail: `Assets/Football/Architecture.md` — "TASK 139 — PLAYER IDENTITY".
- Locked by `Tests/EditMode/PlayerEntityTests.cs` (15 tests) + `PlayerPrefabStructureTests.cs`.

## Task 140 - Player Stats Binding

Status: KEEP / NOT REQUIRED — **PASS** (no production change; architecture already satisfies Stats
Binding).

### 140.1 Authoritative PlayerStats source

`PlayerDefinition.PlayerStats` is the single authoritative authored player-ratings source:
- Type `Football.Data.PlayerStats` — a `[System.Serializable]` **class** (reference type) in the
  `Football.Data` assembly, owned as a public field on `PlayerDefinition`.
- Contents: exactly **7 categories** (`Pace / Shooting / Passing / Dribbling / Defending / Physical`
  outfield + `Goalkeeping` stored for every player) = **35 integer ratings**, plus
  `RatingMin=1 / RatingMax=99 / RatingDefault=50` constants and the `GetInvalidRatings()` integrity
  validation (Task 78). Covered exhaustively by `PlayerStatsTests.cs` and `AttributeValidationTests.cs`.

### 140.2 Runtime binding (established, authoritative, minimal)

The runtime Player Entity reaches its PlayerStats through the existing reference chain:

    PlayerEntity --(Definition)--> PlayerDefinition --(PlayerStats)--> PlayerStats

`PlayerEntity.Definition.PlayerStats` returns the **same authored PlayerStats object** (reference
binding — no copy, no second storage location). No new production API was added.

### 140.3 Decision: KEEP / NOT REQUIRED

- Authoritative source: `PlayerDefinition.PlayerStats` (unchanged).
- Runtime access: `PlayerEntity.Definition.PlayerStats` (valid, already present).
- Duplication: **none** — `PlayerEntity` holds exactly one field (the `_definition` reference) and no
  PlayerStats/category/rating fields.
- Convenience accessor `PlayerEntity.Stats`: **NOT added** — no concrete gap exists (rule: minimal
  production change; strong preference is `Definition.PlayerStats`). Documented, not implemented.
- Mutability: authored ratings are config-integrity-checked (`GetInvalidRatings`) and are **authoring
  data**; no runtime code mutates them. No runtime progression / stat-modification system is created.
- Source of truth: **one** — `PlayerDefinition.PlayerStats`.
- No `StatsManager` / `StatsRegistry` / `StatsCache` / `PlayerStatsComponent` / `PlayerRatingsComponent`
  / runtime stats system is introduced (verified absent project-wide).

### 140.4 Authored data vs runtime state boundary

- **Authored data** (unchanged, data-only): `PlayerDefinition`, `PlayerStats` (including the authored
  `Physical.Stamina` 1-99 rating — an authored rating among the 35, distinct from runtime stamina),
  `Identity`, `PhysicalProfile`, `Profile`.
- **Runtime state** (NOT introduced): position / rotation / velocity / current state / current input /
  possession. No `CurrentSpeed` / `EffectiveRating` / `ModifiedRating` / `BuffedRating` /
  `DebuffedRating` / `MatchRating` / runtime `Stamina` / `Fatigue` / `Form` concepts exist.
- Runtime stamina-condition / fatigue / form and any dynamic stat modifiers / progression are
  **DEFERRED** future runtime-state concepts, deliberately NOT implemented here.

### 140.5 Final API

No API change. The binding is the existing `PlayerEntity.Definition.PlayerStats` read path.

### 140.6 Production changes

**NONE** — KEEP / NOT REQUIRED (rule 14/15: the architecture already satisfies Stats Binding; no
concrete gap justifies new code).

### 140.7 Player prefab integration

Unchanged. `Player.prefab` keeps exactly one `PlayerEntity` on the root and carries NO stats
component anywhere (locked by test). Identity binding intact; stats binding remains authoritative on
`PlayerDefinition.PlayerStats`.

### 140.8 Focused tests

New file `Tests/EditMode/PlayerStatsBindingTests.cs` (7 tests), all passing:
`PlayerDefinition_Exposes_PlayerStats`,
`PlayerEntity_Reaches_AuthoritativePlayerStats_ThroughDefinition`,
`PlayerEntity_DoesNotDuplicate_PlayerStatsStorage`,
`PlayerEntity_HasNoStatsStorageOrAccessor_ThatIsSecondStorage`,
`NoStatsManager_Registry_Cache_Or_StatsComponent_Created`,
`PlayerEntity_DoesNotOwnRuntimeStatState`,
`PlayerPrefab_HasNoStatsComponent_AndKeeps_PlayerEntity`.

Existing data-layer coverage not duplicated: `PlayerStatsTests` (7 categories / exact attributes /
range / defaults / validation / no runtime state / no gameplay logic) and `AttributeValidationTests`
(exactly 35 attributes; single range authority; seven categories validated; no validator types).

### 140.9 Regression

- Focused: **7/7 passed**.
- Full EditMode regression: **1816/1816 passed, 0 failed, 0 inconclusive, 0 skipped** (+7 vs Task 139).
- Compile status: clean; only pre-existing `CS0219` in `FixedTimestepPolicyTests.cs` (not from Task 140).
- Old project integrity: `D:\Projects\unity\ofc` **117/117 unchanged**; no git operations.
- GUID integrity: new file `PlayerStatsBindingTests.cs.meta` guid `0716063227a3fd346b50cdb57337c85d`,
  unique project-wide (0 duplicates across 273 metas).

### 140.10 Deferred items (NOT done by Task 140)

- Runtime stat modification / progression / dynamic modifiers.
- Runtime stamina-condition / fatigue / form systems.
- Gameplay consumers of PlayerStats (movement/shooting/passing/AI etc. runtime remains absent).
- Any convenience `PlayerEntity.Stats` accessor (revisit only if a concrete consumer gap appears).
- PlayerId semantics reconciliation remains DEFERRED (Task 139, unchanged).

## Task 141 - CharacterController (Entity Foundation)

Status: KEEP / NOT REQUIRED — **PASS** (no production change; the Player's CharacterController
already exists and is the authoritative physical character-collision representation, with no
movement implemented).

### 141.1 Audit findings (facts)

- The Player prefab (`Assets/Football/Prefabs/Players/Player.prefab`, established Task 138) contains
  **exactly one** `CharacterController`: on the **Collision** child GameObject.
- Serialized configuration (current YAML state): `height 1.8`, `radius 0.3`, `center {0, 0.9, 0}`,
  `slopeLimit 45`, `stepOffset 0.3`, `skinWidth 0.08`, `minMoveDistance 0.001`; physics material null,
  include/exclude layers 0, providesContacts 0, enabled.
- **No Rigidbody** and **no other Collider** exists anywhere in the Player prefab.
- **No runtime code** references `CharacterController` (only the Editor builder
  `Assets/Football/Editor/FootballSetup.cs` adds/configures it: `height/radius/center` explicitly).
  No `Move()` / `SimpleMove()` call exists. No PlayerMovement / CharacterControllerDriver component
  exists.
- Root prefab carries exactly `Transform` + `PlayerEntity` (Tasks 138/139); hierarchy (Visual /
  Collision / Gameplay / Sockets) unchanged.

### 141.2 Configuration analysis

- **Established (authored, deliberate):** `height 1.8`, `radius 0.3`, `center {0, 0.9, 0}` — set
  explicitly by `FootballSetup.cs` and documented since Task 138.
- **Archived Unity defaults (preserved, NOT product-tuned):** `slopeLimit 45`, `stepOffset 0.3`,
  `skinWidth 0.08`, `minMoveDistance 0.001`. These were left at creation defaults; no product
  requirement documents them, so they are preserved as-is (rule: no arbitrary value tuning). Product
  intent for these values is **UNKNOWN / DEFERRED**.
- **UNKNOWN / DEFERRED:** `detectCollisions` and `enableOverlapRecovery` are not serialized in this
  Unity version (runtime property defaults `true`); `m_Material`, include/exclude layers,
  `providesContacts`, `layerOverridePriority`. No project evidence of a deliberate product choice.

### 141.3 Ownership

- Authoritative character collision/motion primitive: **the single CharacterController on
  `Player/Collision` — KEEP** (no conflicting collider, no Rigidbody).
- Location: **Collision child** (not moved to the root; no convenience relocation — the parent
  transform / future movement behavior operates on this entity).
- No duplicate CharacterController, no Rigidbody, no extra Collider introduced.

### 141.4 Configuration decision

- Preserve **all** current values (both established and default-preserved). No value changed.
- No runtime code may modify the configuration (verified by test: no runtime CharacterController
  reference exists).

### 141.5 Implementation

**KEEP / NOT REQUIRED** (rule 14/15): the existing setup satisfies the Task 141 entity foundation.
No production code change, no new runtime type, no new prefab component.

### 141.6 Player prefab integration

Unchanged. The CharacterController remains the sole Collider-derived component on the Collision child;
`PlayerEntity` remains on the root; no movement/rotation/state-machine/input components attached.

### 141.7 Focused tests

New file `Tests/EditMode/CharacterControllerContractTests.cs` (8 tests), all passing:
`PlayerPrefab_ContainsExactlyOneCharacterController`,
`CharacterController_IsLocated_OnCollisionChild`,
`CharacterController_EstablishedConfiguration_IsPreserved`,
`CharacterController_PreviouslyDefaultedValues_ArePreserved_NotRetuned`,
`PlayerPrefab_HasNoRigidbody`,
`PlayerPrefab_HasNoCollider_OtherThanTheCharacterController`,
`NoRuntimeCode_HoldsA_CharacterControllerReference`,
`NoPlayerMovementComponent_OrDriver_Exists`.

Overlap with existing suites is deliberate but minimal: the full prefab structure remains locked by
`PlayerPrefabStructureTests` (Task 138) and the absence of a runtime movement system by
`MovementDebugCategoryTests` (Task 55); the new fixture locks only the CharacterController-specific
entity contract.

### 141.8 Regression

- Focused: **8/8 passed**.
- Full EditMode regression: **1824/1824 passed, 0 failed, 0 inconclusive, 0 skipped** (+8 vs Task 140).
- Compile status: clean, 0 error CS, 0 warning CS in the final full run log.
- Old project integrity: `D:\Projects\unity\ofc` **117/117 unchanged** (byte-identical paths/sizes/
  timestamps vs baseline); no git operations.
- GUID integrity: new file `CharacterControllerContractTests.cs.meta` guid
  `135f12276ab9b20449f83ba88c1091bb`, unique project-wide (0 duplicates across 274 metas).

### 141.9 Boundary / deferred items (NOT done by Task 141)

- **No movement behavior** — no walk/run/sprint speed, acceleration, gravity, ground detection.
- **No slope / step handling** — `slopeLimit`/`stepOffset` remain archived defaults; product tuning
  DEFERRED until a movement system defines requirements.
- **No grounded/isGrounded state, no velocity state, no airborne state.**
- **No rotation behavior, no input consumption, no state machine, no ball interaction.**
- `detectCollisions`, `enableOverlapRecovery`, physics material, layer overrides: UNKNOWN/DEFERRED
  (no product requirement).
- All of the above belongs to later movement / locomotion Tasks.
- PlayerId semantics reconciliation remains DEFERRED (Task 139, unchanged).

## Task 142 - Visual Root

Status: KEEP / NOT REQUIRED — **PASS** (no production change; the Player already has one
authoritative, presentation-only Visual Root in the form of the `Player/Visual` boundary).

### 142.1 / 142.2 Audit findings (facts)

- `Player/Visual` (prefab GameObject `908863342391622937`) is a **direct child** of the Player root
  and is **exactly one** object named "Visual" in the whole prefab.
- Visual carries a single component: **Transform** (`2412289790427987434`). No MonoBehaviour, no
  Renderer, no collider, no script of any kind.
- Visual children (Task 138 scaffold, preserved): `Model` (`9099178839783580821`), `Animator`
  (`5149835650971359690`), `AudioSource` (`6141484430008514792`) — each **Transform-only**
  (exactly one component).
- No `MeshRenderer` / `SkinnedMeshRenderer` / `Renderer`, no `Animator` component, no `AudioSource`
  component anywhere in the prefab (no `!u!23` / `!u!95` / `!u!137` blocks; only `!u!114` in the
  prefab is `PlayerEntity` on the root).
- **No model/animation assets exist in the project** — zero `.fbx`, `.controller`, `.anim`,
  `.mesh`, `.mat` files; the only prefabs are Player / SoccerBall / Stadium.
- **No runtime code references Visual** — no `GetComponentInChildren`, no `Transform.Find`,
  no `.Visual` binding anywhere in Runtime. Only the Editor builder `FootballSetup.cs` creates the
  Visual child, and `PlayerPrefabStructureTests` (Task 138) locks the hierarchy.
- No `VisualRoot` / `VisualController` / `PlayerVisual` / `VisualManager` / `SkinManager` /
  `KitManager` / `ModelController` / `AnimationController` type exists anywhere.
- `PlayerEntity` declares no visual member (single authored `PlayerDefinition` reference only).
- `Docs/PrefabConventions.md` already documents: `Player/ ├── Visual/ ← presentation only` and
  "Visual hierarchy separate from gameplay hierarchy".

### 142.2 Classification

- **FACT**: Visual Root = `Player/Visual`, transform-only, direct child of Player, presentation-only.
- **INFERENCE**: `Model`/`Animator`/`AudioSource` placeholders are intentional Task 138 scaffolding
  for later presentation Tasks.
- **UNKNOWN**: rendered model contents / visual details (no assets exist yet).
- **DEFERRED**: Animator behavior (Task 143), sockets behavior (Task 144), collision-layer
  configuration (Task 145), movement (Task 159+), models/kits/skins.

### 142.3 Visual Root ownership

- Authoritative Visual Root: **the existing `Player/Visual` object — KEEP**. Player remains the
  entity root; `Visual` is the presentation-only child boundary.
- No parallel visual root was created (no `VisualRoot`, `PlayerVisual`, `Visuals`, `View`, etc. —
  locked by test).
- Visual is presentation-only, independent of Collision (CharacterController stays on the separately
  unowned physical representation) and of Gameplay.
- The Visual Root is a **Transform-only boundary at this stage** (no component added by Task 142).

### 142.4 Entity / Visual boundary

Verified separation (locked by tests):
- `Player` = entity/logical root (Transform + PlayerEntity).
- `Visual` = presentation hierarchy (transform-only scaffold: Model / Animator / AudioSource).
- `Collision` = physical representation (CharacterController).
- `Gameplay` = future gameplay systems (empty child shells).
- `Sockets` = attachment points.
- Visual owns **none** of: movement, collision, input, PlayerIdentity, PlayerStats, state machine,
  ball possession, AI, replay, network, gameplay decisions.

### 142.5 API / data design

- **No runtime reference to the Visual Root is required now** — no consumer exists.
- No new API / component / accessor was introduced (no `VisualController`, `PlayerVisual`,
  `VisualManager`, `SkinManager`, `KitManager`, `ModelController`, `AnimationController`).
- `PlayerEntity` exposes no Transform / GameObject / visual accessor (identity binding only).

### 142.6 Implementation / consolidation

**KEEP / NOT REQUIRED** (rule 20/21): the existing Visual hierarchy is correct and documented;
no production change was made. Nothing was added to make the task appear implemented (no mesh,
no materials, no kits/skins, no animation behavior, no Animator Controller, no visual scripts).

### 142.7 Player prefab integration review

Unchanged and verified:
```
Player            (Transform + PlayerEntity)
├── Visual        (Transform only; → Model/Animator/AudioSource, each Transform-only)
├── Collision     (Transform + CharacterController)
├── Gameplay      (empty shells: Input/Movement/Rotation/StateMachine/Actions/BallInteraction/Animation)
└── Sockets       (LeftFoot/RightFoot/Head/BallControlPoint)
```
- Visual remains a direct child of Player; separate from Collision and Gameplay.
- PlayerEntity remains on Player root; CharacterController remains on Collision.
- No gameplay MonoBehaviour was attached to Visual; hierarchy stable.
- Model/Animator/AudioSource placeholders preserved as-is.

### 142.8 Focused tests

New file `Tests/EditMode/VisualRootContractTests.cs` (11 tests), all passing:
`PlayerPrefab_ContainsExactlyOneVisualObject`,
`Visual_IsDirectChildOfPlayerRoot`,
`PlayerPrefab_HasNoParallelVisualRoot`,
`Visual_IsTransformOnlyBoundary`,
`Visual_HasExactlyTheEstablishedChildStructure`,
`Visual_AndItsChildren_AreTransformOnlyPlaceholders`,
`Visual_SubtreeContainsNoPresentationOrPhysicsComponents`,
`CharacterController_IsNotOwnedBy_Visual`,
`Visual_IsSeparateFrom_CollisionAndGameplay`,
`NoVisualRuntimeManagement_Created`,
`PlayerEntity_DeclaresNoVisualOwnership`.

Overlap with existing suites is deliberate but minimal: full prefab structure stays locked by
`PlayerPrefabStructureTests` (Task 138) and the absence of any animation runtime by
`AnimationDebugCategoryTests` (Task 57); the new fixture locks only the Visual Root boundary
contract. One test fix during the run: `PlayerEntity_DeclaresNoVisualOwnership` initially matched
the inherited `Component.transform` plumbing — restricted to declared members and excluding the
universal `transform`/`gameObject` accessors (verified by passing focused run).

### 142.9 Regression

- Focused: **11/11 passed**.
- Full EditMode regression: **1835/1835 passed, 0 failed, 0 inconclusive, 0 skipped** (+11 vs
  Task 141) at `C:\Users\moham\AppData\Local\Temp\opencode\t142\full_regression.xml`.
- Compile status: clean, 0 error CS, 0 warning CS in the final full run log.
- Old project integrity: `D:\Projects\unity\ofc` **117/117 unchanged** (byte-identical
  paths/sizes/timestamps vs baseline); no git operations.
- GUID integrity: new file `VisualRootContractTests.cs.meta` guid
  `24d09d04d87c3c245b0a67209ba430c2`, unique project-wide (0 duplicates across 275 metas).
- Unexpected changes: one transient Unity startup failure of the first full-run launch (exited
  code 1 immediately, process lock from the focused run); relaunched cleanly — final full run
  completed with the results above.

### 142.10 Boundary / deferred items (NOT done by Task 142)

- **No Animator behavior / animation state machine / Animator Controller** (Task 143).
- **No model / mesh / skinned mesh implementation, no materials, no kits/skins.**
- **No sockets behavior** (Task 144); **no collision-layer configuration** (Task 145).
- **No movement / rotation / input / state machine / ball gameplay** (later Tasks).
- The Visual hierarchy remains **scaffold-only**; the `Model` object is an empty Transform
  placeholder, interpreted strictly (an empty Transform is not a rendered model).
- PlayerId semantics reconciliation remains DEFERRED (Task 139, unchanged).

## Task 143 - Animator Root

Status: KEEP / NOT REQUIRED — **PASS** (no production change; the Player already has one
authoritative Animator Root boundary in the form of the transform-only `Player/Visual/Animator`
object).

### 143.1 / 143.2 Audit findings (facts)

- `Player/Visual/Animator` (prefab GameObject `5149835650971359690`, Transform
  `6084545515332345887`) is a **direct child** of Visual (father = Visual's Transform
  `2412289790427987434`) and is **exactly one** object named "Animator" in the whole prefab.
- It carries a single component: **Transform** (no children). **CRITICAL DISTINCTION: the
  GameObject named "Animator" is NOT a UnityEngine.Animator component.**
- There is **no `!u!95` Animator component anywhere in the Player prefab** (root, Visual subtree,
  Collision, Gameplay all animator-component-free — the only `!u!114` is PlayerEntity on root).
- **No animation assets exist in the project**: zero `.controller`, `.anim`, `.clip`, `.fbx` files.
- **No runtime/production code references UnityEngine.Animator or any animator API** (no
  Play/CrossFade/SetBool/SetFloat/SetTrigger/Playables/parameters/Root Motion). No
  AnimatorController / AnimationController / AnimationManager / AnimationBridge /
  AnimationStateMachine / PlayerAnimator / AnimatorControllerWrapper type exists anywhere.
- `Model` and `AudioSource` are siblings of `Animator` under Visual (each separate, Transform-only).
- Existing locks: `PlayerPrefabStructureTests` (Task 138) asserts the `Animator` child exists under
  Visual; `AnimationDebugCategoryTests` (Task 57) locks "no production animation system" and "no
  Animator component on the prefab"; `VisualRootContractTests` (Task 142) locks the Visual subtree
  is Transform-only.

### 143.2 Classification

- **FACT**: Animator Root = `Player/Visual/Animator`, Transform-only placeholder, direct child of
  Visual. No Unity Animator component exists.
- **INFERENCE**: the "Animator" object is the scaffolded animation boundary (Task 138 placeholder
  for the future Animator component).
- **UNKNOWN**: future Animator component configuration, controller, parameters, clips.
- **DEFERRED**: the entire animation implementation (controller, clips, blend trees, transitions,
  parameters, Root Motion, gameplay-to-animation communication) — all belong to later Tasks.

### 143.3 Animator Root ownership

- Authoritative Animator Root: **the existing `Player/Visual/Animator` object — KEEP**. It remains
  under Visual (presentation root). Player root and Collision remain free of any Animator.
- The transform-only placeholder is **sufficient at this stage**; no parallel Animator Root was
  created.

### 143.4 Animation / gameplay boundary

- Ownership chain (future): Player Entity → Gameplay/State → (future) Animation system → Animator
  Root → Model / visual representation.
- The Animator Root owns **none** of: movement, CharacterController, collision, identity, stats,
  input, state machine logic, ball interaction, AI, replay, network, gameplay decisions (verified
  by tests: no MonoBehaviours, no Colliders/Rigidbody under the Animator Root; no gameplay subtree
  under it).

### 143.5 API / data design

- **No runtime reference to the Animator Root is required now** — no consumer exists.
- No new API/component was introduced (no `PlayerAnimator`, `AnimationController`,
  `AnimatorControllerWrapper`, `AnimationManager`, `VisualAnimationManager`). No Animator
  parameters, no animation IDs, no animation state enums added.
- `PlayerEntity` declares no Animator member (no gameplay-to-animation communication).

### 143.6 Implementation / consolidation

**KEEP / NOT REQUIRED** (rule 26/28): the existing Animator Root structure is correct and
documented; no production change. No Animator component, no Animator Controller, no clips, no
parameters, no transitions, no Root Motion, no animation scripts added.

### 143.7 Player prefab integration review

Unchanged and verified:
```
Player                         (Transform + PlayerEntity)
└── Visual                     (Transform only; presentation root)
    ├── Model                  (Transform only — future mesh)
    ├── Animator               (Transform only — Animator Root boundary)
    └── AudioSource            (Transform only)
```
- Animator remains under Visual; separate from Collision and Gameplay.
- PlayerEntity remains on Player root; CharacterController remains on Collision.
- No gameplay MonoBehaviour attached to Animator; no animation runtime system introduced;
  hierarchy stable. Animator preserved as Transform-only.

### 143.8 Focused tests

New file `Tests/EditMode/AnimatorRootContractTests.cs` (9 tests), all passing:
`PlayerPrefab_ContainsExactlyOne_AnimatorNamedObject`,
`AnimatorRoot_IsDirectChildOfVisual`,
`AnimatorRoot_IsTransformOnly_NotAUnityAnimatorComponent`,
`NoUnityAnimatorComponent_Exists_AnywhereInPlayerPrefab`,
`AnimatorRoot_IsPresentationOnly_NoGameplayOrPhysics`,
`AnimatorRoot_IsSeparateFrom_CollisionAndGameplay`,
`NoAnimatorController_Clips_OrAnimationAssets_Exist`,
`NoAnimationRuntimeManager_Created`,
`PlayerEntity_DeclaresNoAnimatorMember`.

The tests explicitly enforce the GameObject-named-"Animator" vs UnityEngine.Animator-component
distinction and never assert a Unity Animator component must exist. Reused existing coverage where
appropriate (no duplicate assertions of hierarchy already locked by Tasks 138/142/57).

### 143.9 Regression

- Focused: **9/9 passed**.
- Full EditMode regression: **1844/1844 passed, 0 failed, 0 inconclusive, 0 skipped** (+9 vs
  Task 142) at `C:\Users\moham\AppData\Local\Temp\opencode\t143\full_regression.xml`.
- Compile status: clean, 0 error CS, 0 warning CS in the final full run log.
- Old project integrity: `D:\Projects\unity\ofc` **117/117 unchanged** (byte-identical
  paths/sizes/timestamps vs baseline); no git operations.
- GUID integrity: new file `AnimatorRootContractTests.cs.meta` guid
  `3d8a21caf3bd34e4f9d3600fbe158d2d`, unique project-wide (0 duplicates across 276 metas).
- Unexpected changes: none.

### 143.10 Boundary / deferred items (NOT done by Task 143)

- **No animation system**: no Animator Controller, no clips, no parameters, no transitions, no
  blend trees, no Root Motion, no animation events.
- **No gameplay-to-animation communication** (no bridge; entity exposes nothing animation-related).
- **No movement / Player State Machine behavior / ball gameplay / sockets behavior / collision-layer
  changes** (later Tasks).
- The Animator Root remains a **Transform-only scaffold**; the `Animator` object is an empty
  Transform placeholder, interpreted strictly (a GameObject named "Animator" is NOT a Unity
  Animator component).
- PlayerId semantics reconciliation remains DEFERRED (Task 139, unchanged).

## Task 144 - Socket System

Status: KEEP / NOT REQUIRED — **PASS** (no production change; the Player already has an
authoritative Socket structure as a plain, named Transform hierarchy — the preferred
Transform-based architecture).

### 144.1 / 144.2 Audit findings (facts)

- `Player/Sockets` (prefab GameObject `5248918721182547524`, Transform `4250116324313715490`) is a
  **direct child** of the Player root and is **exactly one** object named "Sockets" in the whole
  prefab.
- It is **Transform-only** (one component) at local position `(0, 0, 0)`, with **exactly four**
  children, each a single-component Transform placeholder (no children):
  - `LeftFoot` (obj `1427906854860780568`, Transform `8780422919586045893`) → local `(-0.1, 0.05, 0)`
  - `RightFoot` (obj `9211741675396790121`, Transform `6917018095287186050`) → local `(0.1, 0.05, 0)`
  - `Head` (obj `29074085518041440`, Transform `2689268104496913309`) → local `(0, 1.8, 0)`
  - `BallControlPoint` (obj `25654867446244989`, Transform `8654491856172948880`) → local `(0, 0.3, 0.4)`
- These positions are **authored/established in the Editor builder** (`FootballSetup.cs`:
  `LeftFoot (-0.1,0.05,0)`, `RightFoot (0.1,0.05,0)`, `Head (0,1.8,0)`,
  `BallControlPoint (0,0.3,0.4)`) and preserved in the prefab — documented scaffold values, not
  invented "correct football" coordinates.
- **No runtime code references the Sockets hierarchy.** No socket component, no
  SocketSystem/SocketManager/SocketRegistry/SocketDatabase/PlayerSocketController/
  DynamicSocketCollection type exists anywhere. No `SocketPoint`/`AttachmentPoint` types.
- No duplicate socket hierarchy; no speculative sockets (`Camera`/`Hand`/`Chest`/`Equipment`/
  `BallKick`/`PassTarget` absent).
- Existing coverage: `PlayerPrefabStructureTests` (Task 138) asserts the `Sockets` child exists and
  holds the expected socket names. `Docs/PrefabConventions.md` documents `Sockets/ ← attachment
  points`. A GameObject named `BallControlPoint` does NOT mean ball interaction is implemented.

### 144.2 Classification

- **FACT**: `Player/Sockets` is the authoritative socket container (Transform-based, child of the
  Player root); the four sockets are Transform-only attachment points with the established scaffold
  positions.
- **INFERENCE**: each named socket is intended as a future attachment/reference point (foot, head,
  ball-control location).
- **UNKNOWN**: exact attachment semantics per socket (future consumers).
- **DEFERRED**: ball interaction, dribbling/passing/shooting/tackling, foot/head IK, animation
  behavior, equipment behavior, movement, rotation — all future consumers of socket transforms,
  outside Task 144.

### 144.3 Socket ownership

- Authoritative Socket container: **the existing `Player/Sockets` object — KEEP**. Player root owns
  it; individual sockets are represented **simply by Transforms** (no separate Socket component
  required). No socket manager/system required — rule 25 (prefer Transform-based architecture when
  sufficient). No parallel socket hierarchy created.

### 144.4 Socket / gameplay boundary

- Sockets are **references/attachment points, NOT gameplay logic**. They own none of: ball
  possession, dribbling, kicking, passing, shooting, tackling, movement, state, input, AI, replay,
  network, animation logic (verified by tests: no MonoBehaviours, no Collider/Rigidbody under
  Sockets; CC stays on Collision; Visual/Collision/Gameplay are separate subtrees).

### 144.5 API / data design

- **No runtime socket API is required now** — no consumer exists.
- No new API/component created (no `SocketSystem`, `SocketManager`, `SocketRegistry`,
  `SocketDatabase`, `PlayerSocketController`, `DynamicSocketCollection`). No public Transform
  accessors introduced for hypothetical future systems. The existing Transform hierarchy is
  sufficient.

### 144.6 Socket structure / transform design

- Final socket set (unchanged): **LeftFoot, RightFoot, Head, BallControlPoint**.
- Positions: **preserved** exactly as authored (scaffold values above); NOT renamed, NOT
  repositioned, NOT re-tuned. No extra sockets added.

### 144.7 Implementation / consolidation

**KEEP / NOT REQUIRED** (rule 27): the existing Sockets hierarchy is correct and documented; no
production change. No runtime socket manager/component added; no ball attachment, no IK, no
equipment, no animation implemented.

### 144.8 Player prefab integration review

Unchanged and verified:
```
Player                         (Transform + PlayerEntity)
└── Sockets                    (Transform only; container at origin)
    ├── LeftFoot              (Transform; (-0.1, 0.05, 0))
    ├── RightFoot             (Transform; (0.1, 0.05, 0))
    ├── Head                  (Transform; (0, 1.8, 0))
    └── BallControlPoint      (Transform; (0, 0.3, 0.4))
```
- Sockets is a child of Player; socket transforms stable; no duplicate socket hierarchy.
- PlayerEntity remains on Player root; CharacterController remains on Collision; Visual intact.
- No gameplay MonoBehaviour under Sockets; no runtime socket manager introduced.

### 144.9 Focused tests

New file `Tests/EditMode/SocketContractTests.cs` (10 tests), all passing:
`PlayerPrefab_ContainsExactlyOneSocketsContainer`,
`Sockets_IsDirectChildOfPlayerRoot`,
`Sockets_HasExactlyTheFourEstablishedSockets`,
`Sockets_AndEachSocket_AreTransformOnlyAttachmentPoints`,
`Socket_Positions_MatchEstablishedScaffoldValues`,
`Sockets_ContainNoGameplayMonoBehaviour`,
`Sockets_DoNotOwn_CharacterControllerOrAnyPhysics`,
`Sockets_AreSeparateFrom_Visual_Collision_Gameplay`,
`NoSpeculativeSockets_Added`,
`NoSocketManager_Registry_OrSystem_Created`.

Socket-name presence was already covered by `PlayerPrefabStructureTests` (Task 138); the new
fixture adds only the Task 144 ownership/boundary/manager-absence contract. No brittle or
false-positive tests; no tests for hypothetical gameplay.

### 144.10 Regression

- Focused: **10/10 passed**.
- Full EditMode regression: **1854/1854 passed, 0 failed, 0 inconclusive, 0 skipped** (+10 vs
  Task 143) at `C:\Users\moham\AppData\Local\Temp\opencode\t144\full_regression.xml`.
- Compile status: clean, 0 error CS, 0 warning CS in the final full run log.
- Old project integrity: `D:\Projects\unity\ofc` **117/117 unchanged** (byte-identical
  paths/sizes/timestamps vs baseline); no git operations.
- GUID integrity: new file `SocketContractTests.cs.meta` guid
  `d56347de6874c74458a8e661cc8faa26`, unique project-wide (0 duplicates across 277 metas).
- Unexpected changes: none.

### 144.11 Boundary / deferred items (NOT done by Task 144)

- **No ball interaction / possession, no dribbling / passing / shooting / tackling.**
- **No foot/head IK, no animation behavior, no equipment behavior.**
- **No movement / rotation / Player State Machine behavior / input consumption / collision-layer
  gameplay.**
- Socket consumers (future systems that attach to or read the four socket Transforms) are
  **DEFERRED** — the sockets remain stable references only.
- PlayerId semantics reconciliation remains DEFERRED (Task 139, unchanged).

## Task 145 - Collision Layers

Status: KEEP / NOT REQUIRED — **PASS** (no production change; the Player's collision-layer
configuration is the Unity default and is authoritative — no custom layers, no custom collision
matrix, and no layer-to-gameplay coupling exist or are required).

### 145.1 / 145.2 Audit findings (facts)

- `ProjectSettings/TagManager.asset` defines **only the Unity built-in layers**: `Default`,
  `TransparentFX`, `Ignore Raycast`, `Water`, `UI` (index 3 empty/reserved). `tags: []`. **No
  custom Player / Ball / Environment / field layer name exists.**
- `ProjectSettings/DynamicsManager.asset` (authoritative 3D physics settings): `m_LayerCollisionMatrix`
  is the **Unity default all-`ff` matrix** (every layer collides with every layer); no custom
  collision-matrix rule; all other physics settings are defaults.
- `Player.prefab`: **all 19 GameObjects are on `m_Layer: 0` ("Default")** — root, Visual subtree
  (Model/Animator/AudioSource), Collision, Gameplay subtree, and Sockets subtree, including the
  SoccerBall-adjacent physics. No prefab object references a non-default layer.
- Player prefab physics composition (unchanged from Tasks 138/141): exactly **one**
  `CharacterController` on the **Collision** child; **zero** Rigidbody; **zero** other Collider
  (box/sphere/capsule/mesh); **zero** trigger components.
- Runtime code: **no** `GameObject.layer` assignment, **no** `Physics.IgnoreLayerCollision`,
  **no** `CompareTag`, **no** `GetMask`/`NameToLayer`, **no** collision-matrix manipulation
  anywhere in the Runtime.
- The **only `LayerMask` in the codebase** is `Football.Data.MovementConfig.GroundLayer`
  (`Runtime/Data/MovementConfig.cs:25`) — an **authored data field** on a ScriptableObject config;
  it is NOT read by any runtime code and ground detection is not implemented (future movement
  concern). No evidence it represents a defined project layer.
- Editor-only physics: `FootballSetup.cs:178-188` builds the **SoccerBall** physics shell
  (Rigidbody + SphereColliders) — ball-only, also on the default layer; not Player scope.
- `Docs/PrefabConventions.md` documents `Collision/ ← physics shell`. Architecture Tasks 142/143/144
  listed "collision-layer configuration (Task 145)" as not-done — now addressed here.

### 145.2 Classification

- **FACT**: authoritative layer config = Unity defaults. Player is on "Default" (0) throughout;
  matrix is default all-collide; TagManager has no custom layers; no layer constants exist.
- **INFERENCE**: the Collision child (CharacterController) is the intended physical body using the
  default layer until future gameplay defines dedicated layers.
- **UNKNOWN**: whether future gameplay (player-vs-player, player-vs-ball, environment) will need
  dedicated layers/collision-matrix rules — no project evidence; honestly not invented.
- **DEFERRED**: ground detection via `MovementConfig.GroundLayer`; player-player / player-ball /
  tackle / knockback / trigger behavior; any custom layer matrix.

### 145.3 Collision Layer ownership decision

1. Layer owning the Player's physical body: **the Unity built-in "Default" layer (0)** — the layer
   the authoritative prefab assigns today. No Player-specific layer exists or is invented.
2. Player root vs Collision child: **same layer ("Default")** — KEEP. No split required.
3. Collision child may carry its own layer: yes in principle, but nothing justifies a change; the
   current assignment is authoritative and sufficient.
4. Authoritative configuration system: **`ProjectSettings/TagManager.asset`** (layer definitions)
   + **`ProjectSettings/DynamicsManager.asset`** (collision matrix). Both are Unity serialized
   project settings.
5. Collision behavior control: at this stage by **CharacterController component settings**
   (Task 141) on a single collapsed layer; the uniform default matrix carries no distinguishing
   semantics yet.
6. Is the collision-matrix configuration sufficient? **YES** — no gameplay depends on layer
   separation; no change.

### 145.4 Player / Collision / Visual / Sockets / Gameplay boundary

- **Collision** = the physical representation (CharacterController). Stays on "Default".
- **Visual** = presentation only — physics-free (has no Collider/Rigidbody). Layer "Default".
- **Sockets** = Transform-only attachment/reference points — physics-free. Layer "Default".
- **Gameplay** = no physics components (transform-only children). Layer "Default".
- No collider added to Visual/Sockets; no Rigidbody; no triggers; no extra colliders anywhere.

### 145.5 Layer / Gameplay boundary review

- Collision layers define only potential physical interaction, and here they are Unity defaults.
  **No gameplay semantics** (tackle logic, possession, ball control, movement, state transitions,
  knockback, decisions, input priority, AI) are encoded in any layer/constant/matrix.
- The layer matrix future gameplay might need is **UNKNOWN / DEFERRED** — not invented.

### 145.6 Implementation / consolidation

**KEEP / NOT REQUIRED.** No production change. No speculative layers, no layer renaming, no matrix
rewrite, no LayerManager/CollisionManager, no code-based layer management, no new colliders/
Rigidbody/triggers. `ProjectSettings` untouched (TagManager & DynamicsManager at defaults).

### 145.7 Player prefab integration review

Unchanged and verified:
```
Player                         (Transform + PlayerEntity)   layer "Default"
├── Visual                     Transform (+Model/Animator/AudioSource)  physics-free
├── Collision                  Transform + CharacterController  ← physical body, layer "Default"
├── Gameplay                   Transform (+7 transform-only children)  physics-free
└── Sockets                    Transform (+LeftFoot/RightFoot/Head/BallControlPoint)  physics-free
```
- PlayerEntity intact on root; CharacterController intact on Collision; Collision is the physical
  boundary; Visual/Sockets/Gameplay physics-free; layer assignments consistent (all "Default");
  no new collider/rigidbody/trigger; no gameplay MonoBehaviour introduced.

### 145.8 Focused tests

New file `Tests/EditMode/CollisionLayerContractTests.cs` (12 tests), all passing:
`PlayerPrefab_Exists_AndLoads`,
`Player_Root_IsOn_DefaultLayer_Only`,
`Every_PlayerGameObject_UsesOnlyTheDefaultLayer_NoSpeculativeLayer`,
`Collision_Child_UsesThe_Same_DefaultLayer`,
`Collision_Child_RemainsThePhysicalRepresentation`,
`Visual_OwnsNoPhysicsComponents`,
`Sockets_OwnNoPhysicsComponents`,
`Gameplay_OwnsNoPhysicsComponents`,
`PlayerPrefab_HasNoRigidbody`,
`PlayerPrefab_HasExactlyOneCollider_TheCharacterController_NoTriggers`,
`PlayerEntity_RemainsOnRoot_And_CharacterControllerOnCollision`,
`NoCustomLayer_Defined_OrLayerManager_Created`.

Facts-only: layer assertions are driven by `LayerMask.NameToLayer("Default")`; the "no custom
layer" proof reads `ProjectSettings/TagManager.asset` via `SerializedObject` and requires exactly
the Unity built-in layer set. Deeper CharacterController geometry stays with
`CharacterControllerContractTests` (Task 141) — not duplicated here. No hypothetical player-ball/
player-player layer rules asserted.

### 145.9 Regression

- Focused: **12/12 passed** (compile fix during the run: `PlayerEntity` reference fully qualified
  to `Football.Players.PlayerEntity`).
- Full EditMode regression: **1866/1866 passed, 0 failed, 0 inconclusive, 0 skipped** (+12 vs
  Task 144) at `C:\Users\moham\AppData\Local\Temp\opencode\t145\full_regression.xml`.
- Compile status: 0 error CS, 0 warning CS in the final full run log. (The known pre-existing
  `CS0219` in `FixedTimestepPolicyTests.cs` lines 149/150 appears only in the incremental
  focused-run compile log; it is pre-existing, unrelated to Task 145 — no evidence otherwise.)
- Old project integrity: `D:\Projects\unity\ofc` **117/117 unchanged** (byte-identical
  paths/sizes/timestamps vs baseline); no git operations.
- GUID integrity: new file `CollisionLayerContractTests.cs.meta` guid
  `d1c9b87ddc564ba499489068fa55a389`, unique project-wide (0 duplicates across 278 metas).
- Unexpected changes: none (production untouched; ProjectSettings untouched).

### 145.10 Boundary / deferred items (NOT done by Task 145)

- **No player-vs-player collision behavior; no tackle collision; no knockback/push reactions.**
- **No player-ball collision behavior; no triggers.**
- **No Rigidbody, no extra colliders, no movement, no ground detection, no slope handling, no
  State Machine behavior, no input consumption, no ball gameplay.**
- **No custom layers and no collision-matrix rules invented.** Any future Player/Ball/Environment
  layer + matrix requirements are UNKNOWN / DEFERRED.
- `MovementConfig.GroundLayer` remains an **unused authored data field** (future ground
  detection config surface, not a runtime layer rule).
- PlayerId semantics reconciliation remains DEFERRED (Task 139, unchanged).
