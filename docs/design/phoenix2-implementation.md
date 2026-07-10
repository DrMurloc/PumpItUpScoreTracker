# Phoenix 2 parallel-mix implementation plan

Working plan for the `claude/phoenix2-parallel-mix` branch. Each commit is independently buildable,
keeps `main` releasable, and ships dark until the final exposure commit. Check boxes as commits land
so a broken session can resume from the last checked item.

## Locked decisions (owner, 2026-07-04)

- Everything Phoenix 1 does works for Phoenix 2 **in parallel**, keyed by mix, following the user's
  current-mix selection. XX stays in its legacy tables/paths — untouched.
- **No toggle.** Phoenix 2 becomes selectable the moment the release deploys. "Coming soon" states
  instead for: **Titles** (empty Phoenix 2 title list, incl. difficulty titles), **WhatShouldIPlay**,
  **score import** (backend wired as Phoenix-1-identical; UI gated until the owner verifies against
  his own kit), and **official leaderboards / world rankings** (P2 mirror deferred — the P2 site
  replaced per-level rating boards with one daily Pumbility board, `?t=` filter, login-gated).
  **Tier lists fall back to the Phoenix 1 list with a "provisional" badge** until P2 data accumulates.
- `MixEnum.Phoenix2` DB Guid = `a9b7d3c1-52e8-4f06-9b1a-2f8c33e01948` — `MixIds.cs` and the
  production seed script are a matched pair. `scores.Mix.Name` widens to 10 (`Enum.Parse` round-trip;
  DB name is literally `Phoenix2`, display name "Phoenix 2").
- Default mix for new users stays **Phoenix** at release. Displayed ratings/titles follow the
  **viewer's** current mix, blank when the player has no data in it.
- `/Login/PiuGame` keeps **Phoenix 1 as the identity source**; P2 card aliases backfill additively on
  first P2 import. The `"INVALID"` account sentinel must stop conflating "authenticated but no
  card/gametag associated" (everyone's launch-week state on P2) with bad credentials.
