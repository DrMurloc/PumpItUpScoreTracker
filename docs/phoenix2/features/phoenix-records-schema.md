# Feature: PhoenixRecords schema (and PlayerStats)

> Status: **design locked** · Last updated: 2026-05-16

The structural change that makes Phoenix 1 and Phoenix 2 scores coexist. Same pattern applies to `PlayerStats`.

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`mix-model.md`](mix-model.md)
- [`CLAUDE.md`](../../../CLAUDE.md) — section "One DbContext", section "Test conventions"
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) — section "Data access"

## Scope

- Adding `MixId` to `PhoenixRecords` (`PhoenixRecordEntity`) and `PlayerStats` (`PlayerStatsEntity`)
- Composite FK to `ChartMix(ChartId, MixId)` on `PhoenixRecords`
- Unique key expansion to `(UserId, ChartId, MixId)`
- Backfill existing rows to the `Phoenix` mix ID
- Repository signature changes (required `MixEnum` parameter, no defaults)
- Pre-flip integration tests asserting mix isolation

## Out of scope

- Weekly Charts / Tier List output tables — see [derived-data.md](derived-data.md)
- TournamentSession Mix column — see [qualifiers-mom.md](qualifiers-mom.md)
- Event schema changes that *consume* the new column — see [events.md](events.md)

## Locked decisions

- **A1** — `PhoenixRecords.MixId` carries a composite FK to `ChartMix(ChartId, MixId)`. The score is formally a child of "chart-in-this-mix," matching where per-mix note count already lives.
- **A2** — Add the `(ChartId, MixId)` secondary index alongside the new wider unique. SQL health is currently fine; preemptive is cheaper than retroactive.
- **A3** — Every `EFPhoenixRecordsRepository` method takes `MixEnum mix` as a **required** parameter. No default values. The compiler is the audit tool.
- **A4** — Before the column flips on in prod, the integration suite (`ScoreTracker.Tests.Integration`) has per-method coverage asserting "a Phoenix 1 caller sees zero Phoenix 2 rows" for every read-path repository method.
- **A5** — Existing rows backfill to `Phoenix` mix ID in the same migration that adds the column.
- **B1** — `PlayerStatsEntity` follows the same migration shape: add `MixId`, expand PK, backfill existing rows to `Phoenix`. Phoenix 1 forward — XX has its own table already (`BestAttempts`).

## Schema change shape

### PhoenixRecords

Current (approximate):

```sql
CREATE TABLE scores.PhoenixRecords (
    UserId UNIQUEIDENTIFIER NOT NULL,
    ChartId UNIQUEIDENTIFIER NOT NULL,
    Score INT NULL,
    Plate INT NULL,
    IsBroken BIT NOT NULL,
    RecordedDate DATETIME2 NOT NULL,
    -- ...
    CONSTRAINT PK_PhoenixRecords PRIMARY KEY (UserId, ChartId)
);
```

After:

```sql
ALTER TABLE scores.PhoenixRecords
    ADD MixId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PhoenixRecords_MixId
        DEFAULT '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B'; -- Phoenix mix ID

-- Backfill is implicit via the default; verify with a row count before dropping default.

ALTER TABLE scores.PhoenixRecords DROP CONSTRAINT DF_PhoenixRecords_MixId;

ALTER TABLE scores.PhoenixRecords DROP CONSTRAINT PK_PhoenixRecords;
ALTER TABLE scores.PhoenixRecords
    ADD CONSTRAINT PK_PhoenixRecords PRIMARY KEY (UserId, ChartId, MixId);

ALTER TABLE scores.PhoenixRecords
    ADD CONSTRAINT FK_PhoenixRecords_ChartMix
        FOREIGN KEY (ChartId, MixId) REFERENCES scores.ChartMix(ChartId, MixId);

CREATE INDEX IX_PhoenixRecords_ChartId_MixId
    ON scores.PhoenixRecords (ChartId, MixId);
```

EF Core migration mirrors this — generated with `dotnet ef migrations add AddMixIdToPhoenixRecords`.

### PlayerStats

Same shape:

```sql
ALTER TABLE scores.PlayerStats
    ADD MixId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PlayerStats_MixId
        DEFAULT '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B';

ALTER TABLE scores.PlayerStats DROP CONSTRAINT DF_PlayerStats_MixId;
ALTER TABLE scores.PlayerStats DROP CONSTRAINT PK_PlayerStats;
ALTER TABLE scores.PlayerStats
    ADD CONSTRAINT PK_PlayerStats PRIMARY KEY (UserId, MixId);
```

## Repository signature changes

Every method on [`IPhoenixRecordRepository`](../../../ScoreTracker/ScoreTracker.Domain/SecondaryPorts/) gets a `MixEnum mix` parameter. Same for `IPlayerStatsRepository`.

Example shape (illustrative, not exhaustive):

```csharp
// Before
Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(Guid userId, CancellationToken ct = default);

// After
Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(Guid userId, MixEnum mix, CancellationToken ct = default);
```

**No default values for the `MixEnum` parameter.** The compiler error is the migration audit tool — every caller has to be touched, deliberately.

## Files this touches

### Domain
- Modified: `ScoreTracker.Domain/SecondaryPorts/IPhoenixRecordRepository.cs`
- Modified: `ScoreTracker.Domain/SecondaryPorts/IPlayerStatsRepository.cs` (and any other stat-shaped ports)

### Data
- Modified: `ScoreTracker.Data/Persistence/Entities/PhoenixRecordEntity.cs` (add `MixId`)
- Modified: `ScoreTracker.Data/Persistence/Entities/PlayerStatsEntity.cs` (add `MixId`)
- Modified: `ScoreTracker.Data/Persistence/ChartAttemptDbContext.cs` (composite FK config)
- Modified: `ScoreTracker.Data/Repositories/EFPhoenixRecordsRepository.cs`
- Modified: `ScoreTracker.Data/Repositories/EFPlayerStatsRepository.cs` (or equivalent)
- New: `ScoreTracker.Data/Migrations/<timestamp>_AddMixIdToPhoenixRecords.cs`
- New: `ScoreTracker.Data/Migrations/<timestamp>_AddMixIdToPlayerStats.cs`

### Application
- Every handler that calls a touched repository method needs the `MixEnum` plumbed in. The compiler error list is the work list.

### Tests
- New: `ScoreTracker.Tests.Integration/Repositories/EFPhoenixRecordsRepositoryMixIsolationTests.cs` — one test per read-path method asserting Phoenix 1 callers see zero Phoenix 2 rows. See [phase-1-safety-nets.md](../phases/phase-1-safety-nets.md) — these are pre-flight, before the column ships.

## Risks

- **Wider unique index rebuild on a large table.** `PhoenixRecords` is the largest user-data table. The migration rebuilds the clustered index on every row. Test on a prod-sized restore before shipping; consider running off-hours if needed.
- **Silent mix-bleed if a query forgets the filter.** This is the failure mode the A4 integration tests defend against. Don't ship the column without those tests green.
- **Composite FK adds a join cost on insert.** Every score write now verifies `(ChartId, MixId)` exists in `ChartMix`. Negligible compared to the integrity guarantee, but worth measuring during pre-flip load testing.

## Open questions

None known. Ready to implement once Phase 1 (the safety net tests) is in place.

## Changelog

- 2026-05-16: Doc created from workshop.
