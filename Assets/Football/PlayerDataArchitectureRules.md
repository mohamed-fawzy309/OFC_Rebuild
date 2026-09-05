# MASTER PLAYER DATA ARCHITECTURE RULES
# PHASE 2 — PLAYER DATA
# OFFICIAL DESIGN — DO NOT CHANGE WITHOUT APPROVAL

PROJECT:
D:\Projects\unity\OFC_Rebuild

These rules are the approved architecture for Player Data.

They must be treated as the source of truth for Tasks 66–88.

DO NOT change, remove, rename, merge, split, or reinterpret these rules
without explicitly proposing the change and receiving approval first.

==================================================
1. PLAYER MODEL
==================================================

There is ONE unified PlayerDefinition model.

Do NOT create separate:

GoalkeeperDefinition
OutfieldPlayerDefinition
AttackerDefinition
DefenderDefinition

unless explicitly approved later.

Every player uses the same unified PlayerDefinition + PlayerStats model.

==================================================
2. PLAYER DATA STRUCTURE
==================================================

PlayerDefinition is organized into:

PlayerDefinition
│
├── Identity
│   ├── PlayerId
│   ├── Name
│   ├── Nationality
│   └── Club Reference
│
├── PhysicalProfile
│   ├── Age
│   ├── Height
│   └── Weight
│
├── Profile
│   ├── PrimaryPosition
│   ├── SecondaryPositions
│   ├── PreferredFoot
│   ├── WeakFoot
│   ├── SkillRating
│   ├── OverallRating
│   ├── CardType
│   └── PlayStyle
│
└── PlayerStats
    ├── Pace
    ├── Shooting
    ├── Passing
    ├── Dribbling
    ├── Defending
    ├── Physical
    └── Goalkeeping

==================================================
3. IDENTITY
==================================================

Identity data describes WHO the player is.

Required concepts:

PlayerId
Name
Nationality
Club Reference

Identity data is NOT gameplay rating data.

Do not put:

Age
Height
Weight
OverallRating
Pace
Finishing
etc.

inside Identity unless explicitly approved.

==================================================
4. PHYSICAL PROFILE
==================================================

PhysicalProfile contains physical/biographical measurements:

Age
Height
Weight

These are NOT rating values.

Use real-world units.

Examples:

Age:
24

Height:
183 cm

Weight:
78 kg

Do NOT represent them as 1–100 ratings.

==================================================
5. PLAYER PROFILE
==================================================

Profile contains role/capability information:

PrimaryPosition
SecondaryPositions
PreferredFoot
WeakFoot
SkillRating
OverallRating
CardType
PlayStyle

These are NOT grouped into PlayerStats categories.

==================================================
6. PRIMARY POSITION
==================================================

PrimaryPosition determines the player's main role/profile.

Example:

PrimaryPosition = ST
    → Outfield player profile

PrimaryPosition = CB
    → Outfield player profile

PrimaryPosition = GK
    → Goalkeeper profile

Do NOT use the existence of Goalkeeping stats alone to determine whether
the player is a goalkeeper.

The primary position determines the player's primary role.

==================================================
7. SECONDARY POSITIONS
==================================================

A player may have multiple valid positions.

Use:

PrimaryPosition
+
SecondaryPositions

Example:

PrimaryPosition:
CM

SecondaryPositions:
CAM
CDM

Do NOT duplicate the PlayerDefinition for each position.

Position data describes where the player CAN play.

==================================================
8. PREFERRED FOOT
==================================================

PreferredFoot is the player's naturally preferred foot.

Examples:

Right
Left

Do NOT use a numeric rating.

==================================================
9. WEAK FOOT
==================================================

WeakFoot is a rating representing the player's ability to use the
non-preferred foot effectively.

Example:

PreferredFoot = Right
WeakFoot = 4

WeakFoot is player capability/profile data.

It is NOT:

- Passing
- Dribbling
- Physical
- Shooting

Do not create a second WeakFoot system.

==================================================
10. SKILL RATING
==================================================

Use:

SkillRating

NOT:

SkillMovesRating
CanDoSkills
SkillMovesLevel

unless explicitly approved.

SkillRating represents the player's general ability to perform
technical skill moves.

Example:

SkillRating = 5

Suggested conceptual scale:

1
2
3
4
5

The exact gameplay meaning of each value will be defined later.

SkillRating does NOT list individual skills.

Do not create a SkillMove system in Player Data.

==================================================
11. OVERALL RATING
==================================================

Use:

OverallRating

