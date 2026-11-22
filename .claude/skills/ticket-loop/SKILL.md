---
name: ticket-loop
description: Astrid's house loop for ticket work - from a Jira key, or a feature idea with no ticket yet, to a built, field-tested, merge-ready PR and a closed ticket, one self-contained phase at a time. Use it whenever a session opens with "load up DO-NNNNN", "let's scope out X", "break this down", "let's build this", or an idea to scope (product scope files the ticket). Each phase has its own file in steps/, read only when that phase starts; a phase moves on only when the engineer says yes; every checkpoint ends with a numbered "What I need from you".
argument-hint: [JIRA-KEY or a feature idea]
---

# The ticket loop

The person running this may be new to Astrid and to the problem the ticket lives in. The loop is
the same every time, so it is learned once. It runs in phases, in this order:

| Phase | File |
|---|---|
| 1. Product scope - or understanding the ask of a ticket already scoped | [steps/1-product-scope.md](steps/1-product-scope.md) |
| 2. Mocks - suggested, never forced | [steps/2-mocks.md](steps/2-mocks.md) |
| 3. Spec - the ticket's spec change, on its branch, reviewed before any code | [steps/3-spec.md](steps/3-spec.md) |
| 4. Architecture | [steps/4-architecture.md](steps/4-architecture.md) |
| 5. Commit order | [steps/5-commit-order.md](steps/5-commit-order.md) |
| 6. Execution - the build on the spec's branch, the PR and its review | [steps/6-execution.md](steps/6-execution.md) |
| 7. Field testing | [steps/7-field-testing.md](steps/7-field-testing.md) |
| 8. Wrap up | [steps/8-wrap-up.md](steps/8-wrap-up.md) |

## How the phases move

- **Each phase is self-contained and blind to the ones after it.** Read a phase's file when that
  phase starts - never before - and do only that phase's work. This file names the phases and says
  nothing more about them. Inside a phase, never talk about what a later phase will do or decide
  ("we'll settle that in architecture", "the review will catch it"). What the current phase can't
  settle is a question now, or it stays out.
- **A phase iterates until it is done.** Every checkpoint (the frame below) waits for the engineer.
  While a phase has an open question, its checkpoint asks it, and "go" takes your recommendations
  and stays in the phase. When the engineer says "workshop", expect several rounds.
- **When you think a phase is done, ask whether to go into the next one.** The checkpoint recaps what
  the phase settled, and its one question is the move: "Move on to <next phase>?" Only a yes to that
  question moves on - "go" counts as a yes there, and only there. Never start a phase's work - a
  mock, a spec, a class list, a commit - before that yes. Two moves are the exceptions, below.
- **Execution hands off to the field test.** Once the review's verdicts are applied and no
  question is open, execution goes straight into field testing, with no question: its checkpoint is
  followed, in the same message, by field testing's first, which says how to field test it and ends
  "I'll wait on your field test." *(explicitly defined by engineer)*
- **The field test is the hand-off.** Once the engineer says field testing is done - nothing more to
  test, or the PR merged - the move into wrap up needs no question, and nothing after it waits on the
  engineer.
- **Only the engineer, in so many words, skips or merges phases** ("just give me the commit order").
  The loop never proposes it.
- **One session often runs the whole range,** from an idea with no ticket to a merge-ready PR. The
  phases are turns in one conversation, and the whole ticket should fit one context window.
- **A session can also stop at any checkpoint** - scope now, build later. What each phase settled
  goes on the ticket when it moves on - for product scope, only where it departed from the body - so
  the next session starts from it (Load).

## Ground rules

- **The engineer owns the decisions.** Product decisions are part of their ownership, and the
  architecture is *their* pre-review - no architect has to sign it off. You recommend; they decide.
  They can trust their judgment: the architecture docs are there to brush up on, and we can always
  iterate.
- **Iterate with quality.** A ticket is one high-quality iteration. Rounds inside a phase are
  normal. Anything past the acceptance criteria is **scope creep** - allowed, and labeled as such
  wherever it appears: the question that adds it, its commit, the PR description, the Jira comment.
  What doesn't fit is written down, never dropped: work for later as a follow-up ticket in the epic
  (offered), a gap we knowingly accept in `docs/ACCEPTED-TECH-DEBT.md`.
- **The spec leads, and the spec wins.** Astrid is spec-first (`docs/SPECS.md`): a ticket's spec
  change is committed and reviewed on its branch before any code, the code is built on that branch,
  and one PR merges both. Code that contradicts a requirement is a defect in the code; a
  contradiction outside the ticket's task is reported, never changed on either side. *(explicitly
  defined by engineer)*
- **Point at docs, don't restate them.** `docs/` is the source of truth, and a second copy here
  would drift. Name the doc that teaches a concept, gloss a term in half a sentence the first time
  it appears, and hold only the decision rules in this skill.
