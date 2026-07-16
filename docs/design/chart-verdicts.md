# Chart verdict engine — design

Companion to [chart-details-overhaul.md](chart-details-overhaul.md). Turns the chart page's
distributions into **plain-language verdicts** — "hard to pass for a D20, generous to score once
you survive it" — because the graphs are evidence, not answers. The headline verdict is also the
page's **meta description and JSON-LD description**: unique, data-derived, quotable by LLMs
(the AEO play).

## Principles

- **Rule-based, never a runtime LLM** — cost, latency, determinism, and nine locales all say
  templates.
- **The engine returns structured facts; Web renders words.** A domain service in
  ChartIntelligence computes `ChartVerdictFacet(Kind, Direction, Magnitude, Params…)`;
  templates live Web-side in resx (`L[…]`, all nine locales in the same pass). No UI string
  ever rides a vertical.
- **Facets quantize before templating** — a verdict sentence changes only when the underlying
  band actually moves, not because a nightly recompute wiggled a decimal (stable meta
  descriptions).
- **Only confident facets speak.** Every facet has a minimum-evidence bar; below it the facet is
  omitted, never hedged into noise.

## Facets v1 (all computable from existing analytics)

| Facet | Source | Fires when | Example rendering |
|---|---|---|---|
| **PassVsScore** | Pass tier category × Scores tier category (`GetTierListWithFallbackQuery`) | either ≠ Medium | "Hard to pass for its level — generous to score once passed." |
| **PassBand** | pass counts by competitive level | ≥ 20 passes | "Passes cluster between competitive levels 19 and 21." (the interquartile band of everyone who passed — nothing about *first* passes; the name said so and the copy repeated it into nine locales) |
| **YieldKnee** | avg-score-by-level curve | avg crosses SS+ (975k) inside the observed range | "Opens up around level 22; below 21, expect low 900s." |
| **LetterWall** | letter-grade percentile curve | max adjacent percentile drop ≥ 25 pts | "SS is routine here — SSS is top-decile." |
| **PlateResidual** | median plate vs `ScoringConfiguration.ExpectedPlateForScore(median score)`, within a (level, song-type) cohort | ≥ 50 scores AND \|residual\| ≥ 1 plate step | "Plates run worse than scores predict — a kill-spot signature." / "…better than predicted — smooth attrition." |
| **StyleFingerprint** | skill chips + NPS/sustain/TUT | top skill coverage ≥ 25% | "A stamina twister: half the chart twists, 108s under tension." |
| **History** | per-mix levels from the catalogs | debut ≠ current mix or any level delta | "Debuted in XX at D19; rerated D20 in Phoenix." |
| **Population** | score counts | always | "115 tracked scores · 62% pass rate." |

### The plate constraint (owner pushback, 2026-07-14 — binding)

Plates are **never derived from judgments in this codebase** — they arrive from the official
site with imported scores; the only in-repo model is `ExpectedPlateForScore` (score → typical
plate). Therefore raw plate distribution mostly restates the score distribution, and the ONLY
plate verdict permitted is the **residual vs that baseline within a (level, song-type) cohort**
(long charts plate structurally worse — thresholds don't scale with note count). Additionally:
**no lifebar/"gauge" language from plates.** Gauge claims may only come from
`LifebarSimulator`-derived facts (misses are cliff events: up to ~270 life + a 0.7 multiplier
crash; goods heal nothing; holds FEED the gauge while held). A simulated
miss-cluster-survivability facet is a future candidate, not v1.

## Composition

- **Headline** (hero + meta description): highest-salience 2 facets joined. Salience order:
  PassVsScore (when non-neutral) → LetterWall → YieldKnee → StyleFingerprint. Population appended
  to the meta description.
- **Graph captions**: each evidence graph is *led* by its facet's sentence (sentence first,
  curve second). A graph whose facet didn't fire gets its neutral descriptive caption — never a
  fabricated verdict.
- **Personalized layer** (signed-in, island-rendered, not SEO): "at your level: peers average
  962k; 40% of them pass" — from the same curves + `GetPlayerStatsQuery`. Out of the cached
  anonymous HTML by construction.

## Shape

- `ChartVerdictService` (ChartIntelligence domain service, pure, unit-tested per facet with
  hand-built fixtures) + `GetChartVerdictQuery(chartId, mix)` → facet list.
- No new table v1: inputs are persisted analytics (tier lists, letter difficulties, scores);
  results `IMemoryCache`d per (chart, mix) with daily TTL aligned to the nightly recomputes.
  Revisit persistence only if the meta-description path needs it.
- Coverage: facet computers are exactly the kind of logic `ScoreTracker.Tests` exists for —
  never `[ExcludeFromCodeCoverage]`.

## Future facets (recorded, out of scope)

- **Judgment-shape facets** once recorded-play judgment distributions accumulate (kill-spot
  location, accuracy-vs-consistency splits).
- **Gauge survivability** via `LifebarSimulator` at the chart's NPS/level.
- **Section heat** once piucenter per-section density/difficulty is ingested ("the back half is
  the fight").