This represents the player's overall rating.

OverallRating is NOT the same as:

SkillRating

They are separate concepts.

SkillRating:
    ability to perform technical skill moves

OverallRating:
    overall player rating

IMPORTANT:

Do NOT blindly store OverallRating as an arbitrary number if the
architecture eventually computes it from attributes.

Before implementation, Task 66/67 must audit whether OverallRating should
be:

A. explicitly authored
OR
B. derived from PlayerStats according to position/category weighting.

Do not silently choose one without reviewing the existing project and
architecture.

If a decision is required, propose it before locking implementation.

==================================================
12. CARD TYPE
==================================================

CardType identifies the player's card classification.

FINAL APPROVED enum value set (Task 82 revision):

Basic
Iconic
Legend
Ultimate
Prime
Form
Signature
Elite

This REPLACES the earlier proposed Base/Rare/Special (which are NOT enum members and NOT defaults).

CardType is presentation/data classification.

CardType must NOT become gameplay authority.

==================================================
13. PLAY STYLE
==================================================

PlayStyle represents player behavioral/technical style.

Do NOT use:

string

unless explicitly justified.

Prefer strongly typed data.

IMPORTANT:

Determine whether one player may have:

ONE PlayStyle

or:

MULTIPLE PlayStyles

through an explicit architecture decision.

Do not silently invent multiple styles.

PlayStyle is data.

It does NOT execute gameplay.

==================================================
14. PLAYER STATS
==================================================

PlayerStats contains the player's numeric football attributes.

The six primary outfield categories are:

1. Pace
2. Shooting
3. Passing
4. Dribbling
5. Defending
6. Physical

Goalkeeping is also stored in PlayerStats as a seventh category.

==================================================
15. PACE
==================================================

Pace contains:

Acceleration
SprintSpeed

==================================================
16. SHOOTING
==================================================

Shooting contains:

AttackingAwareness
Finishing
ShotPower
LongShots
Volleys
Penalties

==================================================
17. PASSING
==================================================

Passing contains:

Vision
ShortPassing
LongPassing
Crossing
FreeKickAccuracy
Curve

IMPORTANT:

Curve belongs to PASSING in this project architecture.

Do NOT move Curve to:

Shooting
Dribbling

without approval.

==================================================
18. DRIBBLING
==================================================

Dribbling contains:

Dribbling
BallControl
TightPossession
Agility
Balance
Reactions

IMPORTANT:

TightPossession belongs to Dribbling.

It represents the player's ability to retain/control the ball closely
under pressure and during close control.

==================================================
19. DEFENDING
==================================================

Defending contains:

DefensiveAwareness
Interceptions
StandingTackle
SlidingTackle
Heading

==================================================
20. PHYSICAL
==================================================

Physical contains:

Strength
Stamina
Jumping
Aggression

==================================================
21. GOALKEEPING
==================================================

Goalkeeping contains:

Diving
Handling
Kicking
Positioning
Reflexes
Parrying

IMPORTANT:

Goalkeeping stats exist for EVERY player.

Do NOT remove Goalkeeping stats from outfield players.

==================================================
22. GOALKEEPER RULE
==================================================

The player is treated as a goalkeeper when:

PrimaryPosition == GK

When PrimaryPosition == GK:

the primary displayed/used attribute profile is:

Goalkeeping

showing:

Diving
Handling
Kicking
Positioning
Reflexes
Parrying

The normal outfield categories are NOT the primary goalkeeper
attribute profile.

Outfield categories may still exist in PlayerStats.

==================================================
23. OUTFIELD PLAYER RULE
==================================================

When:

PrimaryPosition != GK

the player is treated as an outfield player.

Primary displayed categories:

Pace
Shooting
Passing
Dribbling
Defending
Physical

Goalkeeping stats still exist.

==================================================
24. OUTFIELD PLAYER USED AS GOALKEEPER
==================================================

IMPORTANT:

An outfield player may be assigned to goalkeeper during a match.

Therefore:

Goalkeeping stats MUST remain available for every player.

Example:

Player:
PrimaryPosition = ST

Goalkeeping:
Diving = 12
Handling = 10
Kicking = 15
Positioning = 8
Reflexes = 14
Parrying = 9

If the gameplay system later assigns this player to GK:

the gameplay/presentation layer can use those Goalkeeping stats.

Do NOT create duplicate goalkeeper data.

Do NOT convert the player into a separate GoalkeeperDefinition.

==================================================
25. GOALKEEPER VS GOALKEEPING STATS
==================================================

