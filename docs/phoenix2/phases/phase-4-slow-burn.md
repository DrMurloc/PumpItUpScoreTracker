# Phase 4: Slow burn

> Status: **[ ] Not started** · Last updated: 2026-05-16

Post-launch work that doesn't block Phoenix 2 from shipping. Tackled at leisure as scope opens.

## Load these first (required)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`docs/phoenix2/features/derived-data.md`](../features/derived-data.md)
- [`docs/phoenix2/features/title-lists.md`](../features/title-lists.md)
- [`docs/phoenix2/features/known-fragile.md`](../features/known-fragile.md)
- [`docs/phoenix2/phases/phase-3-launch-week.md`](phase-3-launch-week.md) — verify it's done

## Prerequisites

- Phase 3 complete: Phoenix 2 is live, the `[LiveMix]` flip happened, the scraper works against PIU's Phoenix 2 site.

## In scope

- Derived-data tables that were deferred during Phase 2 (the "fully rebuilds each cycle" ones)
- DB-backed titles migration (if scope opens)
- Older-mix support (pre-XX), including the schema rework around DifficultyLevel/ChartType not existing in older mixes
- Scoring philosophy work for pre-Phoenix mixes (different score logic, plates may not exist, etc.)
- Scraper architectural rework — if the launch-week patches accumulated too much fragility, consider a fuller restructure
- Populating `Phoenix2TitleList` as the title list firms up
- Possibly: alerting on Hangfire job failures (if Phase 1's smoke assertion is judged insufficient)

## Out of scope

- Anything not listed above is either Phase 3 hotfix territory or genuinely not in this rollout.

## Locked decisions

None new — this phase is execution against decisions captured in feature docs. Specific items reference their feature docs by ID.

## Tasks (pick up as scope allows; no fixed order)

### Derived-data deferred items

1. [ ] **Sweep the derived-data audit catalog** (from Phase 2). For each table marked "fully rebuilds, defer":
   - Verify it's actually rebuilding cleanly post-Phoenix-2.
   - If a stale row from before the cutover is causing user-visible weirdness, migrate it now.
   - Otherwise leave alone until next natural touch.

### DB-backed titles

2. [ ] **Design** a `TitleDefinitions` table. Reference [title-lists.md](../features/title-lists.md) — the existing static classes (`PhoenixTitleList`, `Phoenix2TitleList`) are the seed data.

3. [ ] **Migration** to seed the table from the static classes.

4. [ ] **Admin UI** for editing titles (minimal — even a /hangfire-style admin page is fine).

5. [ ] **Replace static-class lookups** in `TitleSaga` with repo lookups.

6. [ ] **Remove `// TODO: DB-backed titles` comments** when done.

### Older-mix support

7. [ ] **Schema audit** for pre-Phoenix-1 mix support. Specifically:
   - `Chart` model has non-nullable `DifficultyLevel` and `ChartType`. Older mixes don't have these. Options: nullable, per-mix Chart projection, or older-mix-only entity.
   - Score logic differs per mix. Older score storage may need a separate table or a polymorphic Score concept.

8. [ ] **Decide on storage shape** based on the audit. Document in a new `docs/phoenix2/features/older-mixes.md`.

9. [ ] **Implement manual-entry pages** per supported older mix (sibling of `UploadXXScores.razor`).

10. [ ] **Mix-tag everything** so older-mix scores don't leak into Phoenix/Phoenix2 leaderboards.

### Scraper rework (only if Phase 3 patches are creaking)

11. [ ] **Evaluate scraper fragility post-Phase-3.** If hardcoded URLs grew, regex patches accumulated, and the scraper feels brittle, consider:
    - Centralizing all URLs in a config class
    - Replacing regex parsing with a proper HTML parser query language (HtmlAgilityPack already in use)
    - Adding contract tests against snapshot HTML samples

12. [ ] **Decide whether to act**, or accept the cost of the existing shape until a future mix forces the issue.

### Phoenix 2 title list population

13. [ ] **Populate `Phoenix2TitleList`** as the title set is documented post-launch. Iterative — each batch is a small PR.

### Cross-mix Tier List views

14. [ ] **Tier List cross-mix awareness.** Make the Tier List feature aware of scores from other mixes the user has played. Two variants worth considering (potentially both):
    - **Highlight previously-passed charts**: in Phoenix 2's Tier List, visually distinguish charts the user has passed in Phoenix 1 (even with no Phoenix 2 score yet). Especially useful in the early Phoenix 2 weeks when score history is sparse — players can see "I've already cleared this, just need to redo it."
    - **Best score across all mixes**: per chart, surface the user's best score across any mix it's been recorded in. Reduces "what's my actual peak on this chart" to a single number regardless of which mix it was set in.

    Cross-mix score comparison is only meaningful where scoring philosophy is consistent (Phoenix 1 ↔ Phoenix 2 — same Phoenix score model). XX and older mixes use different scoring; don't extend without re-thinking the comparison.

### Optional: alerting

15. [ ] **Hangfire job failure alerting.** If the Phase 1 smoke assertion proves insufficient (e.g., it fires too late, or only on the recurring job, missing ad-hoc imports), add a richer alerting pipeline:
    - Application Insights alerts on logged errors
    - Discord webhook on admin channel for any Hangfire job failure

## Success criteria

This phase doesn't have a binary "done" — it's a backlog. Items are individually shippable. Track progress here in the task list.

## Open questions

- **Older-mix score logic**: which historical mixes have well-defined score formulas, and which require approximation? The "complications" the user has acknowledged.
- **Scraper rework scope**: is the cost-of-change in the existing shape painful enough to justify rework, or is it cheaper to keep patching?

## Changelog

- 2026-05-16: Phase doc created from workshop.
- 2026-05-16: Added "Cross-mix Tier List views" — highlight previously-passed charts and/or best score across all mixes.
