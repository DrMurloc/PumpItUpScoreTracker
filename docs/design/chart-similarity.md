# Chart similarity graph — design & settled formula

Companion to [chart-details-overhaul.md](chart-details-overhaul.md). A **database graph** (owner
call, 2026-07-14: nodes = charts, edges = evidence-weighted similarity — no visualization in this
wave; a visual map is a possible later toy). Owned by **ChartIntelligence** (difficulty
analytics), following its existing nightly `Recalculate*` message pattern.

## What an edge means

"Players who care about chart A will recognize chart B as *the same kind of problem*" — same
demanded skillset, same difficulty behavior, similar body of evidence. Edges power the
chart page's **similar shelf** (jacket cards + why-chips) and double as the site's internal-link
mesh for SEO.

## Gates (hard filters, before any scoring)

- Same mix (the graph is per-mix; edges never cross mixes).
- Same chart type (Single↔Single, Double↔Double). **Co-Op excluded v1** (no competitive-level
  semantics, tiny population).
- Level within ±2 of the anchor.
- **Same-song charts excluded** — siblings are navigation (the hero's sibling bubbles), not
  discovery; they'd otherwise dominate every shelf.

## Signals — each sub-score in [0, 1]

**S_style — skill-coverage cosine (weight 0.30).** Vector over the mapped skill vocabulary
(`GetChartSkillChipsQuery`): component = segment-coverage fraction; whole-chart qualities (null
coverage — piucenter's sustained/bursty) contribute a fixed 0.6 component. Cosine similarity.
Missing when either chart has no banked skill data.

**S_behavior — difficulty behavior (weight 0.25).** Mean of available components:
- Pass tier category distance: `1 − |catA − catB| / 6` (7-step `TierListCategory` scale).
- Score tier ("Scores" list) distance: same form.
- Letter-grade percentile curve: `1 − mean(|pA(g) − pB(g)|)` over the shared grade axis.
- Continuous scoring level (`GetChartScoringLevelsQuery`): `1 − min(1, |slA − slB| / 2)`.

**S_players — population residual correlation (weight 0.25; the strongest signal).** For every
user with scores on both charts (same mix): residual = user's score minus the chart's
population-average score at that user's competitive-level bucket (the score-by-level curve the
page already draws). Then:

```
S_players = max(0, pearson(residualsA, residualsB)) · n / (n + 20)
```

`n` = shared scorers; **missing when n < 30**. The shrinkage term keeps thin overlaps honest;
negative correlation clamps to 0 (actively-dissimilar is not an edge, and the shelf never needs
"opposites").

**S_intensity — step-analysis profile (weight 0.10).** Scalars: NPS, sustain fraction
(sustain seconds / duration), time-under-tension fraction, note count — each z-scored within the
(type, level) cohort; `S_intensity = clamp01(1 − mean(|zA − zB|) / 3)`. Missing without
step-analysis rows.

**S_meta — metadata prior (weight 0.10).** `0.5·sameStepArtist + 0.2·sameSongType +
0.2·clamp01(1 − |bpmA − bpmB| / 60) + 0.1·sameDebutMix`.

## Combination (settled)

```
available = signals present for the pair
score     = Σ (w_i · S_i) / Σ w_i      over available          (weight renormalization)
score    ×= 1 − 0.15 · |levelA − levelB|                        (level affinity inside the ±2 gate)
edge exists only if ≥ 2 non-meta signals are available          (meta alone never makes a neighbor)
```

- **Top-K = 8** per (chart, mix), **floor = 0.55** — fewer than 8 neighbors is fine; the shelf
  renders what exists (with its empty-state sentence when a chart is too sparse for any edge).
- Symmetric by construction → compute once per unordered pair, store both directions for query
  simplicity.
- Weights are constants in the domain service (unit-tested breakdown); tuning happens by PR after
  the **calibration checkpoint**: dump top-K for ~20 charts the community has strong opinions
  about, owner eyeballs, weights adjust. Not config — the formula is product behavior.

## Why-chips (explainability is the product)

Per-edge, persist the sub-scores; the UI derives at most **two** chips by priority:

| Priority | Condition | Chip (localized) |
|---|---|---|
| 1 | S_players ≥ 0.45 (n ≥ 30) | "players score alike" |
| 2 | S_style ≥ 0.75 | "same skill profile" — named when one shared skill has ≥ 30% coverage on both ("twist-heavy like this") |
| 3 | S_behavior ≥ 0.8 | "behaves the same for its level" |
| 4 | same step artist | "same step artist" |
| 5 | S_intensity ≥ 0.8 | "same intensity" |

## Storage & compute

- **Table `ChartSimilarity`** (ChartIntelligence contribution, internal entity):
  `(Mix, ChartId, SimilarChartId, Score, SignalsJson, SharedScorers, ComputedAt)`,
  PK (Mix, ChartId, SimilarChartId), index (Mix, ChartId, Score DESC). Row in
  DATABASE-SCHEMA.md with the migration.
- **Nightly** `RecalculateChartSimilarityCommand` (Hangfire one-liner → bus consumer, per mix),
  scheduled **after** the tier-list/letter-difficulty recomputes it reads from (their UTC slots
  are in SCHEDULED-JOBS.md; slot this ≥ 06:00 ET). Work shape: bucket charts by (type, level
  window); build per-chart residual maps in one pass over scores; pair-score within buckets.
  Idempotent full rewrite per (mix, chart).
- **Read**: `GetSimilarChartsQuery(chartId, mix)` → ordered edges + signal breakdown. No
  read-side cache (revised at B2): the read is a single PK-prefix seek returning ≤ 8 rows,
  and the page's output cache is the real caching layer — a memory cache here would only
  add a staleness window after the nightly rebuild.

## Future upgrades (recorded, out of scope)

- **Judgment distributions** (once OfficialMirror starts banking recorded plays): a
  judgment-shape vector per chart — sharper than score residuals for "same kind of problem".
- **piucenter section density/difficulty**: upgrades S_intensity from whole-chart scalars to a
  section-profile distance, and localizes *where* two charts are alike.
- Cross-mix edges ("this Phoenix 2 chart plays like that XX classic") — needs residuals
  normalized across scoring eras; revisit with demand.
