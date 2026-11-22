# Phase 6 - Execution

**Build the approved commit order on the spec's branch, open the PR, and review it.** It runs
without checkpoints until the review's verdicts are applied; a feature question the build forces
is the exception - ask it, don't guess.

## The spec is the contract

The reviewed spec commits are already on the branch, and Astrid's spec wins (`docs/SPECS.md`): the
code is built to them, and the PR merges them with it.

- **A test that proves a requirement names it** with `[Verifies("<ID>")]` - the bare ID, one per
  attribute, never in a comment (`docs/HOW-TO-TEST.md`). `Ratchets/SpecRatchetTests` fails on a tag
  whose ID no spec holds.
- **Every requirement has a test:** `Ratchets/RequirementCoverageTests` fails on a requirement no
  test names unless it is on the gap list (`Ratchets/RequirementGapInventory.cs`), and on an ID
  still listed that a test now names. The branch's new IDs go on the list in its first build commit
  and come off as their tests land; the PR leaves none of the IDs it builds there.
- **Rules go where the pre-review placed them,** each carrying `/// <summary>Implements <ID>.</summary>`
  for the requirements it decides. `Ratchets/RulesArePureTests` fails on a rule that names a listed
  IO, wait or clock type, or one of the ports (an interface in a `Ports/` or `Queries/` folder) - a
  list, so a pass is no proof of purity: the shell reads the facts and the time, and passes them in
  (`docs/ARCHITECTURE-CODE.md`, "Where a rule lives").
- **A code word joins `docs/GLOSSARY.md` with the code that speaks it:**
  `Ratchets/McpVocabularyTests` fails on a listed word no tool speaks yet.
- **Code that contradicts a requirement is a defect in the code.** One in code this ticket changes
  is fixed here; one outside the ticket's task is reported - For later, a finding for the owning
  team (`docs/SPECS.md`) - never fixed or re-specced here.

## Build

- Work the commit order. One line before each commit says which one is starting.
- Every commit builds (`dotnet build DealerOn.Astrid.slnx`) and its fast suites are green - unit
  (ratchets included) and bUnit. **Push after every commit.** The slow tiers - integration and
  Playwright - never run per commit (`docs/HOW-TO-TEST.md`, "Running").
- The architecture's names and placement are the contract, and the reviewed spec is the behavior's.
  Note every deviation - a rename, a moved responsibility - for the PR description.
- **A requirement the build can't meet as written** is a feature question: ask it. The answer amends
  the spec in the same PR, in its own commit, and the PR description lists it as a deviation. A
  detail the build settles where a requirement left room, without changing what it promises, goes in
  the spec only when someone outside the code would notice it - in a commit of its own, listed under
  "Decided without you".
- **A change to the architecture itself** - a new cross-context dependency, a new project, a new bus
  - stops the build and asks.
- Scripts (`.ps1` / `.bicep` / `.cmd`) stay plain ASCII.

## Open the PR

