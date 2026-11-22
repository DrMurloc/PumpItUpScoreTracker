# Stage 1 - setup

1. **The branch.** From an up-to-date `main`, cut `claude/spec-extraction` (or reuse it when resuming). A worktree
   under `.claude/worktrees/` works the same: the working folder sits inside whichever checkout the session runs in.
2. **The working folder:** `python -I .claude/skills/spec-extraction/tooling/init.py . .claude/skills/spec-extraction
   [--findings <folder outside the repo>]`. It refuses to go on if git does not ignore `.claude/spec-extraction/`.
3. **Fill `general.json`** (`<wd>/general.json`): read each value against the repo as it is today, add the stage 0
   answers to `decisions` in the owner's words, and replace the `<every ruling ...>` line (the bake refuses while a
   `<...>` placeholder is left; delete the line if there is nothing yet).
4. **Check `shape-args.json`:** every project under `ScoreTracker/` and every folder under `Pages/` belongs to a
   group's scope. A vertical added since 2026-11-22 needs a group or a line in one. Missing pieces surface later as
   unclaimed items, but a group that names them maps them properly.
5. **The run log:** fill the header of `<wd>/run-log.md` (branch, checkout, baseline, findings folder) and the
   agreed estimate.
6. **Keep the machine awake** for the long workflows if the host offers it, and note the session id: `sum_usage.py`
   needs it.

Nothing to commit in this stage.
