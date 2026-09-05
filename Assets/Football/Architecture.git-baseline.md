# Football Project Architecture

## Core Principles

### 1. Composition over Inheritance
Gameplay behavior is built by composing small, focused components rather than deep inheritance hierarchies. A Player entity owns components for movement, rotation, actions, and state ΓÇö none of which inherit from each other.

### 2. Single Source of Truth
All configuration lives in ScriptableObjects under `Data/`. Runtime state lives on MonoBehaviours or dedicated state classes. No duplicated definitions across scripts and assets.

### 3. Gameplay vs Presentation Separation
```
Gameplay Logic ΓåÆ State / Event / Data
                    Γåô
              Presentation (Animation, VFX, Audio, Camera)
```
Presentation systems READ gameplay state. They never OWN or DRIVE gameplay state. The Animator never moves the character. The Camera never determines gameplay position.

### 4. Data-Driven Configuration
Player attributes, team definitions, match rules, ball physics, movement parameters ΓÇö all stored in ScriptableObjects. Code references data assets, not hardcoded values.

### 5. State Machines
- **Match Level**: Boot ΓåÆ Loading ΓåÆ MainMenu ΓåÆ PreMatch ΓåÆ KickOff ΓåÆ Playing ΓåÆ Goal ΓåÆ HalfTime ΓåÆ FullTime ΓåÆ Pause
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
- **AI**: Player components accept any `IPlayerInput` ΓÇö AI plugs in naturally
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
  Γåô
Input, Teams, Players, Ball, Actions, World, Camera, UI (depend on Core only)
  Γåô
Match, AI (depend on Core, Players, Ball, Teams)
  Γåô
Editor (depends on anything, editor-only)
Tests (depends on assemblies under test)
```

Rule: Presentation assemblies never feed back into gameplay assemblies.

## Lifecycle Interfaces

### IFootballService
The sole lifecycle contract for project-level gameplay services:
- `Initialize()` ΓÇö called once when the service becomes active
- `Shutdown()` ΓÇö called when the service is being torn down
- `IsInitialized` ΓÇö whether the service is currently active

Ownership: Composition Root / Bootstrap creates services, injects dependencies, then calls `Initialize()`. No Singleton or Service Locator involved.

Timing: After object creation, after dependencies are available, before the service is exposed to gameplay, before dependent systems begin ticking.

Idempotency: `Initialize()` is expected to be called exactly once per lifecycle. Ownership prevents duplicate calls.

Failure: Exceptions propagate to the caller. Partially initialized objects are not treated as successfully initialized.

### Why No Standalone IInitializable
A standalone `IInitializable { void Initialize(); }` interface was evaluated and intentionally omitted. Reasons:

1. **Every `Initialize()` call in this project belongs to `IFootballService`.** No non-service object currently requires a separate initialization contract.
2. **`IState` already owns per-state lifecycle** via `Enter()` / `Exit()` ΓÇö which is semantically distinct from service initialization.
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
5. **Adding `ITickable` would create confusion.** Developers would need to decide whether a new system should be an `IState`, an `ITickable`, or a MonoBehaviour ΓÇö three overlapping mechanisms for the same purpose.

The existing architecture is sufficient: state machines drive gameplay ticks, Unity drives presentation ticks. If a future system genuinely needs a new tick contract (e.g., a deterministic simulation clock for replay), it should be introduced at that point with a specific purpose, not preemptively.

## Event Architecture

### IGameEvent Contract
All gameplay events implement `IGameEvent` (single property: `float Timestamp` representing simulation time). Events are **immutable structs** ΓÇö fields are assigned via constructor or init-only at creation, never mutated after construction.

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
Core (zero references ΓÇö lowest layer)
  Γåô
Input, Teams, Players, Ball, Actions, World, Camera, UI (depend on Core only)
  Γåô
Match, AI (depend on Core + Players + Ball + Teams)
  Γåô
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
No Singleton pattern for gameplay architecture. A single runtime instance may exist because the Composition Root owns one instance. `FootballDebugSettings.Instance` is a pre-existing ScriptableObject convenience accessor ΓÇö classified as technical debt for future cleanup, not a recommended pattern.

## ServiceRegistry

`ServiceRegistry` is a pure C# infrastructure class in `Football.Core`. It provides controlled, type-aware registration and lookup of `IFootballService` instances. It is explicitly **not** a Singleton, a Service Locator, a factory, or a DI container.

### Ownership and Scope
- The Composition Root creates the single `ServiceRegistry` instance and owns its lifetime. There is no `ServiceRegistry.Instance` and no `DontDestroyOnLoad`; scope is Composition-Root-bounded.
- Services are created by their owner (the Composition Root), registered explicitly, initialized by the owner, and shut down by the owner. The registry never constructs, initializes, or shuts down services.
- The registry is a plain C# object owned via explicit reference. It is not a scene-discoverable component and is not located via `Find*`.

### API
- `Register<TService>(TService service)` where `TService : IFootballService` ΓÇö explicit registration. Throws `ArgumentNullException` on null, `InvalidOperationException` on duplicate.
- `Get<TService>()` ΓÇö throws `InvalidOperationException` if the service is absent.
- `TryGet<TService>(out TService service)` ΓÇö returns false (no throw) when absent.

### Policies
- Registration is explicit; no automatic construction (`Activator`/reflection factories) is used.
- Duplicate registration fails loudly; never silently replaced.
- Lookup is type-safe and returns the exact registered instance.
- Registry use is restricted to explicit Composition Root / injected references. Gameplay systems must not perform hidden `Get<T>()` on-demand dependency resolution.

## SceneLoader

`SceneLoader` is a pure C# infrastructure class in `Football.Core`. It is the **single owner** of runtime scene-loading mechanics.

### Ownership and Scope
- SceneLoader is owned and referenced by the Composition Root (via GameBootstrap). It is not a Singleton (`SceneLoader.Instance` does not exist), does not use `DontDestroyOnLoad`, and does not use a Service Locator to discover dependencies.
- GameBootstrap orchestrates; SceneLoader performs scene mechanics; gameplay systems must not call `SceneManager` directly.

### Source of Truth for Loadable Scenes
- The runtime-available scene set is the set of scenes registered in the **Build Settings**, discovered once at construction via `SceneUtility.GetScenePathByBuildIndex`. This works in a built player without Editor or `AssetDatabase` APIs.
- Project note: the Football scenes currently under `Assets/Football/Scenes/` are **not** yet registered in the Build Settings (only the template `Assets/Scenes/SampleScene.unity` is). For those scenes to load at runtime they must be added to the Build Settings. This is a build-configuration gap, not addressed by SceneLoader itself.

### Policy
- **Synchronous**: Loading is synchronous (`SceneManager.LoadScene` single mode). This is the chosen early policy ΓÇö simple and deterministic for the current foundation. Future asynchronous loading can be introduced behind the same loader abstraction without changing callers.
- **Loading state**: explicit `SceneLoadState` (`Idle`, `Loading`, `Loaded`, `Failed`), read-only. Exposed: `CurrentState`, `CurrentScene`, `TargetScene`, `IsLoading`, `IsLoaded`, `IsFailed`.
- **Concurrent loads**: rejected (`InvalidOperationException`) ΓÇö a second load request while a load is in progress fails loudly and never replaces the first.
- **Same-scene loads**: rejected (`InvalidOperationException`) ΓÇö reloading the currently active scene is not permitted.
- **Invalid input**: null, empty, or whitespace scene path throws `ArgumentException`.
- **Missing scene**: a scene not in the available set throws `InvalidOperationException`.
- **Unity load failure**: exceptions are surfaced as `InvalidOperationException` with context; state transitions to `Failed`. Failures are never swallowed, and no automatic fallback or retry is performed.
- **No per-frame polling**: SceneLoader has no `Update`/`FixedUpdate`/`LateUpdate`.

## Scene Transition System

`SceneTransitionSystem` is a pure C# coordinator in `Football.Core` that controls **transition requests and transition lifecycle**. It does NOT take over scene-loading mechanics. The runtime dependency chain is: `SceneTransitionSystem ΓåÆ SceneLoader ΓåÆ SceneManager`.

### Ownership and Scope
- **GameBootstrap** = application startup orchestration.
- **SceneTransitionSystem** = transition request/lifecycle orchestration.
- **SceneLoader** = the ONLY owner of actual scene-loading mechanics (the only class that references `SceneManager`).
- **Unity SceneManager** = the actual engine operation.
- The transition system is a pure C# object owned by the Composition Root. It is not a MonoBehaviour, not a Singleton (`SceneTransitionSystem.Instance` does not exist), does not use `DontDestroyOnLoad`, and does not outlive its owner. Bootstrap lifetime is preserved ΓÇö no second persistent global runtime.

### Valid Requests and State
- A transition is a single `CurrentScene ΓåÆ TargetScene` request. `CurrentScene` is **read through to the loader** (`_loader.CurrentScene`) so there is exactly one mutable source of truth for the current scene ΓÇö no divergent second field.
- Explicit minimal state: `SceneTransitionState { Idle, Loading, Completed, Failed }` (read-only). This is distinct from `SceneLoadState`: the loader answers "what is happening with the actual load?", the transition answers "what is happening with the requested transition?".
- Exposed: `CurrentState`, `CurrentScene` (read-through), `TargetScene`, `IsTransitioning`, `LastError`.

### Policies
- **Delegate loading**: `RequestTransition` calls `SceneLoader.LoadScene(target)`. The transition system contains no direct `SceneManager` call and does not duplicate Build Settings lookup, scene validation, or scene existence checks.
- **Same-scene**: rejected (`InvalidOperationException`) ΓÇö reloading the current scene is not a valid transition and is never treated as a successful transition.
- **Concurrent/conflicting**: rejected (`InvalidOperationException`) ΓÇö a request issued while another is `Loading` is rejected, the first request remains authoritative, and there is no queue, no silent replacement, and no second loader operation.
- **Reentrancy**: a transition callback that attempts a second request while still `Loading` is rejected cleanly without recursion or state corruption.
- **Completion**: on a successful load the state becomes `Completed`, `TargetScene` no longer pending, and `CurrentScene` (via the loader) reflects the new scene. No duplicate completion is emitted.
- **Failure**: on a failed load the state becomes `Failed`, `LastError` preserves the original exception (surfaced as `InvalidOperationException` with the root cause as `InnerException`). Completion never fires after a failure; there is **no automatic retry**, **no fallback**, and **no queue continuation**.
- **Synchronous**: the current loader is synchronous, so a request runs to `Completed` or `Failed` within one call. No fake asynchronous polling is introduced; the design delegates to the loader abstraction so async could be added later without rewriting the public API.
- **No per-frame polling**: no `Update`/`FixedUpdate`/`LateUpdate`.
- **No presentation/input**: no loading UI, progress, fades, or input reading ΓÇö requests arrive explicitly.

## Startup Order

`GameBootstrap` (a MonoBehaviour in `Football.Core`) is the application's **single startup entry point**. It coordinates the deterministic startup sequence and does NOT absorb the responsibilities of the systems it coordinates.

### Startup Phases
The pipeline progresses through explicit, observable phase boundaries:

```
Boot
 Γåô
[Internal] StartupBegan
[Internal] InfrastructureReady    (Phase 0/1: bootstrap + core/infrastructure setup)
[Internal] ServicesRegistered     (Phase 2: service registration)
[Internal] ServicesInitialized    (Phase 3: service initialization)
[Internal] InitialSceneLoaded     (Phase 4: initial scene load ΓÇö if a scene is configured)
[Internal] Complete               (Phase 5: startup complete)
 Γåô
