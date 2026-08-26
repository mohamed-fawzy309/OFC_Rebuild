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

## Lifecycle Interfaces

### IFootballService
The sole lifecycle contract for project-level gameplay services:
- `Initialize()` — called once when the service becomes active
- `Shutdown()` — called when the service is being torn down
- `IsInitialized` — whether the service is currently active

Ownership: Composition Root / Bootstrap creates services, injects dependencies, then calls `Initialize()`. No Singleton or Service Locator involved.

Timing: After object creation, after dependencies are available, before the service is exposed to gameplay, before dependent systems begin ticking.

Idempotency: `Initialize()` is expected to be called exactly once per lifecycle. Ownership prevents duplicate calls.

Failure: Exceptions propagate to the caller. Partially initialized objects are not treated as successfully initialized.

### Why No Standalone IInitializable
A standalone `IInitializable { void Initialize(); }` interface was evaluated and intentionally omitted. Reasons:

1. **Every `Initialize()` call in this project belongs to `IFootballService`.** No non-service object currently requires a separate initialization contract.
2. **`IState` already owns per-state lifecycle** via `Enter()` / `Exit()` — which is semantically distinct from service initialization.
3. **`IFootballService` already satisfies the generic initialization capability.** Adding `IInitializable` creates ambiguity: does a class that implements both have two independent initialization contracts, or does one inherit from the other?
4. **Football is a domain-specific game, not a general-purpose framework.** Generic interfaces are justified when they serve unrelated systems. Here, initialization is a service lifecycle concern.
5. **No concrete class currently needs `IInitializable` without also needing `Shutdown()` and `IsInitialized`.** The full lifecycle (init + shutdown + status) is the real contract.

This decision preserves architectural clarity. `IFootballService` remains the single source of initialization semantics.

### Why No Standalone ITickable
A standalone `ITickable { void Tick(float deltaTime); }` interface was evaluated and intentionally omitted. Reasons:

1. **`IState.Tick(float deltaTime)` already owns per-frame state updates.** Every class that needs externally managed ticking in this project is an `IState` being driven by an `IStateMachine`.
2. **State machines already own tick orchestration.** `GenericStateMachine<TStateId>.Tick()` calls `IState.Tick()` on the active state. No additional abstraction is needed.
3. **Unity MonoBehaviour lifecycle covers presentation.** `Update()`, `FixedUpdate()`, `LateUpdate()` on MonoBehaviours are the appropriate mechanism for presentation-layer per-frame behavior. Wrapping them in an interface adds complexity without improving testability in a meaningful way.
4. **No non-state, non-MonoBehaviour system currently needs external tick control.** No MatchClock, Replay system, or AI scheduler exists that would benefit from a generic tick contract.
5. **Adding `ITickable` would create confusion.** Developers would need to decide whether a new system should be an `IState`, an `ITickable`, or a MonoBehaviour — three overlapping mechanisms for the same purpose.

The existing architecture is sufficient: state machines drive gameplay ticks, Unity drives presentation ticks. If a future system genuinely needs a new tick contract (e.g., a deterministic simulation clock for replay), it should be introduced at that point with a specific purpose, not preemptively.

## Event Architecture

### IGameEvent Contract
All gameplay events implement `IGameEvent` (single property: `float Timestamp` representing simulation time). Events are **immutable structs** — fields are assigned via constructor or init-only at creation, never mutated after construction.

Event naming uses past-tense (e.g., `GoalScoredEvent`, `MatchStartedEvent`) to communicate facts that already happened, not commands.

### GameEvents Dispatcher
`GameEvents` is a static typed dispatcher in `Football.Core`. It is a thin forwarding mechanism, not a global event bus or service locator.

Rules:
- **Publishers** are the systems that own the gameplay fact (e.g., Match system publishes `MatchStartedEvent`).
- **Subscribers** receive notifications for relevant systems (UI, Audio, VFX, replay, analytics).
- **Presentation systems** (Animation, Camera, UI, Audio) may subscribe but never publish gameplay events.
- **Unsubscribe** is mandatory when the subscriber's lifecycle ends. Leaked delegates are bugs.
- **Synchronous dispatch** is the default. Events fire immediately on `Raise()`. Queued/deferred dispatch is reserved for future replay/network simulation.
- **Event spam is prohibited.** Events represent meaningful occurrences (goals, possession changes, kicks), not per-frame state transport.
- **Events do not carry mutable gameplay state containers.** Payloads are value snapshots, not shared references.

## Dependency Rules

### Assembly Dependency Graph
```
Core (zero references — lowest layer)
  ↓
Input, Teams (depend on Core only)
  ↓
Players, Ball, Actions, World (depend on Core + Input)
  ↓
Match, AI (depend on Core + Players + Ball + Teams)
  ↓
Camera, UI (depend on Core only — read gameplay state)
  ↓
Editor (editor-only, may reference anything)
Tests (references assemblies under test)
```

