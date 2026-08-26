# Football Project

Clean architecture rebuild for a football/soccer game built in Unity 6.

## Architecture

See [Architecture.md](Architecture.md) for detailed design principles.

## Project Structure

```
Assets/Football/
├── Art/            - Visual assets (characters, stadium, UI, VFX)
├── Animation/      - Animation clips, controllers, processed assets
├── Audio/          - Sound effects, commentary, crowd, music
├── Materials/      - Shared materials
├── Prefabs/        - Prefab definitions (players, ball, stadium, UI, effects)
├── Scenes/         - Bootstrap, menus, match, test scenes
├── Data/           - ScriptableObject data definitions
├── Runtime/        - All runtime C# source code
├── Editor/         - Editor-only tools and setup scripts
├── Tests/          - EditMode and PlayMode tests
└── Settings/       - Project-specific settings
```

## Key Design Decisions

- Composition over inheritance
- Gameplay separated from presentation
- Data-driven configuration via ScriptableObjects
- State machines for match and player states
- Lightweight event system (typed C# events, no global EventBus)
- Interface-driven dependencies
- No singletons, no giant GameManager

## Assembly Structure

```
Football.Core          → Foundation interfaces, events, debug
Football.Input         → Input abstraction layer
Football.Players       → Player state machines, player components
Football.Ball          → Ball state and physics foundation
Football.Actions       → Football action definitions
Football.Match         → Match state machine, rules
Football.Teams         → Team definitions and management
Football.AI            → AI decision framework (future)
Football.Camera        → Camera system
Football.UI            → User interface
Football.World         → World environment, stadium
Football.Editor        → Editor tools
Football.Tests.EditMode
Football.Tests.PlayMode
```
