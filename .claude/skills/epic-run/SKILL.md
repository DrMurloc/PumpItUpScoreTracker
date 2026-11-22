---
name: epic-run
description: Run an epic's ready stories end to end with agents - plan, build, review, close - while the engineer only merges. Use it when the engineer says "work through everything that's well scoped and ready to go in the X epic", "send it on the epic" or "run the epic". One conversation at the start settles the run; after it nothing waits on the engineer until the end, escalations go to the chat, and one wrap-up ticket gathers them - or the final report, in a run of one or two tickets. Reuses the ticket loop's phases and review.
argument-hint: [EPIC-KEY]
---

# The epic run

You orchestrate. Agents plan, build and check; you review their work on the engineer's behalf, keep the run's
state, and talk to the engineer. The ticket loop (`.claude/skills/ticket-loop/`) defines every phase the agents
run; this skill strings them together across an epic. It writes down how the security lane MVP ran
(<epic key>, 2026-10-04 to 2026-10-06): four tickets, 14 pull requests, one wrap-up ticket (<ticket key>).

The engineer's rules for a run *(explicitly defined by engineer)*:
- "minimal, if any, deviations from the scopes we've already set in stone. Try not to make feature
  decisions mid-flight (i'd expect minor ones to happen), when in doubt, keep things simple + clean".
- One conversation at the start. After it, nothing waits on the engineer until the end; they watch the chat.
- The engineer merges each PR as they see it. Never merge, complete or auto-complete a PR.
- No waiting on field tests between tickets. A field-test fix lands on its ticket's branch and merges
  forward.

## 1. Start - the one conversation

Read the epic (`getJiraIssue`, markdown, with comments) and list its children
(`searchJiraIssuesUsingJql`, `parent = <EPIC>`: summary, status, issue type, and their Dependency
links). Then read what each one depends on - its links and its body's "Depends on" - inside the epic or
out. When the epic points to an ADR, read it too: it is the effort's architectural decision, and every
spec-reviewer and planner brief names it.

**The status narrows the search; a ticket's content decides whether it is ready** *(explicitly defined
by engineer)*. Keeping ready tickets at READY FOR WORK helps find what is ready. It never means a ticket
in another status goes unconsidered:

| Status | Read it as |
|---|---|
| **READY FOR WORK** | Probably ready: its branch should hold its spec change, reviewed. Check it before you trust it. |
| NEW, Analysis, Product Scoped | Check it too: it may be ready and never moved. A stub, or a ticket still being scoped with the engineer, is not. Product Scoped means ready to spec, with no spec branch yet. |
| Development or later | In flight or done. List it, don't touch it. |

A ticket is ready when nothing in it is left for a person; the ticket loop's kinds of unknowns
(`SKILL.md`, "Unknowns, by kind") sort what is. The title's tag hints at them, and the body decides.
Flag, never fix, any ticket that has:
- an open item or question in its body: a product requirement still open, or a technical decision
  the epic's ADR doesn't settle;
- a surface it changes with no approved mock (a "Mocks - approved" comment, or a mock its body names);
- an open loop - an exploration only a person can finish: one where Claude queries data and works
  with a person to refine something actionable *(explicitly defined by engineer)*.

A closed loop - an exploration Claude can run and act on alone - is part of the build, not a
blocker, once its strategy is signed off (below). An outside input never keeps a ticket out: its code
merges without it.

**The spec comes before the build** - Astrid is spec-first, and the spec wins (`docs/SPECS.md`). A
ticket's spec change is reviewed when a "Spec - reviewed" comment names its branch and the branch holds
the commits; an older "Spec diff - approved" comment counts only when its commits edit the specs in
the context folders, `docs/specs/<bounded-context>/`. A ticket that changes no behavior a spec
states - a Refactor, a dependency bump - needs none. Any other ticket that is otherwise ready is
**missing its spec change**:
- **Ask before writing it.** AI writes the spec change a ticket is missing only on the engineer's
  explicit approval, asked in the conversation *(explicitly defined by engineer)*. One yes covers
  every ticket the conversation names: nothing is written in the conversation, and each spec change
  is written just before its ticket's build and reviewed on the engineer's delegation before any
  code (section 2) *(explicitly defined by engineer, 2026-11-14)*.
- **A spec only an exploration can settle, rarely:** when an exploration has to run before anyone can
  know the behavior *(explicitly defined by engineer)*, AI writes the spec change after that
  exploration, on its ticket's branch, in commits of its own, before its code. The approval names
  that ticket at the start. Post it in the chat when it lands, and the engineer reviews it with the
  PR.