- **One context window per ticket.** Compacting mid-ticket loses the reasons behind earlier
  decisions. Broad searches go to an Explore sub-agent; only its conclusions come back. Read the part
  of a file you need, don't re-read what hasn't changed, and filter build and test output down to
  the failures. Suggest compacting only at a clean break - usually a scope-creep iteration starting
  - and only once the state is saved in commits, the PR description and Jira.

## The frame - every checkpoint

1. **Header:** `**DO-NNNNN - Phase 4 of 8: Architecture**` - a phase keeps its number when an earlier
   one is skipped. Before a ticket exists, name the idea instead of the key. Then one line on what
   this phase decides. The first checkpoint also says whether the epic merges dark or live.
2. **Body:** opens with two or three plain sentences, and asks nothing. An item that needs the
   reader points at its question ("question 3"); an item that needs nothing says so, or is cut.
   Never write "open loops" for whatever is left over - name the thing: *What the data showed*,
   *What I'm assuming*, *For later*. "Open loop" names one kind of unknown and nothing else
   (Unknowns, by kind). Nothing in it is about a later phase.
3. **What I need from you** - always last, always numbered:
   - **While the phase has an open question:** each question with your recommendation (on a dark
     epic, each also offers **Defer to a future ticket in the epic** - Decision rules); a mock or
     other artifact to review, by link; anything to do outside the chat - a person to ask, a data
     read to OK. Then: "Answer by number, or say **go** to take my recommendations."
   - **When you think the phase is done:** the recap, then one question - the move, with what it
     records on the ticket - and nothing else: "Post the pre-review on the ticket and move on to the
     commit order?"
   - **When execution is done:** no move question. Field testing's first checkpoint follows in the
     same message, and its "What I need from you" closes the message, ending "I'll wait on your
     field test."

   No pop-up question widgets - numbered plain text only.

## Load - the ticket and its family

**No ticket yet** (the session opened with an idea): start at product scope, which files the ticket.
Its first checkpoint names the epic the ticket will likely go under - dark or live follows from it -
and offers no Defer until that epic is settled.

**Given a key** - engineers usually hand over a **subtask**:
- Atlassian MCP `getJiraIssue` (`fields` including `comment`, `responseContentFormat: markdown`) for
  the key **and its parent** - Business Context, Acceptance Criteria and "Decisions locked" live on
  the story. Given a story, `searchJiraIssuesUsingJql` with `parent = <KEY>` gives its children and
  their statuses (what sibling lanes already did).
- **A sub-task's behavior is scoped on its parent:** its acceptance criteria and approved phases
  live on the story, and the sub-task's branch carries the spec change for only the requirements it
  builds.
