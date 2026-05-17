# Phase 2: Pre-launch

> Status: **[ ] Not started** · Last updated: 2026-05-16

Everything that can land before Phoenix 2 ships, none of which depends on what PIU's site looks like. After this phase, the codebase is ready for Phoenix 2; only the `[LiveMix]` attribute and the import-flow shape remain.

## Load these first (required)

Refuse to proceed if any of these are missing:

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`docs/phoenix2/features/mix-model.md`](../features/mix-model.md)
- [`docs/phoenix2/features/phoenix-records-schema.md`](../features/phoenix-records-schema.md)
- [`docs/phoenix2/features/events.md`](../features/events.md)
- [`docs/phoenix2/features/notifications-gating.md`](../features/notifications-gating.md)
- [`docs/phoenix2/features/qualifiers-mom.md`](../features/qualifiers-mom.md)
- [`docs/phoenix2/features/derived-data.md`](../features/derived-data.md)
- [`docs/phoenix2/features/title-lists.md`](../features/title-lists.md)
- [`docs/phoenix2/phases/phase-1-safety-nets.md`](phase-1-safety-nets.md) — verify it's done
- [`CLAUDE.md`](../../../CLAUDE.md)
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md)

## Prerequisites

- Phase 1 complete: integration tests in place, smoke assertion live, backfill dry-run validated.

## In scope

- `MixEnum.Phoenix2` added to the enum (without `[LiveMix]`)
- `[LiveMix]` attribute created and applied to `Phoenix`
- `ICurrentUserAccessor` split into `GetUserSelectedMix()` and `GetLiveMix()`
- `UiSettingsAccessor` read-time default removed for persisted users; anonymous fallback retained
- Backfill migration: `Universal__CurrentMix = Phoenix` for every existing user without it
- New-user-create handler writes `CurrentMix = LiveMix`
- UI mix selector filters by selectability rule (only `[LiveMix]` mix and earlier are shown)
- `PhoenixRecords` schema migration: add `MixId`, composite FK to `ChartMix`, wider unique, secondary index
- `PlayerStats` schema migration: add `MixId`, expand PK
- `TournamentSession` schema migration: add `MixId` (set-once)
- Qualifier configuration: add `MixId`, thread through Qualifier queries/commands
- Derived-data table audit (Weekly Charts, Tier List outputs, Community Leaderboards): catalog and migrate the ones that persist across recomputes
- Event schema: add `MixEnum Mix` to listed events (see [events.md](../features/events.md))
- Repository signatures: required `MixEnum` parameter on `IPhoenixRecordRepository`, `IPlayerStatsRepository`, derived-data repositories
- Application: thread `MixEnum` through queries/commands; default to LiveMix only at controller/page edge
- `Phoenix2TitleList` stub (empty or near-empty is fine)
- `TitleSaga` branches by mix
- `CommunitySaga` and Discord-publishing consumers gated on `event.Mix == GetLiveMix()`

## Out of scope (defer to Phase 3)

- Moving `[LiveMix]` from `Phoenix` to `Phoenix2` — that's the launch-day flip
- `UploadPhoenix2Scores.razor` page — shape depends on PIU's site
- Phoenix 2 scraper rework — shape depends on PIU's site
- The defensive corruption guard's tuning — needs real Phoenix 2 traffic to threshold
- Populating `Phoenix2TitleList` — depends on what Phoenix 2 ships

## Locked decisions affecting this phase

All of A1–A5, B1, C1–C4, D1, D2, E1, E2, F1, F2, F3, H1, J1.

The phase docs are not the place to re-debate these. See the feature docs for the canonical decision text. Reference by ID in this doc and in PRs.

## Tasks

### Foundation (do first)

1. [ ] **Add `LiveMixAttribute`.** New file `ScoreTracker.Domain/Enums/LiveMixAttribute.cs`. Simple `AttributeUsage(Field)` marker. See [mix-model.md](../features/mix-model.md).

2. [ ] **Add `Phoenix2` to `MixEnum`, mark `Phoenix` with `[LiveMix]`.** File: `ScoreTracker.Domain/Enums/MixEnum.cs`.

