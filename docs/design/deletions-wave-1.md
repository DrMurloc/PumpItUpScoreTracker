# Deletions — Wave 1

One PR, 20 commits. Pure deletions: no logic changes, no behaviour rewrites. Signature and
constructor changes are in scope (removing a now-unused port method, dropping an interface entry
from a saga). Anything needing logic changed to unblock it is in
[deletions-wave-2.md](deletions-wave-2.md).

**Every commit builds and every suite passes.** Two ordering rules produce the sequence:

1. **Outside-in along the dependency graph** — `Presentation → Application → Infrastructure`.
   Leaves go first, so nothing is removed until the commit before it has proven it unreferenced.
2. **A port always dies before its implementation** (see [commits 12–18](#ports-and-implementations--commits-1218)).
   DI binds by reflection, so deleting an implementation first can leave a runtime-only failure
   that no compiler and no test catches. Deleting the port is what makes the compiler enumerate
   every remaining consumer.

The migration is last, because it is the only step that touches data.

## Tables are never hard-deleted

**Owner standard (2026-07-27): no `DROP TABLE`, ever.** A table whose code is deleted leaves the
EF model but its rows stay queryable in the **`archive` schema**, so a revived feature starts from
real data. See [CLAUDE.md](../../CLAUDE.md) and [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md), where
the standard now lives permanently.

```sql
IF SCHEMA_ID('archive') IS NULL EXEC('CREATE SCHEMA archive');
ALTER SCHEMA archive TRANSFER scores.Match;
```

**Never edit an applied migration** (or its `.Designer.cs`). `__EFMigrationsHistory` says it
already ran, so an edit changes nothing in production while silently altering every
build-from-scratch environment that replays the chain. Scaffold a **new** migration, then
hand-replace each generated `DropTable` with the transfer above — editing a new, unapplied
migration is normal.

- `archive` is the only sanctioned destination. `back`, `bup`, `books`, `smx` are pre-existing
  schemas of unknown provenance — leave them alone.
- ⚠ `back.Match` (83 rows) holds **fewer rows than live `scores.Match`** (490). Stale partial
  backup, not a prior archive. Do not mistake it for the job already being done.
- `scores.ChartSkillArchive` belongs in `archive` too, but rides
  [nuke-old-skill-categories.md](nuke-old-skill-categories.md) N2, not this wave.

---

# Commit order

## Presentation — commits 1–6

Nothing depends on Presentation, so it goes first. After commit 6 every doomed Application type is
unreferenced.

### 1. Fix the dead links (no deletions yet)

| File | Change |
|---|---|
| `Pages/Competition/RecordTournamentSession.razor:539,544` | repoint to `/Tournaments/MarchOfMurlocs`, **add the missing `return`s** — `NavigateTo` does not halt, so line 546 reaches `CurrentUser.User` (`?? throw new UserNotLoggedInException()`) and anonymous visitors race a redirect against an exception |
| `Pages/Competition/MatchTournamentQualifiersSubmit.razor:706` | repoint to `/` (guard for `TournamentId == default`, unreachable behind the `:guid` constraint) |
| `Pages/Competition/MatchTournamentQualifiers.razor:160` | delete the **Bracket Manager** button — `/Tournament/{id}/Brackets` has no route and 404s today |

Deliberately first and deliberately deletion-free: it repairs live 404s and an exception path
before anything becomes hard to undo.

### 2. Delete the nine orphaned pages

`Progress/CompetitiveLevel` · `Experiments/GameStats` · `Experiments/Distribution` ·
`Experiments/NoteCounts` · `Experiments/SimilarPlayers` · `Experiments/ChartScoringLevels` ·
`Experiments/ChartLetterDifficulties` · `Admin/BulkVote` · `Admin/ChartUpdate`

`Pages/Experiments/` disappears entirely. All nine have zero inbound links and consume only shared
queries with many other callers, so the cascade is nil.

> ⚠ **Same commit, mandatory**: remove the `Pages/Experiments/GameStats.razor` (12) and
> `Pages/Experiments/ChartLetterDifficulties.razor` (8) entries from `UiColorTokenTests.Allowance`.
> The ratchet explicitly fails on orphans — *"no longer exists — remove its allowance entry"* — so
> deleting the pages without this turns the suite red. The ratchet tightens as a bonus.

**Side benefit**: these pages hold 9 of the ~30 direct repository injections into Web (a CLAUDE.md
violation) — `IChartRepository`, `IPlayerStatsRepository`, `IScoreReader`, `IUserRepository`,
`ITitleRepository`, `ITierListRepository`.

### 3. Delete `/Tournaments`

`Pages/Competition/Tournaments.razor`. Separate from commit 2 because commit 1 has to land first.
The nav's Compete menu already dispatches `HighlightedEvents` to the same three destinations; only
the Upcoming / Previous archive is lost, which the owner will rebuild with the new tournament
ecosystem.

### 4. Delete the bracket page and its API surface

- `Pages/Competition/MatchTournamentAdmin.razor` — the only page touching `IMatchRepository`
- `Dtos/Api/MatchDto.cs`
- `Controllers/Api/TournamentController.GetMatches` + the `api/tournament/{id}/matches` route

> ⚠ **Public `api/*` change.** `TournamentsApiShapeTests` covers only `GetTournaments` (verified),
> so nothing goes red — but per CLAUDE.md this is breaking-change territory for partner tools.
> Its own commit so it is reviewable in isolation, and worth an announcement.

### 5. Strip `Pages/Admin/Admin.razor`

Markup, handler, and backing fields for:

1. **"Do It"** (`RebuildOfficialLeaderboard`) — one live publish inside 40 lines of commented-out
   archaeology (391–429)
2. **BITE 8 machinery** — the `MissingUsers` button loop, `Restore()`, the `Bite8` field, and the
   `GetMissing` call at line 585 that runs on every page load
3. **Backfill User Tier Lists** · **Rebuild Tier Lists** · **Backfill Community Highlights** ·
   **Backfill Folder Levels**
4. **The whole recap card** — Compute My Season Recap · Compute All Season Recaps · Rebuild Recap
   PG Cards · Rebuild Recap Total PUMBILITY
5. **Recalculate Phoenix 2 Player Ratings**
6. **ReCalculate Ratings** — button, progress bar, `_isReCalculating` / `_maxReCalculate` /
   `_currentReCalculate`
7. **Create Song card** — markup, `CreateSong()`, the `NewChart` class, `AddChart()`, `SetMin()`,
   ~12 backing fields

Then **7 injections go unused**: `ChartAttemptDbContext`, `IFileUploadClient`, `IBotClient`,
`IUserRepository`, `ITierListRepository`, `IMemoryCache`, `IQualifiersRepository`.

> `@inject ChartAttemptDbContext Database` (line 297) is the **only** DbContext injection in the
> whole Pages/Components tree and a direct CLAUDE.md violation. Both its usages are inside the
> commented-out block — deleting dead comments clears a real architecture violation.

> The recap **feature survives**: `RecapSaga.Consume(ScoreHighlightsCapturedEvent)` (line 82)
> recomputes a player's recap after every score session, and the read path
> (`EFPlayerSeasonRecapRepository` handling `GetPlayerRecapQuery`) is untouched. Only the manual
> bulk sweep dies.

**Kept**: Clear Cache · Content Lock (+ dialog) · Cycle credential keys · Update Chart video ·
the PIU Center and Official Leaderboards links.

This commit removes the last publisher of six bus commands and the last caller of `FixAvatars`.

### 6. Strip `Pages/Admin/OfficialLeaderboardsAdmin.razor`

- **Seed baseline from legacy tables** — button, `SeedBaseline()`, `_seedQueued`. Inert: it refuses
  to run once sealed snapshots exist, and both mixes have a completed baseline (verified, latest
  2026-07-19).
- **Refresh rating boards (one-time)** — button, `RefreshRatingBoards()`, `_ratingBoardsQueued`,
  and the comment at 43–47 documenting its own deletion.

**Kept**: Run import now · Rebuild weekly highlights · Refresh popularity · the mix selector · all
three tables.

---

## Application — commits 7–11

One commit per vertical, each independently green.

### 7. Match Application layer — `ScoreTracker.Application`

| Folder | Files |
|---|---|
| `Commands/` | `CreateMatchLinkCommand`, `DeleteMatchLinkCommand`, `FinalizeMatchCommand`, `PingMatchCommand`, `ResolveMatchCommand`, `UpdateMatchCommand`, `UpdateMatchScoresCommand` |
| `Events/` | `MatchUpdatedEvent` |
| `Handlers/` | `MatchSaga` |
| `Queries/` | `GetAllMatchesQuery`, `GetMatchLinksFromMatchQuery`, `GetMatchLinksQuery`, `GetMatchPlayersQuery`, `GetMatchQuery` |

\+ delete `ScoreTracker.Tests/ApplicationTests/MatchSagaTests.cs`.

> `GetRandomChartsQuery` **stays** — `MatchSaga` is one of five senders; `ChartsController:137` and
> `ChartRandomizer.razor` ×3 are live. The `Randomizer → Application` divergence in CLAUDE.md:202
> is **not** unpinned by this wave; moving the query is a rearchitecture, not a deletion.

### 8. ChartIntelligence

- `Contracts/Messages/BackfillUserTierListsCommand`
- `Application/UserTierListSaga` — drop the `IConsumer<BackfillUserTierListsCommand>` entry, its
  `Consume` overload, and `BackfillDelayPerUser`. **Class survives** on
  `IConsumer<PlayerScoresUpdatedEvent>`
- `UserTierListSagaTests` — drop the backfill test (~178)

> ✅ **KEEP** `RateChartDifficultyCommand`, `RateChartDifficultyHandler`,
> `ReCalculateChartRatingCommand` and `RateChartDifficultyHandlerTests`. Owner call: community
> difficulty voting returns for the legacy mixes, so the write path is dormant, not dead.

### 9. PlayerProgress

- `Contracts/Messages/`: `BackfillFolderLevelsCommand`, `RebuildRecapPgCardsCommand`,
  `RebuildRecapTotalPumbilityCommand`, `RecalculateMixRatingsCommand`, `CalculateSeasonRecapsCommand`
- `Application/FolderLevelSaga` — drop consumer entry + `Consume`. Survives on
  `IRequestHandler<GetPlayerFolderLevelsQuery>`
- `Application/PlayerRatingSaga` — drop consumer entry + `Consume` (165). Survives, 4 other roles
- `Application/RecapSaga` — drop **three** consumer entries + `Consume` overloads (97, 329, 371).
  Survives on `IConsumer<ScoreHighlightsCapturedEvent>`
- Tests: `FolderLevelSagaTests` (contexts 29–32) · `PlayerRatingSagaTests` (601) ·
  **`RecapSagaTests` — every `CalculateSeasonRecapsCommand` test** (41, 53, 70, 85, 99, 114, 129,
  140, 161, 274, 295, 315, 332, 385, 401, 429, 453, 483, 495, 508) plus 190, 232 and the context
  helpers at 715–741. **Keep the `ScoreHighlightsCapturedEvent` coverage**

### 10. Communities

- `Contracts/Messages/BackfillCommunityHighlightsCommand`
- `Application/BackfillCommunityHighlightsConsumer` — **whole class**, no other role
- Delete `ScoreTracker.Tests/ApplicationTests/BackfillCommunityHighlightsConsumerTests.cs`

### 11. OfficialMirror one-time commands

- `Contracts/Messages/`: `SeedBaselineSnapshotCommand`, `RefreshRatingBoardsCommand`
- `Application/LeaderboardSweepSaga` — drop both consumer entries and their `Consume` overloads
  (192, 259), the one-time note at line 32, and the sealed-snapshot escape at 56. Survives on the
  sweep itself + `GetMissingChartsQuery`
- `LeaderboardSweepSagaTests` — seed contexts (323–326), one-time repair block (467–535)

This removes the last caller of `IOfficialLeaderboardRepository.GetAllEntries` (line 202 sits
inside `Consume(SeedBaselineSnapshotCommand)`).

---

## Ports and implementations — commits 12–18

### The port always dies before its implementation

DI binds ports **by reflection**, not by a registration line —
[`RegistrationExtensions.cs:28-32`](../../ScoreTracker/ScoreTracker.CompositionRoot/RegistrationExtensions.cs)
walks `implementationType.GetInterfaces()` and binds whatever it finds. Nothing in the wiring
mentions `EFMatchRepository` or `IMatchRepository` by name.

So deleting an **implementation** first proves nothing: the build stays green, the tests stay
green, and a consumer still injecting the port gets a **runtime DI resolution failure** that no
compiler and no test will catch.

Deleting the **port** first is the proof. The compiler instantly enumerates every remaining
user — the `: IPort` on the implementation, every `@inject`, every constructor parameter — before
anything else is removed. So each pair is two commits:

1. **Delete the port, strip `: IPort` from the implementation.** Green. The implementation is now
   an orphaned plain class that nothing can resolve.
2. **Delete the orphaned implementation.**

(For explicitly-registered vertical services the compiler catches it either way, but the rule is
uniform so nobody has to remember which is which. It does *not* apply to pages — a page is a leaf,
nothing can depend on it, and routes are strings.)

### 12. `IOfficialSiteClient.FixAvatars`

Interface member first, then its body — same commit, since removing the interface member is what
surfaces any remaining caller.

- `OfficialMirror/Domain/IOfficialSiteClient.FixAvatars` → `Infrastructure/OfficialSiteClient.FixAvatars()`
- the `_leaderboards` field and ctor parameter on `OfficialSiteClient`
- `OfficialSiteClientTests` mocks at 377, 609

Its only caller was the commented-out `Admin.razor:400`, removed in commit 5.

### 13. `IOfficialLeaderboardRepository` — the port

15 methods, and it had **three call sites in the whole solution**; 13 were dead already and commits
11 and 12 took the other two. Deleting the port is what confirms that.

- `OfficialMirror/Domain/IOfficialLeaderboardRepository.cs`
- strip `: IOfficialLeaderboardRepository` from `EFOfficialLeaderboardRepository`
- `Wiring/OfficialMirrorRegistrationExtensions:34` — the explicit DI line
- `Application/LeaderboardSweepSaga` — the `_legacy` field and ctor parameter
- `LeaderboardSweepSagaTests` mocks at 40, 107

### 14. `EFOfficialLeaderboardRepository` and its entities

- `Infrastructure/EFOfficialLeaderboardRepository.cs`
- `Infrastructure/Entities/UserOfficialLeaderboardEntity.cs`, `Entities/UserWorldRanking.cs`
- both `ToTable` lines in `Wiring/OfficialMirrorModelContribution:16,17`
- Delete `Tests.Integration/EFOfficialLeaderboardRepositoryTests.cs`

> ⚠ **`scores.OfficialLeaderboard` is NOT part of this.** Despite the name it is the *current*
> snapshot dimension table, joined throughout `EFOfficialSnapshotRepository` and
> `EFOfficialRecordRepository`.

> ⚠ **World rankings disappear as a feature.** `GetAllWorldRankings` / `SaveWorldRanking` /
> `DeleteWorldRankings` and `scores.UserWorldRanking` (6,122 rows) had no callers outside this
> repository and its own integration test — already dead, only the storage was still wired.
> ARCHITECTURE.md lists world rankings in OfficialMirror's charter; commit 20 updates it.

### 15. `IMatchRepository` — the port

The case the reflection rule exists for: nothing references either side by name.

- `ScoreTracker.Domain/SecondaryPorts/IMatchRepository.cs`
- strip `: IMatchRepository` from `EFMatchRepository`

A green build here is the proof that no consumer survived commits 1–11.

### 16. `EFMatchRepository` and the Match entities

- `ScoreTracker.Data/Repositories/EFMatchRepository.cs`
- `ScoreTracker.Data/Persistence/Entities/MatchEntity.cs`, `MatchLinkEntity.cs`
- `ChartAttemptDbContext` — both `DbSet`s and their model configuration
- Delete `Tests.Integration/EFMatchRepositoryTests.cs`

> Commits 14 and 16 leave `ChartAttemptDbContextModelSnapshot.cs` listing entities whose classes
> are gone. That compiles and stays green — model snapshots reference entities by **string** type
> name, not by type. Commit 19 regenerates it.

### 17. Port method trims

Interface member and implementation body in the same commit; the interface edit is what breaks any
remaining caller.

- `Domain/SecondaryPorts/IQualifiersRepository` — `GetMissing`, `Restore`, `FixLeaderboard`, and
  their implementations in `EventCompetition/Infrastructure/EFQualifiersRepository` (127, 164, +1)
- `OfficialMirror/Domain/IOfficialSnapshotRepository.DeleteRatingPlacements` and its implementation
  in `EFOfficialSnapshotRepository:324`

### 18. Domain and SharedKernel data types

Pure data — no reflection involved, so deleting them is compiler-verified on its own.

- `ScoreTracker.Domain/Records/MatchMachineRecord.cs`, `Records/WorldRankingRecord.cs`
- `ScoreTracker.Domain/Views/`: `MatchLink.cs`, `MatchPlayer.cs`, `MatchScoring.cs`, `MatchView.cs`
- `ScoreTracker.SharedKernel/Enums/MatchState.cs`

> ⚠ `TournamentType.Match` **stays** — CEO 2026: Project Storm is `Type=Match`, live, 3 qualifier
> submissions in, cutoff 2026-08-12. Different enum entirely. See
> [wave 2 §1](deletions-wave-2.md).

---

## Migration — commit 19

Last, because it is the only commit that touches data.

```
dotnet ef migrations add ArchiveDeletedFeatureTables --startup-project ../ScoreTracker.CompositionRoot
```

scaffolded from `ScoreTracker.Data`, then hand-edited: create the `archive` schema, and replace
every generated `DropTable` with `ALTER SCHEMA archive TRANSFER`.

| Table | Rows |
|---|---|
| `scores.Match` | 490 |
| `scores.MatchLink` | 889 |
| `scores.RandomSettings` | 132 |
| `scores.TournamentPlayer` | 482 |
| `scores.TournamentMachine` | 1 |
| `scores.UserOfficialLeaderboard` | 136,616 |
| `scores.UserWorldRanking` | 6,122 |

`ChartAttemptDbContextModelSnapshot.cs` regenerates here — that one is *supposed* to change.

---

## Docs — commit 20

- **DATABASE-SCHEMA.md** — move the Match subsystem rows and the two leaderboard rows into the
  **Archived** section (they still exist in SQL; the doc is where a revival looks first)
- **ARCHITECTURE.md** — page table: drop the `Experiments/` row, `/CompetitiveLevel` from
  `Progress/`, `/Tournaments` from `Competition/`; remove world rankings from OfficialMirror's
  charter line
- **API.md** — remove `api/tournament/{id}/matches`
- **CLAUDE.md** — already carries the archive + migration standard; leave the
  `Randomizer → Application` divergence note in place

---

## Verification

Between commits:

```bash
dotnet build ScoreTracker/ScoreTracker.sln -c Debug
```

Fast suites at every commit; integration and E2E from commit 12 onward and mandatory at 19:

```bash
dotnet test ScoreTracker/ScoreTracker.Tests.Integration/ScoreTracker.Tests.Integration.csproj
```

Watch the test **count**, not just the green — a stale DLL passes a suite that no longer compiles,
and this PR deletes a lot of tests on purpose. Note the expected drop at commits 2, 7, 9, 10, 13,
14 and 16, and reconcile it against the list above.

The port-deletion commits (12, 13, 15, 17) are the ones that matter most: a **clean build there is
the evidence** that nothing still consumed what the earlier commits removed. If one of them fails
to compile, that is the system working — the failure names every consumer that was missed.

Localization keys for deleted markup stay in place: `LocalizationKeyTests` only requires every
locale to carry the same key set as `en-US`, so orphaned keys are inert. Cleanup is
[wave 2 §2](deletions-wave-2.md).
