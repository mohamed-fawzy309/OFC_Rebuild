# Naming Conventions

## C# Types and Members

### Classes, Structs, Enums, Interfaces
- PascalCase
- Descriptive and meaningful
- Interfaces prefixed with `I`

Examples:
```
PlayerMovement
BallController
MatchStateMachine
IPlayerInput
IGameEvent
GameStateId
```

NOT:
```
PM
BC
Manager2
Stuff
```

### Private Fields
- camelCase
- Prefixed with underscore: `_fieldName`

Examples:
```
_privateSpeed
_currentState
_maxStamina
```

### Public Properties
- PascalCase
- No prefix

Examples:
```
public float Speed { get; }
public bool IsGoalkeeper { get; set; }
```

### Methods
- PascalCase
- Verb-first naming

Examples:
```
Initialize()
ChangeState()
Tick(float deltaTime)
CalculateShotPower()
```

### Parameters and Local Variables
- camelCase
- Descriptive

Examples:
```
float deltaTime
int playerIndex
Vector3 targetPosition
```

## Namespaces
- Match assembly name structure
- PascalCase

```
Football.Core
Football.Input
Football.Players
Football.Ball
Football.Actions
Football.Match
Football.Teams
Football.AI
Football.Camera
Football.UI
Football.World
Football.Data
```

## Files
- Match the primary type name
- One primary type per file

```
PlayerMovement.cs          → contains PlayerMovement class
MatchStateMachine.cs       → contains MatchStateMachine class
GameStateId.cs             → contains GameStateId enum
```

## Assembly Definitions
```
Football.Core
Football.Input
Football.Players
Football.Ball
Football.Actions
Football.Match
Football.Teams
Football.AI
Football.Camera
Football.UI
Football.World
Football.Editor
Football.Tests.EditMode
Football.Tests.PlayMode
```

## Prefabs
- PascalCase
- Named by purpose

```
Player.prefab
SoccerBall.prefab
Stadium.prefab
```

NOT:
```
player_final.prefab
test_ball.prefab
New Prefab.prefab
```

## ScriptableObject Assets
- PascalCase
- Descriptive of configuration purpose

```
PlayerDefinition.asset
MatchRules.asset
MovementConfig.asset
BallConfig.asset
```

## Scenes
- PascalCase
- Named by function

```
Bootstrap.unity
MainMenu.unity
Match.unity
PlayerMovementTest.unity
BallPhysicsTest.unity
```

NOT:
```
Scene1.unity
Test2.unity
NewScene.unity
```

## Folders
- PascalCase
- Named by domain/function

```
Runtime/
Editor/
Data/
Prefabs/
Scenes/
```
