# Official Leaderboards Overhaul — Phoenix 2, history, and the snapshot model

Status: **shipped on this branch** (2026-07-16, C0–C13). Post-deploy owner steps: press **Seed baseline from legacy tables** per mix on `/Admin/OfficialLeaderboards` (once, before the first Sunday sweep), optionally **Run import now**; refresh the `PiuTest:Password` user-secret (stale — every authenticated live canary is red) and verify the prod service account still logs in. Follow-up PR after prod verification drops the four legacy tables. The P2 board-depth recon (`Phoenix2ChartBoardReconTests`) runs once credentials are fixed; the pager handles one page or many either way.

The Official Mirror's leaderboard side gets rebuilt bottoms-up: Phoenix 2 boards (top 300, PUMBILITY), a week-over-week history mechanic, weekly editorial highlights, player identity (game tag → UserId, board → ChartId), and the two standing bugs fixed *by construction*. Phoenix 1 imports keep running until that site sunsets, then its final snapshot becomes the permanent archive. The player score-import path (`RunImport`, `/Login/PiuGame`, api/* endpoints) is **out of scope** except where it gains the identity hook.

## 1. Locked decisions (owner, 2026-07-16)

| # | Decision |
|---|---|
| 1 | **Weekly cadence, everything.** One Sunday sweep per mix; no daily pumbility polling ("I'm not bombarding PIUGame every day"). |
| 2 | Highlights v1 = PUMBILITY movers + most-boards-climbed + new #1s (level 24+, must *beat* the standing record) + **world firsts**: first-ever of each grade band **SS and up, PG included**, tracked **per chart and per folder** (S24, D24, …). Same-week rule below. Skipped weeks self-label ("vs Jun 28 (2 weeks)"). |
| 3 | Renames: auto-detect heuristic **proposes**, owner accepts from the admin page. Never auto-merge. |
| 4 | PlayerCompare and the avatar leaderboard are **dropped for now** (revisit later). |
| 5 | Popularity: **no backfill**; accumulate from the first new-pipeline snapshot. Song view aggregates chart ranks at read time. |
| 6 | The P2 rankings hub ranks by **official PUMBILITY board values**; our computed rating is drill-down detail. (P1 keeps computed rankings — it has no pumbility board.) |
| 7 | Highlights are **page-only** for now; rows are shaped so a Discord card can consume them later without rework. |
| 8 | UserId linking: **import-confirmed identity only.** Same tag claimed by two accounts → most recently active wins (each import overwrites). The account-merge tool is the user-facing fix owner will point people at. |
| 9 | `RecapPlayerTypeCalculator` (+ `RecapPlayerType`) moves to **SharedKernel** — owner plans reuse beyond recap. |
| 10 | DB headroom: prod is S2/50 DTU; owner raised the max-size cap himself. **Well-indexed live queries on raw history are fine** — the 2026-07-10 incident was missing indexes, not table size. No pre-aggregation fleet; see §6. |

**Same-week rule (final):** players landing the *same record score in the same snapshot week* are co-credited (two PGs in one week → both highlighted). Matching a standing record in any later week credits nothing. A jump across several bands claims only the highest new band. The baseline snapshot claims every already-achieved band **silently** — week one is not 3,000 highlights. A world-first absorbs its "new #1" (no double listing).

## 2. Why a snapshot model (the two bugs die here)

Today `UserOfficialLeaderboard` is clear-and-rewritten *per board name* each sweep. Two structural bugs follow:

1. **Stale rows**: a board that drops out of a run (song rename on either side, failed/empty fetch, removed chart) is never cleared — its last import lingers forever and keeps feeding world rankings.
2. **"Last Updated" lies**: the timestamp is stamped only at the very end of one multi-hour `Consume`; any failure or app restart (in-memory bus) skips it, and Hangfire reports success regardless because the job only publishes.

The rebuild writes **immutable snapshots**: a header row per run, placements appended under it, and a `CompletedAt` seal flipped only when the whole sweep lands. "Current" = latest sealed snapshot. Fell-off players simply aren't in the new snapshot; a failed run never becomes visible; the seal *is* the Last Updated; and history is the snapshots we didn't delete. The header doubles as the run-state record (stage, counts, error) — importable health is a lookup, not an investigation.

## 3. Data model (all OfficialMirror-internal, `<Vertical>/Infrastructure/Entities/`)

Dimensions (strings live here, once):

