# Phase 3 - Spec

**Decides** what the system will do once the ticket ships: the requirements its specs gain, change or
lose, each with its scenarios. Astrid is spec-first and the spec wins (`docs/SPECS.md`): the spec
change is written on the ticket's branch before any code, the engineer reviews it, and the code is
built to it. Approving it confirms every requirement in it. **Stays on** what someone outside the
code would notice - a page, a tool's answer, a job's schedule, a failure, an access rule. Nothing
here says how it gets built: no classes, layers, schema or tests.

**Runs** for every ticket that changes behavior a spec states, or adds behavior no spec describes yet.

## The branch

- **Cut the branch** off `main` - `claude/<key-lower>-<slug>` - unless the ticket already has one.
  The spec commits go on it now and the code later, and one PR merges both, so `main` never
  describes unbuilt behavior. *(explicitly defined by engineer)*
- **A sub-task's branch** carries only the requirements the sub-task builds.
- **The commits:** `docs(specs): <the behavior change, in a few words> (DO-NNNNN)`, touching
  `docs/specs/` only - and `docs/GLOSSARY.md` for a word the engineer chose. The one exception: a
  commit that removes or replaces an ID also drops every tag that names it - each test's
  `[Verifies("<that ID>")]` attribute (or an older comment tag) under `tests/`, and the ID in each
  rule's `Implements` tag under `src/` - and takes the ID off its spec's line in
  `Ratchets/RequirementGapInventory.cs`, touching only those. The old test no longer proves the new
  behavior, and the build tags its new tests `[Verifies("<the new ID>")]`: a test names a
  requirement with the attribute, never in a comment. Each commit passes `Ratchets/SpecRatchetTests`
  and is pushed.
- **New IDs wait for the build:** a spec commit runs only the spec lint, so the branch's new IDs go
  on the gap list at its first build commit, not here (`docs/SPECS.md`, "Every requirement has a
  test").

## Writing the spec change

`docs/SPECS.md` says how Astrid writes specs and points at the practice whose shape they take. The
rules a change trips on:

- **Which spec:** the one whose front matter's `claims` lists the surface - the page, MCP tool, job,
  Slack post or event - even when the ticket calls the feature something else; otherwise the index,
  `docs/specs/README.md`, by the feature's name. Behavior no spec describes yet starts a new spec
  (`docs/SPECS.md`).
- **Every acceptance criterion that changes behavior lands as at least one requirement,** and every
  requirement has a scenario. A criterion that can't be written as Given/When/Then isn't testable
  yet: make it a question.
- **Rules at the level people talk about the feature,** not the level the code branches at: rules
  that would always change together are one rule, with a table for their cases.
- **Fields are requirements:** a change that adds or renames fields lists each one - its name, what
  it holds, its values or where they come from. An approved mock's field list carries over as written.
- **IDs:** a new requirement takes the spec's next free number. A reworded one, or one whose values
  or scenarios change, keeps its ID; one whose subject, trigger or outcome changes takes a new ID, and
  the old one is removed as replaced. A removed one is deleted from the spec and recorded in the
  index's Removed register; its number never returns. Once merged, an ID is never reused or
  renumbered: two branches can take the same new number, so re-check the highest ID each time main
  is merged in - the branch that merges second takes the next free one.
- **Claims move with the surfaces:** a new page, MCP tool, job, Slack post or event joins the front
  matter's `claims`; a removed one leaves it.
- **Paths join with their files:** a spec commit lists no path whose file the build has yet to
  create - a new page, tool or job file, or a new spec's feature folder in a converted context
  (`src/<Context>/*/<Feature>/**`; the converted list is in `Ratchets/SpecPathTests`). A glob that
  matches no file fails the build, so the build commit that creates the file adds its path
  (`docs/SPECS.md`, "Paths").
- **Behind a toggle:** a requirement whose behavior a toggle switches opens with a Where clause, in
  the name the other specs give that toggle - *Where the security lane
  (`RemediationPipeline:SecurityLaneEnabled`) is on* - and never says whether it is on anywhere.
- **A Bug** usually meets a spec that already says the right thing: the change adds the scenario that
  would have caught it. When the spec was wrong too, it corrects the requirement.
- **Words:** every term a requirement uses is already in use - in `docs/GLOSSARY.md`, the bounded
  context's own model, the ticket, or on a surface people use. A coined or business-specific word is
  a question, with where it came from; a chosen one enters `docs/GLOSSARY.md` with this phase's
  commits. A code word - a `websites_*` tool's parameter, or an answer or contract field - is named
  in the requirement but joins the glossary's "Websites MCP words" tables with the code that speaks
  it, because `Ratchets/McpVocabularyTests` fails on a listed word no tool speaks yet. Industry terms
  pass.
- **No Status lines, Out lines, work-item keys or provenance tags:** the engineer's approval confirms
  the spec, and the ticket keeps the history.
- **The spec wins outside the ticket too:** where today's code contradicts a requirement this ticket
  doesn't change, report it under For later - never re-spec it or fix it here.

## The message

The frame from SKILL.md, headed "Phase 3 of 8: Spec". Body:

1. **The branch** - its name, its spec commits, and a link to its diff against `main`.
2. **The spec change**, by spec - each requirement added, changed or removed: its ID, name and
   statement, with its decision table where it has one. The scenarios are in the branch's diff.
3. **Removed or replaced** - each ID and the register line it gets.
4. **Settled in the writing** - each value or detail the ticket left open that the writing had to
   pick (a limit, a time window, what a person is told), so the review sees it.
5. **For later** - contradictions in today's code outside this ticket, and anything else that needs
   nothing from the reader.

**What I need from you:** the review - approving the spec confirms every requirement in it, and the
code will be held to it; each question the writing raised (a criterion that isn't testable, a new
word, a value nobody chose), with a recommendation (on a dark epic, each also offers Defer).

## Moving on

When you think the spec change is reviewed - no question open - the checkpoint asks one question:
"Post the spec on the ticket and move on to <next phase>?" The comment is headed **Spec - reviewed by
<engineer> on <date>**: the branch, and the requirement IDs added, changed or removed, by spec. On the
yes, the ticket moves to **READY FOR WORK** - its branch open, its spec change committed and reviewed
*(explicitly defined by engineer)*; one already past that status stays where it is.
