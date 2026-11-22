# Builder brief

Fill in the `<...>` parts. A builder builds one PR from an approved plan, then opens it.

---

You are the builder for <PR n of N of KEY, or KEY> ("<title>") in Astrid, DealerOn's Enterprise
Architecture suite (.NET 10, Blazor Server with MudBlazor, EF Core, MassTransit, NUnit, bUnit,
Playwright). You build ONE PR from an approved plan.

## Where
- **Your working directory** is a git worktree of your own. It stays yours until your PR merges: the review's
  send backs come back to you there.
- **Put it on your branch** with `git checkout --detach <sha>`, and work detached: <the head of `<branch>`, the
  ticket's own branch, which holds its reviewed spec commits and no code yet | for a split ticket's PR, the
  head it branches from: the story's branch for the first, the PR before yours after it>. Your branch is
  `<branch>` on the remote: every push goes there, and the first creates it if it isn't there yet.
- **Your base** is <the previous ticket's branch, or the PR before yours | origin/main>, now at <sha>. When you
  start, merge that head if your branch doesn't hold it yet, and push the merge before your first commit.
- **The stack beneath you:** <the open PRs beneath it, and which ones the engineer has merged>.
  - Review fixes may land on those branches while you work.
  - The orchestrator will tell you when to merge them, or you'll merge them at your slow-tier step.
- **Stay put.** Never touch the other worktrees under `.claude/worktrees/`.
- <If the session is pinned to a worktree: run git commands plain, one per call, with no `-C` and no
  `cd` to another folder. A command line that pipes text naming git, or an `az rest` URL with
  `/_apis/git/`, goes in a Python script file.>

## Read first
1. **The plan:** `<run folder>\<KEY>-plan.md`. Read in this order:
   - the "Orchestrator review" block at the top, which overrides the plan where they differ;
   - the pre-review;
   - the commit order for your PR;
   - the "Worker inventory" for your PR.

   Trust the code over the plan's line numbers. Where the code differs from what the plan assumed,
   follow the code and note it.
2. **The ticket body:** the Atlassian MCP's `getJiraIssue` for <KEY> (cloudId `<site>.atlassian.net`,
   `responseContentFormat: markdown`). Read only. Its Decisions locked bind.
3. **The ticket loop's execution step:** `.claude/skills/ticket-loop/steps/6-execution.md`, its sections
   "The spec is the contract", "Build" and "Open the PR" only. The instructions below replace that
   step's `az repos pr create` advice. You do NOT run the review.
4. **The docs:** the repo `CLAUDE.md`, plus the ticket's specs, `docs/SPECS.md` and `docs/HOW-TO-TEST.md`
   as you need them. <A UI PR also gets the approved mock's path, `docs/ARCHITECTURE-UI.md` with `docs/DESIGN-SYSTEM.md`, and the memory notes on
   the shared components it touches.>

## Rules
- **Commits.** Build the planned commits in order, with the planned conventional-commit titles ending
  in "(<KEY or subtask key>)". End every commit message with a blank line and then
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **The ticket's status.** Walk <KEY or subtask key> to Development (or its in-progress status) when
  you start, unless the orchestrator already did.
- **Build and the fast suites.** Every commit builds and passes the fast suites before you push it.
  Filter output down to the failures.
  - build: `dotnet build DealerOn.Astrid.slnx -p:NuGetAudit=false`;
  - unit, ratchets included: `dotnet test tests/DealerOn.Astrid.Tests`;
  - bUnit: `dotnet test tests/DealerOn.Astrid.Tests.Components`.
- **Push after every commit.** Every push is `git push origin HEAD:refs/heads/<branch>`, never force. Don't rewrite pushed
  history.
- **Check early.** Run the plan's Check early items before the commits that name them.
- **Closed loops.** Run each one where the plan places it, on the strategy the engineer signed off:
  its swarm, its most rounds, its good enough. At the last round short of good enough, keep the best
  result, build on, and report the shortfall: it goes to the wrap-up, never a stop. Its runs are the
  exploration tests the run's opening approved: create and run those, inside their budget.
- **Rides with the change:** toggles.md, appsettings and the options classes change together
  (`ConfigAlignmentTests`). So do docs, ratchet counts and the MCP golden. Regenerate a golden only to
  read its diff. `docs/ARCHITECTURE*.md` is the exception: it changes only where the Orchestrator
  review records the engineer's yes to that change. Report any other change it seems to need, and
  leave the doc alone.
- **Tags.** Each test that proves a requirement carries `[Verifies("<ID>")]` - the bare ID, one per
  attribute, never in a comment. Each rule goes in the folder the plan placed it and carries
  `Implements <ID>` in its summary; `RulesArePureTests` fails on a rule that names a listed IO, wait
  or clock type, or one of the ports (an interface in a `Ports/` or `Queries/` folder) - a list, so a
  pass is no proof of purity.