**The trace first.** Run `./spec-trace.ps1` (`docs/HOW-TO-TEST.md`, "Tracing a branch to its
requirements") and answer each FLAG it prints: with a touched test that proves the requirement's new
words, or with a line in the PR description saying why none is needed. The flags are answered
before the review runs, so it starts from the trace.

`az repos pr create` targeting `main`. **Plain ASCII only** in the title and description - the az CLI
silently discards non-ASCII characters. Write the description to a file and pass it with
`--description @file`; ADO caps it at 4000 characters. The description says what changed, names the
requirements the spec commits add, change or remove, answers the trace's flags, links the reviewed
spec and the approved pre-review on the ticket, and lists the deviations and any scope creep. How to
test stays out of it.

## Review - once

Run it once the last commit is pushed and the PR is open, with the slow tiers green on the PR's head. A red
build or a failing tier is never reviewed: it goes back to whoever built the PR first.

**Skip it for a very small change** - one the engineer can read faster than the check runs: copy,
docs, a config value, a rename, a fix of a few lines. Never skip it when the diff touches
authorization, a toggle's gate, a schema or migration, or a write to stored data. A skipped check
is one line in the checkpoint's Bugs section, with the offer to run it.

Otherwise it is the review. It reports five kinds of finding *(explicitly defined by engineer, 2026-11-13)*:
- **Regression** - existing functionality that got worse. It quotes what worked before: the old code, a test,
  or a spec.
- **Bug** - newly built functionality that's broken, by the bar below.
- **Missed criterion** - an acceptance criterion not delivered; a critical criterion or journey no test would
  catch breaking; or a spec or doc the codebase keeps that the change left untrue - never one it doesn't keep.
  It is not a flag: it goes back to whoever built the PR like a bug. A rule the codebase writes down that the
  change breaks counts as one when the fix only brings the code back in line *(explicitly defined by engineer,
  2026-11-12)*.
- **Scope creep** - "Features/changes that were added that were NOT part of the original ask", the original
  ask being the ticket's ask, its criteria and its locked decisions *(explicitly defined by engineer,
  2026-11-13)*. A low-level call or a wording fix isn't scope creep; something a person or another system
  would notice is. It is not a flag, and it never goes back to whoever built the PR on its own: keeping or
  removing it is the engineer's call, and an unrequested surface - a reply, page, help text, tool or Slack
  post - is recommended for removal *(explicitly defined by engineer, 2026-11-10: "i don't want that. get rid
  of it.")*.
- **Flag** - rare: a place where a person's judgment is needed and there is code to review, "put your code
  review time here". It is never a fix to make or defer. Each is **urgent** - eyes on it before merge - or
  **FYI**.

Everything it keeps lands in one of two lanes *(explicitly defined by engineer, 2026-11-04)*:
- **Send back** - a real regression, bug or missed criterion this PR introduced, whose fix stays inside the
  ticket's scope and approved architecture, a plan that fails included when its fix only changes a number, a
  limit or a guard the plan named *(suggested by AI, accepted by engineer, 2026-11-12)*. Whoever built the PR
  fixes it, and the engineer never sees it. A review sends a PR back once, and once more for a problem its
  fixes caused.
- **Escalate** - what needs a person's eyes: every flag; all scope creep; a finding whose fix changes what a
  requirement or a locked decision promises, what a person sees or the approved architecture, a plan that
  fails included; a finding that hangs on something outside the repo; a problem the PR didn't introduce, only
  exposed, which stays with a person and never goes back to whoever built the PR *(suggested by AI, accepted
  by engineer, 2026-11-13)*; and anything else still wrong after the send-back.

Every escalation goes in one bucket, and sells itself in three short parts, a few lines with no paragraphs
*(explicitly defined by engineer, 2026-11-13)*:
- The buckets: **Fix or review before go-live** when the change ships behind a toggle, or **Fix or review
  before deploy** when it doesn't - every urgent flag goes here, and scope creep users or another system
  would see; **Not a blocker, still worth your eyes** - other scope creep goes here; and **Backlog, don't
  stress about it**. An FYI flag goes in one of the last two.
- The parts: **What** - the kind and the one-line problem, with file and line; **Why** - what breaks or who it
  affects, and why a person has to look; **Suggested action** - the concrete next step.

What is neither is declined and forgotten. The **skeptic** rules on every finding *(explicitly defined by
engineer, 2026-11-13)*: it merges findings that share a root, argues for simplicity, and sorts each into its
lane and each escalation into its bucket, or declines it. It may decline a flag that isn't worth a person's
time, and never softens one it keeps. Quality comes first: the check is sized for what it catches, not for what
it spends *(explicitly defined by engineer, 2026-11-02)*. A bug is best fixed by taking complexity out, and a
finding is a claim to weigh, not work to do *(explicitly defined by engineer, 2026-10-06)*.

- **Each role sees only its own brief,** in `review/`: [reviewer.md](../review/reviewer.md),
  [flags.md](../review/flags.md) and [skeptic.md](../review/skeptic.md), plus the writing rules every role
  shares, [writing.md](../review/writing.md). The reviewers never hear of the skeptic, and the skeptic reads
  only its own brief and the writing rules. No role reads this file: when each runs, and what happens to
  what it finds, is yours to know, not theirs.
- **Everything the review writes** - each finding, flag, reply and question, each thread you post and each
  line of the checkpoint - follows [writing.md](../review/writing.md): what it is first, then why it reaches
  the reader, then the suggested action or the details *(explicitly defined by engineer, 2026-11-13)*.
- Each is a general-purpose sub-agent. It gets the PR URL, the branch name and the branch it merges
  into, the worktree path, the ticket's key and where its approved pre-review is, the ticket's
  Decisions locked, the deviations the PR description lists, the epic's toggle when it ships dark,
  and one line: "you are the <bug reviewer | flag reviewer | skeptic> in a review: read
  `.claude/skills/ticket-loop/review/<reviewer | flags | skeptic>.md` and do your part."
  The skeptic also gets the ids of the threads it replies on, and how to reply on ADO threads and set
  a thread's status (below).

**1. The review.** One bug reviewer and the flag reviewer run at once *(explicitly defined by engineer,
2026-11-07: a second, blind bug reviewer is "just excessive")*. The bug reviewer finds regressions and bugs;
the flag reviewer walks the plan for missed criteria and scope creep, and raises the flags. They stay two,
blind to each other. Neither comments; each returns its list to you.

**2. You post it.** Post each regression, bug, missed criterion and scope creep as its own thread, opening
with **Reviewer:** and its kind. Post each flag as its own thread, opening with **Flag:**, urgent or FYI, and
naming who should look. Post the flag reviewer's "Couldn't check" line and the REVIEW.md rules it applied or
set aside as one closed thread.

**3. The skeptic** replies and rules on every thread. Each reply ends in its **Verdict:** line - Send back,
Escalate or Decline - and it sets a Decline's status. Its list comes back to you: per thread, the id, the
verdict and one line, and for each Escalate its bucket and its three parts. Read each Decline's line: a
Decline is only for a finding outside the bar or not worth a person's time, and a real bug is never declined
for being rare.

- All of them only comment on the PR's threads, so each thread reads finding -> skeptic, ending in its
  verdict -> outcome, and whoever approves the PR sees why something was or wasn't fixed.
  Everything posts as the engineer's az identity, so each comment names its role in bold
  (**Reviewer:**, **Flag:**, **Skeptic:** - whose reply ends in its **Verdict:** line - and
  **Outcome:**).
- ADO threads go through the REST API - `az devops invoke --area git --resource pullRequestThreads`
  or `az rest` with the az access token; `az repos pr` has no thread command. A new thread or a reply
  is POSTed; a status (`fixed`, `wontFix`, `byDesign`, `closed`) is a PATCH on the thread.

**What counts as a bug** *(explicitly defined by engineer, 2026-10-18)*: "the thing we built is
broken, would be considered an outage, would make people distrust the reliability of the tool, or
would generally leave a user scratching their head about what the tool is actually doing." Not a
bug: "Claude suggested functionality that solves a use case we never planned for but has zero impact
on the functionality that we DID plan for." A bug quotes the planned behavior it breaks - an
acceptance criterion, a spec requirement, a decision the ticket locks, or something that worked before
the change - and the input that really triggers it; a finding that can't is not raised *(suggested by
AI, accepted by engineer, 2026-11-02)*. Code that follows a plan which fails on a real input is a bug in
the plan, never declined: it takes its lane like any bug. A finding that hangs on something outside the repo
is settled by a person: it escalates, never declines. A path that isn't live yet still counts. A finding outside the bar is declined and forgotten: never
fixed in the PR, noted, escalated or filed. Each bug role's brief carries this bar.

