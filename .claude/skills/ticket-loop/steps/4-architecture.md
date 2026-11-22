# Phase 4 - Architecture: the engineer's pre-review

**Decides** how the approved scope gets built: which classes, where they live, how they connect, and
the words they are named in. **The bar:** the architecture fits the codebase and each name says what
its class does, so nobody needs to read every line to trust it. **Leaves out** a product recap and a
behavior recap - the approved scope, the mocks and the reviewed spec hold them.

It is the engineer's pre-review, not an architect's. The header line says so: "This is your
architectural pre-review: approve it if the structure and the names make sense to you. The
architecture docs are there if you want to check anything, and we can always iterate."

## Research - trace before you write

- **The rules:** `docs/ARCHITECTURE-CODE.md` (the contexts, the quad, the dependency rules, what goes
  in `.Contracts`, where a rule lives, feature folders), plus `docs/ARCHITECTURE-DATA.md`,
  `docs/ARCHITECTURE-UI.md` or `docs/ARCHITECTURE-TESTING.md` when the change touches their axis.
- **The effort's ADR:** when the ticket's epic or refactor has one, it is that effort's architectural
  decision - how it reads ARCHITECTURE*.md - so design inside it; a design that departs from it is a
  question. It is not governance: cite ARCHITECTURE*.md, never an ADR, as the codebase's rules.
- **The behavior this design has to deliver:** the reviewed spec change on the ticket's branch - the
  contract the code is held to - with the approved scope and mocks, and the specs in `docs/specs/`
  that claim the other surfaces it touches.
- **The code:** trace every flow the change touches, end to end, in the context's own pattern - where
  its rules actually run - rather than an assumed one. Grep the real blast radius; never trust a
  list. An Explore sub-agent can find where things are, but every file the plan changes, you open
  yourself: a file you have not opened cannot be in the plan.
- **The ticket's own technical scope** - a comment from product scoping - is a hint, not a plan: main
  has moved since, and product scoping doesn't dig as deep as this phase. Read it, then work from
  main. Where you land somewhere else, say so in one line; that line is how a constraint the ticket
  didn't flag gets caught. **Exception:** a detail the ticket marks as important - often for a reason
  that isn't obvious - binds like Decisions locked. List those under "Held from the ticket"; one that
  looks wrong becomes a question, never a workaround. A Validation comment's test list is a hint the
  same way; what it says must be proven still has to be proven.
- **Governance:** `governance_search` the Standards the change touches - boundaries, data, security,
  observability, testing, AI architecture - and cite them by `docPath`. Published binds; Drafting is
  directional. Astrid's own standing rules are in `docs/ARCHITECTURE.md` and its companions.
- **A design finding that would change what the approved scope promises,** or a requirement a spec
  states, is a feature question: ask it. An answer that changes a requirement amends the spec on the
  branch first, in a commit of its own.

## The message

The frame from SKILL.md, headed "Phase 4 of 8: Architecture". Tables over prose: name each class once,
in its table, and refer to it after that. A typical ticket's pre-review fits in about 1,000 words; a
big one runs longer through its tables, not its prose. Body sections in this order - Verticals, Data,
Pattern breaks and Dark deploy say "none" when empty, because that tells the reader something; skip
any other empty section.

1. **Summary** - the body's opening two or three sentences: which contexts and hosts change, what is
   new, and what is not (no new project, bus, event or external write, when that holds).
2. **Verticals** - the bounded contexts that change, and the ones only read, each with the
   `.Contracts` port it goes through (existing or new). The one rule: never another context's
   internals.
3. **The classes, by layer** - a table per changed context (Contracts / Domain / Application /
   Infrastructure - most contexts have no Application) and per host touched (`Hosts/{host}`,
   `Application/{host}.Application`). Each new, changed or deleted type appears once, with a
   one-line job; for a changed type, what changes. Name new types in the ubiquitous language - words
   the language gate (below) lets through - so the name says what it does without the code; a class
   name is no place to coin a word. A layer with nothing gets one line saying why. In a context
   converted to feature folders (`docs/ARCHITECTURE-CODE.md`, "Feature folders"; the converted list
   is in `Ratchets/SpecPathTests`), each new file goes in the feature folder of the spec it serves,
   which that spec's `paths` already cover, or which a new spec's glob joins with the folder's first
   file. Shared setup - the project files, `DependencyInjection.cs`, the DbContext and its
   design-time factory, `Migrations/` - stays where the context keeps it and belongs to no spec; any
   other file in no spec's paths fails the build. Elsewhere, a new file the feature lives in joins its
   spec's `paths` by name (`docs/SPECS.md`, "Paths").
