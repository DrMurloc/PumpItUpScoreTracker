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

- [ ] **Commit 3c — WeeklyChallenge + EventCompetition + OfficialMirror schema.**
  `WeeklyTournamentChart`, `UserWeeklyPlacing`, `QualifiersConfiguration` (+MixId column, backfill
  Phoenix), `UserTournamentSession` (+MixId), `UserOfficialLeaderboard`, `UserWorldRanking`,
  `OfficialLeaderboardImportState` (singleton row → per-mix).

- [ ] **Commit 4 — Read/write ports take the mix.**
  `IScoreReader` (all methods), EF repositories filter by mix, every call site passes an explicit
  mix (behavior identical: callers pass Phoenix until the UI plumbing commit). Score-recording
  commands and the API record endpoint thread mix through.

- [ ] **Commit 5 — Bus events carry the mix.**
  `PlayerScoresUpdatedEvent`, `ScoreImportCompletedEvent`, `ImportStatusUpdatedEvent`,
  `PlayerStatsUpdatedEvent`, `TitlesDetectedEvent`, `NewTitlesAcquiredEvent`,
  `UserWeeklyChartsProgressedEvent` + consumers route by mix; `ContractEventSerializationTests`
  updated deliberately; Discord "[Phoenix 2]" prefix in CommunitySaga message builders.

- [ ] **Commit 6 — Sagas un-hardcoded; per-mix computation.**
  TierListSaga / ScoringDifficultySaga / PlayerRatingSaga / PumbilityProjectionSaga /
  RecommendedChartsSaga / WeeklyTournamentSaga / CommunitySaga: replace ~30 `MixEnum.Phoenix`
  literals with parameterized mix; weekly rotation runs per mix (guard: skip a mix with no charts);
  tier-list read path returns badged P1 fallback when the P2 list is empty; `Phoenix2TitleList`
  exists and is EMPTY; Pumbility/stats recompute per mix.

- [ ] **Commit 7 — Web UI plumbing + under-construction states.**
  Pages pass current mix everywhere (Charts, Progress, Pumbility, records, CSV upload's
  `GetChartQuery`, UserLabel/leaderboards use viewer mix); recording flows stamp mix; "Coming soon"
  states for Titles / WhatShouldIPlay / Import / OfficialLeaderboards under Phoenix 2; tier-list
  provisional badge; new localization keys populated in all eight locales.

- [ ] **Commit 8 — API mix parameter.**
  Optional `mix` query param (default Phoenix) on phoenixScores GET/record + tier-list/chart
  endpoints as applicable; contract tests extended additively; API.md updated.

- [ ] **Commit 9 — Import backend, dormant behind the Coming-soon UI.**
  Per-mix `PiuGameConfiguration` (mix → BaseUrl/AmPassUrl; P2 = piugame.com); stepball regexes accept
  `/l_img/p2/`; ??→29 level fallback (1948 D29); INVALID-sentinel → typed "no profile associated"
  result distinct from bad credentials; `ImportOfficialPlayerScoresCommand` + saga thread the mix;
  P2 card aliases backfill on import. E2E stays P1-only until kit fixtures exist.

- [ ] **Commit 10 — Exposure.**
  MainLayout mix menu includes Phoenix 2 (display names); docs pass (ARCHITECTURE / DATABASE-SCHEMA /
  API / SCHEDULED-JOBS as touched); release-notes draft for the owner (under-construction list).

## Launch runbook (owner-driven)

1. Merge + deploy the release (migration bundle applies in the gated stage).
2. Immediately run the regenerated seed script (`PIU Phoenix 2 - ChartMix seed.sql`, currently in the
   owner's Downloads; regenerate from a fresh site sweep in launch week — data is pre-release).
3. Announce, with the under-construction list.
4. After kit verification: un-gate import UI (and later, the real Phoenix 2 title list, WSIP, mirror).

## Post-release track (separate PRs)

- Admin "paste JSON blob" new-song/chart tool (Phoenix2-only; `koreanName` REQUIRED — it feeds the
  `ko-KR` culture-name rows that Korean-session imports match against; image URL downloaded and
  rehosted via `IFileUploadClient`).
- Documented "check for new Phoenix 2 charts" workflow (site sweep diff → official YouTube channel
  for videos/artists/BPM/Korean titles → song images from official leaderboard pages → paste-ready JSON).
- Phoenix 2 leaderboard mirror / world rankings (new pumbility_ranking semantics, authenticated scraping).
- Rivals-page features (blocked on card association; scrape surface exists at /my_page/rival.php).
