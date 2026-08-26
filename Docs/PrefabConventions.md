# Prefab Conventions

## Naming
- PascalCase
- Named by primary purpose
- One word or compound word describing function

Examples:
```
Player.prefab
SoccerBall.prefab
Stadium.prefab
Referee.prefab
```

## Structure
- Visual hierarchy separate from gameplay hierarchy
- Gameplay child objects are structural shells until implementation
- Component placement follows architecture patterns

```
Player/
├── Visual/          ← presentation only
├── Collision/       ← physics shell
├── Gameplay/        ← gameplay components (empty shells)
└── Sockets/         ← attachment points
```

## Location
- Reusable prefabs: `Assets/Football/Prefabs/`
- Domain subfolders: `Players/`, `Ball/`, `Stadium/`, `UI/`, `Effects/`
- Test-only objects stay in Test scenes — never promoted to Prefabs/

## Rules
- Prefabs must not contain hardcoded references to scene objects
- Prefab connections use serialized references, not Find/Object.FindObjectOfType
- Variant prefabs inherit from a base, never copy-paste full hierarchy
- Prefab editing done in Prefab Mode, not in-scene overrides (except test scenes)