### Forbidden Dependencies
| From | To | Reason |
|------|-----|--------|
| Core | Any gameplay assembly | Core is the foundation layer |
| Presentation (Camera, UI) | Gameplay (Players, Ball, Match, AI) | Presentation observes; it does not own gameplay |
| Gameplay | Presentation | Gameplay must not depend on how it is visualized |
| Any assembly | Editor | Editor is editor-only |
| Data (ScriptableObjects) | Runtime state | Configuration is static; runtime state is mutable |

### Validation
An automated dependency validation test (`ArchitectureDependencyTests`) verifies the asmdef graph at test time. This prevents forbidden dependencies from being introduced accidentally.

## Composition Root

The `_Bootstrap` GameObject in `Bootstrap.unity` is the Composition Root. Its children (`GameBootstrap`, `ServiceRegistry`, `Settings`, `SceneLoader`) are placeholder GameObjects that will be wired with MonoBehaviours in Phase 2.

Rules:
- Services are created by the Composition Root, not by gameplay systems.
- Dependencies are injected at creation time, not discovered at runtime.
- The Composition Root is not a gameplay god-object. It owns lifecycle, not behavior.
- There is exactly one Composition Root per application.
- The Bootstrap scene loads first and persists.

## Dependency Injection Policy

### Approved Patterns
- **Constructor injection**: Pure C# classes (state objects, pure services, logic classes).
- **Composition Root injection**: Major runtime dependencies connected at bootstrap.
- **Prefab/Scene references**: `[SerializeField]` for authoring-time and scene-owned references.
- **Cached local references**: GetComponent at initialization time, cached in a field.

### Discouraged Patterns
- Uncontrolled `FindObjectOfType` / `FindFirstObjectByType` / `FindAnyObjectByType` at runtime.
- Repeated `GetComponent` in hot paths (Update/FixedUpdate).
- Static mutable fields as implicit dependency injection.

### Forbidden Patterns
- Hidden Service Locator (`GlobalServices.Get<T>()` available everywhere with hidden dependencies).
- Gameplay Singletons as architecture (a runtime instance may exist because the Composition Root owns one instance; that does NOT make the type a Singleton).
- Global mutable state as dependency injection.

### Singleton Policy
No Singleton pattern for gameplay architecture. A single runtime instance may exist because the Composition Root owns one instance. `FootballDebugSettings.Instance` is a pre-existing ScriptableObject convenience accessor — classified as technical debt for future cleanup, not a recommended pattern.

## Single Source of Truth

| Domain | Authority | Observer |
|--------|-----------|----------|
| Player gameplay state | Player state system | Animation, UI, Camera |
| Ball possession | Possession gameplay system | UI, Audio, VFX |
| Match score | Match system | UI |
| Player stats | Runtime player object (mutable) | UI, Animation |
| Player definition | PlayerDefinition ScriptableObject | Runtime player (read-only at runtime) |

Rules:
- Animation must not become gameplay authority.
- UI must not own gameplay state.
- Camera must not determine gameplay position.
- Configuration data lives in ScriptableObjects. Runtime state lives on runtime objects.

## Runtime vs Configuration State

### ScriptableObjects
ScriptableObjects under `Data/` are definitions, configuration, and authored data. They are **not** mutable global runtime state.

### Runtime State
Runtime state belongs to runtime objects, runtime services, and explicit simulation state. It is never stored in shared ScriptableObject assets at runtime.

### Existing Config Assets (validated clean)
- `PlayerDefinition` — player identity + attributes (read-only at runtime)
- `TeamDefinition` — team identity + roster + tactics (read-only at runtime)
- `BallConfig` — ball physics parameters (read-only at runtime)
- `MovementConfig` — movement parameters (read-only at runtime)
- `AnimationConfig` — animation tuning (read-only at runtime)
- `DribbleConfig` — dribble tuning (read-only at runtime)
- `MatchRulesDefinition` — match rules + field dimensions (read-only at runtime)

## Time / Update / Simulation Policy

### Update Ownership
- `Update()`: Input sampling (e.g., `HumanPlayerInput`), non-physics frame-driven logic, UI/presentation.
- `FixedUpdate()`: Rigidbody/physics-driven simulation, fixed-step physics interactions.
- `LateUpdate()`: Camera follow, presentation updates dependent on final transforms.

### Simulation Clock
A custom simulation clock is not currently required. `IState.Tick(float deltaTime)` passes caller-provided time into pure logic. When replay or network simulation is implemented, a deterministic simulation clock should be introduced at that point.

### Pause Behavior
During gameplay pause:
- Gameplay simulation stops (state machine stops ticking).
- UI/menu navigation continues.
- Audio may continue or pause depending on context.
- Presentation freezes unless menu is active.

Pause is represented by gameplay state, not by `Time.timeScale` manipulation.

### Determinism
Current expectations: predictable lifecycle, hidden time sources avoided, `deltaTime` passed explicitly. Full deterministic networking is not promised.

## Anti-Patterns Explicitly Avoided

- No giant `GameManager` singleton
- No `PlayerManager` controlling all players
- No animation system driving movement physics
- No camera system determining gameplay position
- No UI system owning gameplay state
- No gameplay logic inside Animator state machines
- No circular assembly dependencies
- No hidden Service Locator
- No per-frame event spam
- No mutable shared ScriptableObject runtime state
- No duplicated state authorities
