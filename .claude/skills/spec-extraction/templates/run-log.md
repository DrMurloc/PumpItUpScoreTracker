# PIU Scores spec extraction - run log (RESUME HERE)

One pull request that extracts PIU Scores' specs from its code, retires the design docs, and lands the spec
checks. Branch `<branch>`, checkout `<path>`, baseline `<branch @ sha>`. Working folder: this folder
(`.claude/spec-extraction/`, git-ignored). Private findings: `<findingsDir>` (outside the repo).

## The estimate and the running total

Estimate agreed with the owner: `<output / cache writes / cache reads, and plan-weeks>`. Stop and check in at
twice that. After each workflow, add a row from `sum_usage.py` (transcripts), never from the usage line.

| After | Output | Cache writes | Cache reads | Running total (output) |
|---|---|---|---|---|

## Decisions (binding; the owner's words)

- <every settle-first answer and every shape check-in ruling, dated>

## Stages

| Stage | What | Workflow run id | Result | Commit |
|---|---|---|---|---|
| 1 Setup | init.py, general.json, shape-args.json | - | | - |
| 2 Shape | shape workflow, shape_out.py, check-in | | | - |
| 3 Specs | spec workflow(s), after_run.py, lint, index | | | |
| 4 Tags | tags.py plan / apply / gaps | - | | |
| 5 Design docs | inventory, map, gap map, repoint, delete, re-aim | | | |
| 6 Checks | the C# spec checks | - | | |
| 7 Wrap | docs, findings, pull request | - | | |

## Interruptions (what stopped, the run id, how it resumed)

## Calls taken mid-run (low-level calls the run made; listed for the owner)

## Problems left for later groups (from after_run.py)

## Open questions for the end
