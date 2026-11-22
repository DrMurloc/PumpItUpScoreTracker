# Phase 7 - Field testing

**Decides** whether the built feature does what the engineer needs, on their machine, with real data.
It iterates: the engineer tests, reports what they find, and each round's fixes land on the PR until
they have nothing more.

## The first checkpoint - how to field test it locally

The frame from SKILL.md, headed "Phase 7 of 8: Field testing". It comes straight after execution's
checkpoint, in the same message, with no question between them. Body:

1. **What to try** - the scenarios the spec commits added or changed, walked: each Given is the
   setup, each When where to click or what to call, each Then what should happen - with the
   scenarios no automated test verifies first.
2. **How to run it** - `docs/HOW-TO-RUN.md`, and the data it needs, cheapest first (below). On a dark
   epic, how to turn the toggle on locally, plus one look with it off, which should match main.

**What I need from you:** field test it locally and tell me what you find; and, when the feature
needs rows that don't exist yet, the offer to seed the local database - only then, never unasked. Its
last line: "I'll wait on your field test."

## Data for testing - cheapest first

- **Nothing** - `dotnet test` covers it.
- **A real-shaped, real-volume database** - import the latest production bacpac (`docs/HOW-TO-RUN.md`
  section 4, the fast path). Minutes; most of the time this is the answer.
- **Rows that do not exist yet, or a bug repro** - a SQL seed script under
  `tests/DealerOn.Astrid.IntegrationTests/<Area>/Seeds/` (`docs/HOW-TO-TEST.md`, "Test data"):
  idempotent, plain ASCII, one scenario per file, runnable by the integration test and by hand
  against `127.0.0.1,14330` (sqlcmd times out on `localhost`: the port listens on IPv4 only).
  Commit it with the test it feeds. Seeds fill **Astrid's own** schemas -
  never a fake of a third party's database.
- **From scratch** - the Dev Setup page bootstrap (`docs/HOW-TO-RUN.md` section 4). Slow, and needs
  the integration secrets; only when the bacpac will not do.

## Findings

- Each finding becomes a fix on this PR - a round of this phase, labeled scope creep when it adds
  scope - or a follow-up ticket in the epic (offered). A finding that changes what the feature should
  do amends the spec first, in its own commit, then the code.
- **New UI:** agree what it needs first - a question when that's unclear. Suggest a mock the way
  product scope does (steps/1-product-scope.md, "Moving on") and make one only on the engineer's yes
  (steps/2-mocks.md, "Building them"); with a mock, build the UI only once the mock is approved.
  While a mock awaits approval, don't build its UI; before any other edit, say in one line that it is
  not the mock.
- The fast suites stay green on every fix, and every fix is pushed.
- Run the review again (steps/6-execution.md, "Review") only after scope creep or a
  significant feature change.
- Each round's checkpoint: what each finding was, what changed for it, and what's left.

## Moving on

When every finding is fixed or ticketed and the engineer has nothing more to test, the field test is
done, and this is the hand-off. The engineer saying so - "field tested", "looks good", "I merged" - is
the move: go straight into the next phase, with no question. It means the iteration is done.