**What a flag is:** rare, as above - the flags in [flags.md](../review/flags.md). What the flag reviewer
couldn't check is not a flag: it is the "Couldn't check" line in the report *(explicitly defined by engineer,
2026-11-13)*. Nor is what the ticket approved, unless the flag brings a fact the approval didn't weigh. A
codebase's REVIEW.md, read from the branch the PR merges into, can add a flag or strengthen one, never remove
or soften one *(explicitly defined by engineer, 2026-11-02)*.

**Then send back, once.** As whoever built the PR, apply every Send back together, exactly as the skeptic ruled
it: push the fixes, reply on each thread with what changed, and set it Fixed. A fix that turns out bigger than
ruled goes back to the skeptic. Then the review runs once more, on the fix commits only
(`git diff <head before>..<head after>`): the bug reviewer and the flag reviewer, with the skeptic on what they
find. What that run keeps: a problem a fix caused goes back to whoever built the PR once more; anything else is
escalated. There is no third round.

**Then escalate.** Each Escalate goes to the engineer at this checkpoint, in its bucket and its three parts;
an answer that changes behavior amends the spec first, in its own commit, then the code. A flag says where to
put code review time, and nothing here settles it. A Send back whose fix couldn't land is escalated too.

**When it runs again** - after scope creep or a significant feature change - it covers the commits since the
last check, with the slow tiers green on the new head first. The review posts only new threads, the skeptic
replies only on new threads, and that run gets its own send-back.

## The message

The frame from SKILL.md, headed "Phase 6 of 8: Execution". Body:

1. **What's built** - two or three sentences, and the PR link.
2. **Decided without you** - decisions forced mid-build and deviations from the pre-review and the
   reviewed spec, each with why. They are the only calls the engineer hasn't seen, so they come first.
3. **The spec** - one line naming the reviewed spec the build implements, then each spec change made
   since the review (an amended requirement, a detail the build settled), by ID and title.
4. **Where a person should look** - the escalations under their three buckets, in order, each in its three
   short parts: What, Why, Suggested action. Then the flag reviewer's "Couldn't check" line. A skipped check is
   the section's one line. What was sent back and fixed isn't listed: its threads on the PR hold it.
5. **For later** - follow-ups and deferred questions, with where each one went.

**What I need from you:** each escalation the skeptic returned, flags aside, in its bucket's order, as a
numbered question in the plain words it gave, with its suggested action as your recommendation. A flag asks
nothing: it stays in "Where a person should look".

## Moving on

When no question is open, execution is done, and the move needs no question: go straight into field
testing (steps/7-field-testing.md). Its first checkpoint follows this one in the same message, so this
one has no "What I need from you" of its own - field testing's closes the message, ending "I'll wait on
your field test." The engineer's field test is what the loop waits on now. *(explicitly defined by
engineer)*
