# Animation Naming Conventions

## Format
```
<Actor>_<Category>_<Action>_<Variant>
```

## Components

### Actor
Character or object performing the animation:
- `XBot` — test character
- `Player` — generic player
- `GK` — goalkeeper
- `Ref` — referee

### Category
Animation domain:
- `Locomotion` — movement (walk, run, sprint, idle)
- `BallControl` — dribbling, receiving, trapping
- `Action` — passing, shooting, tackling, heading
- `Goalkeeper` — diving, catching, throwing
- `Celebration` — goal celebrations
- `UI` — menu/idle poses

### Action
Specific action within category:
- `Run`, `Sprint`, `Walk`, `Idle`
- `Dribble`, `Receive`, `Trap`
- `Pass`, `Shoot`, `Tackle`, `Header`
- `Dive`, `Catch`, `Throw`

### Variant
Optional suffix for directional or contextual variants:
- `Left`, `Right`
- `Weak`, `Strong`
- `Low`, `High`
- `Short`, `Long`

## Examples
```
XBot_Locomotion_Run
XBot_Locomotion_Sprint
XBot_Locomotion_Walk
XBot_Locomotion_Idle
XBot_BallControl_Dribble
XBot_BallControl_Receive
XBot_Action_Pass_Right
XBot_Action_Pass_Left
XBot_Action_Shoot_Right
XBot_Action_Shoot_Left
XBot_Action_Tackle
XBot_Action_Header
XBot_Goalkeeper_Dive_Left
XBot_Goalkeeper_Dive_Right
XBot_Goalkeeper_Catch
XBot_Celebration_Generic
```

## Processed vs Source
- Source clips live under `Animation/Source/Mixamo/`
- Processed (trimmed, retargeted, adjusted) clips live under `Animation/Processed/<Category>/`
- Controllers live under `Animation/Controllers/`

## Import Rules
- Document root motion policy per clip
- Humanoid vs Generic decision recorded
- Loop Time explicitly set
- Compression settings documented
- Scale and axis policy verified
