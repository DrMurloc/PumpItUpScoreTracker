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
  (48 pinned as golden unit tests). Per chart: `Base(pricedLevel) × (gradeMultiplier + plateBonus)` —
  ADDITIVE grade+plate, `Base = 130 + 5·L + 5·max(0, L−24)`; the per-type Singles and
  Doubles totals are each a top-50 pool, but overall PUMBILITY is ONE merged top-50 across
  both types (corrected 2026-07-13, see below). **Launch-era corrections (2026-07-19, read
  per-chart off the official breakdown page `my_page/pumbility.php` and reproduced to the
  cent by `LiveSite/PumbilityOfficialReconciliationTests`): singles price one level UP the
  base curve (an S17 is worth `Base(18)`; the pre-launch xlsx singles rows priced at
  `Base(level)` are superseded), charts below level 10 price at ZERO.** Broken plays never
  count (owner-confirmed, and the breakdown page prices them 0.00). Everything dispatches
  through `ScoringConfiguration.PumbilityScoring(mix, …)`; Phoenix arm byte-identical.

- **BOTH constant tables are per chart type.** Production import telemetry
  (`ScoringObservations`, thousands of per-chart rows off live pools) priced all sixteen plate ×
  chart-type cells and most of the grade ladder. The two tables disagree in eight places:

  | | Singles | Doubles |
  |---|---|---|
  | Extreme Game | **0.014** | 0.012 |
  | Ultimate Game | **0.017** | 0.016 |
  | AA | **1.36** | 1.37 |
  | A+ | **1.33** | 1.35 |
  | A | **1.28** | **1.30** |
  | B | **1.20** | *1.25 (inferred)* |
  | C | **1.10** | **1.20** |
  | D | **1.00** | *1.15 (inferred)* |

  Everything else is shared: the other six plates (RG 0.000 · FG 0.002 · TG 0.004 · MG 0.006 ·
  SG 0.008 · PG 0.020) and the top of the ladder (AA+ 1.39 · AAA 1.41 · AAA+ 1.43 · S 1.45 →
  SSS+ 1.50). **An F is not a rung — it contributes zero**, on both types, the same as a break
  or a sub-10 chart, and that includes a *passing* F. It has to be an exclusion in the formula
  rather than a 0.0 multiplier, because grade and plate ADD here and a zero multiplier alone
  would still pay the plate bonus.

  ⚠ **This one rests on the owner's knowledge of the game (2026-08-12), not on a reading, and no
  instrument we have can check it.** `my_page/pumbility.php` publishes a top 50; an F is
  essentially never in one, and `ScoringObservations` skips any row priced at zero — so an F is
  unobservable by construction and its absence from the telemetry is not evidence either way.
  The competing explanation, if it is ever worth revisiting, is that a chart missing from a
  top-50 page is not *priced* at zero but simply not in the pool. Where the rule bites hardest
  is a sparse Phoenix 2 account and `/Pumbility/Phoenix1`, where a 450k–499k Phoenix 1 score is
  a P1 **D** but a P2 **F** and so reprices from about 0.9 × base to nothing.

  **Which cells are measured and which are guesses.** All eight plate × type cells on each side
  are measured, the whole Singles grade ladder is measured, and on Doubles every rung from AA
  down to D is measured now as well. **B on Doubles is the only guess left in either table.** It
  splits the −0.10 between the measured rungs either side of it (A 1.30 and C 1.20), and is
  pinned by `DoublesBIsTheLastInferredRungAndSplitsTwoMeasuredNeighbours` so that replacing it
  with a live reading is deliberate rather than silent. A Double priced at B closes the last of
  it. F is the one cell no reading can ever fill — see below.

  ⚠ **Do not justify B as "the ladder's uniform −0.05 step" — the ladder is not uniform.** Every
  step from AA down to C is −0.05 or narrower, but **C → D is −0.10**, and that is measured, not
  fitted. The A → C span B sits inside is the only uniform stretch left, and that span is all the
  argument B can carry.

  **A on Doubles was a guess and is now measured — at exactly the guessed value.** Five import
  rows (2026-08-13/14) across four levels and three plates all imply 1.3000 and nothing else:
  D24 MG 326.50 = Base(24) 250 × 1.306 · D25 RG 338.00 = 260 × 1.300 · D26 FG 351.54 =
  270 × 1.302 · D27 FG 364.56 = 280 × 1.302. That does two things beyond closing the cell: it
  corroborates the −0.05 step across the upper ladder rather than merely assuming it, and it
  moves B from *reaching down from A+* to *interpolating between two measurements*. Note where
  the remaining evidence gap actually sits — telemetry has never priced a chart above level 27,
  so **Base(28) = 290 and Base(29) = 300 are extrapolation**, the post-24 kink being confirmed
  only at 25, 26 and 27. The five charts up there are all Doubles (*1949*, *Dead End*,
  *Neo Catharsis*, *Paradoxx* at 28; *1948* at 29) and none has entered an imported pool.

  **D on Doubles was a guess too, and that one was WRONG — it is 1.10, not the extrapolated
  1.15.** Played deliberately to close the cell (2026-08-14) and read off the breakdown page as a
  pair on the same level and plate, which is what makes it airtight: *Your Mind* D10 MG C at
  **217.08** and *Switronic* D10 MG D at **199.08**, both over Base(10) = 180. They differ by
  grade alone, so the 18.00 between them is 0.10 of base whatever Marvelous Game is worth, and
  the C independently confirms 1.20 a second time at a second level. Two consequences. The step
  from C is **double** the −0.05 the ladder holds higher up, which is the thing that made the
  extrapolation wrong rather than merely unlucky. And the type gap at D closes from −0.15 to
  −0.10, matching C's, so the gap **plateaus** across the bottom two rungs instead of widening —
  the shape the old value implied.

  ⚠ **This one changed a shipped price**, unlike the A reading: a Doubles D was being paid
  `0.05 × base` too much (9 points on a D10). It only ever reached beginners — a D never survives
  into a full top-50 — but that is the same population the passing-F bug hit, and they compound.
  **Post-deploy: press "Recalculate Phoenix 2 Player Ratings."**

  **Where the last cell will come from.** Not from strong players: a Double scored in the B
  band (700k–799k) essentially never survives into a top-50 pool. It comes from **a player with
  fewer than fifty charts**, where every chart enters the pool regardless of what it contributes
  — which is how all three bottom readings arrived, on a D12 and a pair of D10s. So B fills from
  a small pool, whether a beginner's or one deliberately made, not from the elite grind.

  **The competing fit is now REFUTED, not merely declined — do not re-derive it.** The Singles
  ladder *widens* going down (steps 0.03/0.05/0.08/0.10/0.10) rather than holding a uniform step,
  and a fit assuming the Doubles ladder had that same shape landed on **A 1.32 and B 1.27**,
  hitting the anchors of the day (A+ 1.35 and C 1.20) just as well as the uniform reading did. It
  was declined on the owner's call, 2026-08-12, as not worth spending on because no player could
  see the difference. Play settled it instead: **A measured 1.30, not 1.32**, so the
  shape-matching fit is dead and its B 1.27 goes with it — which is why B stays at the even split
  of 1.25.

  Worth keeping the episode rather than deleting it, because the two fits behaved exactly as
  fits do: they agreed on every cell that had been observed and disagreed only on the ones that
  had not, which is precisely where one of them was being used. The bottom of this ladder has now
  produced one guess that was right (A) and one that was wrong (D), from the same reasoning, on
  the same day's evidence.

  **What the Doubles A rung visibly decides.** `/Titles` projects a folder per reference grade
  by pricing fifty charts of that folder, so at the bottom reference grade the highest Doubles
  folder sets a ceiling: fifty D29s at an A on a Talented Game plate. At A 1.28 that ceiling is
  19,260 and at 1.30 it is 19,560, which moves `[D] EXPERT LV.9` (19,300), `[D] EXPERT LV.10`
  (19,400) and `DOUBLE MASTER` (19,500) from "no folder reaches this at A" to "D29 does". The
  multiplier deciding that is now measured, so the caveat that used to sit here has **moved
  rather than cleared**: the ceiling is `50 × Base(29) × 1.304`, and `Base(29) = 300` is the
  extrapolated end of the curve — nothing above level 27 has ever been priced, so the 0.3%
  margin on `DOUBLE MASTER` now rests on the base rather than on the grade. Left as is
  deliberately, and for the reason it always was: a
  Doubles pool cannot exceed the merged total, and the highest merged total on the mirrored world
  board is 19,638.92, so all three rungs sit at or past the current world frontier and the claim
  is being made to approximately nobody.

  Two July conclusions were wrong for one type each, and in the same way. The community's
  singles plate values were called a data error (owner call 2026-07-09) — two of the three
  were right, and only the Rough Game −0.010 stays refuted. The A+/AA re-derivation to
  1.33/1.36 was reconstructed from **singles-tab** players, so it was answering for one chart
  type while overwriting the value of the other; the pre-launch 1.35/1.37 it dismissed as
  location-test tuning were doubles observations, and the live page served them again
  unprompted. C repeated the pattern a third time: three Singles rows agreed on 1.10 and were
  taken for the shared value until one Double row read 1.20. **A pool reconstructed from one
  chart type cannot settle a constant for both.**

  `SkillRating` on P2 rows is the merged top-50, so it no longer equals
  `SinglesRating + DoublesRating`; S/D pool gains mint their own
  milestones (P2 only). Exit path for constant adjustments: edit the config, then press
  **Re-price {mix} ratings** on `/Admin` (`RecalculateMixRatingsCommand` bus sweep).
