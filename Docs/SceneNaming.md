# Scene Naming Conventions

## Naming Rules
- PascalCase
- Named by function or purpose
- No numbers, no "New", no generic names

## Standard Scenes

### Production
| Scene | Location | Purpose |
|-------|----------|---------|
| `Bootstrap.unity` | `Scenes/Bootstrap/` | Composition root, service init |
| `MainMenu.unity` | `Scenes/Menus/` | Main menu UI |
| `Match.unity` | `Scenes/Match/` | Core match gameplay |

### Test Scenes
| Scene | Location | Purpose |
|-------|----------|---------|
| `PlayerMovementTest.unity` | `Scenes/Test/` | Test player movement in isolation |
| `BallPhysicsTest.unity` | `Scenes/Test/` | Test ball physics behavior |
| `BallControlTest.unity` | `Scenes/Test/` | Test dribbling and ball control |
| `AnimationTest.unity` | `Scenes/Test/` | Test animation system |
| `MatchRulesTest.unity` | `Scenes/Test/` | Test match rules and state machine |

## Test Scene Structure
Every test scene follows:
```
_Test/
├── Systems/
├── Subject/
├── Camera/
└── Debug/
```

## Forbidden Names
- `Scene1`, `Scene2`, ...
- `NewScene`, `TestScene`
- `untitled`, `sample`
- Any name containing spaces
