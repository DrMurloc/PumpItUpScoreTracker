# Send-back brief

Fill in the `<...>` parts, and send it to the PR's own builder by SendMessage, so it keeps its context. It is
the message a builder gets for its send backs: it applies already-ruled fixes to its PR, in its own worktree,
and closes their threads. A fresh builder gets it only when the original is gone or out of context, with that
builder's handoff notes: start it with `isolation: "worktree"`, and its first step is `git checkout --detach
<sha>`.

---

The review of your PR sent back fixes, already ruled: Azure DevOps PR <id> (<KEY>, "<title>"). Apply each
exactly as ruled, as commits on the PR's branch. <For a fresh builder: you take over this PR in Astrid, a
.NET 10 solution, from the builder that built it. Read its handoff notes first: <paths>.>

## FIRST: your worktree
- **These fixes go in your own worktree,** where you built the PR, detached at <sha>, the PR's head.
- Run git commands plain, one per call. Never run commands in any other worktree.
- If its head isn't <sha>, or a command is refused, stop and report. Don't work around it.
- <If a lower PR's fixes must come in first, give the merge: `git merge --no-ff --no-commit <ref>`.
  Then the message file, then build before the fixes.>

## Read first
- **The threads.** Read each thread's finding - its **Reviewer:** or **Flag:** comment - and its **Skeptic:**
  reply, which ends in the **Verdict:** line, with a GET of
  `https://dev.azure.com/<org>/<project>/_apis/git/repositories/<repo>/pullRequests/<id>/threads/<tid>?api-version=7.1`
  (`--resource 499b84ac-1321-427f-aa17-267ca6975798`). If the session's guard refuses an `az rest`
  whose URL holds `/_apis/git/`, run it from a small Python script.
- **The specs** the fixes touch.

## The fixes
<One numbered item per fix: its thread, then the ruled fix exactly as the skeptic worded it, then the test
that proves it. Name the files and say what a test asserts.>

Keep each fix minimal. If a fix turns out not to work as described, or bigger than ruled, stop and report
rather than redesign it.

## How
- **Commits**, one per fix or as grouped above, each ending with a blank line and then
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`:
  - <the titles, ending "(<KEY or subtask key>)">
- **Build and tests**, with `-p:NuGetAudit=false` everywhere. Filter the output down to the failures.
  - after each commit, all green:
    - `dotnet build DealerOn.Astrid.slnx -p:NuGetAudit=false`;
    - unit: `dotnet test tests/DealerOn.Astrid.Tests -p:NuGetAudit=false`;
    - bUnit: `dotnet test tests/DealerOn.Astrid.Tests.Components -p:NuGetAudit=false`;
  - once at the end:
    - integration: `dotnet test tests/DealerOn.Astrid.IntegrationTests -p:NuGetAudit=false`;
    - <Playwright, when a fix touches a page>.
  - A new test should fail against the old code. Check it, and say so.
- **Push** as a fast-forward, with `git push origin HEAD:refs/heads/<branch>`.
  - Never force. If it's rejected because the branch moved, stop and report.
  - If the push itself is refused, create a local branch `bugfix/<key>-pr<id>` at your HEAD and
    report that instead.
- **Line endings.** Working-tree files are CRLF (LF in the index): keep them, and check `git diff`.
  Use the Edit or Write tools, and avoid `sed -i`.
- **Never** start Aspire or the AppHost, run Worker jobs, connect to any database other than
  Testcontainers, run or edit exploration tests, or call Jira.

## Close the threads
Write each JSON body with Python's `json.dump(body, f, ensure_ascii=True)`.
- On each fixed thread, POST one `{"content":"**Outcome:** <what changed, the sha, the test>","parentCommentId":1,"commentType":1}`
  to `.../threads/<tid>/comments?api-version=7.1`. Then PATCH `{"status":"fixed"}` to `.../threads/<tid>?api-version=7.1`.
- Don't touch the threads that aren't yours: <ids>.

## Report back
- the commit shas and your new head, or the local branch if the push was refused;
- the test results per run;
- each thread's status;
- anything that went differently, including any public signature that changed, since a later PR may
  build on this branch.