- **All 272 Phoenix 2 titles landed** (crawled authenticated from my_page/title.php 2026-07-09):
  [S]/[D] ladders + 8 hidden total tiers gate on the pool values (`Phoenix2PumbilityTitle`;
  `[P.B] BRONZE`..`ALEXANDRITE` (10000..19000) confirmed from worn titles on the live PUMBILITY
  ranking 2026-07-23, the 20000 tier still unreached so its name stays masked); nine skill ladders
  (chart + SSS, `Phoenix2ChartGradeTitle`) with EXPERT/SPECIALIST metas; 34 boss breakers
  (`Phoenix2ChartClearTitle`; `1948 D??` matches any level); step-artist/play-count/CO-OP/judgment
  badges site-detected only. **CO-OP Rating is deliberately not computed** — the site prices one
  and quotes it in title.php's requirement text, but surfaces it on no leaderboard and in no
  per-chart breakdown, so a computed value would have nothing to agree or disagree with (owner,
  2026-08-12). Reading the worn title off the account is the whole answer. **The `/Titles` page is live
  for Phoenix 2** (2026-07-21) — renders through the same grid as Phoenix. **`[Legacy]` titles
  are deliberately excluded**: the site ports the Phoenix 1 titles into Phoenix 2 prefixed
  `[Legacy]`, and we already carry the real Phoenix 1 list, so mirroring them would double every
  Phoenix title under a second mix.
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

