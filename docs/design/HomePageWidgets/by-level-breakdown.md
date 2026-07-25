# By-Level Breakdown widget

Part of the home dashboard widget catalog — see [README.md](README.md) for architecture, locked
decisions (D1–D19), the widget registry contract, and the widget index. **Status: BUILT (C0–C7,
2026-07-12) + UX rounds 1–2 (2026-07-13) — awaiting owner field test.** Catalog-walk widget (D18).

**Render notes:** the shaded distribution band draws as a native `ApexRangeAreaSeries`
(Blazor-ApexCharts 6.1.0 `Top`/`Bottom`), muted and translucent behind the stat lines. **Separate
S/D is a Distribution-only opt-in** — Blazor-ApexCharts 6.1.0 ignores `ApexBaseSeries.Group`, so
grouped S/D bars render as duplicate-labelled overlaps; the stacked aggregations (Breakdown,
Completion) therefore stay combined ("S + D") and S/D separation lives only in the line
distributions. When separated, each type reads by **color** (red Singles / green Doubles), showing
one stat line or a shaded range per type (§ UX iterations).

Mock (interactive config flow, fake data): https://claude.ai/code/artifact/77692444-46e8-451c-ac17-f3f5e2ba6604

---

## What it is

A single, fully configurable per-level graph — "the [`/Phoenix/Progress`](../../../ScoreTracker/ScoreTracker/Pages/Progress/PhoenixProgress.razor)
and [`/Progress`](../../../ScoreTracker/ScoreTracker/Pages/Progress/Progress.razor) pages made
composable and dropped on the home page." A player picks a **scope**, a **metric**, and an
**aggregation**, and the graph follows. **One widget = one graph** (owner) — compose a wall of them by
adding more widgets, never by tiling sub-charts inside one.

**~95% presentation.** Zero new domain, application, data, or migration code. The data layer is the same
set of published queries the Progress pages already call. The only genuinely new logic is a pure
aggregator (percentiles, standard deviation, completion %). No precompute — live pull, refresh paused in
edit mode (D14).

## Model

Three **aggregations** over five **metrics**. The config is *adaptive*: the chosen metric (and mix)
lights up only the options that mean something.

| Metric | Distribution | Breakdown (stacked count / %) | Completion % (threshold → % of folder) |
|---|---|---|---|
| **Score** (numeric 0–1M) | ✅ avg / median / min / max / ±1σ / percentiles (incl. custom Pxx) | — (letter grades **are** the score bands) | ✅ % with score ≥ **X** |
| **Letter Grade** (ordinal) | ~ ordinal average line only | ✅ count per grade | ✅ % with grade ≥ **G** |
| **Plate** (ordinal) | ~ ordinal average line only | ✅ count per plate | ✅ % with plate ≥ **P** |
| **Pass / Clear** (binary) | — | ✅ passed vs unpassed | ✅ % passed |
| **Chart Age** (numeric days) | ✅ full peer of Score (percentiles / ±1σ / band) — the spread of days since each score was set | — | ✅ % recorded within **N** days (recency tiers, fresh = brightest) |

**Chart Age** is a full peer of Score: continuous, so it gets the same distribution stats *and* a
Completion mode ("what fraction of this folder did I touch in the last N days"). Its recency tiers
climb the rarity ramp (freshest on top) since days carry no metal/grade identity. Available for
**every** mix — legacy imports carry a recorded date too.

An **ordinal distribution reads as its ladder, not as ranks**: "Average Plate by Level" averages
plate *ranks*, so the aggregator ships the rung names in `BreakdownResult.YCategoryLabels` and the
widget formats both the y-axis and the tooltip through them — PG / UG / MG, never 5 / 4 / 3. The
bounds land on whole rungs for the same reason: a half-step pad would put a tick between two plates,
where no plate exists.

"Completion Rates" is **not** a metric — it is the Completion aggregation pointed at any metric; plain
pass-rate is Completion-of-Pass. Multi-threshold is allowed (each threshold = one line); misconfiguration
is possible by design (owner: "make it hard to do *unintentionally*, don't nanny") — sensible presets and
defaults carry the 90%.