- Pumbility assumed unchanged for now (the P2 site's own copy confirms top-50 aggregation) but keyed
  per mix everywhere — owner expects Pumbility AND Titles to change in some form; structure for the pivot.
- Weekly challenge runs **parallel boards per mix**; Phoenix 1 weeklies run forever.
- Discord announcements get a **"[Phoenix 2]" prefix** while both mixes are active (no emoji dependency).
- API: **additive optional `mix` parameter on `api/*`, default = Phoenix permanently** (never the
  caller's current mix). Contract-test updates are deliberate, additive diffs.
- Tournaments are mix-agnostic; stamina session registration gets a mix dropdown stored on the
  session (chart pool + level fallback come from the session's mix; `TournamentChartLevel` snapshot
  overrides stay one-per-chart). Qualifiers configuration pins a mix; existing rows backfill Phoenix.
- 1948 D29 renders as "??" on the P2 site but is functionally a 29 — import parser needs a ??→29
  level fallback, and the anchored stepball regexes need to accept the `/l_img/p2/` path segment.

## Update 2026-07-09 — PUMBILITY formula + titles LANDED (supersedes two assumptions above)

The "Pumbility assumed unchanged" and "empty Phoenix 2 title list" placeholders are resolved; both
shipped on `claude/phoenix2-pumbility-crawl-cf2710`:

- **Phoenix 2 PUMBILITY confirmed different and implemented.** Reverse-engineered from the live
  pumbility rankings + per-chart boards, validated against owner-collected real per-chart values
  (48 pinned as golden unit tests). Per chart: `Base(level) × (gradeMultiplier + plateBonus)` —
  ADDITIVE grade+plate, `Base = 130 + 5·L + 5·max(0, L−24)`; totals are two independent top-50
  pools (Singles + Doubles) summed. Grades 1.35 (A+) → 1.50 (SSS+); plates RG 0.000 → PG +0.020
  (doubles-verified table applied to both types — the community's singles-specific UG/EG/RG values
  treated as data error, owner call 2026-07-09; TODO in `ScoringConfiguration`). Below-A+ grades
  pattern-extended, unverified. Broken plays never count (owner-confirmed). Everything dispatches
  through `ScoringConfiguration.PumbilityScoring(mix, …)`; Phoenix arm byte-identical.
  `SkillRating = SinglesRating + DoublesRating` on P2 rows; S/D pool gains mint their own
  milestones (P2 only). Exit path for constant adjustments: edit the config, hit the admin
  "Recalculate Phoenix 2 Player Ratings" button (`RecalculateMixRatingsCommand` bus sweep).
- **All 272 Phoenix 2 titles landed** (crawled authenticated from my_page/title.php 2026-07-09):
  [S]/[D] ladders + 8 hidden total tiers gate on the pool values (`Phoenix2PumbilityTitle`;
  `[P.B] BRONZE` observed live, 7 placeholder names pending reveals); nine skill ladders
  (chart + SSS, `Phoenix2ChartGradeTitle`) with EXPERT/SPECIALIST metas; 34 boss breakers
  (`Phoenix2ChartClearTitle`; `1948 D??` matches any level); step-artist/play-count/CO-OP/judgment
  badges site-detected only (CO-OP Rating formula unknown — TODO).
- **Avatar hosts split by era**: P2 serves `/data/avatar_img2/` — `ImageRegex` accepts both, and
  BOTH shapes are pinned by approval fixtures (this was the recurring avatar bug).
- **P2 leaderboard mirror / world rankings: LANDED (same branch, C10–C14).** The P2 site
  replaced per-level rating boards with `leaderboard/pumbility_ranking.php` (`?t=` = All / `s` /
  `d`, `&page=` pagination, daily 01:00 GMT+9 recompute) — those three tabs now import as the
  mix's "Rating" leaderboards (`PUMBILITY` / `PUMBILITY Singles` / `PUMBILITY Doubles`).
  **piugame.com serves no anonymous ranking traffic** (verified 2026-07-09: the pumbility board
  AND the over_ranking chart list are login-gated; individual chart boards are public), so P2
  imports authenticate with `PiuGame:ServiceUsername`/`ServicePassword` — a dedicated dummy
  account — and fail loudly when unset. Repository reads/clears/world-rankings went per-mix
  end-to-end (chart-board names collide across mixes). The three OfficialLeaderboards pages
  un-gated; `start-phoenix2-leaderboard-import` runs Sundays 16:30 UTC.

## Commit sequence

- [x] **Commit 0 — Remove dead Tesseract dependency; correct OCR doc claims.**
  There is no OCR feature (XX-era experiment; only the package reference survived).
  `ScoreTracker.Web.csproj`, `LayerDependencyTests` allowlist, CLAUDE.md Web row,
  ARCHITECTURE.md "/UploadPhoenixScores (bulk import + OCR)", TECHNOLOGIES.md Tesseract section
  (`PhoenixScoreFileExtractor` is a CsvHelper spreadsheet parser), journal source-kind comments
  (`ocr` out of `ScoreEventJournalEntity` + DATABASE-SCHEMA.md row). Verify: build + fast suites.

- [x] **Commit 1 — Mix model foundation (ships dark).**
  `MixEnum.Phoenix2`; `MixIds` entry (Guid above); migration widening `scores.Mix.Name` to 10;
  `MixEnum` display-name helper ("Phoenix 2"); MainLayout mix menu pinned to an explicit
  (XX, Phoenix) list so the new enum value does NOT auto-appear (the final commit expands it);
  audit every `switch`/ternary on `MixEnum` (esp. `TitleSaga`'s "not XX ⇒ Phoenix" dispatch and
  `MixIds.For`) so Phoenix2 either routes correctly or throws loudly — no silent Phoenix-1 fallthrough.

- [x] **Commit 2 — Journal write path takes the mix.**
  `UpdatePhoenixBestAttemptCommand` gains `MixEnum` (default Phoenix); handler → journal append;
  `EFScoreJournalRepository` drops the hardcoded `MixIds.Phoenix`. The journal is the only
  non-recomputable store — this lands before any P2 score can exist. Component tests.

- [x] **Commit 3a — ScoreLedger schema: MixId + Phoenix backfill.**
  `PhoenixRecords` (unique index → UserId+ChartId+MixId — biggest table, deploy-gated index rebuild),
  `PhoenixRecordStats`. Entity + repo + migration + DATABASE-SCHEMA.md rows.

- [x] **Commit 3b — PlayerProgress + ChartIntelligence schema.**
  `PlayerStats` (PK → UserId+MixId), `PlayerHistory`, `UserTitle`, `UserHighestTitle` (PK → UserId+MixId),
  `TierListEntry` (+MixId), `CoOpRating` (+MixId). Backfill Phoenix everywhere.

- [x] **Commit 3c — WeeklyChallenge + EventCompetition + OfficialMirror schema.**
  `WeeklyTournamentChart`, `UserWeeklyPlacing`, `QualifiersConfiguration` (+MixId column, backfill
  Phoenix), `UserTournamentSession` (+MixId), `UserOfficialLeaderboard`, `UserWorldRanking`,
  `OfficialLeaderboardImportState` (singleton row → per-mix).

- [x] **Commit 4 — Read/write ports take the mix.**
  `IScoreReader` (all methods), EF repositories filter by mix, every call site passes an explicit
  mix (behavior identical: callers pass Phoenix until the UI plumbing commit). Score-recording
  commands and the API record endpoint thread mix through.

- [x] **Commit 5 — Bus events carry the mix.**
  `PlayerScoresUpdatedEvent`, `ScoreImportCompletedEvent`, `ImportStatusUpdatedEvent`,
  `PlayerStatsUpdatedEvent`, `TitlesDetectedEvent`, `NewTitlesAcquiredEvent`,
  `UserWeeklyChartsProgressedEvent` + consumers route by mix; `ContractEventSerializationTests`
  updated deliberately; Discord "[Phoenix 2]" prefix in CommunitySaga message builders.

- [x] **Commit 6 — Sagas un-hardcoded; per-mix computation.**
  TierListSaga / ScoringDifficultySaga / PlayerRatingSaga / PumbilityProjectionSaga /
  RecommendedChartsSaga / WeeklyTournamentSaga / CommunitySaga: replace ~30 `MixEnum.Phoenix`
  literals with parameterized mix; weekly rotation runs per mix (guard: skip a mix with no charts);
  tier-list read path returns badged P1 fallback when the P2 list is empty; `Phoenix2TitleList`
  exists and is EMPTY; Pumbility/stats recompute per mix.

- [x] **Commit 7 — Web UI plumbing + under-construction states.**
  Pages pass current mix everywhere (Charts, Progress, Pumbility, records, CSV upload's
  `GetChartQuery`, UserLabel/leaderboards use viewer mix); recording flows stamp mix; "Coming soon"
  states for Titles / WhatShouldIPlay / Import / OfficialLeaderboards under Phoenix 2; tier-list
  provisional badge; new localization keys populated in all eight locales.

- [x] **Commit 8 — API mix parameter.**
  Optional `mix` query param (default Phoenix) on phoenixScores GET/record + tier-list/chart
  endpoints as applicable; contract tests extended additively; API.md updated.

- [x] **Commit 9 — Import backend, dormant behind the Coming-soon UI.**
  Per-mix `PiuGameConfiguration` (mix → BaseUrl/AmPassUrl; P2 = piugame.com); stepball regexes accept
  `/l_img/p2/`; ??→29 level fallback (1948 D29); INVALID-sentinel → typed "no profile associated"
  result distinct from bad credentials; `ImportOfficialPlayerScoresCommand` + saga thread the mix;
  P2 card aliases backfill on import. E2E stays P1-only until kit fixtures exist.

- [x] **Commit 10 — Exposure.**
  MainLayout mix menu includes Phoenix 2 (display names); docs pass (ARCHITECTURE / DATABASE-SCHEMA /
  API / SCHEDULED-JOBS as touched); release-notes draft for the owner (under-construction list).

## Release-notes draft (owner to edit)

> **Phoenix 2 support is here.** You can now switch to Phoenix 2 from the Mix menu — scores,
> progress, Pumbility, tier lists, weekly challenge, communities, and tournaments all track
> Phoenix 2 separately from Phoenix, and your Phoenix data is untouched.
>
> Still under construction while we verify against real hardware:
> - **Titles** — Phoenix 2's title list hasn't been revealed yet.
> - **What Should I Play** — recommendations return after launch.
> - **Score import** — opens after we verify the importer against a real Phoenix 2 machine.
>   (CSV upload and manual recording work now.)
> - **Official leaderboards / world rankings** — the official site changed how rankings work;
>   the mirror returns later.
> - **Tier lists** show Phoenix data marked *Provisional* until enough Phoenix 2 scores exist.
>
> Tournaments are mix-agnostic: stamina sessions let you pick the mix you played on, and
> qualifiers state which mix they run on.

## Launch runbook (owner-driven)

1. Merge + deploy the release (migration bundle applies in the gated stage).
2. Immediately run the regenerated seed script (`PIU Phoenix 2 - ChartMix seed.sql`, currently in the
   owner's Downloads; regenerate from a fresh site sweep in launch week — data is pre-release).
3. Announce, with the under-construction list.
4. After kit verification: un-gate import UI (and later, the real Phoenix 2 title list, WSIP, mirror).

## Post-release track (separate PRs)

- Admin "paste JSON blob" new-song/chart tool (Phoenix2-only; `koreanName` REQUIRED — it feeds the
  `ko-KR` culture-name rows that Korean-session imports match against). **Landed early** (owner
  pulled it forward to test locally): `/Admin/BulkAddCharts`, schema contract in
  [new-charts-json.md](new-charts-json.md), source images auto-copied to the CDN on Confirm via
  `IFileUploadClient.CopyFromSource`.
- Documented "check for new Phoenix 2 charts" workflow (YouTube watermark walk of the official
  channel for charts/videos/artists/BPM → official site for canonical English/Korean names and
  song images → paste-ready JSON; see the collection-workflow section of
  [new-charts-json.md](new-charts-json.md)).
- Phoenix 2 leaderboard mirror / world rankings (new pumbility_ranking semantics, authenticated scraping).
- Rivals-page features (blocked on card association; scrape surface exists at /my_page/rival.php).
