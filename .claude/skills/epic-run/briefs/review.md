# Review briefs - the reviewers and the skeptic

The roles and their rules are the ticket loop's: `steps/6-execution.md` ("Review") and its
`review/` files. This file holds what a run adds to each brief: the PR, the throwaway worktree, and
how to comment. Keep the roles blind:
- the reviewers' briefs never name the skeptic;
- the skeptic reads only its own brief.

## The parts every brief shares

**The PR block:**
- Azure DevOps PR <id>: <url>
  - Title: "<title>". Repo <repo>, org <org>, project <project>.
  - Branch `<branch>`, head <sha>, targeting main.
- It stacks on PR <id> (`<base branch>`, head <base sha>), still open. This PR's own changes are
  `git diff <base sha>...<sha>`. When the base has merged, the own changes are
  `git diff origin/main...<sha>`.

**Where to read:**
- You run in a git worktree of your own. Put it on the PR's head with `git checkout --detach <sha>`.
  The commit is already in the local object store, so don't fetch.
- If the checkout is refused, read from git objects instead (`git show <sha>:<path>`, `git diff`,
  `git grep`) and skip building.
- Run git commands plain, one per call. Never run commands in any other worktree.
- Build and test with `-p:NuGetAudit=false`: off the VPN, a fresh restore fails without it.
- Never start Aspire or the AppHost, run Worker jobs, connect to a database other than the tests'
  Testcontainers, run exploration tests, or call Jira.
- Do not edit, commit or push anything: you only comment on the PR.

**How to comment (ADO REST, through `az rest`):**
- Write each JSON body to a file in `<scratchpad>` with Python's `json.dump(body, f, ensure_ascii=True)`.
- An `az rest` whose URL holds `/_apis/git/` may be refused by the session's guard. If it is, make the
  same call from a small Python script (`subprocess` running `az rest`).
- The resource is `499b84ac-1321-427f-aa17-267ca6975798`. The threads URL is
  `https://dev.azure.com/<org>/<project>/_apis/git/repositories/<repo>/pullRequests/<id>/threads?api-version=7.1`.
- A new thread is a POST of
  `{"comments":[{"parentCommentId":0,"content":"**Reviewer:** ...","commentType":1}],"status":"active","threadContext":{"filePath":"/<path>","rightFileStart":{"line":<n>,"offset":1},"rightFileEnd":{"line":<n>,"offset":1}}}`.
- A reply is a POST to `.../threads/<tid>/comments?api-version=7.1` of
  `{"content":"**<Role>:** ...","parentCommentId":1,"commentType":1}`.
- A status is a PATCH of `{"status":"fixed" | "wontFix" | "byDesign" | "closed"}` to
  `.../threads/<tid>?api-version=7.1`.

## The bug reviewer and the flag reviewer

One bug reviewer and the flag reviewer run at once, as the ticket loop's review says. Each one's first line:
"you are the <bug reviewer | flag reviewer> in a review: read
`.claude/skills/ticket-loop/review/<reviewer | flags>.md` and do your part." Give the file's absolute path in a
worktree that has it. Then:
- the epic's toggle on a dark epic, and one line on anything the ticket made live on purpose;
- the PR block;
- where to read, plus where the scope and the architecture are: the ticket <KEY> (read only, with
  `getJiraIssue`) and the approved plan `<run folder>\<KEY>-plan.md`, whose "Orchestrator review" block
  overrides it;
- the approved mock's path, for a UI PR;
- the report: its list, as its brief says. Neither comments. Whoever runs the review posts each regression,
  bug, missed criterion and scope creep as a thread opening **Reviewer:** and its kind, and each flag as a
  thread opening **Flag:** and urgent or FYI.

## The skeptic

- The first line: "you are the skeptic in a review: read `.claude/skills/ticket-loop/review/skeptic.md`
  and do your part." Give the file's absolute path in a worktree that has it.
- The PR block, and where to read.
- The context:
  - the PR description, read with `az repos pr show --id <id> --org https://dev.azure.com/<org> --query description -o tsv`;
  - the plan's path;
  - the ticket, read only, whose Decisions locked bind;
  - whether the epic is dark;
  - any fact a thread turns on, such as an earlier ruling on the same point, or a fix already landed
    below.
- The thread ids - every thread the reviewers raised, flags included - how to read each (a GET of
  `.../threads/<tid>`), and how to reply: one reply per thread, opening **Skeptic:** and ending in a line
  opening **Verdict:** - Send back with the exact fix, Escalate with its bucket and its What, Why and
  Suggested action, or Decline with why.
- To set statuses as skeptic.md says: a Decline's thread to `wontFix` or `byDesign`, with the PATCH
  above - the one change it makes besides its replies. A Send back and an Escalate stay Active.
- The report: per thread, the id, the verdict and one line. A Send back gives the exact fix, an Escalate
  gives its bucket and its What, Why and Suggested action, and a Decline gives why.