- **The epic** (the story's parent): does it merge **dark** - behind a rollout toggle that is off in
  prod - or **live**? The epic's description or `docs/toggles.md` names the toggle, but both show
  defaults, not what prod runs today: when there is a toggle, ask at the first checkpoint whether it
  is still off in prod. Dark decides whether a question may be deferred.
- **Comments, by kind:** "Decisions locked" binds. A *Technical scope* comment written during product
  scoping is a hint. **The body is the product scope:** a ticket at Product Scoped or later finished
  it, and a **Product scope - deviations from the body** comment, when there is one, amends the body
  and is read with it. What else this loop posted counts as a finished phase: **Mocks - approved**,
  **Spec - reviewed** (its branch holds the spec commits), **Technical scope - architectural
  pre-review, approved** (the architecture - and the commit order too, when it carries one),
  **Commit order - approved**. What an earlier version of this loop posted counts the same - a
  **Product scope - approved** comment, or a plan headed "Plan (Checkpoint B)", say - and a **Spec
  diff - approved** comment counts as the spec reviewed, unless its commits edit the retired specs
  that sat directly in `docs/specs/`: then the spec phase runs again.
- **The first checkpoint is product scope as understanding the ask** (steps/1-product-scope.md):
  what the ticket asks, which phases its comments show finished, what changed on main since, and what
  that changes. Then the move into the first phase not finished. For a branch that already holds
  commits, merge main into it first.

**Which phases run:** product scope always - the full pass, or understanding the ask. Mocks are a
suggestion, never a gate: product scope's move offers them when the needs call for one
(steps/1-product-scope.md, "Moving on"), and they run only on the engineer's yes. The rest for every
ticket that gets built; an Epic isn't built itself - its stories are - so after product scope it
typically runs only the architecture, which writes its ADR. A phase that doesn't run is
named in the move that skips it. The spec is skipped only by a ticket that changes no behavior a spec
states or should state - a Refactor, a dependency bump: with nothing to review first, the move that
skips it also sets READY FOR WORK.

## Decision rules - apply throughout

- **Never re-open anything under "Decisions locked."** Not even when the code makes an alternative
  look attractive: whoever scoped the ticket already spent that argument, and re-opening it costs
  the epic, not just this ticket.
- **Verify the ticket's factual claims about the repo before building on them** - ADR numbers, what
  the AppHost forwards to which host, migration order, which call sites a deleted port really has
  (grep, do not trust the list). Correct them mechanically and say so.
- When you hit an **unrecorded decision**, classify it:
  - **Technical** - nothing a user sees or another system depends on changes (an encoding, a record
    shape, a DI shape, where a test lives, an internal name). Take your recommendation and list it at
    the current checkpoint as a technical call - or, forced while building, among the PR's
    deviations.
  - **Feature** - behavior, scope, a user-facing surface, or a name a person or another system
    depends on (capability strings, config keys, schema names, MCP tool names). Ask it as a numbered
    question with your recommendation. Where the codebase's conventions already dictate a name - the
    MCP host's paging parameters, say - follow them; that is not a question.
  - **A change to `docs/ARCHITECTURE*.md`** is never a technical call. It is a numbered question of
    its own, and it is written only on the engineer's explicit yes. *(explicitly defined by engineer)*
- **When approved sources disagree** - the acceptance criteria, an approved mock, an approved plan -
  don't pick one silently. Raise it as a question, recommending the newest unless something says
  otherwise.
- **Defer - dark epics, feature-level questions only.** When the epic merges dark, a question about
  what people see or do - a behavior, a surface's shape, copy, scope - also offers **Defer to a
  future ticket in the epic**, for when the engineer can't answer with confidence yet or needs to
  consult someone. Never recommend it by default: the decision stays theirs. Structure and names
  that ship whatever the toggle says - schema (the Worker applies every migration on startup),
  config keys, capability strings - are never deferred, and neither are technical calls. On a defer,
  build what doesn't depend on the answer; where something has to exist, use your recommendation as
  a placeholder behind the toggle and say so. The question lands on a later ticket in the epic - an
  existing one that fits, or a new one you offer to file - and the completion comment lists each
  deferred question with its ticket. A live epic offers no defer: the default would ship to users.
- **What a dark deploy gates: the surface.** Pages and components, MCP tools and Slack posts stay
  behind the toggle until they have been iterated on enough. Back-end changes stay ungated unless
  they meaningfully change another surface - rare, since most work stays inside its vertical - and
  then the gate sits where they reach that surface, not deep in core logic.
- **Third-party facts you cannot confirm from the code** - a Jira field id, an API payload shape, a
  tenant setting - are exploration-test territory (`tests/DealerOn.Astrid.ExplorationTests`, real
  credentials, real calls). Build to the most likely value, name the assumption, and ask to create
  the test; create it **only after a yes**, then run it once and act on what it shows. Compile-only
  fixes a rename forces on an existing exploration test are technical - make them and mention them.
- **Provenance.** Every decision that lands in a Jira comment carries one of four tags - never in a
  spec, which the engineer's approval confirms whole. The tags fall out of *where in the loop* the
  decision happened - nobody judges:

  | Where it was decided | Tag |
  |---|---|
  | An answer in the session, a subtask the engineer wrote, or an untagged "Decisions locked" entry | explicitly defined by engineer |
  | A numbered question the engineer answered | suggested by AI, accepted by engineer |
  | Written in a phase the engineer approved | implicitly accepted by engineer |
  | Forced mid-build, first surfaced after the build | decided by AI mid-loop |

  Guard on row three: not in an approved phase -> it is mid-loop, however obvious. A tagged
  "Decisions locked" entry keeps the tag written beside it: filing a decision there never upgrades
  it.

## Unknowns, by kind

Whatever a ticket still doesn't know is one of five kinds, sorted by who settles it *(explicitly
defined by engineer: product requirements, open loops and closed loops; suggested by AI, accepted
by engineer: technical decisions and outside inputs)*. Product scope names each one it finds, and
the title's tag (Jira, below) says what is left.

| Kind | What isn't known | Who settles it | What it holds back |
|---|---|---|---|
| **Product requirement** | What it does - often because no mock exists yet | The engineer, usually with a mock | Product Scoped, when it is the ticket's core and not a detail the spec settles; and any epic run |
| **Technical decision** | How it's built: a new pattern, where it lives, a new technology | The engineer, by talking it through: there is nothing to find out, only a choice | An epic run, until it is settled - the effort's big ones by its ADR. Not Product Scoped. A low-level call is never one: the build takes it and lists it |
| **Open loop** | How something will integrate, which only trying shows | An engineer, sitting down to explore and experiment | That ticket in an epic run, until the engineer is done - so it goes first |
| **Closed loop** | What "good enough" is, or a data risk to settle before integrating | AI, exploring and iterating on its own | Nothing |
| **Outside input** | An answer or access someone outside the team holds - another team's API, a grant, a key | Them | Going live, never the merge |

