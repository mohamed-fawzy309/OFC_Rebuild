# ScriptableObject Conventions

## Purpose
ScriptableObjects are **data containers** and **configuration assets**. They hold static, shareable data that defines how systems behave.

## Rules

### Runtime State MUST NOT Live in ScriptableObjects
- ScriptableObjects are shared assets
- Multiple systems may read the same SO
- Mutable runtime state causes data races and unpredictable behavior
- Runtime state belongs on MonoBehaviours or dedicated state classes

### Correct Use
```csharp
// GOOD — reading configuration from SO
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private MovementConfig _config;
    
    void Move()
    {
        float speed = _config.RunSpeed; // reading shared data
    }
}

// BAD — writing runtime state into SO
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private MovementConfig _config;
    
    void Move()
    {
        _config.CurrentStamina -= 1f; // WRONG — mutating shared asset
    }
}
```

### Data Hierarchy
| SO Type | Location | Purpose |
|---------|----------|---------|
| `PlayerDefinition` | `Data/Players/` | Player attributes, skills |
| `TeamDefinition` | `Data/Teams/` | Team config, formation, tactics |
| `MatchRulesDefinition` | `Data/Matches/` | Match duration, field dimensions |
| `BallConfig` | `Data/Ball/` | Ball physics parameters |
| `MovementConfig` | `Data/Movement/` | Movement speeds, acceleration |
| `AnimationConfig` | `Data/Animation/` | Blend times, thresholds |
| `DribbleConfig` | `Data/Ball/` | Dribble behavior parameters |

### Creation
- Created via Unity menu: `Create > Football > Data > [Type]`
- Stored under `Assets/Football/Data/`
- Never created at runtime

### Naming
- PascalCase
- Descriptive of configuration domain
- No abbreviations

```
PlayerDefinition.asset
TopScorerPlayerDefinition.asset
MatchRules.asset
QuickMatchRules.asset
```