These are NOT equivalent:

PrimaryPosition == GK
    = player role/profile

Goalkeeping stats exist
    = player has goalkeeper-related numerical data

Therefore:

Goalkeeping stats
    !=
Goalkeeper role

==================================================
26. CATEGORY COUNT
==================================================

Primary OUTFIELD categories:

6

Pace
Shooting
Passing
Dribbling
Defending
Physical

Goalkeeping:

7th category stored in the unified PlayerStats model.

Do NOT call Goalkeeping one of the six outfield categories.

==================================================
27. ATTRIBUTE VALUES
==================================================

All football ratings should use a consistent rating system.

Before implementation:

AUDIT current project.

Then define:

- minimum
- maximum
- default
- validation
- invalid-value behavior

Do NOT assume 1–100 blindly if the project already establishes another
range.

However:

WeakFoot and SkillRating have their own conceptual scales and must not
automatically be treated like all football attributes.

==================================================
28. DATA VS GAMEPLAY
==================================================

PlayerDefinition and PlayerStats are DATA.

They do not:

- move players
- calculate physics
- execute dribbling
- shoot
- pass
- tackle
- make AI decisions
- control animation
- control camera

Gameplay systems consume this data.

==================================================
29. DATA-DRIVEN RULE
==================================================

Gameplay values must NOT be hardcoded inside gameplay systems when they
belong to player data.

Correct:

PlayerDefinition
    ↓
PlayerStats
    ↓
Gameplay system

Incorrect:

Movement system
    ↓
hardcoded player speed

Incorrect:

Shooting system
    ↓
hardcoded finishing value

==================================================
30. SCRIPTABLEOBJECT POLICY
==================================================

PlayerDefinition is expected to be configuration/data.

If ScriptableObject is used:

it stores authored player definition data.

It must NOT contain mutable runtime state such as:

CurrentStamina
CurrentPosition
CurrentVelocity
CurrentScore
CurrentState
CurrentPossession

Those belong to runtime systems.

==================================================
31. RUNTIME VS CONFIGURATION
==================================================

Configuration:

PlayerDefinition
PlayerStats
Player identity/profile
Authored ratings

Runtime:

Current position
Current stamina
Current state
Current velocity
Current possession
Current gameplay role
etc.

Never mix them.

==================================================
32. SINGLE SOURCE OF TRUTH
==================================================

Each player attribute must exist exactly once.

Do NOT duplicate:

Pace
Finishing
Passing
WeakFoot
OverallRating
etc.

across multiple independent mutable objects.

If a derived value exists:

document its source.

==================================================
33. POSITION-AWARE PRESENTATION
==================================================

The UI/presentation layer should display the appropriate attribute profile
according to PrimaryPosition.

Example:

GK:
    Goalkeeping stats

ST:
    Pace/Shooting/Passing/Dribbling/Defending/Physical

This is PRESENTATION/ROLE logic.

Do not duplicate data to achieve it.

==================================================
34. FUTURE EXTENSIBILITY
==================================================

More player data may be added later.

Possible future additions:

- new attributes
- traits
- roles
- special abilities
- additional play styles
- additional card classifications
- goalkeeper-specific data

BUT:

Future extensibility does NOT mean adding speculative fields now.

Add data only when justified.

==================================================
35. NO ATTRIBUTE RENAMING
==================================================

The following names are currently approved:

Acceleration
SprintSpeed

AttackingAwareness
Finishing
ShotPower
LongShots
Volleys
Penalties

Vision
ShortPassing
LongPassing
Crossing
FreeKickAccuracy
Curve

Dribbling
BallControl
TightPossession
Agility
Balance
Reactions

DefensiveAwareness
Interceptions
StandingTackle
SlidingTackle
Heading

Strength
Stamina
Jumping
Aggression

Diving
Handling
Kicking
Positioning
Reflexes
Parrying

Do not rename them casually.

==================================================
36. APPROVED PROFILE NAMES
==================================================

Use:

PlayerId
Name
Nationality
ClubReference

Age
Height
Weight

PrimaryPosition
SecondaryPositions
PreferredFoot
WeakFoot
SkillRating
OverallRating
CardType
PlayStyle

==================================================
37. IMPORTANT UNKNOWN DECISIONS
==================================================

The following must be AUDITED before implementation where necessary:

1. OverallRating:
   authored vs calculated

2. PlayStyle:
   single vs multiple

3. CardType:
   final enum/type set

4. Position representation:
   enum / ID / other typed representation