### Populations (the semantic fork — owner-confirmed)

The three aggregations deliberately count over **different populations**:

- **Distribution** → only charts the player has **played** (no percentile of an unplayed chart).
- **Completion %** → the **whole folder** (unplayed charts count as "not met" — that's the point of
  "PG *every* chart in the folder"). Denominator = all catalog charts in the level×type folder.
- **Breakdown** → player's choice via an **"include unplayed" segment** toggle (mirrors the XX page's
  "Ungraded" bucket).

## Scope

Set once for the whole widget (there is only one graph):

- **Mix** — all recordable mixes (see Metrics below). Config selector mirrors the app mix-picker order
  (`IsPrimary` first).
- **Level range** — reuses the shared [`LevelRangeSlider`](../../../ScoreTracker/ScoreTracker/Components/LevelRangeSlider.razor)
  component (already extracted during the randomizer overhaul; the randomizer's settings panel uses it
  for its Singles/Doubles rows). One instance here — the widget applies a single level range to both S
  and D. "All" = drag to the full 1–28 span (no suggestion chips — owner).
- **Chart scope** — a four-option dropdown: **Singles + Doubles**, **Singles**, **Doubles**, or
  **Co-Op**. Singles + Doubles pools both types; under a **Distribution** aggregation it also offers a
  "show Singles & Doubles as separate data" toggle (Distribution-only — see the render note). Co-Op
  swaps the x-axis from difficulty 1–28 to **player count** (`Chart.Level` encodes player count for
  co-op charts) with a player-count range.

## Metrics & mixes

Metric availability keys off `MixEnumHelperMethods.UsesLegacyScoring(mix)` (= `not Phoenix/Phoenix2`):

- **Phoenix / Phoenix 2** (`!UsesLegacyScoring`) → all four metrics. Data: `GetPhoenixRecordsQuery(userId, mix)`
  → `RecordedPhoenixScore` (numeric `Score`, `Plate`, `IsBroken`, derived `LetterGrade`).
- **XX and every older mix** (`UsesLegacyScoring`) → **Letter Grade, Pass, and Chart Age** (score is
  not 1M-normalized — there is no meaningful "max" — and there are no plates; Chart Age needs only a
  recorded date, which legacy scores have). Data:
  `GetXXBestChartAttemptsQuery(userId, mix)` → `BestXXChartAttempt` (letter grade + broken flag). The
  read seam is **already mix-generic** (the query takes a mix; `IXXChartAttemptRepository.GetBestAttempts(userId, mix)`
  keys off it) — nothing to wire on the read side; the widget lights up per mix as scores exist.
  (Recording legacy-mix scores manually is orthogonal owner work.)

`SupportedMixes` on the descriptor = all mixes; the config panel + resolution clamp handle the rest.

## Sizes

**2×2 (default), 3×2, 4×2** only. One-row-tall line charts were tried on the Competitive Level widget and
rejected as "a smudge, not a graph" (owner), so the widget never offers a 1-row footprint. This requires
adding `SizePreset.FourByTwo` (the struct currently stops at `ThreeByThree`).

## Data seam

A small Web helper resolves the mix and normalizes both record shapes to one aggregator input —
`(Chart, LetterGrade?, Plate?, int? Score, bool IsPlayed, bool IsPass)` — pulling the catalog (level +
type per chart) from the circuit-scoped [`ChartCatalogCache`](../../../ScoreTracker/ScoreTracker/Services/HomeDashboard/ChartCatalogCache.cs).
Co-Op records arrive in the same `GetPhoenixRecordsQuery` call (no extra query); the catalog tells us
which charts are co-op and their player count. Reuses `BoxPlotData.From`-style math (today inlined in
`PhoenixProgress.razor`) generalized into the aggregator.

## Rendering

