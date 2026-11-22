# Merge-forward brief

Fill in the `<...>` parts. A merge agent carries a lower PR's fixes into the branch above it, in a
throwaway worktree, while a builder keeps the build worktree.

---

You merge one branch forward in Astrid (repo EnterpriseArchitectureSuite: .NET 10, Blazor Server,
EF Core, NUnit). Review fixes have landed on `<lower branch>` (PR <id>), and `<upper branch>`
(PR <id>), which stacks on it, must carry them.

## Your worktree
- You run in a git worktree of your own. Put it on the upper branch's head with
  `git checkout --detach <upper sha>`. It's already in the local object store, so don't fetch.
- Run git commands plain, one per call. If the checkout is refused, stop and report.
- Never run commands in any other worktree.

## What comes in
`origin/<lower branch>` is at <sha>. Confirm it with `git rev-parse`; if it differs, use the sha. The
fixes:
<one line per commit: sha, what it changes, and any public signature that changed>

## Steps
1. `git merge --no-ff --no-commit origin/<lower branch>`.
2. **Resolve any conflict** so both sides hold: the upper branch's work and the fixes' meaning. <Name the
   files likely to meet, and any word or path the upper branch renamed that the fixes must take on.>
3. **Adapt any caller** a changed signature breaks, keeping its behavior.
4. <When the upper branch has a completeness check, run it; it must find nothing new.>
5. **Build and tests**, with `-p:NuGetAudit=false`: build, unit, bUnit, then integration once.
   Playwright too when either side touched a page. A failure the merge caused is fixed in the merge.
   Anything else: stop and report.
6. **Commit** with `git commit -F <file>`:
   - the subject `Merge remote-tracking branch 'origin/<lower branch>' into <upper branch>`;
   - a blank line, then one body line naming the fixes, and one on any conflict or adapted caller;
   - a blank line, then `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
7. **Push** with `git push origin HEAD:refs/heads/<upper branch>`. Never force. If it's rejected
   because the branch moved, stop and report.

Keep line endings: working-tree files are CRLF, so check `git diff`, and avoid `sed -i`. Never start
Aspire, run Worker jobs, touch a database other than Testcontainers, or touch Jira or the PR's threads.

## Report back
The merge sha and whether the push went through, each conflict and adapted caller with how you resolved
it, and the test results per run.
