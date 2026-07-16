# Suggested Charts widget

Part of the home dashboard widget catalog — see [README.md](README.md) for architecture, locked
decisions (D1–D19), the widget registry contract, and the widget index. **Catalog-walk pick 1 — the
WSIP release blocker (shipped PR #144).**

---

One widget type, goal as config (D10). Owner decisions (2026-07-12): goal bundles with per-category
toggles; the deviation-powered **skill-gaps goal is HELD** pending owner iteration; **veto ✕ is
edit-mode only** (declutters browse — revisit if people complain); level config = **Any / Dynamic
(competitive level ± spread, follows the player) / Static (pinned range)** with a **chart-level vs
scoring-level basis** toggle (`GetChartScoringLevelsQuery`, printed-level fallback); **shuffle**
re-roll in the body meta row. The page's vestigial `LevelOffset` UI is superseded by the level modes.

- **Goal bundles** (`SuggestedGoal` → engine categories): *Title Hunt* = PushLevel + SkillTitles ·
  *Score Push* = PushPGs + ImproveTop50 + RevisitOldScores · *Fill Gaps* = FillScores · *Pumbility
  Push* = PushPumbility (PR #149: the gain-ranked projected targets that moved off the Account Stats
  widget — `ProjectPumbilityGainsQuery`, biggest overall-rating gain first, stamps "+N"; distinct from
  the random ImproveTop50, and has its own drawer preset). The Weekly category is dropped from the
  widget — the Weekly widget owns that board. Defaults per drawer preset: Score Push = Any level;
  Fill Gaps = Dynamic ±3.
- **Engine** (`RecommendedChartsSaga`): `GetRecommendedChartsQuery` gained additive `Categories` +
  `RecommendationLevelWindow` params — null = legacy, the WSIP page is untouched until cutover.
  An explicit window REPLACES the legacy per-category bands (fills CL−3..CL−1, old scores CL−2..CL)
  and filters the previously unbounded PG/Top-50 categories; title-driven categories ignore it.
  Category names live in `RecommendationCategories` consts (the pushing-title category's name is the
  title's own name).
- **Rendering** (through field-test rounds 2–3): wide sizes (columns > 1) render **horizontal card
  strips** — compact art-forward chart cards with hover-revealed pager arrows (delegated clicks in
  `dashboard-grid.js`, hidden on touch) and honest x-axis edge fades; 2x1 = one merged strip, 2x2 = a
  captioned strip per section. The tall 1x2 keeps `dash-targets` rows but **drops the song name**
  (art tooltip + details dialog carry it) — right column = `LetterGradeIcon` + score with per-category
  detail ("−1,656 to PG", "74 days old"). Fill Gaps rows never show scores (unpassed by definition);
  an optional **tier-list difficulty lens** (Pass/Score, `GetBlendedTierListQuery`, optional
  Personalized blend) fills the column instead, colored via `ThemeScales.DifficultyColor`. Dynamic
  level spreads are **asymmetric** (levels below / levels above). Instance titles follow the
  configured goal via `WidgetDescriptor.DynamicNameKey` so three rapid-fired presets wear distinct
  names. Sizes 1x2 / 2x1 / 2x2, default 1x2.
- **Feedback**: veto ✕ (edit mode) → WSIP's reason dialog (reason/notes/hide, hide default-on) →
  `SubmitFeedbackCommand` into the same per-category server-side store the engine already honors —
  deliberately NOT widget config. Thumbs-up = one-tap **Good Suggestion** in the shared
  ChartDetailsDialog, unlocked when the row click carries a suggestion category.
- **Shell extensions this widget introduced**: `WidgetDescriptor.DrawerPresets` (one add-drawer card
  per pre-filled config, D10) and `ChartClickContext` (the OnChartClick payload — chart + optional
  suggestion category; all widgets raise it).
- **Phoenix 2**: supported; the 272 P2 titles (PR #128) should light Title Hunt up — verify at field
  test, the old page-level P2 gate stays dead (D14).

---

## Hot Streak goal (decisions locked 2026-07-16 — BUILDING)

> **STATUS: fully scoped, all owner questions answered; in build on this section's commit plan.**
> Workshop mock (grouped/flat treatments, config panel, empty state):
> https://claude.ai/code/artifact/6291253d-ed3e-44a6-9bbb-6a2cd141eea5

**The pitch, in the owner's shape:** take the charts you *crushed* in recent sessions, and offer
the charts most like them that you haven't done. "That felt great — here's more of it."

**"Peers"** is the settled site term for *players near your competitive level* — the cohort the
bucket-cached machinery already ranks against. All Hot Streak copy uses it. (Account Stats still
says "competitive matches"; renaming it is deferred, deliberately out of this scope.)

### Locked decisions

| Decision | Call | Why |
|---|---|---|
| Name | **Hot Streak** | The offer is "more of what just worked", not "based on recent sessions". |
| Peers | **Players near your competitive level** — `GetChartScoreRankingsQuery` → `CohortScoreProvider` | The feared cohort-ranking cost is already solved: half-level buckets, 1h-cached per-chart distributions — the 2026-07-10 incident fix *is* the implementation. `Ranking` runs 0.0 (last) to 1.0 (first); the bar is `Ranking ≥ N/100`. |
| Seed gate | **CompetitiveImprover highlight flag AND the Peers bar** (bar slidable to **0 = off**) | `PlayerRatingSaga.FlagCompetitiveImprovers` already stamps a per-chart, per-session flag on exactly the charts that pulled a competitive level up. The flag finds what moved you; the bar (the config knob) picks the standouts within that. Bar at 0 skips the cohort reads entirely. |
| Push floor (targets) | **Per chart type: the 25th-percentile folder level of (that type's competitive top-50 ∪ that type's members of the Pumbility top-50)** | Owner's own formulation — a pool read, not a projection. Per-type because the competitive pools are per-type and Pumbility is moving type-specific; a combined floor would wall off a weaker side's entire lane. **No projected-score algorithms** (owner: the Pumbility one needs work — don't touch it). |
| Recency window | **Configurable: 30 days (default) / 90 days / 1 year / All time** | Approved after confirming `ScoreHighlight` is indexed `(UserId, MixId, OccurredAt)` — the seed read is a TOP-capped backward range scan over only *flagged* rows, so "All time" widens the scan's reach, not the page cost. |
| Layout | **Grouped by seed (default)** — caption per seed ("More like {seed} · you beat {N}% of Peers"), flat mode as config with "≈ {seed}" in the detail slot | Owner: grouped looks better. Dedupe always, both modes: each target once, under its newest seed (tie-break: higher similarity). Percent omitted from captions when the bar is 0. |
| Right column | **Personalized Pass-lens blended tier** (targets are unpassed); old-score targets show their stale score + age | Owner: "super good." Pass difficulty is the honest read for unpassed charts. |
| Old-scores toggle | "Treat very old scores as unplayed" — outdated = `AgeOutlierWeights` weight < 1.0 | Already exactly the owner's category (30-day grace floor + own-record age outlier). Composes with the push floor: the S9 passed three years ago fails the floor regardless. |
| Level config | **None** | The similarity graph gates at folder ±1 ∩ scoring ±1.25, so expansion inherits "near what you just played" — like Title Hunt, this goal pins its own levels. |
| Empty state | "No matching standouts yet, go push yourself to start getting suggestions!" | Flags only exist for sessions that raised a competitive level — a coasting month legitimately runs dry, and the copy says what to do about it. |
| Veto/feedback | Category key `"Hot Streak"`, one key across both layouts | Captions are display-only; the feedback store must not fragment per seed. |

Defaults taken without a decision round (flip any at field test): seeds from CL-improver flags only
(no Pumbility-side seed source — the floor carries Pumbility relevance on the target side, and the
pools overlap heavily); the 25th percentile is a code constant, not config; within-group ordering
happens widget-side by the tier it fetches for display anyway.

### The pipeline

1. **Seeds** — newest-first CompetitiveImprover highlights in the window (capped read), deduped
   per chart, filtered to the config chart type.
2. **Peers bar** — `GetChartScoreRankingsQuery` over the seed charts; keep `Ranking ≥ bar`.
   Skipped when the bar is 0. Tiny cohorts auto-pass (the rankings handler returns 1.0 with no
   cohort scores) — the improver flag already vouches for the seed. Note the semantics: the query
   ranks your **best** score, not the session's score — for recent improvers these coincide almost
   always, and the copy says "you beat", which stays true.
3. **Order seeds** newest session first, then ranking.
4. **Floors** — compute the per-type push floors from the two pools.
5. **Expand** — `GetSimilarChartsQuery` per seed; keep targets that clear the shared similarity
   match floor, sit at or above their type's push floor, aren't vetoed, and are unpassed/broken —
   or outdated-scored when the toggle is on.
6. **Dedupe + attribute** each target to its newest seed; cap (~6 seeds, ~4 targets each,
   tunable consts); emit `ChartRecommendation`s carrying `SeedChartId` + `SeedPeerRanking`.
7. **Widget** groups by seed, fetches the personalized Pass blend per folder present (existing
   Fill Gaps `_lensTiers` flow), orders within groups by it, renders tier/score+age columns.

### What carries it (all shipped; nothing crosses a boundary that isn't already crossed)

| Piece | Where | Notes |
|---|---|---|
| Seed source | `IScoreHighlightRepository` + one new capped read | In-vertical (PlayerProgress) — **the ScoreLedger sessions read the earlier draft called for is not needed**; the flags are richer than the journal for this purpose and already carry session + time. |
| Peer percentile | `GetChartScoreRankingsQuery` → `CohortScoreProvider` | The incident-safe path; do not bypass. |
| Pools for the floor | `GetTop50CompetitiveQuery(type)` / `GetTop50ForPlayerQuery` | Both in PlayerProgress. |
| Similarity | `GetSimilarChartsQuery(chartId, mix)` → top-20 | PK-prefix seek. The contract returns the tail unfiltered; the match floor (0.55, calibrated PR #155) gets promoted from `SimilarChartsShelf`'s private const to a ChartIntelligence contracts constant so shelf and engine share one bar. |
| "Super outdated" | `AgeOutlierWeights` → moves to `Domain.Services.ScoreAgePolicy` | Pure math beside `TierListProcessor` (which it already uses); `TierListBlendBuilder` delegates, behavior byte-identical — the "baselines use the same weights" invariant keeps one implementation. |
| Personalized tier | `GetBlendedTierListQuery(type, level, "Pass", Personalized: true)` | The cost driver — paid widget-side only, one read per folder present in results. |
| Provenance | `ChartRecommendation` gains `Guid? SeedChartId`, `double? SeedPeerRanking` | Additive defaults; WSIP page and existing categories untouched. |
| Widget shell | `SuggestedGoal.HotStreak`, drawer preset, `DynamicNameKey` | The established new-goal recipe. |

### Commit plan

- **C0** — this design-doc rewrite (the locked spec + plan).
- **C1 — Core moves + contracts**: `ScoreAgePolicy` extracted to `Domain.Services` (builder
  delegates); `ChartRecommendation` seed fields; `RecommendationCategory.HotStreak` + category
  const; `GetRecommendedChartsQuery.HotStreakOptions (PeerPercentile, LookbackDays?, IncludeOutdatedScores)`;
  similarity match floor promoted to ChartIntelligence contracts (shelf reads it).
- **C2 — Infrastructure**: capped newest-first flag-filtered highlight read on the existing
  index + integration tests (window, cap, flag filter, ordering).
- **C3 — Engine**: pure `HotStreakPolicy` (floor percentile incl. tiny/asymmetric pools,
  ordering, dedupe/attribution) + DomainTests; the `RecommendedChartsSaga` builder +
  ApplicationTests (bar-0 skips rankings, per-type floors, outdated toggle, veto, all-time).
- **C4 — Web**: config fields (`HotStreakPeerPercentile` 80, `HotStreakLookback` 30d,
  `HotStreakIncludeOldScores` off, `GroupBySeed` on), goal bundle, config panel branch (slider +
  look-back select + toggles, Levels hidden), widget grouped/flat rendering + tier/age column +
  empty state, registry preset + dynamic name; Tests.Components; mock round-2.
- **C5 — Localization**: every new key ×9 locales.
- **C6 — Docs polish**: fold Hot Streak into this doc's shipped-goals list above, "Peers" entry
  in DOMAIN.md.