4. **How it flows** - the actions that cross layers, one line each, by the class names above. For
   example: Save in `ReassignDialog` -> `ReassignRainforestTest` (Contracts) -> its handler (Domain)
   -> the store (Infrastructure) -> the page reloads the board. An action that shares an earlier
   path says so and gives only what differs.
5. **Rules and tests** - one table: each acceptance criterion, with the requirements the reviewed
   spec adds or changes for it (by ID), then each invariant or guard neither names; where the rule
   goes; the class that enforces it; and the test that proves it (tier and fixture; tiers below).
   **Where the rule goes** is its kind and its folder, by the placement table in
   `docs/ARCHITECTURE-CODE.md` ("Where a rule lives"): a decision, sync or read rule in
   `{ctx}.Domain/<Feature>/Rules/`, one rule per THEN clause that returns something different;
   a schedule, retry, overlap wait, access or what a toggle hides is "a list in code" - the Worker's
   `JobCatalog`, `McpToolAccess`, the Web's policies; what is on screen and how it reacts is "UI, not
   a rule". A rule kept elsewhere - a filter or count that stays in SQL, behavior already written as
   an aggregate class - says where and why. Then one line for the tests no rule owns - the ratchets,
   the browser round trips.
6. **Data** - tables, columns (type, null, default) and indexes; the owning schema; the medallion
   tier; the migrations (`docs/ARCHITECTURE-DATA.md`). A one-time data change takes its rung of the
   ladder there.
7. **Pattern breaks** - anything that bends ARCHITECTURE.md or a companion (CODE, DATA, TESTING, UI):
   a new project, a new `.Contracts` dependency, a cross-context read, a new Rabbit bus or event, a
   write to an external system, a new schema, a new host surface. Each with the governance Standard it
   touches. A strategic one - a new vertical, cross-context dependency, bus or schema, a change in
   technology, a new or changed codebase-wide pattern, a shared contract the whole codebase builds on,
   or a change to the dependency rules - gets talked through now and lands as an ADR, an amendment to
   the ADR of the ticket's epic or refactor when it has one (`docs/CONTRIBUTING.md`, policy 5): make it
   a question. The ADR starts from ARCHITECTURE*.md, names in its Related line the sections it
   interprets or extends, and records that decision only; behavior stays in the spec, and class
   shapes, ports and names stay in the ticket. A change the break would make to ARCHITECTURE*.md is a
   question of its own (SKILL.md, "Decision rules"). ARCHITECTURE*.md hold rules, not maps: a new
   vertical, AI caller or check changes them only when it changes a rule, and its listing goes in the docs
   that say what is (the contexts in `docs/DATABASE-SCHEMA.md`, the checks in `docs/RATCHETS.md`). A requirement that adds an event or a history
   table walks the ladder in `docs/ARCHITECTURE-CODE.md` ("Events and history - the ladder") here:
   each step it takes - decide and write rows, announce, keep history - with the "take it when" that
   holds. Event-sourcing is a strategic break: a question, and its own ADR.
8. **Dark deploy** (dark epics only) - each surface the toggle gates (a page or component, an MCP
   tool, a Slack post) and where its gate sits: at the surface, not deep in core logic. A back-end
   change is gated only when it meaningfully changes another surface, and then where it reaches that
   surface (SKILL.md, "Decision rules"). With the toggle off, users see what main shows today.
9. **Held from the ticket** - the flagged details you are treating as binding.
10. **Technical calls I'm taking** - "taking X unless you say otherwise".
11. **Check early** - each technical unknown (a framework behavior, a third-party fact) and how it
    gets proven before the code that depends on it.
12. **New words** - the gate (below): each term the design introduces, where it came from, and what
    it would replace. "None" is worth saying.

**What I need from you:** each new word, to approve or rename; each change to ARCHITECTURE*.md, by
itself; and the feature questions this phase surfaced - schema names, MCP tool names, config keys and
capability strings count, because people and other systems depend on them.

## Language ubiquity: the gate

The architecture is where the ubiquitous language is held, and so where most of DDD is enforced: a
word that names a class becomes a column, a tool, a label and a spec's term. **Every term a name or a
requirement uses must already be in use** - in `docs/GLOSSARY.md`, in a spec, in the bounded
context's own model, in the ticket (its acceptance criteria, Decisions locked, the engineer's words
in the session), or on a surface people already use. **A word the AI coined does not pass.**
"Corpus" is the warning: nobody chose it, and it spread through features anyway.