Ready
```

`BootstrapState` (Boot ΓåÆ Initializing ΓåÆ Ready ΓåÆ Failed/ΓÇª) holds the coarse top-level lifecycle state. `StartupPhase` is a companion enum that exposes the fine-grained phase boundaries that the single "Initializing" value cannot express. They are complementary, not redundant ΓÇö `BootstrapState` tracks lifecycle, `StartupPhase` tracks pipeline position.

### Entry Point
`GameBootstrap.Awake()` ΓåÆ `GameBootstrap.Initialize()`. There is exactly one runtime startup entry point. No `Start`, no secondary manager (BootstrapManager/GameManager/RuntimeManager) exists to trigger startup, and startup does not depend on Unity Script Execution Order.

### Phase Work and Boundaries
The phase work is supplied by the Composition Root through `Configure(...)` (prepare-infrastructure, register-services, initialize-services, load-scene + initial scene path). GameBootstrap coordinates the boundaries only:

- **ServiceRegistry** stores/looks up services. GameBootstrap may orchestrate registration but ServiceRegistry must never auto-initialize services.
- **SceneLoader** performs the actual scene load. GameBootstrap requests the initial scene through it; SceneLoader does not decide startup order and does not own startup.
- **Detailed per-service initialization ordering is owned by the service-initialization subsystem**, not by GameBootstrap.

### Prerequisites
- Service Registration requires: bootstrap entry completed, core/infrastructure available.
- Service Initialization requires: required services registered.
- Initial Scene Load requires: required services initialized.
- Startup Complete requires: initial scene load completed (when a scene is configured).

### Race Prevention
- **Double startup (after completion)**: `Initialize()` when `Ready` is a safe no-op reuse ΓÇö the sequence is never executed twice.
- **Concurrent / re-entrant startup**: `Initialize()` while `Initializing` is rejected (`InvalidOperationException`).
- **Startup vs shutdown**: `Shutdown()` during an in-progress startup is rejected via the invalid-transition guard; startup and shutdown cannot corrupt each other.
- **Terminal states**: `Initialize()` from `Failed`/`Stopped` is rejected.
- Startup is single-threaded and event-driven; no locks, no async scheduler, no retries.

### Failure Handling
If any phase (including the initial scene load) fails, all later phases are halted, `BootstrapState` becomes `Failed`, `StartupPhase` becomes `Failed`, and the failure propagates to the caller. Startup never continues past a failing prerequisite.

### Bootstrap Error Semantics (Task 46)
`GameBootstrap` owns the single, authoritative bootstrap error boundary. Its minimal, immutable error model is deliberately NOT a framework: there is no global error bus, no `ErrorManager`/`BootstrapEventBus` Singleton, and no per-frame reporting.

**Error categories** ΓÇö a small fixed taxonomy that exists to answer diagnosis / reporting / "may startup continue?":

| Category | Meaning | Fatal? |
|----------|---------|--------|
| `Configuration` | Illegal bootstrap configuration / infrastructure failure | Fatal |
| `Service` | Service registration, lookup, dependency, or initialization failure | Fatal |
| `SceneLoading` | Initial scene load / scene transition failure | Fatal |
| `Unexpected` | Any failure not attributable to the narrow categories above | Fatal |

The category and source `System` are derived **deterministically from the failing pipeline phase** (not by sniffing exception types), so classification never masks the lower-level cause.

**`BootstrapError`** is an immutable snapshot of the single failure: `Category`, `Phase`, `System`, and `Exception`. The **original exception is preserved by reference** (type, message, and stack via the exception itself / `InnerException`); detailed ServiceRegistry / ServiceInitializer / SceneLoader / SceneTransitionSystem exceptions are **not** replaced with generic strings.

**Fatal vs recoverable**: every currently required startup failure is FATAL. There are **no recoverable bootstrap paths in the current project**; "recoverable" is not a synonym for "log and continue". Recoverable handling is reserved for future optional systems and is not implemented or claimed here.

**Continuation policy**: a fatal failure stops startup ΓÇö no later phases run, no `Ready`, no `StartupComplete`, no scene load if a prerequisite failed. There is **no automatic retry and no automatic reset**; `Failed` is terminal for that bootstrap lifecycle (restart would be an explicit new lifecycle operation, and none is added here).

**Reporting boundary**: reporting occurs **exactly once, at the bootstrap boundary**, when the failure is captured. The `Configure(...)` hook accepts an injectable `Action<BootstrapError>` reporter seam (default no-op, so production emits no duplicate logs); the Composition Root may wire it to a logger. The original exception is also **rethrown** (`throw;`) so the propagation path is preserved and failures are never swallowed.

**Exposed API (read-only)**: `CurrentError`, `HasFailed`, `FailureCategory` (read-through to `CurrentError`). There are no public setter/escape hatches (`SetError`, `ClearError`, `ForceReady`, `ForceRecover`) ΓÇö the bootstrap owns its own error state.

**No silent failures**: every bootstrap `catch` either propagates the exception and/or moves to `Failed` with stored context. There is no `catch { /* nothing */ }` and no log-then-pretend-success.

**Relationship with Tasks 47ΓÇô48**: Missing-Service (Task 47) and Duplicate-Service (Task 48) diagnostics are built on top of ServiceRegistry / ServiceInitializer failures while GameBootstrap continues to classify them as `Service` category, preserving the detailed exceptions. Task 46 itself implemented only the generic bootstrap error boundary.

### Policy
- **No per-frame polling**: GameBootstrap has no `Update`/`FixedUpdate`/`LateUpdate`; startup is call-driven.
- **No hidden Awake/Start ordering**: other systems are invoked explicitly at their prerequisite boundary, never via reliance on arbitrary Awake/Start sequence.
- **Awake is used only as the physical entry point** to trigger the explicit sequence.

### Initial Scene
The source of truth for the loadable scene set is the **Build Settings** (via SceneLoader). The initial scene path is supplied by the Composition Root configuration ΓÇö it is **not hardcoded** in GameBootstrap. Project note: the Football scenes are not yet in the Build Settings (only `SampleScene` is), so no real football scene can currently load at runtime until that build-configuration gap is closed.

## Service Initialization Order

`ServiceInitializer` is a pure C# coordinator in `Football.Core` that owns the **deterministic, dependency-ordered initialization AND shutdown** of registered services (a paired lifecycle coordinator). It does NOT absorb creation, storage, per-service business logic, or a global Service Locator. It computes ordering only once, at startup ΓÇö never per frame.

### Ownership and Scope
- The Composition Root constructs a `ServiceInitializer` over an explicit service-type collection and an explicit dependency map, and calls `Initialize(registry)` from the service-initialization phase of startup (the `_initializeServices` seam).
- It performs **no reflection-based discovery**, is **not a Singleton**, and asks services nothing about their dependencies ΓÇö dependencies are declared explicitly by type at the coordinator boundary.
- The registry stays a pure store: `ServiceRegistry.Get(Type)` is lookup-only and never constructs, initializes, or shuts down services.
- `ServiceInitializer` owns both initialization and shutdown ordering because shutdown is the exact inverse of initialization over the SAME dependency graph and resolved order ΓÇö pairing them avoids a second, independently authored ordering algorithm.

### Behavior (Initialization)
- **Registration gate**: all declared services are resolved (registered) from the registry BEFORE any service initializes; a missing registration aborts the whole run before initialization begins.
- **Deterministic order**: a topological sort of the explicit dependency graph, using declaration order as the tie-break for independent services ΓÇö the same declaration produces the same order every time and across repeated runs. Exposed via `ResolvedOrder`.
- **Exactly-once**: a service already `IsInitialized` is skipped on a repeated run; services are never re-initialized.
- **Fail-fast**: a service that throws aborts the remaining initialization. The failure propagates as `InvalidOperationException` with identifying context and the original exception as inner. The failed service is not reported initialized, and dependent services are never reached. No rollback occurs.
- **Cycle policy**: a circular dependency is rejected (`InvalidOperationException`) before any service initializes ΓÇö a valid ordering is a prerequisite, computed prior to running.

### Behavior (Shutdown) ΓÇö `Shutdown(registry)`
- **Reverse dependency order**: services are shut down in `reverse(ResolvedOrder)`. If A depends on B, then B initializes before A and A shuts down before B. Same graph, opposite direction.
- **Initialized-subset rule**: only services whose `IsInitialized` is true are shut down. Registered-but-never-initialized services and services that failed initialization are skipped. `Registered` does not imply `Initialized`; `Initialized` does not imply a second shutdown.
- **Exactly-once**: after a successful `Shutdown`, a service is no longer marked initialized, so a repeated shutdown is a safe no-op ΓÇö each service is shut down at most once per lifecycle.
- **Partial startup**: after a failed initialization the coordinator safely tears down whatever subset actually initialized; it is not a transactional rollback and does not run `Shutdown` on dependent services that never initialized.
- **Failure policy (safe teardown)**: shutdown continues across a failing service to release independent resources, all failures are collected, and a single `InvalidOperationException` is thrown with an `AggregateException` (or the single original) as inner. Nothing is swallowed or hidden. The run remains deterministic.

### Policy
- Registry = storage/lookup; `ServiceInitializer` = lifecycle ordering (init + shutdown); services = business logic. Each concern stays separate.
- Ordering is call-driven (explicit `Initialize`/`Shutdown`) ΓÇö no `Awake`/`Start`/`Update`/`OnDestroy`/`OnDisable` dependence, no hidden Unity execution-order or scene-unload reliance. A future Composition Root may trigger the top-level shutdown request from a Unity lifecycle hook, but the service ordering itself is explicit.
- Shutdown never calls `GameEvents.Clear()`. Event subscription cleanup belongs to the owning service: the object that subscribes during `Initialize` unsubscribes that specific handler during `Shutdown`. `GameEvents.Clear()` is a high-level lifecycle operation only, never a per-service shortcut.

## Missing Service Diagnostics (Task 47)

When a REQUIRED service was never registered, the failure must be diagnosable rather than a generic "it broke". `ServiceRegistry` produces a structured, actionable exception instead of an opaque `InvalidOperationException`.

**`ServiceNotFoundException`** (in `Football.Core`, sealed) is raised when lookup of an absent service is attempted. It is deliberately a **subtype of `InvalidOperationException`** so the long-standing contract ΓÇö "lookup of an absent service throws `InvalidOperationException`" ΓÇö remains fully backward compatible, while callers needing the exact cause catch the specific type. It exposes:

| Member | Meaning | Task |
|--------|---------|------|
| `MissingServiceType` | Exactly which service type is missing (never null) | 47.3 |
| `RequestingSystem` | Which subsystem requested the missing service (`Unknown` when not supplied) | 47.2 |
| `Message` | Actionable: names the missing type, the requester, and the resolution steps | 47.4 |

**Detection** (`47.1`) lives in `ServiceRegistry`: `Get<T>()` / `Get(Type)` now throw `ServiceNotFoundException` when the type is absent, and `IsRegistered<T>()` / `IsRegistered(Type)` provide a non-throwing membership probe.

**Requesting-system identity** (`47.2`): `Get` accepts an optional `requestingSystem` hint. The `ServiceInitializer` registration gate passes `nameof(ServiceInitializer)`, so an aborted registration is attributed to the coordinator that drove it.

**Missing vs not-initialized** (`47.5`) are intentionally distinct signals and never conflated:
- **Missing** = never registered ΓåÆ `IsRegistered` is `false`; `Get` throws `ServiceNotFoundException`.
- **Registered but not initialized** = present in the registry, `IsInitialized == false` ΓåÆ `IsRegistered` is `true`; `Get` returns the instance; the lifecycle state is read from `IFootballService.IsInitialized`.

This keeps the registry a pure store: it distinguishes present/absent, while initialization state always comes from the service's own `IsInitialized`, never from the registry.

**Integration**: because `ServiceNotFoundException` is an `InvalidOperationException`, `GameBootstrap`'s Task 46 boundary already classifies a missing-service failure as `Service` category and preserves the full diagnostic as the original exception / inner exception ΓÇö no new bootstrap wiring was needed.

## Duplicate Service Diagnostics (Task 48)

Duplicate registration is a **configuration/composition error**. The FIRST registration is authoritative: the second registration MUST fail, and it must be diagnosed in an actionable, structured way rather than a bare "duplicate".

**`DuplicateServiceException`** (in `Football.Core`, sealed, subtype of `InvalidOperationException`) is raised by `ServiceRegistry.Register<T>` when a type is registered more than once. It is a sibling of `ServiceNotFoundException` (the missing case) ΓÇö the two are distinct and never conflated:

| Case | Exception |
|------|-----------|
| Missing service (never registered) | `ServiceNotFoundException` |
| Duplicate service (registered twice) | `DuplicateServiceException` |

It exposes:

| Property | Meaning | Subtask |
|----------|---------|---------|
| `DuplicateServiceType` | The registration key (service type) that collided; never null | 48.2 |
| `ExistingService` | The service already registered (authoritative); never null | 48.3 |
| `AttemptedService` | The service rejected as a duplicate; never null | 48.4 |

**Detection** (`48.1`): `Register<T>` checks for an existing entry BEFORE adding. On collision it throws `DuplicateServiceException` carrying the existing and attempted instances; the attempted instance is **never stored**.

**Actionable diagnostic** (`48.5`): the message states WHAT collided (the type), WHO the existing registration is, that the attempted duplicate was rejected, and HOW to fix it (remove the duplicate `Register<T>` from the Composition Root). It does not claim origin/location that the system does not actually know (no stack traces, no reflection, no registration metadata capture).

**No silent replacement** (`48.6`): after a failed duplicate, `Get<T>` still returns the original (`existing`), never the attempted instance; the registry's storage grows by zero entries; the existing registration remains the sole instance. There is no "last registration wins" and no silent ignore.

**Composition Root ownership**: the registry does not construct, initialize, or shut down services, and it is not a factory. It remains instance-owned with no `Instance`/`GlobalServices`/Service Locator and no Singleton. `Register` never calls `Initialize`/`Shutdown`.

**Integration**: `ServiceInitializer` registration/init and shutdown are unchanged and continue to propagate the specific `DuplicateServiceException` (a duplicate aborts before initialization). `GameBootstrap`'s Task 46 boundary already classifies a duplicate as `Service` category and preserves the specific exception as `CurrentError.Exception`.

## Game Clock (Task 49)

`GameClock` (in `Football.Core`, sealed, pure C#) is a deterministic source of **FOOTBALL MATCH TIME** ΓÇö it answers "how much match time has elapsed". It is deliberately NOT Unity Time, Simulation Time, Real Time, a Match Manager, a Pause Manager, or a UI timer.

**Authoritative representation**: a single internal `double ElapsedSeconds` is the one source of truth for match time. It is read-only from outside the clock (setter is non-public). No minutes/seconds/display-string is stored as authoritative state; formatting is a presentation concern. Fractional seconds are preserved; comparisons are deterministic.

**State semantics**: minimal `GameClockState { Stopped, Running }`. There is **no** `Paused` state ΓÇö pause-aware time is owned by a later task, and a stopped clock is the sufficient foundation (a clock that is not advancing is simply Stopped). Half-time is a match-state concern, not a clock state; the clock simply stops.

**Start / Stop / Reset** (all deterministic, safe on repeated calls):
- `Start()`: Stopped ΓåÆ Running; no-op if already Running.
- `Stop()`: Running ΓåÆ Stopped; no-op if already Stopped.
- `Reset()`: sets `ElapsedSeconds = 0` and returns to Stopped, from any state; always restartable.

**Advancement ownership**: the clock advances ONLY via `Advance(deltaSeconds)` supplied by an owner. It never sources its own delta and has no `Update`/`FixedUpdate`/`LateUpdate`/coroutine/`InvokeRepeating`. It does not read `Time.deltaTime`. The simulation-time abstraction (a later task) decides where the delta comes from. Advancement:
- positive delta while Running ΓåÆ `ElapsedSeconds += delta`
- zero delta ΓåÆ no-op
- while Stopped ΓåÆ no-op
- negative delta ΓåÆ `ArgumentOutOfRangeException` (no silent backwards simulation)

**Match time vs real/simulation time**: GameClock is independent of wall-clock time (no `DateTime`/`DateTimeOffset`/`Stopwatch`), of Unity `Time` (`Time.time`, `Time.deltaTime`, `Time.realtimeSinceStartup`), and of `Time.timeScale`. None are match authority.

**Regulation duration & half/stoppage compatibility**: the clock carries `RegulationDurationSeconds` as **injected read-only configuration** (sourced by the owner from match rules, e.g. `MatchRulesDefinition.HalfDurationSeconds x NumHalves`), NOT clock state ΓÇö it does not auto-stop or decide match-end. Higher-level Match logic interprets `elapsed >= regulation`. Half-time and stoppage-time do NOT require a second clock: a single `GameClock` can start, advance to a boundary, stop, and resume; future stoppage policy layers over `ElapsedSeconds`.

**Ownership**: the Match/Composition Root owns one `GameClock` per match lifecycle. It is not a Singleton (no `Instance`), not a Service Locator, and is not created by UI/player/camera or per-system. It has no Bootstrap/ServiceRegistry/SceneLoader/SceneTransitionSystem dependency and no gameplay-system dependence.

**Audit note**: the Runtime contains **no pre-existing clock** and **no `Time.*`/`DateTime`/`Stopwatch` usage**; existing code passes caller-provided `deltaTime` through `IState.Tick`/`IStateMachine.Tick` (simulation plumbing, not match time). `GameStateId` already includes `Playing`, `HalfTime`, `FullTime`, `Pause`; `MatchRulesDefinition` already holds `HalfDurationSeconds`/`NumHalves`.

## Simulation Time Abstraction (Task 50)

### Decision: intentionally deferred (no runtime abstraction added)
The audit (50.1) proved there is **no current system that needs a dedicated simulation-time contract**:
- The Runtime contains **zero direct `UnityEngine.Time` calls** (`Time.deltaTime`, `Time.fixedDeltaTime`, `Time.timeScale`, `Time.realtimeSinceStartup`, etc.) in any assembly.
- Every existing simulation consumer already receives **explicit injected delta** via `IState.Tick(float deltaTime)` / `IStateMachine.Tick(float deltaTime)` ΓÇö the pure-simulation isolation this abstraction is meant to provide is **already the established design**.
- There is **no simulation loop**, no fixed-step simulation, and no variable-step consumer that would consume a `SimulationTimeStep`.

Per the task rule ("do not create an empty interface / do not implement until a useful consumer exists"), **no `ISimulationTime`, `SimulationTimeStep`, `SimulationClock`, or `TimeProvider` type was added**. Adding them now would be speculative dead code. The policy and boundary are defined here; the runtime contract (`ISimulationTime { double DeltaSeconds; double FixedDeltaSeconds; }` or a `SimulationTimeStep` value type) is reserved until a real consumer exists.

### The four time concepts are distinct and not interchangeable
- **Real time**: wall-clock/device time ΓÇö never used as gameplay/match authority.
- **Unity frame time**: `Time.deltaTime` ΓÇö engine frame interval.
- **Unity fixed time**: `Time.fixedDeltaTime` ΓÇö physics/step interval.
- **Simulation time**: the delta/step supplied to simulation systems.
- **Game clock (Task 49)**: accumulated football match elapsed time.

### Time source ownership
The timing boundary: **Unity/application timing ΓåÆ time source/adapter ΓåÆ simulation systems**. A pure simulation consumer **reads no `UnityEngine.Time`**; it receives an explicit delta. The source (scaled `Time.deltaTime`, fixed `Time.fixedDeltaTime`, or anything else) is chosen by the **timing owner**, never by individual gameplay systems. `GameClock` stays independent ΓÇö it is advanced explicitly (`GameClock.Advance(delta)`) and must NOT be made to auto-advance from a simulation-time provider.

### Delta-time semantics (the intended contract for a future step type)
- `DeltaSeconds` = elapsed since the previous simulation update; units = **seconds**; **non-negative**; externally supplied; does NOT represent absolute game-clock time; read-only from a consumer's perspective.
- **Zero delta**: no advance, no error.
- **Negative delta**: rejected (`ArgumentOutOfRangeException`) ΓÇö the project has no reverse simulation/replay/rollback.
- **Large delta**: a "large but valid step" is NOT silently clamped; invalid input is distinguished from a large-but-valid step. No hidden stall-masking.

### Fixed-step boundary
Frame delta (elapsed between frame updates) and fixed delta (elapsed between fixed simulation steps) are **not automatically interchangeable**: `Time.deltaTime` must not substitute for `Time.fixedDeltaTime` and vice versa. Task 50 defines only this boundary. The actual fixed timestep policy (Task 52) and Update/FixedUpdate ownership (Task 53) are NOT implemented here.

### Pause boundary
Actual pause-aware time behavior is **owned by Task 51**. Task 50 only establishes that a simulation step MAY be zero (pause ΓçÆ delta becomes 0 for simulation, while unscaled/UI time may continue). No `PauseManager`, `PauseState`, or `Time.timeScale = 0` was added.

### Time-scale boundary
`Time.timeScale` is an **engine timing mechanism, NOT gameplay/pause/match-state authority**. The audit found no runtime code uses `timeScale` as state. If a Unity-side adapter is later added, it must choose scaled vs unscaled explicitly (scaled = affected by timeScale; unscaled = not); the pure gameplay interface is not polluted with presentation timing.

### Determinism boundary
"Deterministic" here means: **same initial state + same inputs + same delta sequence ΓçÆ same resulting state**, with explicit delta input, no hidden `Time` reads in pure simulation logic, no wall-clock dependency, and reproducible tests. Full network determinism, rollback, lockstep, and bit-perfect cross-platform behaviour are **explicitly NOT promised** and require future architecture. No replay/network/rollback machinery was added.

### No duplicate GameClock
Simulation Time (the current step) is a different concept from GameClock (accumulated match time). No `SimulationClock` is created, and `GameClock` is not auto-advanced from simulation time. A future Match system may explicitly coordinate `SimulationStep ΓåÆ GameClock.Advance(delta)` if appropriate.

## Pause-Aware Time (Task 51)

### Decision: policy-only (pause is a GAME STATE / GAMEPLAY AUTHORITY, not an engine clock)
The audit (51.x) found **no runtime gameplay simulation systems, no UI implementation, and no pause-aware input manager** beyond the existing `IPlayerInput.Pause` flag and the sparse `GameStateId.Pause` enum member. Per the task rules ("keep Task 51 primarily an architecture/policy task with test-only validation using existing contracts", "do not create a giant PauseManager unless a real runtime owner/consumer requires it"), Task 51 is **INTENTIONALLY POLICY-ONLY**. No `PauseManager`, `PauseState`, `PauseController`, `PauseSystem`, `PauseStartedEvent`, or `PauseEndedEvent` was created. A pause event may be introduced later only when UI/audio/analytics has a real consumer.

### 51.1 Pause ownership ΓÇö ONE source of truth
- **Authority:** `GameStateId.Pause` (a single enum member in `Football.Core`) is the authoritative gameplay pause state, as part of the Match/Game state owner. It is **NOT** duplicated by a second independently-mutable `PauseState`.
- **Consumers observe, they do not decide:** timing/simulation consumers read pause and react; they do not each keep their own `IsPaused`.
- **Ownership direction:**
  ```
  Match/Game state owner (GameStateId.Pause)
        Γåô observed by
  timing / simulation owner
        Γåô
  GameClock.Stop() / simulation delta 0
  ```
- **NOT owned by:** UI/menu, `Time.timeScale`, or any per-system flag.

### 51.2 Gameplay pause semantics
While paused: player gameplay simulation stops, ball simulation stops, match-time progression stops, gameplay timers stop, gameplay actions are not advanced. Allowed to continue: pause/menu responsiveness, and explicitly-allowed presentation. **Implementation:** player/ball/match systems do not exist yet, so NONE were modified ΓÇö the policy is defined and validated with a test-only `IState` consumer. Pause is enforced by the timing/state owner (supplying delta 0 / stopping the clock), **NOT** by adding `if (paused) return;` to every future gameplay system.

### GameClock relationship
`GameClock` is **MATCH TIME**, not a pause manager, and is NOT modified. It already supports the exact pause boundary via its existing API:
- **Playing** ΓåÆ `GameClock.Start()` (running / advancing)
- **Paused** ΓåÆ `GameClock.Stop()` (stopped / not advanced)
- **Resumed** ΓåÆ `GameClock.Start()` (running again)

The **Match/state owner** (a higher-level state owner) calls `Start()`/`Stop()`; GameClock does **not** discover pause itself and has **no `IsPaused`** (verified: `GameClock` is unchanged from Task 49).

### 51.3 UI pause semantics
- **Gameplay time:** stopped. **Pause UI:** continues. **Menu input:** continues. **Gameplay input:** blocked/ignored. **Presentation:** may continue only if explicitly appropriate.
- **No UI implementation** was created (no pause menu, canvas, resume button, prefab, HUD, or animation). Actual UI pause behavior is **policy-defined only; real UI implementation is future work** (documented here as policy).
- **Unscaled time:** a Unity presentation system, when it exists, may use `Time.unscaledDeltaTime`. Unscaled time is **NOT** added to the pure gameplay simulation contract by default, and **no direct `Time` usage was introduced into Core**.

### 51.4 Simulation behavior while paused ΓÇö delta 0, no catch-up
- Paused ΓçÆ simulation receives **delta 0** via the existing `IState.Tick(0f)` seam. Resume ΓçÆ normal simulation delta resumes.
- **No new `SimulationTime` abstraction** (Task 50 remains intentionally deferred). No global delta mutation, no `if (paused) return;` added to consumers ΓÇö the timing/state owner enforces pause.
- **Catch-up prevention (critical):** a 10s (or 30s) wall-clock pause is **NEVER** fed into simulation as a single `delta = 10.0`/`30.0` step. GameClock stops advancing during pause, and on resume simulation advances from the **next valid step**. Explicitly tested.

### 51.5 Input behavior while paused
- **Pause/resume input:** available (`IPlayerInput.Pause`, sampled from `Escape` by `HumanPlayerInput`).
- **Menu navigation:** available. **Gameplay movement/action input:** blocked/ignored.
- **Stale input / no buffering:** gameplay movement or Shoot/Pass/Tackle held or pressed while paused must **NOT** auto-execute on resume merely because it was held/buffered. **No input buffering is introduced.**
- **Implementation:** no full input-state machine. `HumanPlayerInput` gates gameplay sampling on `IsEnabled` (an explicit ownership/enable seam) and does **not** depend on a global pause singleton. Pause/enable gating is a documented ownership contract, not global mutable state. If a real pause-aware input manager is later needed, it must use the same explicit state/ownership check.

### 51.6 Time progression & `Time.timeScale` policy
- **Zero runtime `Time.*` usage** exists; the audit confirms nothing reads `Time.deltaTime`, `Time.unscaledDeltaTime`, `Time.fixedDeltaTime`, `Time.time`, `Time.realtimeSinceStartup`, or `Time.timeScale` in any Runtime assembly.
- **Option A selected:** pause state is authoritative and **`Time.timeScale` is NOT used** (not implemented). `timeScale`, if ever used later at the Unity boundary, is only an **engine consequence/mechanism**, never the source of truth. Because there is zero current integration, no timeScale abstraction or adapter with no consumer was added.
- **No per-frame polling:** pause is explicit state; no `GameClock.Update()`, `PauseManager.Update()`, or `if (IsPaused)` every frame. The only existing Unity lifecycle callback in the Runtime is `HumanPlayerInput.Update()` (input sampling), which is unchanged.

### 51.7 Resume behavior
On Resume: simulation advances from the **next valid step**; **no** missed-time replay, wall-clock catch-up, replayed held actions, or duplicated events. GameClock continues from its previous elapsed value (e.g. `Elapsed=120.0` before a 30s pause ΓçÆ `Elapsed` stays `120.0`, then `Advance(0.016)` ΓçÆ `120.016`, **not** `150.016`). Pause/Resume does **not** reset GameClock; repeated Pause and repeated Resume are deterministic no-ops.

### Threading
Unity main-thread gameplay is assumed. **No** locks, threads, async pause workers, or concurrent state were introduced.

### Pause representation
Pause belongs to the existing `GameStateId` enum (`Football.Core`). `GameStateId` is currently sparse but is documented here as the intended **gameplay/game-state authority**; no duplicate `PauseState` enum was created.

### Pause vs fixed step / lifecycle boundaries
Pause policy is owned solely by Task 51. **Task 52 (Fixed Timestep Policy) and Task 53 (Update/FixedUpdate ownership) are NOT implemented here.** Pause does not change the fixed timestep; it only prevents advancement.

## Fixed Timestep Policy (Task 52)

### Decision: policy-only (no current production physics consumer)
The audit (52.1) found **no implemented physics gameplay system in the Runtime**:
- The Runtime contains **zero actual physics API usage** ΓÇö no `Rigidbody`/`Rigidbody2D` writes, `AddForce`/`AddTorque`, `MovePosition`/`MoveRotation`, `Time.fixedDeltaTime`, `Physics.*`, `FixedUpdate`, or `LateUpdate` in any Runtime `.cs`. The only matches are doc-comments in `GameClock.cs` and `SceneTransitionSystem.cs`.
- Two physics **components** exist on prefabs but are **not driven by any Runtime code** (audit classification B): `SoccerBall.prefab` has a `Rigidbody`, `Player.prefab` has a `CharacterController`. A component existing on a prefab is NOT an implemented physics system.
- `Ball/`, `World/`, `Actions/` are assembly placeholders; `Players/` holds only `PlayerStateId.cs`.

Per the task rules, Task 52 is therefore **INTENTIONALLY POLICY-ONLY**. It **does NOT** create `BallController`, `PlayerMovement`, `PlayerPhysics`, `MatchSimulation`, or any `PhysicsManager`/`PhysicsClock`/`PhysicsScheduler`, **does NOT** run manual `Physics.Simulate`, and **does NOT** revive the deferred `SimulationTime` abstraction. Actual physics code is future work; only the timing/ownership boundary is defined here.

### Current Unity physics configuration (audited, unchanged)
- `ProjectSettings/TimeManager.asset`: **Fixed Timestep = 0.02 s**, Maximum Allowed Timestep = `0.33333334`, `m_TimeScale = 1`.
- `ProjectSettings/DynamicsManager.asset`: **`m_AutoSimulation = 1`** (standard auto-sim, no manual `Physics.Simulate`), gravity `(0, -9.81, 0)`, default solver iterations `6`.
- These values are the engine defaults and are **acceptable** for the foundation; they are **documented, not changed** (no physics consumer exists to justify an early change).

### 52.2 FixedUpdate ownership
- **FixedUpdate** owns: physics-driven movement, Rigidbody state changes, `AddForce`/`AddTorque`, `MovePosition`/`MoveRotation`, explicit velocity changes, physics collision-driven simulation, physics synchronization.
- **Update** owns: input sampling, non-physics frame logic, user commands, high-level frame decisions.
- **LateUpdate** owns: presentation/camera/final visual synchronization.
- No `FixedUpdate`/`LateUpdate` was added to any system to "prove" this policy. The full Update/FixedUpdate/LateUpdate framework is **Task 53** and is NOT implemented here.

### 52.3 Physics timestep source
- The authoritative physics timestep is the **Unity-engine fixed step (`Time.fixedDeltaTime`)**, currently **0.02 s**.
- **Each system must NOT read it independently** into gameplay logic once a simulation abstraction exists (Task 50 boundary). For now it is simply the engine step; no runtime code reads `Time.fixedDeltaTime`.
- **Configuration ownership is centralized in ProjectSettings.** Gameplay systems MUST NOT modify `Time.fixedDeltaTime` dynamically. If future runtime adaptation is ever required it must be a centralized explicit system; **adaptive timestep is NOT implemented now**.

### 52.4 Physics vs gameplay timing ΓÇö distinct clocks
| Concern | Source |
|---------|--------|
| Frame gameplay | `Update` / explicit frame delta |
| Physics | `FixedUpdate` / fixed delta (engine step 0.02 s) |
| Match clock | `GameClock.Advance(explicit delta)` |
| Simulation step | deferred `IState.Tick(float)` injected delta |

These interact but are **not the same clock**. `GameClock` is **not** a physics clock; `FixedUpdate` is **not** match-time authority; `Time.fixedDeltaTime` is **not** match time. `GameClock` advances only via explicit `Advance(delta)` and never reads the fixed delta.

### 52.5 Rigidbody interaction timing
- **Writes** (`AddForce`, `AddTorque`, `MovePosition`, `MoveRotation`, explicit velocity changes) belong in **FixedUpdate** or another explicit physics-simulation boundary ΓÇö **never `Update()`** (no Rigidbody write from Update unless a proven Unity integration reason exists).
- **Reads** used for physics decisions preferably occur in the physics step; reads for presentation may be context-dependent.
- **Interpolation**: use Rigidbody interpolation for presentation rather than manually moving in Update; do **not** configure interpolation blindly ΓÇö decide per-object when real physics exists.
- No Rigidbody is written by any current code.
- **Ball future compatibility:** future `BallController` intent flows `input/intent ΓåÆ gameplay decision ΓåÆ physics command ΓåÆ FixedUpdate ΓåÆ Rigidbody`. **Not implemented now.**
- **Player future compatibility:** the policy allows the future player system to choose `CharacterController` (which may remain frame-driven when chosen) or `Rigidbody`; neither is forced, and no speculative physics movement was added.

### 52.6 Frame-rate independence
The ultimate property: **same physics inputs + same fixed-step sequence ΓçÆ same physics progression**. Cross-platform bit-perfect determinism is explicitly NOT promised (out of scope).
- **Incorrect patterns to avoid**: `rigidbody.position += speed*Time.deltaTime` in Update, `rigidbody.AddForce(...)` in Update, `physicsStep = frameDelta` substituting frame delta for fixed delta.
- **Multiple fixed steps per frame:** one rendered frame may contain 0, 1, or 2+ `FixedUpdate` calls. Frames are NOT physics steps; do not store "last frame's physics delta" as a substitute for the fixed delta. One Update does not equal one FixedUpdate.

### 52.7 Configuration policy & pause/timeScale relationships
- Fixed timestep **owner**: ProjectSettings (centralized). Configured in `TimeManager.asset` (`Fixed Timestep`). **Runtime override: NO.** Gameplay override: **NO.**
- **Pause does not change the fixed timestep**: pause is `GameStateId.Pause` authority (Task 51) that stops advancement; it **never** sets `fixedDeltaTime = 0`. Actual physics pausing will follow the eventual simulation-ownership policy ΓÇö documented, not implemented (no physics consumer).
- **timeScale does not configure the fixed timestep**: the two are separate concepts; nothing couples them, and `Time.timeScale` is not used (Task 51, Option A).
- **No manual simulation:** auto-simulation is ON; standard Unity physics remains standard. `Physics.Simulate` must not be added as a custom game loop, and no `PhysicsManager`/`PhysicsClock`/`PhysicsScheduler` exists.

### Boundaries
Task 52 owns only physics/timestep rules. **Task 53 (Update/FixedUpdate/LateUpdate responsibilities, input/movement/camera/animation timing) is NOT implemented here.** Pause (Task 51) and Simulation Time (Task 50) boundaries are respected; the deferred `SimulationTime` abstraction is not revived.


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

## Update / FixedUpdate Ownership Rules (Task 53)

### Decision: policy-only (automated lifecycle validation)
The audit (53.x) confirms the **only runtime Unity lifecycle callbacks** are:
- `GameBootstrap.Awake()` ΓÇö bootstrap entry (composition root, not gameplay timing)
- `HumanPlayerInput.Update()` ΓÇö the **only `Update()`** in the Runtime, and it is **input sampling** (the intended, allowed permanent Update consumer)
- `FootballDebugSettings.OnEnable()/OnDisable()` ΓÇö debug asset observer, not gameplay timing

There is **no `FixedUpdate`, no `LateUpdate`**, and **no camera, animation, movement, or ball-physics runtime system** anywhere in the Runtime (only `Data/*Config` ScriptableObjects ΓÇö configuration, not live components). Task 53 is therefore **INTENTIONALLY POLICY-ONLY**: it establishes the authoritative Update/FixedUpdate/LateUpdate ownership rules and locks them with **automated structural validation** (`LifecycleOwnershipTests`). No speculative camera/animation/movement/ball systems were created, and no lifecycle refactor was performed because the single `Update()` usage is legitimate. Because `Football.Core` has an empty asmdef (no UnityEngine references), Core types **structurally cannot** define `Update`/`FixedUpdate`/`LateUpdate` or touch Rigidbody/`Time` ΓÇö a compile-time guarantee, not merely a convention.

### 53.1 ΓÇö Update responsibilities
- Input sampling (`HumanPlayerInput.Update` ΓÇö the only current `Update()` and it is correct)
- Non-physics frame-driven logic
- User commands and high-level frame decisions
- Per-frame UI/presentation that is not transform-synchronization
- **NOT** physics writes, match-clock authority, or simulation stepping.

### 53.2 ΓÇö FixedUpdate responsibilities
- Physics-driven movement, Rigidbody writes (`AddForce`/`AddTorque`/`MovePosition`/`MoveRotation`/explicit velocity), collision-driven simulation, physics state synchronization
- Represents one fixed physics step (Task 52, engine step 0.02 s); a frame may invoke 0, 1, or 2+ fixed steps
- No current Runtime type owns `FixedUpdate` (policy only, per Task 52).

### 53.3 ΓÇö LateUpdate responsibilities
- Presentation/camera follow, final transform synchronization dependent on settled gameplay/physics state
- **Never** gameplay or physics authority
- No current Runtime type owns `LateUpdate` (policy only).

### 53.4 ΓÇö Input sampling timing
Gameplay input is sampled in `Update()` (HumanPlayerInput), independent of physics and presentation callbacks. Sampling/consumption semantics and pause gating (`IsEnabled`) are the **input owner's** responsibility via an explicit ownership/enable seam ΓÇö **not** a global pause singleton. No input state machine or buffering was introduced.

### 53.5 ΓÇö Movement timing
Future policy (no player movement system exists): movement timing is chosen by the player architecture ΓÇö `CharacterController` may be frame-driven in `Update()` when chosen; a Rigidbody-based player drives movement in `FixedUpdate()`. Neither is forced; no speculative physics movement added.

### 53.6 ΓÇö Ball physics timing
Future policy (no BallController exists): ball physics interactions flow `intent ΓåÆ gameplay decision ΓåÆ physics command ΓåÆ FixedUpdate ΓåÆ Rigidbody`. Rigidbody writes for the ball belong in `FixedUpdate`, never `Update`. Not implemented.

### 53.7 ΓÇö Camera timing
Future policy (no camera system exists): camera follow/presentation uses `LateUpdate()` so it consumes settled transforms; it must **never** determine gameplay position (anti-pattern). Not implemented.

### 53.8 ΓÇö Animation timing
Future policy (no animation system exists): animation is **driven by** gameplay/state (observer), never reverse-authoritative over gameplay physics or position. No gameplay logic inside Animator state machines (anti-pattern). Not implemented.

### 53.9 ΓÇö Detection: incorrect lifecycle usage (structural rules)
Because production Core is UnityEngine-free, the frame-dependent physics anti-pattern (`rigidbody.position += speed*Time.deltaTime` in `Update`, `Rigidbody` writes in `Update`, reading `Time.deltaTime` into fixed physics) is **structurally impossible in the pure layer**. Any future system that introduces lifecycle callbacks must declare its ownership explicitly.

### 53.10 ΓÇö Automated timing validation
`Assets/Football/Tests/EditMode/LifecycleOwnershipTests.cs` locks the lifecycle shape:
- Pure Core types (`GameClock`, `GenericStateMachine<>`) define **no** `Update`/`FixedUpdate`/`LateUpdate`.
- `HumanPlayerInput` is the sole `Update()` owner (input sampling) and exposes the `IsEnabled` gate.
- No Runtime type owns `FixedUpdate`/`LateUpdate`.
- No speculative movement/ball/camera/animation systems exist.
- A future task that violates the ownership rules fails these tests, forcing an explicit decision.

### Boundaries
Task 53 owns lifecycle/ownership rules only. **Task 52** owns the fixed timestep/physics timing; **Task 51** owns pause; **Task 50** owns the simulation-time boundary; **Task 49** owns GameClock. These are respected; nothing from them was re-implemented here.

## Football Debug Settings (Task 54)

### Role ΓÇö observability, NOT authority
`FootballDebugSettings` (in `Football.Core/Debug`, a **ScriptableObject**) is **configuration for DEVELOPMENT/DEBUG observability** (logging enablement, diagnostics, visualization, future debug overlay). The governing rule:
- **Gameplay produces state; Debug observes state.**
- Debug flags MUST NOT gate gameplay behavior (forbidden: `if (settings.EnableMovement) player.Move()`); allowed: `if (settings.EnableMovementDiagnostics) debugDrawer.DrawMovement(...)`.
- Debug MUST NOT modify authoritative game state.

Audit (54.1) verified: the type already exists as a ScriptableObject; the Runtime contains **zero `Debug.Log*` usage**; and **no gameplay assembly references `FootballDebugSettings`** (references are limited to its own file + tests). No debug asset has been created (it is optional authored configuration via `CreateAssetMenu`).

### 54.3 Master switch + disabled behaviour
- Master switch: **`DebugEnabled`** (bool), **default OFF (safe)**. All per-category and visualization flags also default **OFF**.
- When debug is disabled: **no unnecessary logging, no telemetry capture, no visualization work, no expensive diagnostics.** Disabled flags incur no runtime cost.
- Per-category switches remain independently toggleable (Task 55+ implement the category systems that consume them).

### 54.4 Development vs release (policy)
- **Development context**: `IsDevelopmentContext` (read-only bool) reports editor / development-build context via a single build-context directive (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`).
- **Release**: debug functionality is only *relevant* in development contexts; **it is NOT a security boundary and is NOT stripped**. Release builds retain the configuration object; categories are simply not observed.
- No build-pipeline infrastructure and no per-file directive spraying. This is documentation + one context expression, not an implementation of Tasks 55ΓÇô65.

### 54.5 Category configuration
Already present and **strongly typed** (public bools, not string-keyed / `Dictionary` / reflection): `EnableMovementDebug`, `EnableBallDebug`, `EnableAnimationDebug`, `EnableMatchDebug`, `EnableAIDebug`, `EnableCameraDebug`, `EnablePerformanceDebug` (the 7 Task 55ΓÇô61 categories) + visualization flags (`ShowPlayerIDs`, `ShowBallTrail`, `ShowFieldBounds`, `ShowPlayerStateLabels`). **No category systems, no category registry, and no enum were introduced** ΓÇö the bools are the clean, existing representation.

### 54.7 Singleton decision ΓÇö KEEP as DEBUG-ONLY technical exception
Evidence: the project's own architecture tests already codify the decision ΓÇö
- `ArchitectureDependencyTests.KnownSingletonFiles` lists `Runtime/Core/Debug/FootballDebugSettings.cs` as **the one tolerated Singleton** in Core.
- `CoreHasNoGameplaySingletons` fails on any *other* Core Singleton, and `SingletonDetector_DetectsFootballDebugSettingsSingleton` asserts the pattern must remain in this file.

**Decision: KEEP `FootballDebugSettings.Instance` (Option A)** as a temporary, **debug-only technical exception**.
- It is **not approved for gameplay / runtime service architecture**.
- Isolated to debug infrastructure; it does **not** control gameplay, create services, control scene loading, own match state/time/input, or store authoritative state.
- **No gameplay code may reference `Instance`.** The single purpose is to let debug infrastructure locate the authored debug configuration.
- No additional Singletons are introduced.

### 54.2 Ownership & ScriptableObject rules
- **Owner**: the authored configuration object (`FootballDebugSettings`) supplies configuration; the future debug category systems (Tasks 55ΓÇô65) consume it; gameplay is independent.
- The ScriptableObject is **configuration, not a mutable global runtime state container** ΓÇö it holds only bool flags. It does not store current player/ball/match/scene/match-time/possession or transient telemetry.
- **Runtime mutation policy**: flags may be toggled during development testing. Nothing alters authoritative game state, and no runtime-persistence system was added.

### Boundaries / file scope
`FootballDebugSettings` stays in `Football.Core/Debug` (moving it would break the architecture test `KnownSingletonFiles` path and the singleton-detector test). No `DebugLogger`/`LoggerService` (Task 62), no throttling (Task 63), no telemetry (Task 64), no overlay (Task 65), and no category systems (Tasks 55ΓÇô61) were implemented here.

## Movement Debug Category (Task 55)

### Audit result ΓÇö no production movement system exists
Re-verified during Task 55: the Runtime has **no movement controller/driver**, **no velocity/speed/acceleration/grounded source**, and **no runtime player state machine**. Only authored configuration ScriptableObjects exist under `Runtime/Data` (`MovementConfig`, `PlayerDefinition`, `DribbleConfig`, `BallConfig` ΓÇö data, not motion). `GenericStateMachine<PlayerStateId>` is never instantiated; `PlayerStateId` is an orphan enum. `IPlayerInput` is implemented (`HumanPlayerInput`/`AIPlayerInput`/`NetworkPlayerInput`/`ReplayPlayerInput`) but **no production code consumes it**. Existing movement debug surface: `FootballDebugSettings.EnableMovementDebug` (and `ShowPlayerStateLabels`) only.

### Decision ΓÇö POLICY-ONLY (Option A)
Because no real movement/runtime consumer exists, Task 55 **manufactures no runtime movement debug code, no fake velocity/state/input data, and no `MovementSnapshot`**. It establishes the **diagnostic contract** (task 55.1/55.2/55.3) and the **observational-only boundary**, and is validated by focused architecture tests. Runtime movement diagnostics are **DEFERRED** until a real production movement controller exists.

### Responsibility (observer, never authority)
Movement gameplay is authoritative; Movement Debug observes. Data ownership contract (all debug access **read-only** when the authoring owner exists):

| Diagnostic data | Owner | Debug access |
|---|---|---|
| Player movement state (`PlayerStateId` etc.) | future player state machine | read-only |
| Velocity / speed / acceleration / grounded | future movement/physics system | read-only |
| `MoveDirection` / `LookDirection` / `Sprint` | input system (`IPlayerInput`) | read-only (observe produced data; never poll directly) |
| Movement configuration | authored config (`MovementConfig`) | read-only |

**Debug must NOT** move the player, change velocity/state, inject input, alter acceleration/speed/rules, enable/disable mechanics, or become movement authority. No debug cheats (freeze/teleport/speed multiplier/noclip/gravity toggle/invulnerability/override), no debug input provider, no static/global movement state, no per-frame diagnostic loops.

### Enablement
Reuses the existing `EnableMovementDebug` (no duplicate flag). Activation requires **both** `DebugEnabled` (master) **and** `EnableMovementDebug` (category) to be ON; `DebugEnabled=false` gates the category regardless of the category flag. Disabled path: no logging, no visualization work, no telemetry, no per-frame allocations. Gameplay must not behave differently based on these flags.

### Boundary discipline (what Task 55 does NOT do)
- **No category systems** beyond Movement (Tasks 56ΓÇô61 deferred).
- **No telemetry** (Task 64): no recorder/history/sampling/retention/replay.
- **No overlay/UI** (Task 65): no Canvas/TextMeshPro/IMGUI/HUD.
- **No gizmos** ΓÇö drawing arrows/velocity from invented values is forbidden with no movement system.
- **No gameplay dependency on Debug**: gameplay assemblies never reference `FootballDebugSettings`; Input/Players remain debug-independent.
- **Not implemented**: Movement debug is POLICY + VALIDATION now; runtime movement diagnostics are FUTURE integration once a movement system exists.

## Ball Debug Category (Task 56)

### Audit result ΓÇö no production Ball system exists
Re-verified during Task 56: Ball/Actions/World/AI/Camera/Teams/UI asmdef folders are **empty** (compile nothing). No `BallController`/Ball runtime script exists ΓÇö only authored data (`BallConfig`) and event structs (`BallKickedEvent`, `PossessionChangedEvent`, neither a driver nor a debug consumer). `SoccerBall.prefab` carries SphereCollider + Rigidbody (non-kinematic, gravity on, mass 0.43) but **no `m_Script` component** ΓÇö nothing drives the Rigidbody. A Rigidbody is **not** a Ball physics system. No velocity/angular-velocity/speed source, no possession system (only the `PossessionChangedEvent` struct and `TeamDefinition.PossessionPreference` authored data), no `PlayerBallInteraction`, no collision/contact system. Existing Ball debug surface: `FootballDebugSettings.EnableBallDebug` + `ShowBallTrail` (pure configuration; control nothing).

### Decision ΓÇö POLICY-ONLY (Option A)
Because no real Ball/possession/physics consumer exists, Task 56 **manufactures no Ball runtime, no possession system, no fake velocity/physics provider, no cheats, no debug input, no telemetry, no overlay, and no logging framework**. It establishes the **diagnostic contract** (56.1/56.2/56.3) and the **observational-only boundary**, validated by architecture tests. Runtime Ball diagnostics are **DEFERRED**.

### Responsibility (observer, never authority)
Ball gameplay is authoritative; Ball Debug observes. Data ownership (all debug access **read-only** when the authoring owner exists):

| Diagnostic data | Owner | Debug access |
|---|---|---|
| Ball state / position / velocity / speed / direction | future Ball physics system | read-only |
| Rigidbody state / angular velocity / contact | Unity physics + future Ball system | read-only |
| Possession owner / possession state / transitions | future possession authority | read-only |
| Kick/pass/shoot / control / interaction state | future ball-action authority | read-only |

**Debug must NOT** change Rigidbody velocity/angular velocity/mass/drag/gravity/constraints; no `AddForce`/`AddTorque`/`MovePosition`/`MoveRotation`; no assigning/clearing possession; no triggering kick/pass/shoot/tackle; no changing ball state, physics config, or match score.

### Possession / physics (56.2)
**Possession diagnostics: DEFERRED** ΓÇö `PossessionChangedEvent` defines the message shape but there is no possessive authority to observe. No `PossessionManager`/`PlayerBallInteraction`/`BallOwnerDebug` was invented.
**Physics diagnostics: DEFERRED** ΓÇö no production Rigidbody-driving code exists; no fake provider created.

### Allowed visualization (56.3, all FUTURE)
Velocity vector, trajectory line, possession marker, contact point, collision normal, ball bounds, Rigidbody center of mass. **Not implemented**: `ShowBallTrail` remains configuration-only (no trail system in Task 56). No UI/overlay (Task 65), no trajectory buffering, no telemetry (Task 64).

### Enablement
Reuses `EnableBallDebug` (no duplicate flag). Activation requires **both** `DebugEnabled` (master) **and** `EnableBallDebug` (category) ON; `DebugEnabled=false` gates the category regardless of the category flag. Disabled path: no logging, no visualization work, no telemetry, no per-frame allocations. Gameplay must not behave differently based on these flags.

### Boundary discipline
- **No Ball systems** (Ball gameplay, possession, interaction are gameplay tasks, not Debug Task 56).
- **No telemetry** (Task 64), **no overlay/UI** (Task 65), **no logging framework** (Task 62), **no throttling** (Task 63).
- **No gameplay dependency on Debug**: Input/Players never reference `FootballDebugSettings`.
- **Not implemented**: Ball debug is now POLICY + VALIDATION; runtime Ball diagnostics are FUTURE integration once a Ball system exists.

## Animation Debug Category (Task 57)

### Audit result ΓÇö no production animation system exists
Re-verified during Task 57: no `Runtime/Animation` folder; **zero** Runtime code references the Animator/animation APIs (no `Play`/`CrossFade`/`SetBool`/`SetFloat`/`SetTrigger`/`GetCurrentAnimatorStateInfo`/`Playables`). Animation asset folders (`Controllers`/`Processed`/`Source`) are **empty** (only `.meta` files); no `.controller`/`.anim`/`.fbx` exist anywhere in the project. `Player.prefab` carries **only CharacterController ΓÇö no Animator component**. No runtime animation driver, no code-driven Animator parameters, no gameplay-to-animation bridge, no Animation Events. Only authored configuration exists: `AnimationConfig` (blend times, `Football.Data` ScriptableObject). Existing Animation debug surface: `FootballDebugSettings.EnableAnimationDebug` (category) + `ShowPlayerStateLabels` (visualization), both pure configuration.

### Decision ΓÇö POLICY-ONLY (Option A)
Because no Animator/animation system exists, Task 57 **manufactures no animation runtime, controller, bridge, snapshot, monitor, debug input, telemetry, overlay, logging, or throttling**. It establishes the **diagnostic contract** and the **presentation/authority boundary**, validated by architecture tests. Runtime Animation diagnostics are **DEFERRED**.

### Responsibility & anatomy of ownership
- **Animation is PRESENTATION. Gameplay State is AUTHORITY. Debug observes both.** Never allow the Animator (or an Animation Event/clip) to decide authoritative gameplay state.
- Animator parameters are **not** gameplay truth (e.g. an Animator "Speed" parameter presents the gameplay `PlayerState`, it does not define it). Debug must never derive or gate gameplay from Animator parameters.
- Data ownership (all debug access **read-only** when the authoring owner exists):

| Diagnostic data | Owner | Debug access |
|---|---|---|
| Gameplay state (`PlayerState`) | gameplay/state system | read-only |
| Animator state / layer / normalized time | Animator / presentation | read-only |
| Animator parameters (bool/float/int) | animation driver / Animator | read-only |
| Transition / blend state | Animator / presentation | read-only |

### State / parameter visibility (57.2)
When a real Animator exists, Debug may **read** current state, layer, normalized time, parameters, and transition state. It must **never write**: `SetBool`/`SetFloat`/`SetInteger`/`SetTrigger`/`ResetTrigger`/`Play`/`CrossFade` (Task 57 is observation only). Status: **DEFERRED** for all of the above (state/parameter/layer/normalized-time) until an Animator exists.

### Transition diagnostics (57.3)
When a real Animator exists, Debug may **read** current state, next state, transition status, transition normalized time, and transition layer. It must **not** initiate/interrupt transitions, change transition duration, or force a clip/state. Status: **DEFERRED** ΓÇö no `AnimationTransitionManager`/`AnimationDebugTransition` invented.

### Authority protection (57.4)
- Animator controls gameplay: **NO** (nothing to control; no bridge).
- Debug mutates Animator: **NO**. Debug sets parameters: **NO**. Debug forces transitions: **NO**.
- Animation Events mutate gameplay: **N/A** (none exist; none invented).
- No debug animation control/cheats (force idle/run/sprint/kick, speed override, freeze/skip, force clip/transition).
- No second state machine / no duplicate of `PlayerStateId`.

### Enablement
Reuses `EnableAnimationDebug` (no duplicate flag). Activation requires **both** `DebugEnabled` (master) **and** `EnableAnimationDebug` (category) ON; `DebugEnabled=false` gates the category. `ShowPlayerStateLabels` remains the state-label visualization flag (no `ShowAnimationState`/`AnimationLabels` duplicate). Disabled path: no logging, no visualization work, no telemetry, no per-frame allocations. Gameplay must not behave differently based on these flags.

### Boundaries
- **No animation gameplay systems** (presentation/gameplay animation are gameplay tasks, not Debug Task 57).
- **No telemetry** (Task 64), **no overlay/UI** (Task 65), **no logging framework** (Task 62), **no throttling** (Task 63).
- **No gameplay dependency on Debug**: Input/Players never reference `FootballDebugSettings`.
- **Not implemented**: Animation debug is now POLICY + VALIDATION; runtime Animation diagnostics are FUTURE integration once an Animator/animation system exists.

## Match Debug Category (Task 58)

### Audit result ΓÇö no production Match system exists
Re-verified during Task 58: `Runtime/Match` contains only `GameStateId.cs` (an **orphan enum** in namespace `Football.Core`, assembled into `Football.Match`) with no production consumer beyond its own declaration. No `MatchManager`/`Controller`/`System`/`Runtime`, no match score system, no match clock owner. `GameClock` (`Football.Core`) is a real, tested class (Task 49) but is **never instantiated or driven by production** ΓÇö no `new GameClock`, no `Start`/`Stop`/`Reset`/`Advance` anywhere in Runtime (class implemented, runtime integration deferred). Match events (`MatchStartedEvent`, `MatchEndedEvent`, `GoalScoredEvent`, `PossessionChangedEvent`, `BallKickedEvent`, `PlayerActionStarted/FinishedEvent`) are `readonly struct` contracts; `GameEvents` is a static subscribe/raise bus with **no production publishers and no subscribers** ΓÇö contracts, not proof of a runtime event flow. Existing Match debug surface: `FootballDebugSettings.EnableMatchDebug` (pure configuration).

### Decision ΓÇö POLICY-ONLY (Option A)
No Match runtime exists, so Task 58 **manufactures no Match runtime, state machine, score system, second clock, event monitor, second event bus, cheats, debug input, telemetry, overlay, or logging**. It establishes the diagnostic contract, ownership/authority boundary, and event observation policy, validated by architecture tests. Runtime Match diagnostics are **DEFERRED**.

### Responsibility & ownership (58.1/58.2)
Match gameplay is authoritative; Match Debug observes. Data ownership (all access **read-only / observe** when the owner exists; owners below marked FUTURE):

| Diagnostic data | Authoritative owner | Debug access |
|---|---|---|
| Match state / phase (`GameStateId`) | FUTURE Match state machine | read-only |
| Match clock (elapsed, half, stoppage) | FUTURE Match owner wiring `GameClock` | read-only |
| Score (home/away, changes) | FUTURE Match score system | read-only |
| Match rules / duration | Match configuration (`MatchRulesDefinition` future) | read-only |
| Match events (type/timestamp/payload) | event publisher (`GameEvents`) | observe, never republish |

- **GameStateId**: architectural contract / planned Match state representation; **not** a runtime authority yet. Do not claim "Match Debug reads current GameStateId" ΓÇö no production value exists.
- **GameClock**: class IMPLEMENTED, RUNTIME INTEGRATION DEFERRED. **Never create a second clock; never instantiate GameClock from debug.**
- **Score**: no production source; score diagnostics **DEFERRED** until authoritative score ownership exists. No `ScoreManager`/`MatchScore`/`DebugHomeScore`/`DebugAwayScore`.

### Event observation (58.3)
Event structs are contracts, not runtime proof. **No production publisher/subscriber exists.** Event diagnostics: **POLICY/DEFERRED** ΓÇö no `MatchEventMonitor`, no second event bus (`GameEvents` is the single bus), no event history buffers, no republishing/mutation. A future observer may read type/timestamp/payload of an individual occurrence only.

### Authority protection (58.4)
Regardless of category enablement, Match Debug must **never** set `GameStateId`, call `ChangeState`, call `GameClock.Start/Stop/Reset`, alter elapsed time, change score, raise `GoalScoredEvent`/`MatchStartedEvent`/`MatchEndedEvent`, alter `MatchRulesDefinition`, or start/end half/match. No match controls/cheats (force goal/edit score/skip time/force kickoff/extra-time/restart/end match/set clock/teleport phases).

### Enablement
Reuses `EnableMatchDebug` (no duplicate flag). Activation requires **both** `DebugEnabled` (master) **and** `EnableMatchDebug` (category) ON. Disabled path: no logging, no visualization work, no telemetry, no per-frame allocations. These flags never influence authoritative Match behavior.

### Rules / configuration
`MatchRulesDefinition` (or similar authored config) is observed as configuration, never modified from Debug; configuration is distinguished from runtime state.

### Boundaries
- **No Match systems** (gameplay Match/score/clock are gameplay tasks, not Debug Task 58).
- **No telemetry** (Task 64), **no overlay/HUD** (Task 65), **no logging framework** (Task 62), **no throttling** (Task 63).
- **No gameplay dependency on Debug**: Input/Players/Match never reference `FootballDebugSettings`.
- **Not implemented**: Match debug is POLICY + VALIDATION; runtime Match diagnostics are FUTURE integration once a Match system exists.

## AI Debug Category (Task 59)

### Audit result ΓÇö no production AI system exists
Re-verified during Task 59: `Runtime/AI` (Football.AI asmdef) is **EMPTY** (no scripts; the AI assembly is not compiled). No AI controller, decision system, target selector, AI state machine, perception, or navigation. The only `Target`/`Decision` tokens in Runtime are scene-transition variables in `SceneLoader`/`SceneTransitionSystem` ΓÇö **not AI**. `AIPlayerInput` (`Football.Input`) is a plain `IPlayerInput` input-abstraction MonoBehaviour with settable properties and **no production consumer** (like Human/Network/Replay inputs; no `IPlayerInput` consumer exists) ΓÇö an input source, **not** proof of AI gameplay. No AI configuration data. Existing AI debug surface: `FootballDebugSettings.EnableAIDebug` (pure configuration).

### Decision ΓÇö POLICY-ONLY (Option A)
No AI runtime exists, so Task 59 **manufactures no AI controller/brain/decision system/target selector/state machine/perception/navigation, no debug AI input, no cheats, no telemetry, no overlay, no logging**. It establishes the diagnostic contract, ownership, anti-mutation rules, and visualization policy, validated by architecture tests. Runtime AI diagnostics are **DEFERRED**.

### Responsibility & ownership
AI gameplay is authoritative; AI Debug observes. Data ownership (all access **read-only** when the owner exists; owners below FUTURE):

| Diagnostic data | Authoritative owner | Debug access |
|---|---|---|
| Current decision / reason / confidence / duration | FUTURE AI decision system | read-only |
| Target / position / type / priority / distance | FUTURE target selector | read-only |
| AI state / previous / transition / duration | FUTURE AI state machine | read-only |
| Perception / sensed / awareness / range | FUTURE perception system | read-only |
| Intent (desired destination/direction/action) | FUTURE AI behaviour | read-only |
| Navigation / path / destination | FUTURE navigation system | read-only |

- **Decision/visibility** (59.2): observe decision category, reason, scores/confidence, timestamp; NEVER force/rewrite decisions, skip evaluation, alter priority, inject fake scores. Observe selected target, position, type, reason, distance, priority; NEVER assign/clear/reorder targets or alter priorities.
- **State diagnostics** (59.3): observe current/previous state, transition, duration; NEVER `ChangeState`/force/reset/pause. `PlayerStateId` Γëá AI state ΓÇö player gameplay state vs decision/behaviour state stay distinct.
- `AIPlayerInput` remains an **input abstraction**, never an AI controller/decision system/debug interface/global state. Debug neither polls nor mutates it.

### Visualization policy (59.4)
FUTURE (target line, destination line, decision marker, perception radius, threat indicator, path/waypoints, selected-target highlight, desired direction) ΓÇö only from real authoritative data; **nothing generated from invented AI values**. No gizmos added solely to prove the category exists. No Canvas/TextMeshPro/HUD/overlay/GUI window (Task 65 owns Debug Overlay).

### Authority protection (59.5)
Debug must **never** change decisions/targets/target priority/AI state, call state transitions, modify perception/navigation/destination/movement intent, inject AI input, or force pass/shoot/tackle/sprint. No AI cheats (force target/pass/shoot/tackle/sprint/behavior, freeze AI, disable perception, reveal all, instant/infinite decision time, teleport, path override). No second decision system, no static/global AI debug state, no second AI authority.

### Enablement
Reuses `EnableAIDebug` (no duplicate flag). Activation requires **both** `DebugEnabled` (master) **and** `EnableAIDebug` (category) ON. Disabled path: no logging, no visualization work, no telemetry, no per-frame allocations. These flags never influence authoritative AI behavior.

### Boundaries
- **No AI gameplay** (decision/target/state/perception/navigation are gameplay tasks, not Debug Task 59).
- **No telemetry** (Task 64), **no overlay/UI** (Task 65), **no logging** (Task 62), **no throttling** (Task 63).
- **No gameplay dependency on Debug**: Input/Players/Match never reference `FootballDebugSettings`.
- **Not implemented**: AI debug is POLICY + VALIDATION; runtime AI diagnostics are FUTURE integration once an AI system exists.

## Camera Debug Category (Task 60)

### Audit result ΓÇö no production camera system exists
Re-verified during Task 60: `Runtime/Camera` (Football.Camera asmdef) contains only the asmdef + `.meta` ΓÇö no scripts; the assembly is not compiled. **Zero** Runtime code references Camera/Cinemachine/CameraController/Follow/LookAt/`fieldOfView`/`Camera.main` (the only "Camera" token in Runtime is the `EnableCameraDebug` flag). **Zero** `void LateUpdate` / `void FixedUpdate` anywhere in Runtime (Task 53's LateUpdate is future presentation policy only; not implemented). No Cinemachine package referenced. The scene files each contain one **raw default Unity Camera** with no controller/Cinemachine and no runtime driver ΓÇö a Camera component is **not** a camera system. Prefabs carry no Camera/AudioListener/Cinemachine components (all `m_TagString: Untagged`). Existing Camera debug surface: `FootballDebugSettings.EnableCameraDebug` (pure configuration).

### Decision ΓÇö POLICY-ONLY (Option A)
No camera system exists, so Task 60 **manufactures no camera system/controller/rig/manager/state machine/target system, no free/fly camera or cheats, no debug input, no telemetry, no overlay, no logging**. It establishes the diagnostic contract, ownership, and anti-control rules, validated by architecture tests. Runtime Camera diagnostics are **DEFERRED**.

### Responsibility & ownership
Camera Debug observes camera presentation; it is **never camera authority**. `ShowPlayerStateLabels` belongs to state-label visualization (movement/presentation-adjacent, currently unused), not Camera Debug. Data ownership (all access **read-only** when the owner exists; owners below FUTURE):

| Diagnostic data | Authoritative owner | Debug access |
|---|---|---|
| Camera state / active camera / mode / transition | FUTURE camera state system | read-only |
| Transform / FOV / projection / clipping | FUTURE camera system | read-only |
| Follow target / look-at target / offset | FUTURE camera target/follow system | read-only |
| Camera timing / LateUpdate owner | FUTURE presentation owner (Task 53) | read-only |

- **Target/follow (60.2)**: Debug never assigns/clears target, changes offset/follow speed/damping/look-at target, or chooses Player/Ball. Target selection belongs to the camera/presentation owner.
- **Camera state (60.3)**: Debug never changes state, switches mode, restarts transitions, or forces an active camera.
- **Transforms (60.1/60.3)**: Debug may READ position/rotation/FOV/projection when a camera exists; never WRITE `transform.position`, `transform.rotation`, `fieldOfView`, `orthographicSize`, clip planes, or culling mask.

### Control prevention (60.4)
Debug must **never** move/rotate camera, change FOV/orthographic size/clipping/culling, select camera/target, modify offset/damping, trigger transitions, or override camera state. No debug camera tools (free/fly camera, teleport, lock, force FOV, zoom cheat, shake override, cinematic control, noclip). Debug never becomes gameplay authority; it must not determine player/ball position, possession, score, match/movement/AI state.

### Enablement
Reuses `EnableCameraDebug` (no duplicate flag). Activation requires **both** `DebugEnabled` (master) **and** `EnableCameraDebug` (category) ON. Disabled path: no logging, no visualization work, no telemetry, no per-frame allocations. These flags never influence authoritative camera or gameplay behavior.

### LateUpdate relationship
Task 53 declares LateUpdate as the future camera/presentation timing owner. Task 60 adds **no** `LateUpdate` loop for diagnostics; camera diagnostics will observe the camera after its presentation owner is implemented.

### Boundaries
- **No camera gameplay/presentation systems** (camera system/controller/follow/target are presentation tasks, not Debug Task 60). No Cinemachine added.
- **No telemetry** (Task 64), **no overlay/UI** (Task 65), **no logging** (Task 62), **no throttling** (Task 63).
- **No gameplay dependency on Debug**: Input/Players/Match never reference `FootballDebugSettings`.
- **Not implemented**: Camera debug is POLICY + VALIDATION; runtime Camera diagnostics are FUTURE integration once a camera system exists.

## Performance Debug Category (Task 61)

### Audit result ΓÇö no performance system, no consumer
Re-verified during Task 61: **zero** Runtime references to Profiler/`UnityEngine.Profiling`/`ProfilerRecorder`/`Recorder`/`CustomSampler`/`ProfilerMarker`/`BeginSample`/`EndSample`/`FrameTimingManager`/`GC.*`/`GetTotalMemory`/`CollectionCount`/`GetAllocatedBytes*`/`Stopwatch`/`Metrics`/`Telemetry`/`Snapshot`/`PerformanceMonitor`/`PerformanceDebug`/`FPS`/`FrameTime`. The only "Performance" token in Runtime is the `EnablePerformanceDebug` flag definition. **Zero** `Time.*` usage in Runtime (GameClock is the time abstraction) and **no** `Physics.Simulate`. Lifecycle: only `HumanPlayerInput.Update()` ΓÇö zero `void FixedUpdate` / `void LateUpdate`. **No performance consumers, no telemetry, no overlay, no persistent metrics**.

### Decision ΓÇö POLICY-ONLY (Option A)
No runtime performance consumer and no metering framework exist, so Task 61 **manufactures no production performance monitor, no metric sampler, no frame/fixed-step/GC instrumentation, no PerfManager Singleton, no profiler framework, no telemetry, no logging, no throttling, no overlay**. It establishes the diagnostic contract, metric ownership, and anti-control rules, validated by architecture tests. Runtime performance measurement is **DEFERRED**.

### Responsibility & ownership
Performance Debug is diagnostic observation; it is **never the performance/critical path itself**. Data ownership (all access **read-only** when the owner exists; owners below FUTURE):

| Metric | Authoritative owner | Debug access |
|---|---|---|
| Frame delta time / frame rate | Unity frame timing boundary | read-only |
| Frame spike / percentile | reading frame timing | read-only |
| Fixed-step count / duration | Unity physics timing boundary (Task 52) | read-only |
| Fixed-step spikes | reading fixed timing | read-only |
| GC collection count | runtime/managed environment | read-only |
| Managed allocation / memory | runtime/.NET | read-only |
| Custom sample durations | explicitly-instrumented system | read-only |

- **Frame timing (61.2)**: Debug is **not** a time provider. `GameClock` (Task 49) and the frame timing boundary own time; Debug may **READ** delta/frame rate when a consumer exists ΓÇö it never becomes FPS/time authority, never builds a frame-history provider.
- **Fixed step (61.3)**: Debug may eventually **observe** fixed-step count/duration, but **never owns** them and never changes `Time.fixedDeltaTime`, ProjectSettings, `Physics.autoSimulation`, or calls `Physics.Simulate`. Fixed-step counts are actual FixedUpdate invocations (0/1/2+ per rendered frame) ΓÇö never assumed 1:1. No measurement while no production FixedUpdate exists (no fake steps). DEFERRED until Task 52/53 have a real consumer.
- **Allocation/GC (61.4)**: Debug must not manufacture allocation tracking. Distinguishes Allocation vs GC Collection vs Managed Memory (not interchangeable). If later needed, uses `GC.GetAllocatedBytesForCurrentThread`/`CollectionCount`/`GetTotalMemory` safely; no custom allocator tracker, no heap scan, **no allocations merely to measure allocations**. DEFERRED (no consumer).

### Control prevention (61.5)
Debug must **never** change frame-rate behavior, simulation speed, physics timestep, `Time.timeScale`, pause/unpause, `GameClock`, AI/player behavior, or camera. It may **READ** debug config and timing config but never **modify** authoritative config at runtime. **No self-inflected profiling**: when disabled there are **no** per-frame metric buffers/strings/history/dictionaries/profiler samples, and **no** per-frame `PerformanceDebug.Update()` poll loop.

### Anti-scope boundaries
- **Not Unity Profiler recreated**: built-in Unity Profiler already answers generic performance requirements; no `ProfilerRecorder`/`CustomSampler`/`ProfilerMarker` simply because they exist. No generic `IMetricProvider`/`IMetricsRegistry`/`IPerformanceRecorder`.
- **Not telemetry** (Task 64 owns snapshots/history/retention/storage). **Not logging** (Task 62). **Not throttling** (Task 63). **Not overlay** (Task 65).
- **No gameplay dependency on Debug**: Input/Players/Match never reference `FootballDebugSettings`.

### Enablement
Reuses `EnablePerformanceDebug` (no duplicate flag). Activation requires **both** `DebugEnabled` (master) **and** `EnablePerformanceDebug` (category) ON. Disabled path: no instrumentation, no per-frame allocations, no polling. Development-oriented; centralized gating via `FootballDebugSettings` (no `#if`/compiler-directive scattering across gameplay code). Documentation does not claim release stripping unless a stripping mechanism is actually introduced (currently **not** ΓÇö instrumentation is simply absent/deferred).

### Boundaries
- **No runtime instrumentation** while no consumer exists; metric ownership is defined above; actual measurement DEFERRED.
- **No telemetry** (Task 64), **no overlay/UI** (Task 65), **no logging** (Task 62), **no throttling** (Task 63).
- **No gameplay dependency on Debug**: Input/Players/Match never reference `FootballDebugSettings`.
- **Not implemented**: Performance debug is POLICY + VALIDATION; runtime performance metrics are FUTURE integration once a performance consumer exists.

## Logging Levels (Task 62)

### Audit result ΓÇö no logging system, no runtime consumer
Re-verified during Task 62: the **only** real `Debug.Log` calls are in `Editor/FootballSetup.cs` (an editor-only setup/verification tool: 4 `Debug.Log` + 1 `Debug.LogError`). **Zero** production Runtime log calls, zero test log calls, zero Editor Runtime logs. **No** central/global logger, **no** `LogLevel`/`LogCategory`, **no** `ILogger`/`ILogHandler`/`LoggerFactory`. Logging categories exist only as the 7 `Enable*Debug` bools on `FootballDebugSettings`; there are **no** separate `Enable*Logging` flags. Structured error reporting (`BootstrapError` T46; `ServiceNotFoundException`/`DuplicateServiceException` T47/48) remains authoritative. `IsDevelopmentContext` (T54) is the single build-context directive; no compile-time stripping implemented. **No** hot-path/per-frame/per-fixed-step logging; `GameEvents` is an event bus, not a logging bus.

### Decision ΓÇö POLICY-ONLY (Option C)
There is **no current runtime logging consumer**, so Task 62 **manufactures no `LogLevel` type, no Logger abstraction, no central/global logger, no logging Singleton, no logging Service Locator, no logging event bus, no telemetry, no overlay, no throttling, no per-frame logging infrastructure**. It defines the logging contract and anti-abuse rules, validated by tests. A runtime logger is **DEFERRED** until a logging consumer exists.

### 62.1 Log levels (conceptual policy, not runtime types)
| Level | Meaning | Typical usage | Forbidden usage |
|---|---|---|---|
| ERROR | Failure/exceptional condition requiring attention; system could not fulfil its responsibility | service init failure, impossible config, unrecoverable failure, invalid required dependency | normal state transitions, expected input, expected transitions, routine failed optional lookup (if failure is normal) |
| WARNING | Unexpected/degraded but survivable | fallback behavior, optional config missing, deprecated usage, recoverable condition | spam every frame for a persistent condition |
| INFO | Important intentional lifecycle/config info | bootstrap completed, service initialized, scene transition completed | every Update / Tick / input sample / physics step |
| DEBUG | Detailed active-investigation diagnostics, easily disabled | state-transition detail, target selection, detailed timing/diagnostic values | none |

Semantics are **documented**, not encoded in an unused enum; no `LogLevel`/`LogSeverity` runtime type exists. Do **not** choose a level on emotional severity. No TRACE/VERBOSE (no requirement).

### 62.2 Category filtering (reuses FootballDebugSettings, no global state)
Filter order: **Master `DebugEnabled` ΓåÆ category `Enable*Debug` ΓåÆ level threshold**. There is **no** minimum-level filter config type (no consumer requires it). Category filtering reuses the existing 7 `Enable*Debug` flags ΓÇö **no** `Enable*Logging` duplicates, **no** `LoggerSettings.Instance`/`GlobalLogConfig`. Logging never requires `GlobalLogger.Instance`.

### 62.3 Development / release policy (documented, not compile-stripped)
- **Editor / Development Build**: all levels available per configuration (`IsDevelopmentContext` true). ERROR/WARNING/INFO/DEBUG per semantics.
- **Release Build**: `IsDevelopmentContext` false; policy is to keep ERROR/WARNING where the architecture needs them and treat INFO/DEBUG as development-focused. **No compile-time stripping is implemented or claimed**; instrumentation simply does not exist in production paths. No `#if UNITY_EDITOR`/`#if DEVELOPMENT_BUILD` scattering across gameplay; build-context gating is centralized in `FootballDebugSettings.IsDevelopmentContext`.

### 62.4 Exception vs logging (preserve structured errors)
Logging = observability; Exceptions = programmatic failure. **Logging never replaces exceptions**: `BootstrapError` (T46) remains the structured bootstrap error path; `ServiceNotFoundException`/`DuplicateServiceException` (T47/48) remain thrown exceptions ΓÇö missing/duplicate services still fail by contract, never swallowed by a log. `GameEvents` is **not** a logging bus (no `LogEvent`). Errors are not used as a second event bus.

### 62.5 Uncontrolled logging prevention
No per-frame / per-fixed-step / per-input / hot-path (Update/FixedUpdate/Tick) logging exists in production. No repeated-warning/error loops, no logs in tight loops exist because **no Runtime logging consumer exists**. Disabled path: logs are not emitted and no logging allocations are introduced (no per-frame string building, no throttling). String formatting/concat/boxing overhead is deferred until a consumer exists (avoidable only where it matters: disabled path / hot path / high frequency). **Throttling / rate limits / time windows / duplicate suppression / per-message cooldowns are Task 63 ΓÇö NOT implemented here.** No log history/persistence/uploads/snapshots (Task 64). No on-screen log console/overlay (Task 65).

### Boundaries
- **Not a second event bus**, not telemetry, not overlay, not throttling, not a profiler.
- **No central/global logger, no Singleton, no Service Locator** for logging; gameplay must not depend on logging.
- **Not implemented**: Logging is POLICY + VALIDATION; a runtime logging system is FUTURE integration once a logging consumer exists.

## Throttled Logging (Task 63)

### Audit result ΓÇö no runtime logger, no throttle, no consumer
Re-verified during Task 63: **zero** production Runtime `Debug.Log*` calls (only the 5 editor-only calls in `FootballSetup.cs` remain). **Zero** Runtime references to Throttle/RateLimit/Cooldown/LogLimiter/Deduplicator/LogGate/Logger. **Zero** `Time.*`/`Stopwatch`/`DateTime` usage in Runtime ΓÇö there is no timing infrastructure to (mis)appropriate; GameClock (match time) is **not** used for logging. The only production lifecycle callback is `HumanPlayerInput.Update()` with **no logs**; zero FixedUpdate/LateUpdate. So **no per-frame / per-fixed-step / hot-path log spam exists** and **no runtime logger exists to throttle**.

### Decision ΓÇö POLICY-ONLY
With **no runtime logger and no throttle consumer**, Task 63 **manufactures no ThrottleManager / LogThrottleManager / Logger / GlobalLogger / LogLimiter / deduplicator, no throttle operational state, no time source, no Singleton, no Service Locator, no per-frame loop, no telemetry, no overlay, no profiler**. It defines the throttling contract, validated by tests. A runtime throttle is **DEFERRED** until a runtime logger/consumer exists.

### 63.1 Ownership
Throttling is a **logging control policy** owned by the **FUTURE logging layer** ΓÇö never gameplay, never GameClock, never Performance Debug, never GameBootstrap, never a service. With no logger, ownership is documented as future logging-layer responsibility; no infrastructure is created.

### 63.2 Per-message / Category policy
- Throttling affects whether a log is **emitted**, never whether the underlying error/exception **occurs**.
- **Message identity (future)**: prefer a stable identity (`category + message key` or explicit id) over the final formatted string ΓÇö "Speed = 5.1" vs "Speed = 5.2" are the same diagnostic. No hash-based message system is built without a consumer.
- **Category (future)**: per-message + per-category may coexist, but avoid broad category suppression that hides unrelated independent warnings (WARNING category must not suppress unrelated warnings).
- **Severity interaction**: ERROR generally not aggressively throttled (critical errors remain observable); WARNING/INFO throttled when repetitive; DEBUG throttled aggressively. No throttle policy may alter exception behavior.

### 63.3 Time source policy
A future throttle must use **unscaled real time** (`Time.unscaledDeltaTime` / `Time.realtimeSinceStartup`, or a diagnostic `Stopwatch` for wall-clock intervals) so throttling still functions during paused/scaled simulation. It must **NOT** use `GameClock` (match time) or simulation time: logging diagnostics must never depend on match state. **No time provider is added** while no logger exists; zero `Time.*`/`Stopwatch` in Core today.

### 63.4 Anti-spam
No per-frame / per-fixed-step / per-input / Tick / tight-loop logging exists today, so there is **no log spam to throttle** ("No current per-frame/per-fixed-step log spam exists"). No aggressive global suppression is introduced. Planned future behavior for same-warning/debug/error-every-frame, multi-instance messages, and different values in one diagnostic is defined per-message (above); critical errors stay observable.

### 63.5 Disabled path & error preservation
- With no throttle, the disabled path performs **zero** throttle work ΓÇö no allocation, no dictionary/hash, no timestamp lookup, no string formatting, no Stopwatch (nothing to claim as "measured zero"; there is simply no code).
- **Logger/infrastructure is never manufactured just to obtain a throttle.**
- **Structured errors are preserved untouched**: `BootstrapError` (T46), `ServiceNotFoundException`/`DuplicateServiceException` (T47/48) remain thrown per their contracts ΓÇö throttling must not turn a missing/duplicate service into "logged once and ignored".

### Boundaries
- **Not telemetry / history / retention / upload / persistence** (Task 64).
- **Not overlay / on-screen throttle monitor / log panel** (Task 65).
- **Not a profiler** (Task 61), **not events** (no `LogThrottledEvent`), **not a Service Locator**, **not a Singleton**, **no Update/FixedUpdate/LateUpdate loops** for throttling.
- **Distinct from Task 62 log levels**: throttling controls output frequency; log levels control which level may be emitted.
- **Not implemented**: Throttling is POLICY + VALIDATION; a runtime throttle is FUTURE integration once a runtime logger/consumer exists.

## Telemetry Snapshots (Task 64)

### Audit result ΓÇö no telemetry runtime, no consumer
Re-verified during Task 64: **zero** Runtime references to Telemetry/Snapshot/RingBuffer/CircularBuffer/Recorder/Archive/Persistence/Serialize/Deserialize/Analytics/WebRequest/UnityWebRequest/Upload/Replay/Capture/History. (`ReplayPlayerInput` is an *input source* ΓÇö input handling, **not** a replay recorder.) **No** snapshot model, storage, retention, or capture scheduler exists. `FootballDebugSettings` has **no** `EnableTelemetry`/`CaptureFrequency`/`Retention`/`MaxSnapshots` fields (the only "telemetry" token is a tooltip string). **No** telemetry asmdef.

### Decision ΓÇö POLICY-ONLY
With **no real telemetry consumer**, Task 64 **manufactures no TelemetryManager / TelemetrySystem / TelemetryService / TelemetryRecorder / TelemetryBuffer, no snapshot model, no storage, no retention, no capture scheduler, no network/upload, no replay, no event bus, no Singleton, no Service Locator, no per-frame loop**. It defines the snapshot contract, validated by tests. A telemetry snapshot model is **DEFERRED** until a consumer exists.

### 64.1 Responsibility
Telemetry **observes** authoritative runtime state at a point in time; it **never owns or mutates** it. A snapshot is **observational data** ΓÇö not authoritative state, not a command/action/input/decision. `Authoritative Runtime State = current truth`; `Telemetry Snapshot = copied observation at time T`. Snapshot changes never change runtime state; runtime changes never retroactively mutate old snapshots.

### 64.2 Data model (deferred)
When a consumer appears, the smallest practical model: **metadata** (unscaled-real timestamp, frame/step id, source/category), plus optional **timing / match / player / ball / AI / camera** observations **only from real authoritative owners**. No "universal game snapshot" holding every future system. Timestamp semantics are explicit (never conflate simulation time, GameClock match time, unscaled real time, frame index). GameClock may be **copied** (`ElapsedSeconds`) but never retained as mutable authority; since GameClock is not wired into production, match-time telemetry is **DEFERRED**. **Live object references are avoided** (Player/Ball/Match/AI/Camera/MonoBehaviour/Transform/Rigidbody); prefer value copies / stable IDs. Snapshots must survive scene transitions/destroyed objects.

### 64.3 Immutability (deferred)
Snapshots are immutable after creation ΓÇö readonly struct/fields, defensive value copies. A readonly reference to a mutable `List<>`/`Dictionary<>` is **not** immutable; collections are copied or exposed immutably. Do not over-engineer for scalar-only snapshots.

### 64.4 Capture frequency (policy/deferred)
Foundation: **ON-DEMAND** capture. **No** per-frame / per-fixed-step capture by default (expensive). Future periodic capture must be explicit and measurable; fixed-step telemetry only on actual fixed steps (none exist); no GameClock as a fixed-step scheduler; no event-driven machinery without a consumer.

### 64.5 Retention (deferred)
Foundation: **NO runtime retention** while there is no consumer (no speculative history buffer). If introduced later: bounded memory ΓÇö declared max count or time window, **never** unbounded `List<Snapshot>` for long matches. Retention ownership = the telemetry layer, never gameplay (`Player.telemetryHistory` etc.).

### 64.6 Mutation protection (mandatory policy)
Telemetry **must not** modify Player/Ball/Match/score/possession/AI decision/AI target/camera/animation/input/GameClock/simulation time/`Time.timeScale`/`fixedDeltaTime`. **No commands/`Action`/delegate/callback** inside snapshots. Snapshots do **not** execute behavior. Gameplay must function identically with telemetry disabled (no `if (!telemetry) player.Disable()`). **No** telemetry Singleton, **no** Service Locator dependency. **No** network/HTTP/upload/persistence; **no** replay (snapshots are not replay frames); **no** second event bus; **not** logging (T62); **not** throttling (T63); **not** a profiler (T61). Telemetry is **independent** of Logging/Throttling/Overlay (T65).

### Boundaries
- **No speculative telemetry system** ΓÇö no TelemetryManager/System/Service/Recorder/Buffer, no snapshot model, no retention, no scheduler, no upload, no replay, no event bus, no Singleton, no Service Locator, no Update/FixedUpdate/LateUpdate loop.
- Structured errors (`BootstrapError`, service exceptions) are unaffected.
- **Not implemented**: Telemetry is POLICY + VALIDATION; a telemetry snapshot model is FUTURE integration once a real consumer exists.

## Debug Overlay Foundation (Task 65)

### Audit result ΓÇö no overlay, no presentation runtime, no data providers
Re-verified during Task 65: **zero** Runtime references to Overlay/DebugUI/DebugPanel/DebugWindow/DebugHUD/DebugCanvas/DebugMenu/DeveloperConsole/Canvas/CanvasGroup/OnGUI/GUILayout/TextMeshPro/TMP_/UIDocument/VisualElement ΓÇö no UI technology is used. `Runtime/UI` contains only `Football.UI.asmdef` (no scripts; **not compiled**); `Runtime/Camera` contains only `Football.Camera.asmdef` (likewise empty). **No debug overlay toggle**: existing input is gameplay-only (`HumanPlayerInput`: LeftShift/E/Space/Escape for sprint/tackle/interact/pause); no F1ΓÇôF5/Tab/BackQuote hotkey. All debug categories (55ΓÇô64) are **policy-only** ΓÇö there are **no debug data providers** to present and **no gameplay to observe**. No overlay prefab (Prefabs holds only SoccerBall/Player/Stadium).

### Decision ΓÇö POLICY-ONLY (Option C)
No production presentation/UI/Camera runtime and no debug data providers exist, so Task 65 **manufactures no DebugOverlay/DebugCanvas/DebugConsole component, no prefab, no toggle, no input action, no debug data-provider framework, no GameObject/Canvas, no OnGUI/Update loop, and no fake gameplay values**. It defines the overlay foundation contract, validated by tests. A runtime overlay is **DEFERRED** until a presentation runtime and real debug data providers exist.

### 65.1 Ownership (documented contract)
| Concern | Owner |
|---|---|
| Visual elements | FUTURE Debug Overlay presentation layer (UI assembly) |
| Debug configuration | `FootballDebugSettings` |
| Diagnostic data | FUTURE debug category/data providers (read-only) |
| Toggle intent | FUTURE dev/debug input owner |
| Gameplay truth | gameplay (authoritative) |

Overlay must **never** own GameState / Match score / GameClock / Player / Ball / AI / Camera / Animation / Physics state. **Lifetime**: not yet chosen ΓÇö scene-local vs on-demand, **no** `DontDestroyOnLoad`/persistent global UI without a reason.

### 65.2 Data presentation boundary
Overlay consumes **debug data**, not gameplay objects ΓÇö never reach into `PlayerController`/`GameManager` private mutable fields. Flow: gameplay ΓåÆ authoritative state ΓåÆ debug category/provider ΓåÆ read-only diagnostic data ΓåÆ overlay presentation. **No `UniversalDebugDataProvider`/`IDebugDataProvider<T>`/`DebugDataRegistry`** (no consumers). **No fake values**: forbidden to show Player Speed / Ball Velocity / AI Target / Match Score unless real runtime sources exist. With no providers, an overlay would show only visibility + enabled-category status + "no runtime diagnostic provider available".

### 65.3 Toggle / input ownership
A future toggle is owned by **developer/debug input**, not gameplay input. `HumanPlayerInput` (gameplay) is **not** repurposed to toggle the overlay. **No** overlay toggle/input action exists today and none may be added without a runtime overlay.

### 65.4 Category visibility
Overlay visibility would be driven by **`DebugEnabled` (master) + the 7 `Enable*Debug` category flags** ΓÇö no duplicate overlay-visibility flags (`ShowOverlay`/`OverlayVisible`) are added.

### 65.5 Not gameplay UI
Overlay is a developer presentation tool, **not** a gameplay HUD / match scoreboard / player-control UI / pause menu / inventory / production UI. Overlay never modifies gameplay (no pause/control/score/state/camera changes through it).

### 65.6 Core independence
Verified asmdef boundary: `Football.UI` ΓåÆ references `Football.Core`; **`Football.Core` ΓåÆ references nothing** (Core has zero UI/Camera/overlay dependency). Any future overlay lives in a presentation assembly referencing Core, **never** the reverse. Core must never host overlay/UI types.

### Boundaries
- **Not** telemetry (T64), **not** logging (T62), **not** throttling (T63), **not** a second debug architecture.
- **No** update/LateUpdate/OnGUI/Awake/Start runtime loop, **no** Singleton/Service Locator for the overlay, **no** overlay prefab while there is no runtime overlay.
- **Not implemented**: Overlay is POLICY + VALIDATION; a runtime Debug Overlay is FUTURE integration once a presentation runtime and real debug data providers exist.


























## Runtime vs Configuration State

### ScriptableObjects
ScriptableObjects under `Data/` are definitions, configuration, and authored data. They are **not** mutable global runtime state.

### Runtime State
Runtime state belongs to runtime objects, runtime services, and explicit simulation state. It is never stored in shared ScriptableObject assets at runtime.

### Existing Config Assets (validated clean)
- `PlayerDefinition` ΓÇö player identity + attributes (read-only at runtime)
- `TeamDefinition` ΓÇö team identity + roster + tactics (read-only at runtime)
- `BallConfig` ΓÇö ball physics parameters (read-only at runtime)
- `MovementConfig` ΓÇö movement parameters (read-only at runtime)
- `AnimationConfig` ΓÇö animation tuning (read-only at runtime)
- `DribbleConfig` ΓÇö dribble tuning (read-only at runtime)
- `MatchRulesDefinition` ΓÇö match rules + field dimensions (read-only at runtime)

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
