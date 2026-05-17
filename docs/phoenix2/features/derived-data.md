# Feature: Derived data tables (Weekly Charts, Tier Lists, Community Leaderboards)

> Status: **design locked** · Last updated: 2026-05-16

Tables that aggregate or project PhoenixRecords data. They need the same `MixId` treatment so Phoenix 1 and Phoenix 2 derived data stay independent.

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`mix-model.md`](mix-model.md)
- [`phoenix-records-schema.md`](phoenix-records-schema.md)

## Scope

- Weekly Chart entity (rotation per mix)
- Tier List output entities (rankings per mix)
- Community Leaderboard projections (per mix)
- Any other derived-data table that joins `PhoenixRecords`
- Audit pass after the primary schema lands

## Out of scope

- The primary `PhoenixRecords` schema change — see [phoenix-records-schema.md](phoenix-records-schema.md)
- Tournament/Match-shaped derived data — feature being dropped, don't touch

## Locked decisions

- **J1** — Phoenix 1 and Phoenix 2 derived data are entirely separate. Weekly Chart rotations, Tier List rankings, and Community Leaderboard projections each carry `MixId` and read/write per-mix.
- Same migration shape as PhoenixRecords: add `MixId`, expand PK, backfill existing rows to `Phoenix`.
- Tables that are **fully rebuilt each cycle** (e.g., a Tier List that's recomputed weekly from scratch) can defer to Phase 4 since stale data clears on the next rebuild. Tables that **persist across recomputes** (e.g., Weekly Chart rotation history) need the column before Phoenix 2 has data.

## Why this matters

Without `MixId` on these tables:

- Weekly Charts would mix Phoenix 1 and Phoenix 2 chart selections — the "what was last week's weekly chart" query returns nonsense across the mix boundary.
- Tier Lists would compute rankings across Phoenix 1 and Phoenix 2 scores combined — meaningless.
- Community Leaderboards would mix scores across both mixes — fixable post-hoc but ugly.

## Tables in scope (audit list)

To verify during implementation — grep for entities and DbSets:

- **Weekly Charts**
  - `WeeklyTournamentChart` / `WeeklyChart*` entities in `ScoreTracker.Data/Persistence/Entities/`
  - The rotation history table (whatever stores "chart X was the weekly chart for week N")
  - User progress against weekly charts (`UserWeeklyChartsProgressedEvent` writes here)
- **Tier Lists**
  - Tier list output entities (the persisted tier assignments per chart)
  - Difficulty-tier projections used by the Tier List UI
- **Community Leaderboards**
  - Community leaderboard projection tables
  - Community-specific rating snapshots
- **World Rankings**
  - World ranking snapshots / projections
  - Per-user world rank history
- **Pumbility / PUMBILITY+ projections** (if persisted; some may be query-time computed)

The audit during Phase 2 catalogs which actually exist, which need `MixId`, and which can defer.

## Migration shape (per table)

Same as PhoenixRecords:

```sql
ALTER TABLE scores.<Table>
    ADD MixId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_<Table>_MixId
        DEFAULT '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B'; -- Phoenix mix ID

ALTER TABLE scores.<Table> DROP CONSTRAINT DF_<Table>_MixId;

-- Drop old PK, add new one including MixId
ALTER TABLE scores.<Table> DROP CONSTRAINT PK_<Table>;
ALTER TABLE scores.<Table>
    ADD CONSTRAINT PK_<Table> PRIMARY KEY (<existing cols>, MixId);
```

## Files this touches

- Entities under `ScoreTracker.Data/Persistence/Entities/` — list firmed up during audit
- Their associated `EF*Repository` implementations
- The corresponding Domain ports — add required `MixEnum` parameter, same pattern as `IPhoenixRecordRepository`
- Queries/handlers that consume these — thread `Mix` through, default to LiveMix at the controller/page edge
- New EF migrations per table

## Risks

- **Forgotten table.** A derived-data table missed in the audit silently mixes data after Phoenix 2 has rows. Mitigate with an audit checklist in Phase 2 — list every entity that joins `PhoenixRecord` or `ChartId`, sweep each for whether it needs `MixId`.
- **Sequence with PhoenixRecords migration.** These migrations should land **after** PhoenixRecords gets its `MixId`, so the join behavior is consistent. Don't interleave.
- **Rebuilt-each-cycle tables.** Tempting to defer all of them, but the ones that *don't* fully rebuild (Weekly Chart rotation history) need `MixId` before Phoenix 2 has data. Audit carefully.

## Open questions

- Full entity list — resolved during Phase 2 audit.
- Are any of these tables already mix-tagged (e.g., via a join to `ChartMix`)? Verify; some may not need the column if they already resolve mix through their chart join.

## Changelog

- 2026-05-16: Doc created from workshop.