| Table | Shape | Keys/notes |
|---|---|---|
| `OfficialLeaderboard` | Id int identity, MixId guid, Type ("Rating"/"Chart"), Name, ChartId guid? , ChartType?, Level? | Unique (MixId, Type, Name); index (ChartId). Chart boards carry their catalog chart — the `"Song S20"` string-parse dance dies. Rating boards (P1 per-level lists, P2 PUMBILITY All/S/D) have null ChartId. |
| `OfficialPlayer` | Id int identity, MixId, Username, AvatarUrl?, UserId guid?, UserIdSource ("None"/"Import"), LastSeenAt | Unique (MixId, Username); index (UserId). Absorbs `OfficialUserAvatar`. |

Facts:

| Table | Shape | Keys/notes |
|---|---|---|
| `OfficialLeaderboardSnapshot` | Id int identity, MixId, StartedAt, LastProgressAt, CompletedAt?, IsBaseline bit, Stage, BoardsExpected int, BoardsWritten int, Error? | Index (MixId, CompletedAt). Header = run state + seal. Unsealed = invisible to every read; deleted by the janitor after 7 days. LastProgressAt is the run's heartbeat (stamped by every checkpoint): the overlap guard only respects an unsealed run whose heartbeat is under 15 minutes old, so a killed run releases the lock in minutes instead of blocking re-triggers. |
| `OfficialLeaderboardPlacement` | SnapshotId, LeaderboardId, PlayerId, Place int, Score decimal(9,2) | Clustered PK (SnapshotId, LeaderboardId, Place, PlayerId) — append-only writes, board reads in display order. NC (PlayerId, SnapshotId) INCLUDE (LeaderboardId, Place, Score) — player timelines/search. decimal keeps official pumbility cents (today's model truncates to int). |
| `OfficialChartPopularity` | SnapshotId, ChartId, Place | Popularity is chart-ranked, not player-ranked — its own skinny table. |
| `OfficialBoardRecord` | LeaderboardId PK, HighScore, AchievedSnapshotId | The record book. A band is claimed iff HighScore ≥ its floor, so one number encodes every claimed band. Rebuildable from history. |
| `OfficialFolderRecord` | (MixId, ChartType, Level) PK, HighScore, AchievedSnapshotId | Folder-level record book (S24, D26, …), level 24+ only in practice. |
| `OfficialWeeklyHighlight` | Id, SnapshotId, MixId, Kind, SortOrder, PlayerId, LeaderboardId?, ChartId?, ChartType?, Level?, GradeBand?, Score?, PrevValue?, NewValue? | Kinds: PumbilityMover, BoardsClimbed, NewNumberOne, ChartGradeFirst, FolderGradeFirst. Co-credits = one row per claimant sharing (Kind, subject); UI groups. Index (SnapshotId). |
| `OfficialPlayerRenameProposal` | Id, MixId, OldPlayerId, NewPlayerId, OldUsername, NewUsername, AvatarMatched bit, Top50Overlap int, Status (Pending/Accepted/Dismissed), CreatedSnapshotId | The old/new username text columns are the audit trail — accept hard-merges the player rows (§5). |

Growth: P2 steady state ≈ 150–200k placement rows/week ≈ 350 MB/yr all-in at this shape. P1 adds ~60k/week until its sunset bounds it. Storage is a non-issue at the raised cap; the index set above is the actual deliverable (incident lesson: ship indexes with the query shapes).

Legacy tables (`UserOfficialLeaderboard`, `UserWorldRanking`, `OfficialUserAvatar`, `OfficialLeaderboardImportState`) stop being read at cutover but are **dropped in a follow-up PR** after the owner runs the baseline seed in prod (§8).

## 4. The sweep pipeline (rebuild of the `StartLeaderboardImportCommand` consumer)

One staged run per mix, checkpointing `Stage`/`BoardsWritten` on the header as it goes:

1. **Open** — purge unsealed snapshots older than 7 days (janitor), skip if another run is heartbeat-live, create header. First-ever run for a mix (no prior sealed snapshot) auto-marks `IsBaseline`.
2. **Rating boards** — P2: PUMBILITY All/Singles/Doubles, paged until end (existing scraper). P1: the per-level rating list. Upsert dims, append placements. Olympic tie places computed exactly as today.
3. **Chart boards** — enumerate the 20+ song list, then per board: fetch (**paged** — see §9 recon), parse, upsert leaderboard dim *with ChartId* (the scraper already holds the `Chart`; it stops flattening it away), upsert players, append that board's placements, checkpoint. Politeness delay between fetches. Boards that fail to fetch/parse are *skipped and counted* (header carries skip count) — a skipped board simply has no rows in this snapshot, which reads as "no data this week", never as stale data.
4. **Popularity** — `top_steps.php` scrape → `OfficialChartPopularity` rows + the existing "Popularity" tier-list categorization (unchanged output).
5. **Enrichment** — the "Official Scores" tier-list feed (`TierListProcessor`) repointed to the in-memory snapshot data, same output as today. No stored world rankings and no stored player stats — those are live queries now (§6).
6. **Highlights** — diff vs the previous sealed snapshot + both record books → `OfficialWeeklyHighlight` rows, record book updates, rename-proposal detection (§5). Skipped entirely when this run is baseline (records still prime).
7. **Seal** — `CompletedAt = now`. This is the page's Last Updated.

Failure at any stage leaves the header holding stage + error; the site keeps serving the last sealed snapshot. v1 retry = re-trigger from admin (restarts the snapshot clean); true mid-run resume is a noted upgrade if real runs prove flaky — with run visibility and ~40-minute durations, restart-on-retrigger is acceptable.

Scraper hardening riding along (`PiuGameApi` / `OfficialSiteClient`): `EnsureSuccessStatusCode` on the GET path (an error page must not parse as an empty board), paging support in `GetSongLeaderboard` (same next/last-icon protocol as the pumbility scraper), per-row parse failures counted and logged instead of silently swallowed, a configurable politeness delay, and avatar mirroring deduped **per unique URL per run** (today it's a blob existence check per row — 3× worse at 300-deep).

## 5. Player identity

**UserId linking (decision 8).** The player score import (`RunImport`) learns the account's game tag authoritatively — after account resolution it upserts `OfficialPlayer(mix, tag).UserId = userId, UserIdSource = Import, LastSeenAt = now`, overwriting any previous link (imports are activity, so last-import-wins *is* most-recently-active). No string-matching backfill against `Users.GameTag` — import-confirmed only. Linked players render as `UserLabel` (profile link); unlinked render tag + mirrored avatar. Touchpoint to investigate in build: when the account-merge tool merges users, re-point `OfficialPlayer.UserId` rows (event consumer if Identity publishes one; admin note if not).

**Renames (decision 3).** During the highlights stage, tags that *disappeared* this snapshot are paired against tags that *appeared*: candidate when avatar URL matches **and** the old player's top-50 placements substantially reappear under the new tag (same chart, score ≥ old — scores only ever improve). Candidates land as Pending proposals with their evidence; the admin page lists them with **Accept — merge history** / Dismiss. Accept re-points the old player's placements and highlight rows to the new player id and deletes the old dim row (the proposal row keeps both usernames as audit; a freed tag can be re-registered by a different human later, which correctly creates a fresh player). No proposal, no merge — a vanished tag with no candidate is just churn.

## 6. Query strategy (the DTU conversation, settled)

Owner's correction stands: **live, well-indexed queries on raw history are the default.** Only two things materialize, argued on their own merits:

- `OfficialWeeklyHighlight` — editorial content, fixed once the import lands, identical for every visitor; detecting "first ever" wants the timeline in hand at import. Rules change → admin **Rebuild weekly highlights** replays all history (also rebuilds record books).
- The record books — tiny running state that makes first-detection O(boards), rebuildable.

Everything else is a seek or a cached aggregate:

| Read | Shape |
|---|---|
| Board view (current) | Seek: latest sealed SnapshotId → (SnapshotId, LeaderboardId) clustered range, ≤300 rows in display order. |
| Player search / statuses | Seek on NC (PlayerId, SnapshotId) at latest snapshot. |
| Player history (pumbility / rank / per-board timelines) | Seek on (PlayerId) across snapshots — 52 rows/yr. |
| Rankings hub (counts, #1s, archetypes, P1 computed rating) | One set-based query over the latest snapshot (`ROW_NUMBER() OVER (PARTITION BY PlayerId …)` top-50 per player for archetype/computed rating; grouped counts for the rest), `IMemoryCache` until the next seal. Replaces the old per-user×3 N+1 (`CalculateWorldRankings`) outright. If the cache-miss cost ever annoys, promoting this to import-time is a drop-in — schema already fits. |
| Highlights / popularity | Trivial seeks on their tables. |

## 7. Technical scope by layer

**SharedKernel** — `RecapPlayerType` + `RecapPlayerTypeCalculator` move in (pure over SharedKernel types; PlayerProgress re-points). Nothing else: `ScoringConfiguration.PumbilityScoring` already covers both mixes.

**Domain (vertical-internal `OfficialMirror/Domain/`)** — the pure logic, unit-tested hard: `HighlightsCalculator` (movers, boards-climbed, record beats with same-week co-credit, chart/folder band crossing with highest-band collapse, baseline silence), record-book update math, `RenameProposalDetector` (avatar + top-50-overlap thresholds), snapshot diffing. `WorldRankingService` (per-user N+1) is deleted; its replacement lives in the set-based query of §6.

**Application (vertical-internal `OfficialMirror/Application/`)** — the sweep consumer is extracted from `OfficialLeaderboardSaga` into a focused `LeaderboardSweepSaga` (stages of §4, rebuild command, janitor); the old saga keeps the player-import/account handlers it already owns plus the identity-link hook. New read handlers for the hub. **Contracts** (`Contracts/Queries` + `Commands`): the sweep-side read queries are consumed *only* by the pages this overhaul rebuilds or deletes (verified — the api/* controllers touch only the player-import contracts), so they reshape freely: new `GetWeeklyHighlightsQuery`, `GetOfficialBoardQuery`, `GetOfficialPlayerProfileQuery`, `GetOfficialRankingsQuery`, `GetOfficialPopularityQuery`, `GetImportRunsQuery`, `GetRenameProposalsQuery`; commands `AcceptRenameProposalCommand`, `DismissRenameProposalCommand`, `RebuildWeeklyHighlightsCommand`, `SeedBaselineSnapshotCommand`; the legacy leaderboard read queries retire with the old pages. `Tests.Api` goldens must not change.

**Infrastructure (vertical-internal `OfficialMirror/Infrastructure/`)** — the nine entities of §3 + `OfficialMirrorModelContribution` + one migration; `EFOfficialLeaderboardRepository` reshaped around the snapshot lifecycle (create/checkpoint/seal/latest-sealed, dim upserts, batched placement writes, merge re-pointing, record books, proposals, runs); `PiuGameApi`/`OfficialSiteClient` hardening per §4; WireMock fixture captures of the P2 board pages for parser approval + E2E.

**Presentation (`ScoreTracker/Pages/OfficialLeaderboards/`)** — one rebuilt hub at `/OfficialLeaderboards` (`@rendermode Interactive`), four views per the rev-2 mock: **This Week** (default; compact movers + boards-climbed band, world firsts with folder banners, new #1s), **Rankings** (official values on P2 / computed on P1, Δ chips, archetype chips, pinned you-row when linked, expandable top-50), **Players** (search any tag; tiles, ApexCharts pumbility + rank history via `MixThemes` hex accessors, placements with Δs), **Popularity** (charts/songs, trend sparklines). View state rides `?view=` via history interop, *not* `NavigateTo` (post-flip lesson from the TierLists race). `/PlayerRankings` redirects into the hub; `LeaderboardSearch`, `PlayerCompare`, and the avatar board are deleted, nav/menus updated. New admin page `/Admin/OfficialLeaderboards`: rename proposals, import-runs table, Run-now (per mix), Rebuild highlights, Seed baseline. All strings through `L[…]`, every locale in the same pass; import-health chip + skeletons per UX rules; density preference honored on the two list-shaped views (`Density__OfficialLeaderboards`).

## 8. Cutover

1. PR merges; migration applies via the deploy pipeline. Recurring jobs keep their ids/crons (SCHEDULED-JOBS descriptions updated).
2. Owner post-deploy, on `/Admin/OfficialLeaderboards`: press **Seed baseline from legacy** once (builds a sealed `IsBaseline` snapshot per mix from the legacy `UserOfficialLeaderboard` rows using the existing name-parse logic, primes record books — zero-gap cutover, no week-one highlight flood), then optionally **Run import now** per mix rather than waiting for Sunday.
3. Follow-up PR after prod verification: drop the four legacy tables + their entities.

## 9. Verification & test plan

**First build task (C4): P2 recon with the prod service account** — confirm chart-board depth (owner observed top 300) and whether boards paginate (P1 verified: single page, ≤100, no paging; the P2 list is login-gated so this needs the service login). Captured HTML becomes the WireMock/parser fixtures.

| Suite | Coverage |
|---|---|
| `DomainTests` | HighlightsCalculator (co-credit, later-tie-never, band collapse, baseline silence, gap labels), record-book math, rename detector thresholds, diff edges (board vanished, player vanished, first appearance). |
| `ApplicationTests` | Sweep stage flow: seal only on full success, failure stamps stage+error, janitor, baseline auto-detect; identity-link overwrite semantics; accept/dismiss merge; rebuild replay. |
| `Tests.Components` (bUnit) | Each hub view renders from mocked queries: mover ordering + lead flourish, folder-first banner, co-credit grouping, you-row pin, empty/skeleton states, admin proposal cards. |
| `Tests.Integration` | Snapshot lifecycle against real SQL (unsealed invisible, latest-sealed wins, unique dims, merge re-pointing under the clustered PK), migration applies, placement index shapes serve the §6 queries. |
| `Tests.E2E` | One workflow: admin triggers the P2 sweep against WireMock board fixtures → hub shows the board, a highlight, and the sealed Last Updated. |
| `Tests.Api` | No golden changes (assert-only). |

## 10. Commit plan

Each commit builds green with fast suites passing; integration/E2E land with their commits.

- **C0** — this design doc.
- **C1** — SharedKernel: move `RecapPlayerType` + `RecapPlayerTypeCalculator`; re-point PlayerProgress, Web, tests.
- **C2** — Mirror schema v2: eight entities, model contribution, migration, DATABASE-SCHEMA.md rows, integration smoke (uniques, seal invisibility).
- **C3** — Repository + snapshot lifecycle: port reshape, dim upserts, batched writes, checkpoint/seal, latest-sealed reads, integration tests.
- **C4** — Scraper hardening + P2 recon: paged `GetSongLeaderboard`, status checks, politeness, parse-failure counters, avatar URL dedup; P2 fixtures captured + parser approval tests.
- **C5** — Sweep pipeline v2: `LeaderboardSweepSaga` stages onto snapshots, per-board checkpointing, janitor, tier-list feeds repointed, popularity table, seal-as-timestamp; `WorldRankingService`/N+1 deleted; ApplicationTests rewritten.
- **C6** — Highlights engine: domain calculators + record books + `RebuildWeeklyHighlightsCommand`; heavy DomainTests.
- **C7** — Identity: import-time UserId link (last-import-wins), rename detector + proposals + accept/dismiss merge; account-merge touchpoint resolved (consumer or documented admin step).
- **C8** — Hub read contracts: queries + handlers with the §6 live/cached strategy; ApplicationTests.
- **C9** — The hub page: four views per mock, `?view=` history interop, `/PlayerRankings` redirect, old pages deleted, nav updated; bUnit coverage.
- **C10** — Admin page: proposals, runs table, Run-now, Rebuild highlights, Seed baseline; bUnit.
- **C11** — E2E: WireMock P2 sweep → hub renders sealed snapshot + highlight.
- **C12** — Localization: every new key in every locale.
- **C13** — Docs pass: SCHEDULED-JOBS.md descriptions, ARCHITECTURE.md code-map line, this doc flipped to shipped, follow-up-PR note for legacy-table drop.

## 11. Open items

- **P2 board pagination** — unverified until C4 recon (design handles either outcome; the parser gains paging regardless).
- **PUMBILITY board depth** — scraper pages until end; snapshot size unknown but bounded by however deep the site serves.
- **Account-merge relink** — mechanism depends on what Identity exposes; resolved during C7.
- **Density modes** — v1 ships the preference on the two list views only; expand if field-testing wants it.
- **Discord weekly card / home-page widget** — deliberately later; highlight rows are already the right feed shape.
- **PlayerCompare successor** — owner wants to revisit; nothing in this model blocks it (compare is two player-seeks).

## 12. Post-ship rounds (field-test evolution on the PR)

- **F1 — Missing-charts inbox**: unmapped board/popularity scrapes upsert `OfficialMissingChart` rows (per-identity dedupe, `LastIdentified` refresh); the admin page resolves them one by one.
- **F2 — piuscores supplementation**: a linked player with under 50 board rows rounds out their chart list from their own ledger bests, flagged `Supplemented` (corner badge on the cards; board rows stay the source for every stat tile).
- **F3 — What It Takes**: fifth hub view — grade-anchored cutlines off the PUMBILITY boards (`CutlineCalculator`, SG plates assumed, All/Singles/Doubles), tier ladder, two stepline charts, landmark table.
- **G1 — Popularity highlights (mock round)**: biggest movers + hottest/coldest per folder proposed above the popularity board; the build follows owner sign-off on the mock.
- **G2 — The co-op board**: fourth Rankings option computed from the mirrored co-op boards. `CoOpBoardCalculator` prices placements with the mix's pumbility formula on the engine's flat co-op base — the flat base means the base value scales the display but never the order — with plates inferred from score (SG below 995,000, UG to 999,999, PG at the perfect). A disclaimer names it a best guess; the top-50 expando filters by chart type so the estimate scale never mixes into real PUMBILITY views.
- **G3 — Folder filter on popularity**: the tier-list `FolderPicker` (grown an additive nullable "All Folders" state) narrows the board and the songs rollup before the top-100 cut; places stay global.
- **G4 — Rankings columns**: the number-ones column removed contract-deep (the Players profile tile keeps its own count); the Boards number links into the Players view with that player selected, and `?view=players&player=TAG` deep-links via replaceState.
