# Phoenix 2 Roadmap

> Status: **planning** · Last updated: 2026-05-16

This is the high-level gameplan for how PIU Scores will evolve as Phoenix 2 releases. It's written for site users — Discord folks reading along, not just devs. Technical details for each feature stack live in [`docs/phoenix2/features/`](docs/phoenix2/features/); the actionable work plan lives in [`docs/phoenix2/phases/`](docs/phoenix2/phases/).

## The short version

- **Phoenix 1 scores stay.** Everything you've recorded continues to work and remains visible.
- **Phoenix 2 starts fresh.** Per how PIU has handled every prior mix, scores reset. We'll keep both mix's scores independently.
- **Every Phoenix 1 feature continues to work in Phoenix 2.** Weekly Charts, Tier Lists, Communities, M.o.M., Qualifiers, World Rankings, Titles — they all get per-mix support. You'll see different results when you switch your active mix at the top of the page.
- **You'll opt in to Phoenix 2.** When Phoenix 2 launches and the site is ready, an admin toggle flips on and the mix becomes selectable. Existing players stay on Phoenix 1 by default until they choose to switch.
- **March of Murlocs supports any mix.** When you register a session, you pick the mix. No mixed-mix sessions. PUMBILITY+ continues to be the M.o.M. scoring system.

## What's likely to change at PIU's end (informed speculation)

Based on previous mix releases:

- Player scores will reset on PIU's official site (we're keeping yours)
- Phoenix 1 webpages will either retire or redirect to Phoenix 2 data
- The title list will change, often in scoring logic too
- Many Phoenix 1 charts won't carry over; many new charts will arrive
- Rarely, a chart from before Phoenix 1 comes back (often polished)
- Some charts will get note-count or video updates (we handle these ad hoc)

What's almost certainly **not** going to change: difficulty levels, chart types, song metadata, the relationship of charts to songs, score logic, letter grades, plate logic.

What's a real wildcard: whether PIU's username/password import path keeps working. The site's import flow is built on scraping PIU's "my page." If that goes away, manual entry continues to work, but auto-import would need a redesign.

## How features will behave per mix

| Feature | Phoenix 1 | Phoenix 2 |
|---|---|---|
| Score recording | Continues, untouched | Fresh, independent rows |
| Weekly Charts | Continues its current rotation | Starts a new rotation with Phoenix 2's chart pool |
| Tier Lists | Stays based on Phoenix 1 score history | Rebuilds from scratch as Phoenix 2 data accumulates |
| Communities / Leaderboards | Continues | Independent per-mix views |
| March of Murlocs | Sessions are mix-tagged at creation | Sessions are mix-tagged at creation |
| Qualifiers | Mix is set per Qualifier config | Mix is set per Qualifier config |
| World Rankings | Continues for Phoenix 1 scores | Independent rankings once data accumulates |
| Titles | Existing Phoenix 1 title list | New Phoenix 2 title list (separate) |
| Discord notifications | Quiets once Phoenix 2 is the live mix | Fires for Phoenix 2 score events |
| Import | Existing Phoenix import page | New Phoenix 2 import page (shape depends on what PIU ships) |
| Older mixes (XX and pre-XX) | Manual entry continues; goal is to extend to even older mixes over time | Same — manual entry, mix-tagged |

## The "live mix" concept

The site has a notion of which mix is currently "live" — that's the one new players default to, the one Discord notifications fire for, and the one that controls a few other defaults. Mix changes happen once every couple of years, so this is a small flag on the mix enum itself rather than a runtime setting. When Phoenix 2 is ready to be the default, we move the flag and deploy.

While Phoenix 2 is in flight (added to the code but not yet the live mix), it'll be invisible in the mix selector. Once it's live, both mixes are selectable side by side.

## Score reset and partitioning

Phoenix 1 scores live in the same database table as Phoenix 2 scores will, but each row gets a mix tag. This is invisible to you — you'll never see Phoenix 1 and Phoenix 2 scores mixed together — but it means we don't have to delete or archive anything to "reset" for Phoenix 2. If PIU surprises us by carrying scores between mixes (unprecedented but possible), we'll handle that ad hoc.

## Tier list and Weekly Charts during the warmup

Tier lists, weekly chart placements, and world rankings will be sparse to nonexistent for Phoenix 2 in the first weeks. We'll keep the mix unselectable until enough scores accumulate that those features mean something, then announce in Discord when it's available.

## What we don't know yet

The big unknown is what PIU's website will look like at Phoenix 2 launch. Most of our import flow depends on scraping specific URLs and parsing specific HTML structure. If those URLs change shape or PIU rebuilds their player pages, the import flow has to be reworked. The plan accommodates this — most of the schema and feature work doesn't depend on the import side, so we can land most changes before Phoenix 2 ships, and tackle the import shape in the week after we see what the site looks like.

## Roadmap (action plan)

| Phase | When | What |
|---|---|---|
| [Phase 1: Safety nets](docs/phoenix2/phases/phase-1-safety-nets.md) | Now | Pre-flight test coverage and smoke checks so the migration can't silently corrupt Phoenix 1 data |
| [Phase 2: Pre-launch](docs/phoenix2/phases/phase-2-pre-launch.md) | Before Phoenix 2 ships | Schema, accessors, events, gating — everything that doesn't depend on what PIU's site looks like |
| [Phase 3: Launch week](docs/phoenix2/phases/phase-3-launch-week.md) | Week of Phoenix 2 release | Import-flow shape, defensive guards, the `[LiveMix]` flip, Discord communication |
| [Phase 4: Slow burn](docs/phoenix2/phases/phase-4-slow-burn.md) | Post-launch | Derived-data audits, older-mix support, DB-backed titles if scope opens |

## Feature deep-dives

- [Mix model](docs/phoenix2/features/mix-model.md) — `MixEnum`, the `[LiveMix]` attribute, selectability rules, user-selection vs live-mix accessors
- [PhoenixRecords schema](docs/phoenix2/features/phoenix-records-schema.md) — adding `MixId`, composite FK, backfill, repository signature changes
- [Events](docs/phoenix2/features/events.md) — adding `MixEnum Mix` to score-flow events, semantics
- [Notifications gating](docs/phoenix2/features/notifications-gating.md) — Discord/community side-effects fire only on the live mix
- [Qualifiers & M.o.M.](docs/phoenix2/features/qualifiers-mom.md) — session-level Mix, set-once invariant, Qualifiers config carrying Mix
- [Import flow](docs/phoenix2/features/import-flow.md) — explicit Mix on import command, silent-corruption guard
- [Derived data](docs/phoenix2/features/derived-data.md) — Weekly Charts, Tier Lists, Community Leaderboards per-mix
- [Title lists](docs/phoenix2/features/title-lists.md) — `Phoenix2TitleList` parallel to `PhoenixTitleList`
- [Known-fragile scraper spots](docs/phoenix2/features/known-fragile.md) — silent-failure inventory and smoke assertion plan

## Carve-outs

- **Tournament domain other than M.o.M. and Qualifiers is being dropped going into Phoenix 2.** The old Match-shaped tournament features won't be carried forward. Only March of Murlocs and Qualifiers remain mix-aware.
- **Pre-Phoenix mixes (older than XX) are an "eventually" goal**, not in scope for Phoenix 2 release. The complication is that Difficulty Level and Chart Type don't exist as concepts in those mixes, which requires schema rework that's intentionally out of scope here.
