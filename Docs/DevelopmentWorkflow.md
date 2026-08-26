# Development Workflow

## Branch Strategy

### main
- Release-ready state only
- Never commit directly; always merge from `develop` or `hotfix/*`
- Always tagged at merge points

### develop
- Integration branch for all features
- Must always compile and pass tests
- Feature branches merge here

### Feature branches
```
feature/<feature-name>
```
Example: `feature/player-movement`, `feature/ball-physics`

### Bugfix branches
```
bugfix/<bug-name>
```
Example: `bugfix/sprint-state-persistence`

### Hotfix branches
```
hotfix/<issue-name>
```
Example: `hotfix/crash-on-match-start`

## When to Commit
- After a logical unit of work is complete
- After tests pass
- Before switching context
- After resolving a merge conflict
- At end of session (work-in-progress commit with `WIP:` prefix)

## When to Create a Tag
- When merging a milestone to `main`
- Follow semantic versioning: `v0.1.0`, `v0.2.0`, etc.

## Merge Expectations
- Feature branches merge into `develop` via fast-forward or merge commit
- `develop` merges into `main` only when release-ready
- Never force-push to `main` or `develop`
- Resolve conflicts in the feature branch before merging

## Commit Message Convention
```
<type>: <description>
```

Types:
- `feat:` — new feature
- `fix:` — bug fix
- `refactor:` — code restructuring without behavior change
- `test:` — adding or updating tests
- `docs:` — documentation only
- `build:` — build system or dependency changes
- `chore:` — maintenance tasks
- `perf:` — performance improvement

Examples:
```
feat: add player input abstraction
fix: correct sprint state persistence
test: add ball physics regression coverage
refactor: separate player state machine from movement
docs: update architecture rules
chore: initialize git repository
```

## Rollback Procedure

### Inspect status
```bash
git status
git log --oneline -10
```

### Restore to a previous commit (soft — keeps changes staged)
```bash
git reset --soft HEAD~1
```

### Restore to a previous commit (mixed — keeps changes unstaged)
```bash
git reset HEAD~1
```

### Abandon all changes since last commit (DANGEROUS)
```bash
git reset --hard HEAD
```

### Revert a specific commit (safe — creates new commit)
```bash
git revert <commit-hash>
```

### Abandon a feature branch
```bash
git switch main
git branch -D feature/<name>
```

### Return to a known-good state
```bash
git log --oneline        # find the good commit
git switch main
git reset --hard <commit-hash>   # DANGEROUS — overwrites working tree
```

## Safety Rules
- Prefer `git revert` over `git reset` for shared branches
- Never force-push to `main` or `develop`
- Always create a branch before making changes
- Never commit secrets, keys, or credentials
- Verify `git status` before and after every commit
