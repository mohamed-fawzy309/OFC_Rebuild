# Football Project Architecture

## Core Principles

### 1. Composition over Inheritance
Gameplay behavior is built by composing small, focused components rather than deep inheritance hierarchies. A Player entity owns components for movement, rotation, actions, and state — none of which inherit from each other.

### 2. Single Source of Truth
All configuration lives in ScriptableObjects under `Data/`. Runtime state lives on MonoBehaviours or dedicated state classes. No duplicated definitions across scripts and assets.

### 3. Gameplay vs Presentation Separation
```
Gameplay Logic → State / Event / Data
                    ↓
              Presentation (Animation, VFX, Audio, Camera)
```
Presentation systems READ gameplay state. They never OWN or DRIVE gameplay state. The Animator never moves the character. The Camera never determines gameplay position.

### 4. Data-Driven Configuration
Player attributes, team definitions, match rules, ball physics, movement parameters — all stored in ScriptableObjects. Code references data assets, not hardcoded values.

### 5. State Machines
- **Match Level**: Boot → Loading → MainMenu → PreMatch → KickOff → Playing → Goal → HalfTime → FullTime → Pause
- **Player Level**: Idle, Walk, Run, Sprint, Dribbling, Passing, Shooting, Tackling, Stunned, etc.

State machines are interface-driven (`IState`, `IStateMachine`) for testability and reuse.

### 6. Event-Driven Communication
Systems communicate through typed C# events. No global EventBus singleton. Events are lightweight structs or classes fired through typed delegates.

### 7. Input Abstraction
`IPlayerInput` interface allows pluggable input sources:
- HumanInput (keyboard/gamepad)
- AIInput (AI decision-making)
- ReplayInput (playback)
- NetworkInput (future networking)

### 8. Future Compatibility
- **AI**: Player components accept any `IPlayerInput` — AI plugs in naturally
- **Replay**: Record input + game state, replay through `IPlayerInput`
- **Networking**: Replace local input with network-synchronized input
- **Multiple Game Modes**: Match state machine supports different flow definitions

## Folder Ownership Rules

| Folder | Owns | Does NOT Own |
|--------|------|-------------|
| `Runtime/Core` | Interfaces, events, debug settings | Any gameplay logic |
| `Runtime/Players` | Player state, player components | Ball logic, match logic |
| `Runtime/Ball` | Ball state, ball physics | Player possession logic |
| `Runtime/Actions` | Action definitions and execution | Direct movement code |
| `Runtime/Match` | Match state machine, rules | Player-specific behavior |
| `Runtime/Teams` | Team data, team management | Individual player logic |
| `Runtime/AI` | AI input generation | Direct player control |
| `Runtime/Camera` | Camera follow, framing | Gameplay decisions |
| `Runtime/UI` | Display logic | Gameplay authority |
| `Runtime/World` | Environment, boundaries | Gameplay rules |
| `Runtime/Input` | Input abstraction | Any specific input mapping |
| `Data/` | ScriptableObject definitions | Runtime state |

## Assembly Dependency Direction

```
Core (no dependencies)
  ↓
Input, Teams (depend on Core)
  ↓
Players, Ball, Actions, World (depend on Core, Input)
  ↓
Match, AI (depend on Core, Players, Ball, Teams)
  ↓
Camera, UI (depend on Core, read gameplay state)
  ↓
Editor (depends on anything, editor-only)
Tests (depends on assemblies under test)
```

Rule: Presentation assemblies never feed back into gameplay assemblies.

## Anti-Patterns Explicitly Avoided

- No giant `GameManager` singleton
- No `PlayerManager` controlling all players
- No animation system driving movement physics
- No camera system determining gameplay position
- No UI system owning gameplay state
- No gameplay logic inside Animator state machines
- No circular assembly dependencies