5. Nationality representation:
   enum / ID / reference

6. ClubReference:
   TeamDefinition reference / ID / other project-approved reference

7. Attribute range:
   exact minimum/maximum

8. Height and Weight:
   exact stored units/types

Do NOT silently invent architecture for these.

Propose first when ambiguous.

==================================================
38. NO SPECULATIVE GAMEPLAY
==================================================

Do NOT create gameplay systems during Player Data tasks.

Do NOT create:

PlayerController
MovementSystem
ShootingSystem
PassingSystem
DribblingSystem
TackleSystem
GoalkeeperSystem
AISystem

just because Player Data exists.

==================================================
39. NO SPECULATIVE UI
==================================================

Do NOT create:

PlayerCardUI
StatsPanel
FIFA-style card UI
Rating screen
PlayerDetailsUI

unless the current task explicitly requires it.

The Player Data architecture must exist independently from presentation.

==================================================
40. NO FIFA COPYING REQUIREMENT
==================================================

The design may be INSPIRED by football games such as FIFA/EA FC,
but this is NOT a requirement to reproduce FIFA exactly.

Our architecture is the source of truth.

Do NOT add an attribute solely because FIFA has it unless it fits the
project's gameplay design.

==================================================
41. TASK DISCIPLINE
==================================================

When working on a Player Data task:

1. Audit existing project.
2. Read current relevant files.
3. Reuse correct existing structures.
4. Propose architecture changes if needed.
5. Implement only the current task.
6. Add focused tests.
7. Compile.
8. Run regression tests.
9. Verify no unrelated systems changed.
10. STOP at the specified task boundary.

Do NOT implement later Player Data tasks automatically.

==================================================
42. CHANGE CONTROL
==================================================

If you believe a rule needs to change:

DO NOT silently change it.

Instead report:

PROPOSED CHANGE:
<change>

REASON:
<reason>

IMPACT:
<impact>

WAIT FOR APPROVAL.

==================================================
43. FINAL APPROVED PLAYER STRUCTURE
==================================================

PlayerDefinition
│
├── Identity
│   ├── PlayerId
│   ├── Name
│   ├── Nationality
│   └── ClubReference
│
├── PhysicalProfile
│   ├── Age
│   ├── Height
│   └── Weight
│
├── Profile
│   ├── PrimaryPosition
│   ├── SecondaryPositions
│   ├── PreferredFoot
│   ├── WeakFoot
│   ├── SkillRating
│   ├── OverallRating
│   ├── CardType
│   └── PlayStyle
│
└── PlayerStats
    │
    ├── Pace
    │   ├── Acceleration
    │   └── SprintSpeed
    │
    ├── Shooting
    │   ├── AttackingAwareness
    │   ├── Finishing
    │   ├── ShotPower
    │   ├── LongShots
    │   ├── Volleys
    │   └── Penalties
    │
    ├── Passing
    │   ├── Vision
    │   ├── ShortPassing
    │   ├── LongPassing
    │   ├── Crossing
    │   ├── FreeKickAccuracy
    │   └── Curve
    │
    ├── Dribbling
    │   ├── Dribbling
    │   ├── BallControl
    │   ├── TightPossession
    │   ├── Agility
    │   ├── Balance
    │   └── Reactions
    │
    ├── Defending
    │   ├── DefensiveAwareness
    │   ├── Interceptions
    │   ├── StandingTackle
    │   ├── SlidingTackle
    │   └── Heading
    │
    ├── Physical
    │   ├── Strength
    │   ├── Stamina
    │   ├── Jumping
    │   └── Aggression
    │
    └── Goalkeeping
        ├── Diving
        ├── Handling
        ├── Kicking
        ├── Positioning
        ├── Reflexes
        └── Parrying

==================================================
44. GOALKEEPER DISPLAY RULE
==================================================

IF:

PrimaryPosition == GK

THEN primary player stat profile is:

Goalkeeping

Diving
Handling
Kicking
Positioning
Reflexes
Parrying

ELSE:

primary player stat profile is:

Pace
Shooting
Passing
Dribbling
Defending
Physical

Goalkeeping data remains stored in both cases.

==================================================
45. IMPORTANT FINAL RULE
==================================================

The purpose of Phase 2 Player Data is:

DATA FIRST.

PlayerDefinition defines the player.

PlayerStats defines the player's football ratings.

Gameplay consumes those values.

Presentation displays those values.

Neither gameplay nor presentation should duplicate the authoritative
player data.

Do not start implementation until the current task is explicitly given.