Blazor-ApexCharts (the existing dependency the Competitive Level widget and the Progress pages use), not
the mock's hand-SVG. Line charts for Distribution/Completion, stacked bars for Breakdown.

- **Series colors**: when S/D are **pooled**, a single combined distribution reads by stat — center
  stats (median/avg) strongest, extremes faintest (line-emphasis alpha), one neutral hue. When S/D are
  **separated**, color encodes the **type** (red Singles / green Doubles, the app's S/D game vocabulary
  from `MixThemes.ChartTypeHex`); each type shows one stat line or a shaded range band, dotted min/max
  outside an IQR/±σ band. Grade/plate/score-tier segments wear their own identity color (grade & plate
  metals, or the rarity ramp for score/recency tiers) resolved from the segment label.
- **Category colors** (grade/plate breakdown): from a new **`ThemeScales.RarityHex(...)`** literal-hex
  accessor — ApexCharts can't read CSS custom properties at config time, so the rarity ramp needs a
  literal-hex sibling to the CSS tokens (precedent: `DifficultyHex` already serves the SkiaSharp share
  card). Colors stay on the sanctioned rarity ramp, never hand-rolled.
- **Optional band fill** (Distribution, Score): shade between a chosen pair (IQR / min–max / avg±σ) for a
  box-plot-as-ribbon look.

Lifecycle contract (D14): fixed-footprint skeleton per size; empty state (no played charts → record/import
CTA); isolated errors; dynamic title reflecting the config.

## Config contract shape (public — D19)

`ByLevelBreakdownConfig` is exported/imported and reflected into the capability schema, so its shape is a
**public API contract** (breaking-change discipline, golden-JSON approval test in `ScoreTracker.Tests.Api`).
Everything is a scalar/enum except the **completion thresholds**, which are a discriminated list —
modeled as a single `IReadOnlyList<CompletionThreshold>` where `CompletionThreshold = (ThresholdKind Kind,
string Value)` (score→int-as-string, grade/plate→enum name, pass→none). One list, string-union values, so
the emitted JSON-Schema stays clean and stable.

## States

- No played charts in range → "No scores in this range yet — record or import to see your breakdown."
- Legacy mix with only Grade/Pass available → Score/Plate hidden in config, no error.
- Error isolated per the host's `<ErrorBoundary>`.

## Decisions (A–E, ratified 2026-07-12)