3. [ ] **Add new Mix row to the database** for Phoenix 2 with a stable GUID. Record the GUID in `EFChartRepository.MixGuids` dictionary.

4. [ ] **Split accessor surface.** `ICurrentUserAccessor` gains `GetUserSelectedMix()` and `GetLiveMix()`. Implementations updated.

5. [ ] **Backfill migration for `UserUiSettings`.** A5. Verify on prod-sized restore.

6. [ ] **Remove read-time mix default for persisted users.** [`UiSettingsAccessor.cs`](../../../ScoreTracker/ScoreTracker/Services/UiSettingsAccessor.cs). Anonymous fallback to `GetLiveMix()` retained.

7. [ ] **`CreateUserHandler` writes `CurrentMix = LiveMix`** for new accounts.

8. [ ] **UI selectability filter** in `Shared/MainLayout.razor`. Phoenix 2 invisible until `[LiveMix]` moves.

### Schema (do after foundation)

9. [ ] **`PhoenixRecords` migration.** A1, A2, A3, A5. See [phoenix-records-schema.md](../features/phoenix-records-schema.md).

10. [ ] **`PlayerStats` migration.** B1.

11. [ ] **`TournamentSession` migration.** F1, F3 (set-once). Backfill existing rows to `Phoenix`.

12. [ ] **Qualifier configuration migration.** F2. Backfill existing rows to `Phoenix`.

13. [ ] **Derived-data audit and migration.** J1. Sweep `ScoreTracker.Data/Persistence/Entities/` for tables that join `PhoenixRecord` or `ChartId`. Catalog each as "needs `MixId` now" or "fully rebuilds, defer to Phase 4."

### Application (do after schema)

14. [ ] **Event schemas.** D1. Add `MixEnum Mix` (required) to events listed in [events.md](../features/events.md). Update every publisher to pass it; update every consumer to use it.

15. [ ] **Repository signatures.** A3. Mix as required parameter on every read/write method of affected repositories.

16. [ ] **Thread Mix through Application.** E1. Every query/command that touched Phoenix-hardcoded code (see [explore findings: TierListsController, WeeklyChartsController, PumbilityProjectionSaga, Community queries, WorldRankingService](../features/mix-model.md)) gains `MixEnum` in its contract. Default to `GetLiveMix()` *only* at the Razor page / API controller edge.

17. [ ] **Notification gating.** [`notifications-gating.md`](../features/notifications-gating.md). `CommunitySaga` and Discord-publishing consumers add the `event.Mix != GetLiveMix() → return` guard.

18. [ ] **`Phoenix2TitleList` stub + `TitleSaga` branching.** E2. Empty `Phoenix2TitleList` is acceptable.

## Success criteria

- [ ] `dotnet build ScoreTracker/ScoreTracker.sln -c Release` succeeds.
- [ ] `dotnet test ScoreTracker/ScoreTracker.Tests/ScoreTracker.Tests.csproj` runs green.
- [ ] `dotnet test ScoreTracker/ScoreTracker.Tests.Integration/ScoreTracker.Tests.Integration.csproj` runs green — including the Phase 1 mix-isolation tests, now non-vacuously.
- [ ] Application boots locally with both Phoenix and Phoenix2 in the enum; mix selector shows only Phoenix and XX (Phoenix 2 hidden per selectability rule).
- [ ] A test user can: select Phoenix → import scores → see them recorded with `MixId = Phoenix`. Switching to Phoenix 2 in the UI is not yet possible (correct — gate works).
- [ ] Manually insert a Phoenix 2 score row in dev DB → verify it doesn't appear in Phoenix mode views.

## Open questions

- The derived-data audit may surface tables we didn't anticipate. Log findings in Changelog.
- Some Application-layer changes are touchpoint-heavy (every controller/page). Worth splitting into smaller PRs by feature area? Default: yes, separate PRs per feature stack.

## Changelog

- 2026-05-16: Phase doc created from workshop.