**A ready ticket outside READY FOR WORK moves there** once its spec change is reviewed, or it needs
none - above all one that unblocks a ticket in the run, such as a bug outside the epic that a story
depends on *(explicitly defined by engineer)*. Walk it at the start
([briefs/jira-helper.md](briefs/jira-helper.md)) and say so in the conversation; one whose spec
change AI writes moves there in its turn (section 2). A ticket that isn't ready stays out, and the
conversation names what is left in it for a person.

Then propose:
- **the run order:** Dependency links first, then the epic's own order. Each ticket's branch stacks on
  the previous ticket's head, and the first on main, so the order is also the stacking;
- **the specs:** each ticket's spec change - reviewed (its branch), missing (the one ask that
  covers them all), one only an exploration can settle (and why), or none needed;
- **the closed loops:** a strategy for each - the swarm's shape (how many agents, each doing what),
  the most rounds it gets, and what good enough is: what it checks itself against, and the bar. The
  run proposes, and the engineer signs each one off *(explicitly defined by engineer)*. A closed loop
  that reaches its last round short of good enough keeps its best result, and the shortfall is
  escalated: it is never a stop (section 5);
- **the exploration tests that cost money:** each one the run will need - an AI benchmark, a closed
  loop's runs - with its budget. The engineer approves them here, and the run spends inside each
  budget without asking again. A test that costs nothing needs no approval (the repo's `CLAUDE.md`,
  "Exploration tests") *(explicitly defined by engineer)*;
- **what stays out:** the engineer's own tickets, launch or ops work, anything they name.

Ask whether the epic merges dark, behind a rollout toggle that is off in prod (the ticket loop's
`SKILL.md`, "Load"). It decides the reviewer's toggle line and the close path.

