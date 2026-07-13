# By-Level Breakdown widget

Part of the home dashboard widget catalog — see [README.md](README.md) for architecture, locked
decisions (D1–D19), the widget registry contract, and the widget index. **Status: BUILDING
(2026-07-12) — model + decisions A–E locked, mock signed off.** Catalog-walk widget (D18).

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

Three **aggregations** over four **metrics**. The config is *adaptive*: the chosen metric (and mix)
lights up only the options that mean something.

| Metric | Distribution | Breakdown (stacked count / %) | Completion % (threshold → % of folder) |
|---|---|---|---|
| **Score** (numeric 0–1M) | ✅ avg / median / min / max / ±1σ / percentiles (incl. custom Pxx) | — (letter grades **are** the score bands) | ✅ % with score ≥ **X** |
| **Letter Grade** (ordinal) | ~ ordinal average line only | ✅ count per grade | ✅ % with grade ≥ **G** |
| **Plate** (ordinal) | ~ ordinal average line only | ✅ count per plate | ✅ % with plate ≥ **P** |
| **Pass / Clear** (binary) | — | ✅ passed vs unpassed | ✅ % passed |

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
- **Chart scope** — **Singles / Doubles** (with a "show S & D as separate lines" toggle) **or**
  **Co-Op** (mutually exclusive; swaps the x-axis from difficulty 1–28 to **player count** — `Chart.Level`
  encodes player count for co-op charts — with a player-count range).

## Metrics & mixes

Metric availability keys off `MixEnumHelperMethods.UsesLegacyScoring(mix)` (= `not Phoenix/Phoenix2`):

- **Phoenix / Phoenix 2** (`!UsesLegacyScoring`) → all four metrics. Data: `GetPhoenixRecordsQuery(userId, mix)`
  → `RecordedPhoenixScore` (numeric `Score`, `Plate`, `IsBroken`, derived `LetterGrade`).
- **XX and every older mix** (`UsesLegacyScoring`) → **Letter Grade + Pass only** (score is not
  1M-normalized — there is no meaningful "max" — and there are no plates). Data:
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

- **Series colors**: S/D lines from the `MixPalette` chart pair (README §2.5). In Distribution, color
  encodes the **stat** and line style encodes the **type** (S solid / D dashed) — the second axis is
  "which stat," not "which era," so this differs from the Competitive Level widget's color=type / style=era.
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

One add-drawer card per pre-filled config, so the multi-personality type stays discoverable:

- **Box Plot** — Score · Distribution · Min/P25/Median/P75/Max · IQR band · S&D separate · Lv 17–23.
- **Grade Wall** — Letter Grade · Breakdown · 100%-normalized · include unplayed · Lv 17–24.
- **PG Race** — Plate · Completion · thresholds MG / UG / PG · S&D separate · Lv 17–23.
- **Clear Progress** — Pass · Completion · S&D separate · all levels.
- **Nerd Mode** — Score · Distribution · deciles + ±1σ · Lv 20–23 (the deliberately busy case).
- **Score Push %** — Score · Completion · thresholds ≥950k / ≥990k / 1,000,000 · Lv 17–23.
