# Phase 5 - Commit order

**Decides** the order the approved architecture gets built in. **Leaves out** a design recap: refer to
the architecture's classes by name.

## Rules

- **The spec's branch:** the commits go on the ticket's branch, after its reviewed spec commits. A
  ticket with no spec change - a Refactor, a dependency bump - cuts the branch now, off `main`:
  `claude/<key-lower>-<slug>`, and the order says so in a line.
- **The spec is already written:** the order builds what its reviewed commits say, and no commit
  changes a requirement. A design need that would is a question now; its answer amends the spec
  first, in a commit of its own.
- **A commit is a slice of behavior with its tests and docs.** Never one commit per test, never the
  tests in a commit of their own. Every commit builds and passes the fast suites.
- **Unknowns first.** Each item from the architecture's "Check early" opens the commit that depends
  on it, with the fallback if it fails. A fallback that would change a feature is a question now.
- **Replacements before removals,** so every commit builds.
- **The tags ride their code.** Each commit names, in its body, the requirement IDs it implements;
  each test it brings that proves a requirement carries `[Verifies("<ID>")]`, the bare ID, never a
  comment; and each rule it brings carries `Implements <ID>` in its summary, in the folder the
  pre-review placed it.
- **The gap list** (`Ratchets/RequirementGapInventory.cs`): the branch's new requirement IDs go on
  it in the first build commit - the spec commits ran only the spec lint, so
  `RequirementCoverageTests` first meets them there - and each comes off in the commit that brings
  its test. The pull request leaves none of the IDs it builds on the list; an epic run's one spec
  branch leaves the later tickets' there for their own PRs. Only those IDs go on, never a list pasted
  over the old one; `PrintTheGapList` prints what the list lacks and what it no longer needs
  (`docs/HOW-TO-TEST.md`, "The spec ratchets").
- **Paths join with their files:** the commit that creates a file a spec's feature lives in adds its
  path to that spec - a new page, tool or job file by name, and, in a context converted to feature
  folders, a new spec's folder glob (`src/<Context>/*/<Feature>/**`) with the folder's first file. It
  changes the spec's paths and nothing else in it (`docs/SPECS.md`, "Paths"); `SpecPathTests` fails
  on a glob that matches no file.
- **The docs ride their code** - the commit whose mechanism they describe:
  - the ADR the ticket asks for or the pre-review settled with the engineer - an amendment to the ADR
    of the ticket's epic or refactor when it has one, otherwise a new one (`docs/adrs/`; never feature
    behavior or tactical design);
  - every row of CLAUDE.md's "Keep docs current" table the commit touches - MCP-TOOLS, toggles,
    DATABASE-SCHEMA, CAPABILITIES-AND-ROLES, WORKER-JOBS and the rest. A change to
    `docs/ARCHITECTURE*.md` is never routine upkeep: it rides only as the exact change the engineer
    approved as its own question (SKILL.md, "Decision rules").
- **Scope creep gets its own commit,** labeled in the message.
- **A big ticket names its clean break** - the commit after which compacting would lose nothing,
  should the context run short.

## The message

The frame from SKILL.md, headed "Phase 5 of 8: Commit order". Body:

1. **The branch** - one line: its name, and the commits already on it - the spec's, and any others.
2. **The commits**, numbered from the first after the spec's. Each: its conventional-commit title;
   the requirement IDs it implements; what changes, by the architecture's names; its tests (the
   fixtures, and the IDs they verify, which come off the gap list); its docs (the Keep-docs-current
   rows); and what you'd see after it, when there is something to see.
3. **Why this order** - dependencies, unknowns first, removals last.
4. **Overlaps** - other branches in flight that touch the same files, specs included, and how to
   handle them (merge main before commit 1; again after commit N if the other one lands first).

**What I need from you:** each question the order raised - a fallback that would change a feature,
an overlap that needs the engineer's call - with a recommendation.

## Moving on

When you think the order is done - no question open - the checkpoint asks one question: "Post the
commit order on the ticket and move on to <next phase>?" The comment is headed **Commit order -
approved by <engineer> on <date>**, provenance tags included; skip the post for a tiny ticket when
the engineer says so. On the yes, the ticket moves to **Development**.
