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

## Proposed goal: "Based on recent sessions" (owner idea, 2026-07-15 — NOT SCOPED)

> **STATUS: an idea with one unanswered question at its centre.** Everything below is grounded in
> shipped contracts, but **§"Who are 'peers'?" decides whether this is a cheap goal or a re-run of
> the argument that put skill-gaps on hold.** Do not build past that question.

**The pitch, in the owner's shape:** take the charts you *crushed* in recent sessions, and offer
the charts most like them that you haven't done. "That felt great — here's more of it."

### The shape

1. **Seed** — charts from recent sessions where you performed **>80% compared to peers**
   (configurable % in the configurator).
2. **Order seeds** by session age, then by performance level.
3. **Expand** — for each seed, its similar charts ([chart-similarity.md](../chart-similarity.md)).
4. **Keep** the ones you **haven't passed**.
5. **Rank** through your **personalized tier list**.
6. **Configurator toggle: "include very old scores"** — charts whose score is *super outdated* join
   the not-passed pool as eligible targets. A chart you passed once three years ago and never
   touched is unexplored territory in every sense that matters.

### What already exists (name these, don't rebuild them)

| Piece | Contract | Notes |
|---|---|---|
| Sessions | `GetRecentSessionsQuery(UserId, Page, PageSize)` → `RecentSessionsPage` | `SessionGroup` carries `Start`/`End`/`SessionId`; rows carry `ChartId`, `OccurredAt`, `Score`, `PreviousBest`. Session age is free. Rows predating session capture group by calendar day — the ordering must tolerate `SessionId == null`. |
| Similarity | `GetSimilarChartsQuery(chartId, mix)` → top-20 | PK-prefix seek, ≤20 rows. **Cheap** — N seeds = N seeks. Shipped PR #155. |
| Personalized tier list | `GetBlendedTierListQuery(type, level, lens, Personalized: true)` | **The cost driver.** One read per (type, level) touched, and the personalized blend is the slow one. Seeds spanning 5 levels × 2 types = 10 blend reads. |
| "Super outdated" | `TierListBlendBuilder.AgeOutlierWeights` | Already exactly the owner's category: a score is old only when it is **both** past the 30-day grace floor **and** an age outlier (mean + 1σ) in the player's *own* record. `OutdatedScoreCount` already surfaces the count on the Personalized Breakdown. **The toggle needs no new math.** |
| Widget shell | `SuggestedGoal`, `DrawerPresets`, veto/feedback | A new goal is an enum value + a category + a preset. |

The similarity graph gates at level ±1 ∩ scoring level ±1.25, so expansion stays near the seed by
construction — this goal inherits "near what you just played" for free and does not need its own
level window. That is a **reason to prefer it over a level-mode config**, not an accident.

### Who are "peers"? — the question that decides the feature

">80% compared to peers" has no shipped implementation, and the three ways to get one are not
close to each other.

| Option | Cost | The problem |
|---|---|---|
| **(a) Everyone who played the chart** | Moderate | **Probably meaningless.** A D23 player beats 80% of *everyone* on nearly every chart below their level — the seed set becomes "every easy chart you touched", and the goal recommends downward forever. |
| **(b) Players near your competitive level** | ⚠️ **Dangerous** | This is almost certainly what the owner means, and it is cohort ranking — **the exact query shape that melted prod on 2026-07-10** (100% CPU, worker-limit exhaustion; mitigated by bucket caching + a covering index, PR #129). Doable, but it must reuse the cached buckets and be designed against that incident, not around it. |
| **(c) Letter-grade percentile** — `ChartLetterGradeDifficulty.Percentiles` (banked 0–100, per chart per grade) | **Cheap** | Banked already, but **coarse** (grade granularity, ~7 buckets) and it is "everyone", so it inherits (a)'s problem. On a chart where 60% of players SSS, it cannot discriminate within that 60%. |

**A fourth framing worth considering, because it may be what's actually wanted and it costs
nothing:** *the chart's scoring level is already the peer-normalized expectation.* It is measured
from the whole population — "this chart scores like a 22.3" is a statement about peers. So
"overperformed" can be read as **your score here vs. what you typically score at this scoring
level**, with no peer query at all. That is not literally "beat 80% of players"; it is "beat your
own peer-calibrated baseline". It may be the better feature *and* it is free — but it is a
different claim, and the widget must not say "top 20% of players" if this is what it computes.

### Reconcile with the held skill-gaps goal first

**D10's skill-gaps goal is HELD pending owner iteration on the deviation approach**, and this idea
is its cousin: both are "find where you over/under-perform and act on it". Skill-gaps looks for
weakness, this looks for strength, but they want the same missing primitive — a trustworthy
per-chart or per-skill over/under-performance signal. Settling that primitive once serves both.
**Building this without settling it means having the deviation argument a second time.**

Note `PlayerSkillDeviationsHandler` already publishes deviations **per skill**, not per chart, and
converts to score units at the boundary. If per-skill is enough ("you're crushing bracket charts →
here are bracket charts you haven't passed"), much of this exists and the peer question dissolves.
That is a materially different — and cheaper — feature than the per-chart one described above, and
it may be the same feature the owner wants.

### Open questions for the owner

1. **Peers = who?** (a), (b), (c), or the scoring-level framing? This decides everything below it.
2. **Is the seed per-chart or per-skill?** Per-skill is nearly free and reuses shipped machinery.
3. **What does ">80%" mean when the config says 80** — top 20% of players, or a 0–100 knob on
   whatever signal wins Q1? The label has to match the math.
4. **"Ordered by session age, then performance level"** — session age *ascending* (newest session
   first) is assumed. Worth confirming: it means yesterday's good chart outranks last month's
   great one.
5. **How many sessions back** is "recent"? A page? A date window? Config?
6. **Name.** "Based on recent sessions" describes the seed, not the offer. The offer is "more of
   what just worked."