That is the one conversation, in the ticket loop's checkpoint frame, ending with a numbered "What I
need from you". It settles the run and writes nothing *(explicitly defined by engineer, 2026-11-14)*:
it covers the send list and its flags, the order, which tickets are missing a spec change, the closed
loops' strategies, the money-costing tests and their budgets, what stays out, and dark or live. Every
spec change and plan waits for its ticket's turn (section 2). On the engineer's "send it", once every
ticket on the send list has its spec reviewed, needs none, or has the yes for AI to write it, every
closed loop has its strategy signed off, and every money-costing test has its budget, go: start the
run folder (The run's state, below) and the first ticket (section 2). "Send it" waits on no spec. A
story they workshop first, through the ticket loop's product scope, mocks and spec with them, joins
once nothing in it is left for a person.

## 2. Per ticket - the spec and the plan

**Ticket by ticket, in run order, at most one ticket ahead of the build** *(explicitly defined by
engineer, 2026-11-14: "we don't get that churn but instead just start working through each ticket as
if I said 'load up this ticket' with you orchestrating as me and passing anything worth my eyes into
wrap up tickets")*. When ticket N's build starts, ticket N+1's spec change (when it's missing) and its
plan start, and never more than that. The first ticket's spec change and plan start right after
"send it", its branch cut from main. For ticket N+1:
- **A spec writer** ([briefs/spec-writer.md](briefs/spec-writer.md)) writes its spec change, in
  commits of its own, on N+1's own branch, cut from N's branch head.
- **A spec reviewer** ([briefs/spec-reviewer.md](briefs/spec-reviewer.md)) reviews it on the
  engineer's delegation, against the ticket, the approved mock and the epic's ADR. You rule on each of
  its findings and the writer's questions, and the writer fixes what you keep, resumed by SendMessage.
  Only a finding that needs a mock or the ADR changed is escalated, to the wrap-up ticket (section 5),
  and the run goes on as specced.
- **A helper posts "Spec - reviewed"**, headed as reviewed on the engineer's delegation, and walks the
  ticket to READY FOR WORK ([briefs/jira-helper.md](briefs/jira-helper.md)).
- **Its planner** plans against the code beneath it as built, N's branch (below).
- **You review the plan,** and a helper posts the pre-review and the commit order (below).
- **Its build starts when N's PR opens,** as section 3 says.

The plan, for every ticket:
- **A planner agent per ticket** ([briefs/planner.md](briefs/planner.md)) writes the ticket loop's
  architecture pre-review and commit order against the ticket's reviewed spec, plus a file-by-file
  inventory for the builder, to `<KEY>-plan.md` in the run folder, one ticket at a time, never two at
  once. It plans against the code beneath it as built: the previous ticket's branch at its latest push
  (main, for the first). Each plan says what it assumes about the commits that build has yet to push -
  the previous ticket's plan names them - and which commit runs each of the ticket's closed loops, on
  the strategy the engineer signed off.
- **Review each plan on the engineer's behalf** before its build starts. Put an "Orchestrator review" block at
  its top, which overrides the plan where they differ. It records your adjustments and which decision
  points you take: the simplest option that keeps to the ticket. A planned departure from the ticket's
  scope, its reviewed spec or architecture is an escalation (section 5); build the simplest option
  while it waits.
- **A planned change to `docs/ARCHITECTURE*.md` is never approved on the engineer's behalf.** It is
  an escalation: the PR is built without it, and it lands only on the engineer's yes. A strategic
  break the plan finds is an escalation too; by default its answer amends the epic's ADR, when there
  is one, rather than starting a new one.
- **A helper posts the pre-review and the commit order on the ticket** as two comments, headed as
  approved on the engineer's delegation ([briefs/jira-helper.md](briefs/jira-helper.md)).
- **A Bug ticket skips the architecture.** Its planner writes the fix plan and the commit order only.
  *(explicitly defined by engineer)*
- **A ticket the plan splits into more than one PR gets one subtask per PR** *(explicitly defined by
  engineer)*:
  - File each subtask under the story before its PR's build starts, titled for the PR's theme, with a
    body saying what that PR builds: the plan's slice of the commit order.
  - The PR's branch, commits and title carry the subtask's key, not the story's. The first PR's
    branch is cut from the story's own branch, so the reviewed spec rides it.
  - A subtask walks the short way: NEW, then in progress when its build starts, then Closed (Done) when
    its PR merges. List the transitions at each status, and skip the granular steps.
  - A ticket with one PR stays on the story's key.

## 3. Per PR - the build

- **One builder agent per PR** ([briefs/builder.md](briefs/builder.md)), each started in a worktree of its own
  (the Agent tool's `isolation: "worktree"`), which stays its own until its PR merges, so its send backs land there.
  Its branch is the ticket's own branch, holding its reviewed spec commits, stacked on the previous
  ticket's head (the first on main), and it merges that head again when it starts. A split ticket's
  PRs take branches of their own (section 2): the first cut from the ticket's branch, each later one
  off the PR before it. It:
  - walks its ticket or subtask to Development when it starts;
  - builds the plan's commits on the spec, with the fast suites green and a push after each one. The
    first build commit on a branch puts that branch's new requirement IDs on the gap list
    (`Ratchets/RequirementGapInventory.cs`), and each comes off with its test;
  - merges its base again, then runs the slow tiers once;
  - runs `spec-trace.ps1` and answers each flag on a requirement it builds, with a touched test or a
    line in the PR description;
  - opens the PR against main, naming what it stacks on.
- **The plan is the contract, and the reviewed spec is the behavior's.** Deviations go in the PR
  description. A requirement the builder can't meet as written, or a change to the architecture
  itself, stops it and comes back to you. The spec wins: code outside the builder's task that
  contradicts a requirement is reported to you, never changed on either side.
- **Pipelining:** when a PR opens, its review starts, and the next PR's build starts on top of it.
- **Merge forward:**
  - a fix pushed to a lower PR is merged into the branches above it by a merge agent
    ([briefs/merge-forward.md](briefs/merge-forward.md));
  - a builder merges its base again before its slow tiers.
- **When the engineer merges a PR, its branch is deleted:** merge `origin/main` instead.

## 4. Per PR - the review

The ticket loop's review (`steps/6-execution.md`, "Review"): the bug reviewer finds regressions and bugs,
the flag reviewer finds missed criteria, scope creep and the rare flags, and the skeptic argues for simplicity
and rules on every finding - Send back, Escalate or Decline. The PR's own builder applies the Send backs
([briefs/send-back.md](briefs/send-back.md)).
- **Setup.** Each role runs in a throwaway worktree of its own. [briefs/review.md](briefs/review.md)
  has the setup each brief adds.
- **Keep the roles blind**, as the ticket loop says:
  - the reviewers never hear of the skeptic;
  - the skeptic reads only its own brief.

  Never brief this file's rules to a role.
- **The skeptic rules** on every thread. Read each Decline's line.
- **Where it goes:** each Escalate, every flag among them, goes to section 5 and stays an Active thread on the
  PR, where the engineer reads it before merging. Each Send back goes back to the PR's own builder, never to
  the engineer as a question *(explicitly defined by engineer, 2026-11-07: "proposed fixes that don't require
  my eyes go back to worker")*:
  - **The builder that built the PR** makes the fixes, resumed by SendMessage so it keeps its context, as commits
    on its PR's branch - never a fresh fixer agent, never a separate PR *(explicitly defined by engineer,
    2026-11-09: "'send back to worker' means literally 'send back to worker'")*. Start a fresh builder only if
    the original is gone or out of context, from its handoff notes.
  - **It works in its own worktree,** where it built the PR (section 3): a session pinned to one worktree can't
    write in another, and a resumed agent keeps its own. The fixes merge forward as section 3 says.
  - **What the fix check keeps** - the review run again on the fix commits: a problem a fix caused goes back to
    the builder once more, the same way; anything else goes to section 5. There is no third round.

## 5. Escalations - to the wrap-up ticket

What gets escalated:
- the skeptic's Escalate verdicts, and anything else the fix check keeps (section 4);
- a spec review's finding that needs a mock or the epic's ADR changed (section 2), while the run goes
  on as specced;
- a plan's or a build's departure from a ticket's accepted Ask, ACs, Decisions locked, reviewed spec
  or approved architecture;
- code outside an agent's task that contradicts a requirement, which the agent reported rather than
  changing either side;
- a change to `docs/ARCHITECTURE*.md` a plan or a build needs, and a strategic break a plan finds;
- a closed loop that ran out of rounds short of good enough: its best result, and how far short it
  landed;
- anything else that is the engineer's call.

What a bug is *(explicitly defined by engineer, 2026-10-18)*: "the thing we built is broken, would
be considered an outage, would make people distrust the reliability of the tool, or would generally
leave a user scratching their head about what the tool is actually doing." A built behavior that
fails under conditions that really happen counts: bad data that exists, a timeout, a restart mid-job.
Not a bug: "Claude suggested functionality that solves a use case we never planned for but has zero
impact on the functionality that we DID plan for" - it is never escalated, noted or filed. Copy, UX
refinements and departures from a mock are for mocks and the field test, not escalations. A call the
spec writing or the build took goes in the completion comment with its provenance tag.

**The run stops and waits only for something critical found mid-build** - a review finding marked
stop-the-flow. Everything else moves forward, as close to the plan as it can, and what needs the engineer's
eyes goes onto the wrap-up ticket - or into the final report, in a run of one or two tickets (section 7) -
never as a question in the chat *(explicitly defined by engineer, 2026-11-07)*. Mention each escalation in the
chat in one line when it arises - what it is, its bucket and your recommendation - and ask nothing:
- the PR stands as built (a bug that would hurt prod at merge is never escalated: the skeptic sends it
  back now);
- when the engineer answers anyway, the change lands on its branch and merges forward.

Record each one, with its bucket, in the run log's Escalations list and on the wrap-up ticket, when the run
has one, the moment it is raised, and add the engineer's answer when they give one, so nothing is lost
mid-chat or mid-context.

## 6. Merges and closes

- **Check for the engineer's merges** when an agent reports, and on their word (`git fetch`, then main's merge
  commits).
- **When a ticket's last PR merges:**
  - post its completion comment in the shape of the ticket loop's `steps/8-wrap-up.md`;
  - close it, through Toggle Release when the epic merges dark, with Resolution = Done
    ([briefs/jira-helper.md](briefs/jira-helper.md)).
- **A subtask closes when its own PR merges.** The story's completion comment lists its subtasks and
  their PRs.

## 7. The wrap-up ticket

One wrap-up ticket under the epic, as a Bug, gathers everything escalated to the chat *(explicitly defined by
engineer)*. It is filed when its first item lands, amended as items land, and prioritized at the end *(explicitly
defined by engineer, 2026-11-10: "I expect wrapup tickets to get opened as soon as there's something to put on
them, and we can ammend through the build and prioritize at the end")*. Its sections are the review's three
buckets, in this order *(explicitly defined by engineer, 2026-11-13)*:
1. **Fix or review before go-live** when the epic ships behind a toggle, or **Fix or review before deploy**
   when it doesn't: every urgent flag, scope creep users or another system would see, and what would wrong a
   person or stored data once people use it;
2. **Not a blocker, still worth your eyes:** other scope creep, keep-or-revert calls on scope deviations,
   smaller feature calls, FYI flags worth a look;
3. **Backlog, don't stress about it:** worth doing, and nothing goes wrong while it waits.

Each item sells itself in three short parts, a few lines with no paragraphs, written as the review's writing
rules say ([../ticket-loop/review/writing.md](../ticket-loop/review/writing.md)): **What** - the kind and the
one-line problem, with file and line; **Why** - what breaks or who it affects, and why a person has to look;
**Suggested action** - the concrete next step. A scope deviation's What says what the scope said and what was
built, and its Suggested action is keep or revert; for scope creep, remove it, or keep it and add it to the
ticket's criteria and spec. Items the engineer already answered in the chat are listed
with their answer. When the last PR's review is done, check each item's bucket, then add the go-live steps.

A run of one or two tickets files no wrap-up ticket *(explicitly defined by engineer, 2026-11-13)*: once its PRs
are ready to merge, its escalations come out in the chat with the final report, in the same buckets and parts.

Then clean up the run's throwaway worktrees after a look at each, and mark the run log finished.

## The run's state

- **The run folder** lives outside the repo: `notes/<epic-slug>/<YYYY-MM-DD>-run/`, in the folder
  above your git repos. It holds:
  - `run-log.md`, started from [run-log-template.md](run-log-template.md). Its "Resume here" block
    gives every ticket's and PR's state, the worktrees, the next steps and the escalations. Keep it
    current after every step: plan reviewed, PR opened, review ruled, fix pushed, merge seen, ticket
    closed.
  - `<KEY>-plan.md` for each ticket;
  - drafts: PR descriptions, completion comments, the wrap-up.
- **A memory entry points at the run log**, so a reloaded session finds it.
- **The files are the state of record**; the chat may be lost. On resume, read "Resume here", then
  confirm the live state before acting: `git fetch`, the PRs, the worktrees. The engineer merges between
  sessions.

## Pause

"Hold on" stops the run:
- every running agent finishes the commit it's on, pushes it, stops and reports;
- a half-done edit goes to a local `wip/` branch, never a half-built push;
- "Resume here" then says exactly where each one stopped.

## Worktrees and the environment

- **Spec writers and spec reviewers** each run in a worktree of their own (`isolation: "worktree"`),
  detached. A writer starts at the head its ticket's branch is cut from, or at the branch's head when
  it has one, and pushes to the ticket's branch with `git push origin HEAD:refs/heads/<branch>`, never
  force; it keeps the worktree until its spec is reviewed, for the fixes. A reviewer runs
  `git checkout --detach <head>` at the spec's head. Remove both with the throwaways, after a look.
- **Each builder** runs in a worktree of its own (`isolation: "worktree"`), detached, and pushes
  with `git push origin HEAD:refs/heads/<branch>`, never force. It keeps the worktree until its PR merges, for
  its send backs; remove it with the throwaways, after a look.
- **Review roles and merge agents** each get a throwaway worktree (the Agent tool's
  `isolation: "worktree"`), and run `git checkout --detach <head>` there.
  - If a checkout is refused, a read-only role reads from git objects (`git show <sha>:<path>`).
- **A builder on a send back** works in its own worktree (section 3). If a command there is refused, it stops
  and reports.
- **A session pinned to one worktree** (one that entered it with EnterWorktree) is held there by a
  guard:
  - git commands stay inside it: no `git -C <other worktree>`;
  - a command line that pipes text naming git, or an `az rest` URL holding `/_apis/git/`, goes in a
    Python script file run with `python <file>`.
- **Off the VPN,** every `dotnet build` and `dotnet test` in a fresh worktree takes
  `-p:NuGetAudit=false`. Without it, the audit fails the restore (NU1900).
- **Azure DevOps:**
  - PRs and threads go through `az rest`, with JSON bodies written by Python
    `json.dump(..., ensure_ascii=True)`;
  - a description is at most 4000 characters;
  - re-read a stored description: if the trailer's emoji was dropped, send it as `&#129302;`.
- **Jira writes go through a small helper agent,** since a transition's response echoes the whole
  ticket. A bold lead carries no code span, because one ends the bold early.
- **Claims about real-world data** come from the engineer or the data, never from code comments or generated
  scripts. That covers what a table holds and who used a feature.

## Briefs

[briefs/](briefs/) holds one template per role, filled in for each agent: spec-writer, spec-reviewer,
planner, builder, review (the setup the reviewer and skeptic briefs add to the ticket loop's),
send-back (the message a builder gets for its send backs), merge-forward and jira-helper.
