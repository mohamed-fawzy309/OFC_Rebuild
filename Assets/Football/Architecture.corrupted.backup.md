# TASK 103 — FOUL RULES

## 103.1 Foul availability
The single established Foul representation is the authored bool `EnforceFouls` (default TRUE, on) on
MatchRulesDefinition (Header `Fouls`), mirroring the established `EnforceOffside`/`UsePenaltyShootout`
single-boolean rule pattern. It is retained as canonical — no duplicate foul field is added.
`EnforceFouls` is authorable to FALSE (a foul-less match).

## 103.2 Foul category/severity taxonomy (POLICY / DEFERRED)
Foul CATEGORY / SEVERITY taxonomy — standard vs serious vs professional fouls, foul-type breakdown,
advantage-play policy — is **NOT ESTABLISHED** by the project. Per the no-speculative-values rule it
is POLICY / DEFERRED: NO `FoulType`/`FoulSeverity`/`FoulCategory` enum and NO `AdvantageRule`/
`FoulRule` fields are invented.

## 103.3 Boundaries
- **Foul Data** describes the offence (match rule). **Match Cards** describe the disciplinary result
  and are owned by **Task 104**. Foul rules do NOT own card state (no `CurrentYellowCards`/
  `CurrentRedCards`/current booking counts — those are match-card data / runtime state).
- The `foul offence — free kick / penalty kick restart` mapping is a RESTART-type concern owned by
  **Task 106** (restart taxonomy), NOT foul rule data invented here.
- Foul-related player skill ratings (`Aggression`/`Tackling`/`Discipline`/etc.) remain
  player-capability data (`PlayerDefinition`/`PlayerStats`) and are NOT duplicated onto match rules.

## 103.4 No systems / no runtime state
Task 103 is DATA RULES only. NO `FoulSystem`/`FoulDetector`/`FoulManager`/`ChallengeSystem`/
`ContactSystem`/`AdvantageSystem` are created. MatchRulesDefinition holds NO runtime foul state
(`CurrentFoul`/`FoulEvent`/`ActiveFoul`/`LastFoul`/`FoulCount`/`FoulHistory`).

## 103.5 Validation
`EnforceFouls` is a bool with no invalid authored state; the single coherent
`GetInvalidMatchRulesData()` (Task 97) boundary remains the authority and never mutates. NO
independent `FoulValidator` is created.