- **Each new term is a question,** listed at the checkpoint with where it came from. It enters
  `docs/GLOSSARY.md` with the first commit that uses it, and the pre-review's comment tags it
  *explicitly defined by engineer* or *suggested by AI, accepted by engineer*; a word is never
  *implicitly accepted* or *decided by AI mid-loop*. Until then, say it in words already in use.
- **One concept, one name.** A synonym for a term already in use is a new word too, and the existing
  term wins.
- **Inside one context, a word means one thing.** Two contexts may use the same word differently -
  that is what a bounded context allows - so a term that crosses a boundary names its context.
- **Words a person reads on screen are copy,** translated per `docs/ARCHITECTURE-UI.md` section 2 -
  its banned list included - never the model's names.
- **Industry terms pass;** only a coined or business-specific word is a question.

## Test tiers - `docs/HOW-TO-TEST.md`

Derive the tests from the behavior - the acceptance criteria and the scenarios of the requirements
the reviewed spec adds or changes - not from the implementation: each scenario is at least one test,
and a test that proves a requirement names it with `[Verifies("<ID>")]` - the bare ID, never in a
comment (`docs/SPECS.md`, `docs/HOW-TO-TEST.md`).
- Default: **unit** in `tests/DealerOn.Astrid.Tests` (NUnit, the ports faked, MassTransit's
  in-memory harness; no Docker). EF InMemory is shrink-only - `docs/ARCHITECTURE-TESTING.md`,
  "The InMemory rule", held by `Ratchets/InMemoryProviderTests` - so a new test never reaches for it.
- A rule in `Rules/`: **unit**, called directly with nothing faked - a scenario's GIVEN is its
  input, its THEN the return. A spec table of fixed words becomes its cases through `SpecTable`
  (`docs/HOW-TO-TEST.md`, "Reading a spec table into test cases").
- Page or component logic: the `.razor.cs` code-behind tested directly - still unit.
- What a component renders and how it reacts (markup, parameters, events): **bUnit** in
  `tests/DealerOn.Astrid.Tests.Components` - in-process, no browser, no Docker.
- A store, a DB constraint, a migration, or real SQL behavior: **integration** in
  `tests/DealerOn.Astrid.IntegrationTests` on Testcontainers - no `[Explicit]` and no category (the
  project boundary is the signal), run by plain `dotnet test`, needs Docker. InMemory enforces no
  constraints, so a unit test cannot prove any of these.
- A third-party system (Azure DevOps, Jira, the DealerOn database): never faked as a whole - fake its
  port above the adapter, and stub HTTP at the boundary where the tier needs it
  (`docs/ARCHITECTURE-TESTING.md`, "Real dependencies").
- Real browser interactivity: the `[Explicit]` Playwright tier.
- A third-party fact you cannot confirm from code: an exploration-test candidate - a question, never
  created without a yes (SKILL.md, "Decision rules").
- A new appsettings key: `ConfigAlignmentTests` fails until `docs/toggles.md` and the `BaseConfig`
  class match.
- A migration: an integration test that starts at the prior migration state and migrates forward,
  like `WorkItemSubjectMigrationTests`.
- An MCP tool: the catalog golden (`Mcp/McpToolSurfaceTests`) and `Ratchets/McpToolCatalogTests`
  move with it - a golden diff is breaking-change review.

Where a tier choice needs backing, cite the Testing Standard by `docPath`.

## An Epic: its ADR

An Epic isn't built itself, so its pre-review is its ADR - the effort's architectural decision
(`docs/CONTRIBUTING.md`, policy 5) - not a class list, which each story's pre-review decides.
- **Start from `docs/ARCHITECTURE.md` and its companions,** which answer most of the ADR's
  questions: point at what they already answer rather than deciding it again, and name in the ADR's
  Related line the sections it interprets or extends.
- **The ADR records how this effort reads them,** a new technology included, and only that:
  behavior stays in the epic and its specs.
- **A change the effort would need in ARCHITECTURE*.md** is a question of its own (SKILL.md,
  "Decision rules").

The checkpoint's body is the ADR's text, then its pattern breaks and new words. When it is done, the
move is "Commit the ADR, open its PR and point the epic at it?" - an Epic runs no later phase.

## Moving on

When you think the pre-review is done - no question open - the checkpoint asks one question: "Post
the pre-review on the ticket and move on to <next phase>?" The comment is headed **Technical scope -
architectural pre-review, approved by <engineer> on <date>**, provenance tags and the approved new
words included; skip the post for a tiny ticket when the engineer says so. The status stays where it
is.
