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
  | B | **1.20** | **1.25** |
  | C | **1.10** | **1.20** |
  | D | **1.00** | **1.10** |
  | F | **0.90** | *1.00 (inferred)* |

  Everything else is shared: the other six plates (RG 0.000 · FG 0.002 · TG 0.004 · MG 0.006 ·
  SG 0.008 · PG 0.020) and the top of the ladder (AA+ 1.39 · AAA 1.41 · AAA+ 1.43 · S 1.45 →
  SSS+ 1.50).

  ⚠⚠ **A passing F is a REAL RUNG, and the "F is an exclusion" rule was wrong — reversed by a
  live reading on 2026-08-14.** The history matters because it reversed *twice*: F shipped at
  0.90 (interpolated), was ruled an exclusion pricing zero on 2026-08-12 (owner's game
  knowledge; the one F ever seen rendering on the breakdown page showed 0.00), and then a
  deliberately played F settled it — *Monkey Fingers* S12 F MG, official **176.67** =
  Base(13) 195 × (0.90 + 0.006), exact to the cent. The 0.00 that had supported the exclusion
  turned out to be **the sub-10 rule wearing an F grade** — that chart was below level 10, so
  its zero said nothing about F at all. The 0.90 interpolation had been right the whole time.
  Only a **break** and a **sub-10 chart** price at zero. Consequences: the Doubles F is
  **inferred at 1.00** (the measured Singles D → F step is −0.10 and the type gap plateaus at
  −0.10 across C and D; both arguments land there) — and unlike the old exclusion the telemetry
  can settle it, because F rows price nonzero and `ScoringObservations` logs them like any
  other row. The `/Pumbility/Phoenix1` bite also softens: a 450k–499k Phoenix 1 score (P1 D, P2
  F) reprices to 0.90 × base rather than to nothing. And the decomposition's grade part goes
  **negative** on a passing F — the only rung below the ×1.00 reference — which `Decompose`'s
  consumers must tolerate.

  **Which cells are measured, and which are not (2026-08-14).** All sixteen plate × type cells
  are measured, the full Singles ladder **SSS+ → F** is measured, and Doubles is measured at
  every rung except F. Two inferences remain: **the Doubles F 1.00** (pinned by
  `DoublesFIsTheLastInferredRungAndContinuesTheBottomSteps`; the first imported Doubles F
  settles it) and the **base curve above level 27** — see below.

  **The ladder's real shape, now read rather than fitted.** Singles runs −0.05 a rung from A+
  down through C and then **−0.10 per rung across the bottom: C 1.10 → D 1.00 → F 0.90**.
  Doubles mirrors it one notch higher: −0.05 a rung from A+ to C (1.35 · 1.30 · 1.25 · 1.20),
  then −0.10 to D 1.10 (and, inferred, −0.10 again to F 1.00). The widening bottom step is the
  whole story of the guesses this table used to carry: extrapolating the uniform −0.05 produced
  the right A and the right B and the **wrong D**, and the "exclusion" reading of F survived
  only until someone actually played one. Do not describe the ladder as uniform — only the
  A+ → C stretch is.

  **A on Doubles was a guess and is now measured — at exactly the guessed value.** Five import
  rows (2026-08-13/14) across four levels and three plates all imply 1.3000 and nothing else:
  D24 MG 326.50 = Base(24) 250 × 1.306 · D25 RG 338.00 = 260 × 1.300 · D26 FG 351.54 =
  270 × 1.302 · D27 FG 364.56 = 280 × 1.302. Beyond closing the cell it corroborates the −0.05
  step across the upper ladder rather than merely assuming it. Note where
  the remaining evidence gap actually sits — telemetry has never priced a chart above level 27,
  so **Base(28) = 290 and Base(29) = 300 are extrapolation**, the post-24 kink being confirmed
  only at 25, 26 and 27. The five charts up there are all Doubles (*1949*, *Dead End*,
  *Neo Catharsis*, *Paradoxx* at 28; *1948* at 29) and none has entered an imported pool.

  **A board reconciliation was run against Base(28) and could not sharpen it — do not re-run it
  expecting a solve (2026-08-14).** Exactly one D28 pass exists in the mirrored world boards:
  FEFEMZ#1489's 966,723 (AAA+) on *Neo Catharsis*, constant across the 07-26/08-02/08-09
  snapshots while his official PUMBILITY Doubles total is known at each. Reconstructing his
  doubles top-50 from his chart-board rows and bracketing every unknowable plate RG → UG puts
  the official value **inside the bracket at Base(28) = 290 on all three snapshots** — the rung
  reconciles. But the bracket arithmetic is the finding: fifty plate-unknown rows make the
  bracket ~200 points wide, a ±10 change in Base(28) moves the reconstruction ~14, and where a
  player sits inside his own bracket is a per-player plate-profile trait that varies by more
  than that (six no-28 controls sit at stable positions spanning 0.23–0.41). FEFEMZ's own trait
  is unmeasurable — his pre-28-era and singles coverage are both eviction-truncated — so the
  data is consistent with 290 and equally with anything in roughly [285, 320]. Two things it
  does say: **exclusion is disfavored** (a 28 paying zero would put him above every control
  observation at every snapshot), and **no D29 pass exists anywhere in the mirror** — *1948*'s
  board is empty in every snapshot. What would actually close the rung: any imported player's
  breakdown page with a 28 in the pool, the same instrument that closed everything else.

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
  into a full top-50. **Post-deploy: press "Recalculate Phoenix 2 Player Ratings."** (Deploy-state
  note: the F→zero change merged in #264 never deployed, so production priced F at 0.90 all
  along — on Singles, the value the Monkey Fingers reading proved correct. Production's Doubles
  D 1.15/F 0.90 still need this PR's 1.10/1.00.)

  **B on Doubles closed the same day, and that guess was right: 1.25 exactly.** *Danger & Danger*
  D10 EG B at **227.16** = Base(10) 180 × 1.262, minus the 0.012 a Doubles Extreme Game pays.

  **How the bottom readings were obtained, because it is the reusable part.** None came from
  the elite grind — a B, C, D or F never survives into a full top-50 pool, which is exactly why
  these cells stayed open for so long. They came from **a pool holding fewer than fifty
  charts**, where every chart enters regardless of what it contributes. The Doubles C first
  arrived that way by accident on a beginner's D12; the Doubles B, C and D were then played
  *deliberately* on D10s, and the Singles F that overturned the exclusion the same way on an
  S12 (surviving the stage while scoring under 500k, F with a Marvelous Game plate). Anything
  still missing from this formula is reachable the same way: make a small pool and play the
  case you need.

  **The competing fit is now REFUTED, not merely declined — do not re-derive it.** The Singles
  ladder *widens* going down (steps 0.03/0.05/0.08/0.10/0.10) rather than holding a uniform step,
  and a fit assuming the Doubles ladder had that same shape landed on **A 1.32 and B 1.27**,
  hitting the anchors of the day (A+ 1.35 and C 1.20) just as well as the uniform reading did. It
  was declined on the owner's call, 2026-08-12, as not worth spending on because no player could
  see the difference. Play settled it instead, and **refuted it twice**: A measured **1.30, not
  1.32**, and B measured **1.25, not 1.27**. The shape-matching fit is dead on both of its own
  predictions, and the even-split reading it competed with was right on both.

  Worth keeping the episode rather than deleting it, because the two fits behaved exactly as fits
  do: they agreed on every cell that had been observed and disagreed only on the ones that had
  not — precisely where one of them was being used. And the reasoning that won here still is not
  a law: the same even-step logic that nailed A and B produced the **wrong D**, because the step
  changes at the bottom of the ladder. Fitting got three of four; only play could say which one
  it had missed.

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
