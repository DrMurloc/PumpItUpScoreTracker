# How PIU Scores keeps its specs

A **spec** is a short list of numbered rules saying what one feature does today - a page, an API
endpoint family, a recurring job, a Discord post - each rule with a worked example. People read a spec
to learn what a feature does without reading its code, and AI agents build and review code against it.

PIU Scores is **spec-first, and the spec wins**:

- **Spec-first.** A change to what a specced feature does is written into its spec and reviewed before
  any code is built. The code is then built on the same branch, and one pull request merges both, so
  the specs on `main` describe the code on `main`.
- **The spec wins.** Where the code and a spec disagree, the code is wrong. Whoever finds that outside
  their own task - a person or an agent - reports it rather than changing either side.

**New features start as specs, and the design docs are going away.** No specs have been extracted yet:
until a feature's design doc in [docs/design/](design/) is extracted into specs, it stays the record of
what the feature does and why, and its decision rows play the spec's part - written and reviewed before
the code. This page says how specs are kept.

## Where specs live

- **`docs/specs/<bounded-context>/<feature>.md`** - one folder per bounded context, named for its
  vertical in kebab case (`ScoreTracker.ScoreLedger` -> `score-ledger/`), one file per feature. Three
  more folders hold the rules many features share: `shell/` for every page, `api/` for every API
  caller, `jobs/` for every recurring job.
- **[docs/specs/README.md](specs/README.md)** is the index: every spec by folder, with its prefix, and
  the **Removed** register.
- **Finding the right spec:** by the feature's name in the index, or by a surface it claims.
- **Nothing else goes in `docs/specs/`.** Design rationale and decision history stay in
  [docs/design/](design/), words in [DOMAIN.md](DOMAIN.md), the standing architecture rules in
  [ARCHITECTURE.md](ARCHITECTURE.md) and [CLAUDE.md](../CLAUDE.md), and the vertical architecture's
  decisions in [adr/](adr/).

## What a spec looks like

```markdown
---
prefix: WEEKLY
claims:
  routes: [/WeeklyCharts]
  jobs: [update-weekly-charts]
---
# Weekly charts - the charts everyone plays this week

## Purpose

What the feature does and for whom, in a few sentences.

## Requirements

### Requirement: WEEKLY-001 The board rotates when its week ends
Each mix's weekly board MUST be rotated by the first daily run after its week expires, at 05:00 UTC
on a Monday.

#### Scenario: the Monday rotation
- **GIVEN** the Phoenix 2 board's week expires at 05:00 UTC on 2026-11-23
- **WHEN** `update-weekly-charts` runs at 05:00 UTC on 2026-11-23
- **THEN** the finishing placements are written to the placement history
- **AND** fresh charts are drawn, leaving out recently used ones
- **AND** the board's next expiration is 05:00 UTC on 2026-11-30
```

(An illustration of the shape, drawn from [SCHEDULED-JOBS.md](SCHEDULED-JOBS.md) - not an extracted
spec.)

- **Front matter:** the spec's `prefix`, and optionally the `claims` it makes, grouped by kind:
  `routes` (Blazor `@page` and Razor Pages routes, written as the route is), `api` (endpoints, as
  `GET api/v2/...`), `jobs` (recurring jobs), `discord` (bot posts and commands), `events` (bus
  messages the feature publishes). A surface is claimed by one spec at most.
- **One title, then `## Purpose`, then `## Requirements`.**
- **Each requirement** is a `### Requirement: <PREFIX>-<NNN> <name>` heading, one statement with MUST,
  MUST NOT, SHALL or SHALL NOT (never SHOULD or MAY), and at least one `#### Scenario:` with exactly one
  WHEN and at least one THEN.
- **Approving a spec confirms it.** No requirement carries a Status line: the owner's review is the
  confirmation, and an AI agent never confirms a rule on its own.