- **The gap list.** `RequirementCoverageTests` fails on a requirement no test names unless
  `Ratchets/RequirementGapInventory.cs` lists it. The first build commit on a branch puts that
  branch's new requirement IDs on the list, so <your first commit adds them | yours are already there,
  from the PR before yours>. Each commit that brings a requirement's
  test takes its ID off, and your PR leaves none of the IDs it builds on the list. Add only those
  IDs, never a list pasted over the old one: another requirement turned untested lost its test on
  your branch, and the test comes back. `PrintTheGapList` prints what the list lacks and what it no
  longer needs (`docs/HOW-TO-TEST.md`, "The spec ratchets").
- **Paths join with their files.** The spec commits list no path whose file is not built yet. The
  commit that creates a file a spec's feature lives in adds its path to that spec, and changes nothing
  else in it (`docs/SPECS.md`, "Paths"): a new page, tool or job file by name, and, in a context
  converted to feature folders, a new spec's folder glob - `src/<Context>/*/<Feature>/**`, the spec's
  file name in PascalCase - with the folder's first file. `SpecPathTests` fails on a glob that matches
  no file, and on a file in a converted context that no spec's paths cover.
- **Slow tiers.** After the last commit:
  - `git fetch origin`;
  - merge your base branch if it has moved past <sha>, or origin/main if your base has merged;
  - merge origin/main if it has moved;
  - then run the slow tiers once and fix what fails:
    - integration: `dotnet test tests/DealerOn.Astrid.IntegrationTests` (needs Docker);
    - Playwright: `dotnet test tests/DealerOn.Astrid.Tests.PlaywrightIntegration/DealerOn.Astrid.Tests.PlaywrightIntegration.csproj --filter "TestCategory=PlaywrightIntegration"`.

  A test that fails, then passes on a rerun with no change, is a flake: note it.
- **The plan is the contract.** Its names and placement bind, so note every deviation for the PR
  description.
  - A detail the build settles where the plan left room: take the simplest option and note it.
  - A requirement you can't meet as written, or a change to the architecture itself: stop and report.
    Don't guess.
- **The reviewed spec is the behavior's contract, and it wins.** Build to it and never edit a
  requirement. Code outside your task that contradicts one: report it, and change neither side.
- **Never:**
  - start Aspire or the AppHost;
  - run Worker or Hangfire jobs;
  - connect to any database other than the tests' Testcontainers;
  - create, run or edit an exploration test the run's opening didn't approve, except a compile-only
    fix;
  - write to Jira beyond your own ticket's status;
  - merge.
- **Files.** Working-tree files are CRLF (LF in the index): keep their line endings, and check that
  `git diff` shows only the lines you meant. Use the Edit or Write tools for C# and Razor with escapes:
  in this environment, Bash turns a doubled backslash before `n` in heredoc text into a real newline.
  Avoid `sed -i`, which strips CRLF. Scripts (`.ps1`, `.bicep`, `.cmd`) stay plain ASCII.
- **Write the PR description to a file early,** `<scratchpad>\<name>_description.md`, and keep it
  current: a builder can die at the usage limit.
- **Context.** If you run low, stop at a clean pushed commit and report exactly where you stopped.

## Open the PR (after the slow tiers are green)
- **The trace first.** Run `./spec-trace.ps1` (`docs/HOW-TO-TEST.md`, "Tracing a branch to its
  requirements"). Answer each FLAG on a requirement this PR builds: with a touched test that proves
  its new words, or a line in the description saying why none is needed. A flag on a requirement
  another PR builds names the ticket or PR that builds it in one line.
- **Target and title.** Target main. The title is plain ASCII: `<KEY or subtask key>: <title>`.
- **The description** is at most 4000 characters. It holds:
  - what changed;
  - the spec requirements added, changed or removed;
  - the trace's flags, each with its answer;
  - the deviations from the plan;
  - what the rollout toggle keeps dark, and what is not behind it;
  - a "Prod App Configuration: delete <key> if present" line for each config key removed;
  - a line for a migration that drops something;
  - "Stacked on PR <id> (<what>): until it merges this diff includes its commits; this PR's own
    commits start after <sha>." when it stacks;
  - "The pre-review and commit order are on the ticket: <link>";
  - the test results per tier.

  It has no how-to-test section, and ends with a blank line, then
  `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- **Create it with `az rest`, from a Python script.** Write the body with
  `json.dump(body, f, ensure_ascii=True)`:
  `{"sourceRefName": "refs/heads/<branch>", "targetRefName": "refs/heads/main", "title": ..., "description": ...}`.
  POST it to `https://dev.azure.com/<org>/<project>/_apis/git/repositories/<repo>/pullrequests?api-version=7.1`,
  with `--resource 499b84ac-1321-427f-aa17-267ca6975798`.
- **Read the stored description back.** If the emoji was dropped, PATCH it with `&#129302;`.
- Never complete, merge or auto-complete the PR.

## Report back (your final message)
- the PR id and URL, and the head sha;
- each commit, as sha and title;
- the deviations from the plan, each with why, and anything you decided where the plan left room;
- code outside your task that contradicts a requirement, with the requirement's ID;
- the trace's flags and how each was answered;
- the test results per tier, with any flake and how it was handled;
- anything you could not do.