## Update 2026-07-13 — overall PUMBILITY is a merged top-50, not two pools summed (bug fix)

The 2026-07-09 note was wrong on one point: overall ("All" tab) PUMBILITY is a SINGLE top-50
across the merged Singles+Doubles set, not the two per-type pools summed. Reverse-engineered
from the live `pumbility_ranking.php` board (authenticated crawl, 597 dual-type players matched
across the All / `s` / `d` tabs): every player satisfies `max(S,D) <= All <= S+D`, and ~19% sit
strictly below `S+D` — impossible under a two-pool sum, which forces `All == S+D`. The per-type
Singles/Doubles pools (the `?t=s` / `?t=d` boards, and `SinglesRating`/`DoublesRating`) were
already correct.

Fixed: `PlayerRatingSaga.RecalculateCore` (`SkillRating` = merged top-50, same shape as Phoenix),
`Phoenix2TitleList.BuildProgress` (Total ladder = merged top-50), and `PumbilityProjectionSaga`
(one mixed pool). The per-chart formula, `SinglesRating`, and `DoublesRating` are unchanged.
Locked by a live canary (`Phoenix2PumbilityAggregationTests`) and a >50-chart unit test
(`Phoenix2SkillRatingIsAMergedTop50NotTwoPoolsSummed`). **Post-deploy: press
"Re-price Phoenix 2 ratings" on `/Admin` once** — stored P2 `SkillRating` and Total-tier
titles carry the old inflated two-pool total until the sweep recomputes them.

> **What a formula change does and does not leave stale.** Every PUMBILITY figure a player can
> see — the `/Pumbility` pages, the pool, the title track, `api/phoenixScores` — is computed
> from raw scores at read time, so it is correct the moment the release deploys. Only the
> STORED aggregates go stale: `PlayerStats` ratings and the per-chart `PhoenixRecordStats`.
> Those refresh when that player next imports and never otherwise, which is why the sweep
> exists. It is deliberately **silent** — no milestones, no history rows, no ratings-improved
> event, nothing to Discord — because a constant moving is not something a player did, and
> announcing it would tell thousands of people they gained PUMBILITY on a day they did not
> play. Safe to re-run; it recomputes rather than accumulates.
>
> The season-recap finale's projected total was patched by a separate
> `RebuildRecapTotalPumbilityCommand`, which **no longer has a consumer or a button** (deleted
> with the other one-time admin presses). New recaps compute correctly; existing ones keep
> their old projected total unless that path is rebuilt.

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
