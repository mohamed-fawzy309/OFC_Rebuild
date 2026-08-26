# Definition of Done

A task is **Done** only when ALL of the following are true:

## 1. Implementation Complete
- All acceptance criteria met
- All planned work for the task is finished
- No placeholder or stub code left behind

## 2. Compilation Passes
- Zero compile errors in Unity Console
- Zero compile errors in IDE
- No warnings introduced by this change (existing warnings are acceptable)

## 3. Automated Tests Pass
- All relevant EditMode tests pass
- All relevant PlayMode tests pass (where applicable)
- New tests written for new functionality

## 4. Play Mode Verification
- Feature tested in Play Mode where applicable
- Visual behavior matches expected results
- No runtime exceptions in Console

## 5. Edge Cases Checked
- Boundary conditions tested
- Null/empty inputs handled
- State transitions verified
- Concurrent access scenarios considered

## 6. Diagnostics Clean
- No temporary `Debug.Log` statements left in production code
- No debug-only code paths without conditional compilation
- No hardcoded test values

## 7. No Unrelated Systems Changed
- Only files related to the task are modified
- No unintended side effects on other systems

## 8. Documentation Updated
- Architecture docs updated if new patterns introduced
- Code comments added only where complexity warrants it
- README updated if project structure changed

## 9. Git Commit Created
- Commit follows naming convention
- Commit message is descriptive
- Only intended files are staged

## 10. Known-Good State
- Project compiles cleanly
- Existing tests still pass
- No regressions introduced
- Working tree is clean or only has intentional uncommitted changes