- **A** — the aggregator is a **pure class in Web `Services/HomeDashboard/`**, unit-tested in
  `ScoreTracker.Tests.Components` (fast, no Docker; the UI-ladder's lowest level that catches the defect).
- **B** — add `ThemeScales.RarityHex(...)` literal accessor for categorical bar colors.
- **C** — add `SizePreset.FourByTwo`.
- **D** — completion thresholds = single `{ Kind, Value }` list (public-contract shape).
- **E** — `SupportedMixes` = all recordable mixes; metric availability via `UsesLegacyScoring()`; read
  seam already mix-generic.

## Commit plan

| C | Content |
|---|---|
| **C0** | Docs restructure: split each widget spec out of the monolith into `docs/design/HomePageWidgets/`, author this spec, fix references. |
| **C1** | Config contract (`ByLevelBreakdownConfig` + enums + `CompletionThreshold`); `SizePreset.FourByTwo`; `ThemeScales.RarityHex(...)`. |
| **C2** | `ByLevelAggregator` pure class + `ScoreTracker.Tests.Components` unit tests (percentile/stddev correctness, completion denominator = whole folder, distribution population = played-only, include-unplayed, S/D split, co-op grouping). |
| **C3** | Mix-resolved read/normalize seam (Phoenix vs legacy → common shape) + component tests over mocked `IMediator`. |
| **C4** | `ByLevelBreakdownConfigPanel.razor` — adaptive UI, reuses `LevelRangeSlider`, persists via `UpdateHomePageWidgetConfigCommand` + bUnit adaptive-visibility tests. |
| **C5** | `ByLevelBreakdownWidget.razor` — ApexCharts render (skeleton/empty/error), `WidgetRegistry` descriptor + DrawerPresets + bUnit render tests. |
| **C6** | Capability-schema entry + export/import golden-JSON approval test in `ScoreTracker.Tests.Api` (D19). |
| **C7** | Localization (all nine locales) + docs (mark the catalog row shipped, UX-GUIDELINES widget note). |

### Drawer presets (D10)

One add-drawer card per pre-filled config, so the multi-personality type stays discoverable. Current
registry set (`WidgetRegistry`):

- **Score Distribution** — Score · Distribution · Min/P25/Median/P75/Max · IQR band · pooled · Lv 17–23.
- **Singles vs Doubles** — Score · Distribution · separated · IQR shaded range per type · Lv 17–23.
- **Grade Distribution** — Letter Grade · Breakdown · raw counts · include broken/unplayed · Lv 17–24.
- **Plate Distribution** — Plate · Breakdown · raw counts · include broken/unplayed · Lv 17–23.
- **Clear Progress** — Pass · Breakdown · Lv 1–28.
- **Co-Op Completion** — Pass · Breakdown · Co-Op scope · 2–5 players.

The dynamic instance title follows `(metric, aggregation)` (`ByLevelConfigRules.TitleKey`), with two
named exceptions (`WidgetRegistry.DynamicNameKey`) so the separated Score-Distribution config keeps the
title **Singles vs Doubles** and the Co-Op Pass config keeps **Co-Op Completion**. Chart Age and Score
Completion are still fully configurable for power users — they are just no longer *suggested* presets
(Chart Age confuses most players; Score Completion overlaps Grade Distribution).

## UX iterations (2026-07-13)

Field-test rounds after the C0–C7 build; the owner runs, these are the ratified changes:

- **Round 1** — presets renamed to say what they show (Box Plot → Score Distribution, PG Race → Plate
  Distribution, Score Push % → Score Completion); tooltip labels made legible (white, numeric x-axis kills
  the "undefined – undefined" mis-bind); grade/plate segments recolored to the **game's** grade/plate
  colors, not the rarity ranking (`MixThemes.GradeHex`/`PlateHex`, documented as reusable tokens in
  UX-GUIDELINES); Score Completion switched to the stacked-tier form.
- **Round 2** — S/D coloring is **red Singles / green Doubles** (`ChartTypeHex`); separated S/D limited to
  one line/range per type; a **separate-display picker** (Average / Median / Min / Max / shaded range
  IQR / Min–Max / ±1σ, with dotted min/max outside a range band); **Chart Age** metric added, then given
  **Completion** parity with Score (recency tiers); four-option Charts dropdown.
- **Grouped S/D bars: shelved.** Blazor-ApexCharts 6.1.0 ignores `ApexBaseSeries.Group`, so a grouped
  stacked bar renders as duplicate-labelled overlaps. Stacked aggregations stay combined; the
  "separate data" toggle is Distribution-only and self-hides elsewhere. Revisit if the wrapper gains real
  grouped-bar support.
- **Round 3 (V1 lock)** — preset roster trimmed to the six above: Score Completion dropped (it overlaps a
  normalized Grade Distribution), Chart Age dropped as a *suggested* graph (kept configurable); added
  **Singles vs Doubles** (separated Score, IQR shaded) and **Co-Op Completion** (Co-Op Pass breakdown).
  Grade & Plate presets flipped to raw-count Breakdown with the broken/unplayed cap on, so the bar height
  reads as folder size with your distribution stacked inside. Two rendering fixes: (1) the shared
  distribution tooltip is now hand-built (`DistributionTooltip`), because a chart mixing range-area bands
  with line series otherwise renders every line value as `undefined – undefined`; (2) the stroke dash
  array is front-padded by the band count — it is indexed over *all* series and the range-area bands
  render first, so without the pad the dash pattern landed on the wrong lines and Singles/Doubles looked
  mismatched. Separated ranges now read as a box-plot ribbon per type: **solid** min / median / max,
  **dashed** lines along the shaded edges (owner's preferred read).
