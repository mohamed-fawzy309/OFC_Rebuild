# Bug Definition

## What Qualifies as a Bug

A **bug** is any behavior where the actual outcome differs from the intended outcome:

- **Incorrect gameplay behavior** — player moves when they shouldn't, action doesn't execute
- **Incorrect visual behavior caused by a gameplay system** — animation plays wrong state because gameplay sent wrong input
- **Runtime exception** — any unhandled exception in Console
- **Regression** — previously working behavior is now broken
- **Incorrect state transition** — state machine enters wrong state or skips state
- **Invalid physics behavior** — ball passes through colliders, unrealistic forces
- **Performance regression** — measurable FPS drop or frame spike introduced by change
- **Broken data reference** — ScriptableObject or prefab reference lost or null

## Severity Levels

### Blocker
- Project cannot compile
- Core systems non-functional
- Data loss possible
- Blocks all other work

### Critical
- Major feature completely broken
- Crash or hang
- Data corruption
- No reasonable workaround

### Major
- Feature partially broken
- Significant behavior deviation from design
- Workaround exists but is impractical

### Minor
- Edge case behavior issue
- Cosmetic with gameplay impact
- Inconsistent behavior across scenarios

### Cosmetic
- Visual-only issue
- No gameplay impact
- Typos in UI text
- Slightly off colors/positioning

## Bug Report Template
```markdown
### Bug: <short title>

**Severity:** Blocker / Critical / Major / Minor / Cosmetic

**Steps to Reproduce:**
1. ...
2. ...
3. ...

**Expected:** What should happen

**Actual:** What actually happens

**Environment:** Unity version, platform

**Screenshots/Logs:** If applicable
```