When none is left, the ticket is pure Development: writing the code, or turning an approved mock
into code, on patterns that already exist.

- **A closed loop needs something AI can check itself against each round** - the real data, worked
  examples, tests. When only a person can say "better or worse" each round, it is an open loop;
  prompt engineering can be either, by that test. Needing the engineer's yes to reach the data - an
  exploration test that costs money, a production read - keeps it closed.
- **An outside input never blocks the merge** *(explicitly defined by engineer)*. Write the ticket so
  its code merges without it: its criteria are met against the test environment and fakes, behind
  the epic's toggle wherever people would see it, and the Ask names who holds the input. When the
  answer would change what the feature does, it is a product requirement instead.

## Jira

Every Jira write - filing a ticket, posting a comment - is offered in "What I need from you" and done
on a yes, until the field-test hand-off: after it, wrap up's writes - the completion comment, the
close - are done, not offered. The engineer's Jira shape is theirs: subtasks or comments on the
story. Post on the ticket you were given.

**The status moves with the phases,** on the yes that moves a phase on - no extra question. Each
phase's file names the status its move sets. Walk the workflow one transition at a time
(`getTransitionsForJiraIssue` at each status; a Story goes NEW -> Analysis -> Product Scoped -> READY
FOR WORK -> Development), leaving optional screen fields empty, and never move a ticket backwards -
except, on the engineer's yes, a Story found at Product Scoped with a product requirement still open
at its core: Oops, then Restart (to NEW), then Analysis.
**Product Scoped** means ready to spec, with no spec branch yet; **READY FOR WORK** means the ticket's
branch is open with its spec change committed and reviewed by the engineer. *(explicitly defined by
engineer)* A subtask has its own, more granular workflow, and a Refactor its own too (NEW ->
Requirements Scoped -> Ready to Commit -> Development): pick the matching status.

**Filing a ticket** (a new story, a follow-up, a deferred question): put it under the epic whose
stories built the code being changed; only when none fits, under Post Beta Enhancements (<epic key>).
Re-list the epic's children right before filing and follow its naming (`Post Beta NN: ...`,
`Security lane NN: ...`) - other sessions number stories concurrently. A story's body: Business
Context, Ask, Acceptance Criteria, Out of Scope, Depends on, **Decisions locked (do not re-open
mid-flight)**.

**The title carries the ticket's tag** right after that prefix, with the matching label
*(explicitly defined by engineer)* - `Post Beta NN: (Closed Engineering) ...`:
- **(Open Engineering)**, label `open-engineering` - an open loop is left (Unknowns, by kind);
- **(Closed Engineering)**, label `closed-engineering` - only closed loops are left;
- **(Development)**, label `development` - neither.

Each loop the tag counts is a step of the Ask, marked as one: "**Engineering (closed loop), first:**
...". Decisions don't set the tag: a product requirement keeps the ticket out of Product Scoped, and
a technical decision is settled by talking it through. A stub keeps "(stub)" at the end of its
title; an epic and a subtask take no tag. The older "(Engineering)" says only that a loop is left:
the ticket's next load sorts it.

Number the Acceptance Criteria, adding a new one at the end and striking a dropped one through, so a
number always means the same criterion. A locked decision that is already a criterion cites it by
number and keeps its provenance tag - "AC 8 *(explicitly defined by engineer)*" - instead of
restating it. Work a team outside Astrid has to do goes in the Ask, not only under Depends on; an
epic, which has no Ask, gives that work a section of its own.

An epic is strategic and its stories are tactical. An epic's body holds the goal, what its users
get, what is in and out, the few decisions that shape it, and what other teams have to do, at the
level of who and what. Technical decisions, measurements and prerequisites go in the stories that
build them, and the architecture in the epic's ADR when it has one - how this effort reads
ARCHITECTURE*.md - or in ARCHITECTURE*.md where that already answers it. The epic points to them.

Product, stakeholders and other teams read the body too, so write it for them:
- The first sentence states the goal the way the engineer puts it: what changes, and for whom. Don't
  piece a "why" together from the details; an old tool's flaws and tech debt belong in the body.
- Say what happens, not a label for it: "every answer says how old its data is", not "the answers
  stay honest".
- Outside the Acceptance Criteria, an Astrid id or code name (a requirement ID, a job, a class) gets
  a half-sentence gloss the first time it appears.
- A number carries its unit and time window, and a date its reason and a source link, or it is left
  out. The facts carry any urgency, never adjectives.
- A fact from AI output (the context map, an AI review) says so: it is a signal until a person or a
  deterministic read confirms it.