- **Rules at the level people talk about the feature,** not the level the code branches at. Rules that
  would always change together are one rule, with a table for their cases. A spec holds about 10 to 25
  rules, and 30 is the ceiling: past it, split the feature.
- **One name per setting.** A rule a configuration setting switches opens with a Where clause naming the
  setting the way every other spec names it: *Where recurring jobs are parked (`PreventRecurringJobs`)*.
  A spec never says whether a setting is on anywhere.
- **Other systems are named only where the feature works with them,** in one sentence saying that
  system's behavior is not described here (the official PIU site, Discord, YouTube).
- **Synthetic values** in every example: invented players, communities and scores, `example.com`
  addresses. Song and chart names are public catalog facts and may be real.
- **No pull request, issue or ticket numbers.** A spec says what is true; the history lives in git and
  the pull requests.
- **Content safety:** never a credential, key, token or connection string; no player's personal data
  (emails, real names, piugame logins); no hostnames or internal URLs; access rules say who may do what,
  never how to get around them; and a limit that would help someone abuse a feature is stated only as
  existing.

## Paths

Specs carry no `paths` yet: the index routes a change to its spec. Paths come back together with a check
that every glob matches a file.

## How a change flows

1. **Scoped** - the needs are settled. There is no branch yet.
2. **The spec change** - the branch is cut off `main`, and the requirements the change adds, changes or
   removes are committed there, touching `docs/specs/` only. The owner reviews them.
3. **The build** - the code is built on that same branch, to the spec.
4. **One pull request** merges spec and code together.

A change to no behavior - a refactor, a dependency bump - has no spec change. A bug fix usually adds the
scenario that would have caught the bug, and corrects the requirement when the spec described the bug
too.

## Tests name what they verify

A test that proves a requirement names it with xUnit's built-in trait: `[Trait("Verifies", "WEEKLY-001")]`.

- **One ID per trait.** A test that proves several requirements carries one trait for each.
- **Any tier** that CI runs may carry it - `ScoreTracker.Tests`, `Tests.Api`, `Tests.Components`,
  `Tests.Integration` or `Tests.E2E`. An end-to-end journey proves how the parts tie together rather than
  any one rule, so it need not name one. An exploration test never counts: no gate runs it.
- **Run one requirement's tests** with `dotnet test --filter "Verifies=WEEKLY-001"`.
- **Code that decides a requirement may cite its ID in a comment,** as code cites a design doc's D-rows
  today.

The traits are the map from requirements to their tests; nobody keeps one by hand.

## Every requirement has a test

Each requirement has at least one test that names it. The pull request that adds or changes a
requirement brings the test that proves it, and one that removes a requirement drops every `Verifies`
trait naming it.

## Changing, replacing or removing a requirement

- **Reworded,** or its values or scenarios changed: it keeps its ID.
- **Replaced** - its subject, trigger or outcome changed: it takes a new ID, and the old one is removed as
  replaced.
- **Removed:** it is deleted from the spec, and the index's Removed register gets a line,
  `- <ID> <former name> (removed <YYYY-MM-DD>): <reason>.`, ending `Replaced by <ID>.` when another
  replaces it. Retiring a whole feature records each of its IDs and its prefix.
- **IDs are never reused or renumbered once merged.** A new requirement takes its spec's next free number;
  when two branches take the same one, the second to merge takes the next free number.
- **A new spec** goes in its bounded context's folder, with a new prefix - two to eight capital letters or
  digits, starting with a letter - that no spec has declared or retired, and gets its line in the index.

## Findings

Writing or reviewing a spec turns up code that doesn't do what the spec says, or a rule that looks wrong.
Each finding is reported to the owner, citing the spec and the code. It ends one of three ways: the code
is fixed to match the spec, the spec is changed through a reviewed spec change, or the finding is
rejected. The spec never holds the finding's number.

## What holds it

**Nothing yet.** No check runs over `docs/specs/` today: the shape above, unique prefixes and IDs, the
Removed register, and a test for every requirement are held by review until the checks are built.
