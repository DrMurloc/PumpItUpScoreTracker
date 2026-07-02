# Phase 1: Safety nets

> Status: **[ ] Not started** · Last updated: 2026-05-16

Pre-flight work. Goal: make sure the Phoenix 2 migration can't silently corrupt Phoenix 1 data or fail silently. Nothing in this phase changes production behavior.

## Load these first (required)

Refuse to proceed if any of these are missing:

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`docs/phoenix2/features/phoenix-records-schema.md`](../features/phoenix-records-schema.md)
- [`docs/phoenix2/features/known-fragile.md`](../features/known-fragile.md)
- [`CLAUDE.md`](../../../CLAUDE.md) — section "Test conventions"
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) — section "Testing strategy"

## In scope

- Per-method integration tests asserting **"Phoenix 1 caller sees zero Phoenix 2 rows"** for every read-path repository method on `EFPhoenixRecordsRepository` (and `EFPlayerStatsRepository` once it's in scope). These tests are written *before* the `MixId` column lands, but they should pass against the current schema (since there's no Phoenix 2 data yet, the assertion is vacuously true — until the schema change, where it has to hold non-vacuously). Locked decision: **A4**.
- Smoke assertion on the weekly leaderboard import Hangfire job: "imported >0 scores at known levels." Locked decision: **I2**.
- Backfill migration dry-runs against a prod-sized SQL Server restore — verify wider unique index rebuild time and row counts.

## Out of scope (defer)

- Adding `MixId` columns — Phase 2
- Threading `MixEnum` through Application — Phase 2
- Any UI changes — Phase 2
- The actual Phoenix 2 scraper — Phase 3
- Derived-data audit pass — Phase 2 (audit) and Phase 4 (deferred tables)

## Locked decisions affecting this phase

- **A3** — Repository methods take `MixEnum mix` as required (no default). Tests in this phase are written *anticipating* that signature.
- **A4** — Mix isolation integration tests exist and pass *before* the column flips on in prod.
- **I2** — Smoke assertion is mix-agnostic ("scores at known levels imported >0").

## Tasks

1. [ ] **Mix isolation integration tests.**
   - For every public method on `IPhoenixRecordRepository`, add a test in `ScoreTracker.Tests.Integration/Repositories/`:
     - Seed: 5 Phoenix scores for a user. (No Phoenix 2 yet, so seeding Phoenix 2 needs the test to manually insert with the future `MixId` GUID — Phoenix 2's GUID, distinct from Phoenix's. Pre-create the Phoenix 2 mix row if needed for the test's setup.)
     - Call: the method with `MixEnum.Phoenix`.
     - Assert: zero Phoenix 2 rows present in the result.
   - Tests will run *vacuously green* until `MixId` exists; once the column lands they have to hold non-vacuously.
   - Reference the integration test pattern in [`CLAUDE.md`](../../../CLAUDE.md) — Testcontainers.MsSql + Respawn.

2. [ ] **Smoke assertion in `RecurringJobRunner`.**
   - File: [`ScoreTracker/HostedServices/RecurringJobRunner.cs`](../../../ScoreTracker/ScoreTracker/HostedServices/RecurringJobRunner.cs)
   - Method: the one that publishes `StartLeaderboardImportCommand`.
   - After the import completes (note: today this is fire-and-forget via `IBus.Publish`; the smoke check may need to live inside `OfficialLeaderboardSaga.Consume` instead, depending on shape).
   - Logic: count imported scores; assert >100 (tune); assert at least one chart at level ≥20 was observed.
   - On failure: `_logger.LogError` (and optional Discord webhook to admin channel — out of scope unless trivial).

3. [ ] **Backfill migration dry-run.**
   - Take a prod-sized restore (latest backup, restored to a dev DB).
   - Write the candidate migration SQL from [`phoenix-records-schema.md`](../features/phoenix-records-schema.md) (don't ship yet — this is verification only).
   - Apply and measure: index rebuild time, lock duration, disk usage delta.
   - Record findings here (Changelog below) for the Phase 2 cutover decision.

4. [ ] **Audit recurring-job error logging.**
   - Verify Hangfire job failures actually surface somewhere. If they currently fail to a void, decide whether to add Application Insights alerts or accept the silence as a deferred problem.

## Success criteria

- [ ] Integration test suite has per-method mix-isolation coverage for `IPhoenixRecordRepository`.
- [ ] Suite runs green (vacuously, pre-column) via `dotnet test ScoreTracker/ScoreTracker.Tests.Integration/ScoreTracker.Tests.Integration.csproj`.
- [ ] Smoke assertion is in place on the weekly leaderboard import; failure path tested manually (force-return-empty in a dev env, verify log appears).
- [ ] Backfill migration timing documented in this file's Changelog.

## Open questions

- Does the smoke assertion belong in `RecurringJobRunner` or inside `OfficialLeaderboardSaga.Consume`? The job runner publishes fire-and-forget; the saga is where the data actually arrives. Implementation note: probably the saga.
- What's the right threshold for "suspicious data"? Worth checking historical import counts to tune.

## Changelog

- 2026-05-16: Phase doc created from workshop.
