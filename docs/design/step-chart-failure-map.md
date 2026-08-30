# Step chart & failure map

Status: **owner-workshopped 2026-08-29/30, settled; building.** The chart page and the chart
details dialog gain a rendered step chart — the actual arrows, piucenter-style — with a failure
rail beside it: a pin wherever imported runs ended mid-chart, life-bar deaths and Stage Pass
commands as separate series. Quick-link chips scroll to the crux, the notable ranges, and the
death spikes.

Companion specs: [stepfile-precision.md](stepfile-precision.md) — what the simfile corpus can
and cannot say, whose §7 criteria this feature computes at ingest as its show/hide verdict —
and [pass-command-detection.md](pass-command-detection.md), whose journal columns are the
failure data. The mock (workshop artifact, three rounds) fixed the visual language: vertical
time-down strip, panel-colored arrows, pins with ×N counts, minimap-as-navigation.

## 1. The two halves

**The strip** renders a chart's judgement timeline as arrows on a canvas: taps, hold bodies
with head arrows and end caps, segment boundaries from the snapshot's own analysis, a mm:ss
ruler (the corpus has no tempo map in its snapshot form — measure lines arrive only where the
.ssc aligns, D6). Three coloring modes: **Arrows** (classic-skin panel colors: upper diagonals
red, lower blue, center yellow), **Feet** (the snapshot's per-note limb prediction), **Timing**
(DDR-style quantization colors, .ssc-aligned charts only).

**The failure rail** places a pin at every position where an imported run ended. A death at
judgement count J happened at the J-th judgement event of the chart — tap rows and hold ticks
both judge — so the pin's time is `events[J]` after the gate's rescale (D9). Life-bar pins hang
in the near column, proven non-lifebar (Stage Pass) pins in the far column; stacked deaths
cluster within ~1.5 s into one pin carrying ×N. The signed-in viewer's own broken runs mark
the rail in gold.

## 2. Decisions

All owner-settled across the 2026-08-29/30 workshop.

- **D1 — Pins, not a smoothed heat map.** A pin per failure spot, ×N when stacked. Honest at
  N=2 and N=300; the data grows ~250 judged breaks/day, so density arrives on its own.
- **D2 — Two causes, hedged.** Violet is only ever `IsNonLifebarBreak = true` — the proven
  claim. Everything else is the life-bar series. The pin tooltip carries the D34 other-pad
  hedge on Singles; Phoenix 1 has no violet series at all (every proven Pass is Phoenix 2).
- **D3 — Private players are included.** The rail is an anonymous aggregate — counts at
  positions, no identities — like the hero's pass-rate fact, unlike the limbo board's named
  rows. The viewer flag marks only the viewer's own rows, to themselves.
- **D4 — Hybrid source: snapshot default, .ssc extras.** The piucenter snapshot stays the
  backbone (aliases, limbs, segments); the .ssc files supply what it structurally lacks — the
  tempo map and authored `TICKCOUNTS` — which is Timing mode, measure lines, and exact tick
  placement. Enrichment happens at ingest; runtime reads ONE banked payload.
- **D5 — One combined upload.** The .ssc sources ride inside the piucenter snapshot zip under
  `stepfiles/` (mirroring the Rayden checkout's tree). There is no route where an .ssc skips
  the annotate flow, so there is no second upload. ~23 MB snapshot + ~45 MB compressed .ssc
  fits the existing 128 MB upload buffer.
- **D6 — Alignment is the correctness tripwire.** piu-annotate computed the snapshot's tap
  times FROM these .ssc files, and each chart's metadata names its `ssc_file`. Same vintage ⇒
  our C# beat→time must reproduce the snapshot's times. Where it does, per-note beats bank and
  Timing mode lights up; where it does not (parse gap, vintage drift, chart-pick ambiguity),
  that chart keeps seconds only and Timing stays off. A parser bug cannot ship silently.
- **D7 — Blob custody.** The raw .ssc corpus lands in Azure blob, vintage-stamped, so
  re-analysis is an admin button reading the archive rather than a re-upload — and so the
  community's supply is held, the lesson piucenter's wind-down taught. The store reuses the
  photo pipeline's `AzureBlobConfiguration.ConnectionString` (its own container, auto-created);
  **unconfigured environments park it** and lose only the archive copy and the button — the
  parse path reads the zip directly, so local dev, CI and E2E run the whole feature with zero
  Azure config.
- **D8 — The show/hide verdict is computed, not curated.** stepfile-precision §7's criteria run
  at ingest, per chart per mix (NoteCount is per-mix): **Tier A** (file taps exceed the judged
  total, or derived ticks fall below the file's own hold rows), **Tier B** (zero-hold file
  against a game demanding > 5% ticks), **Tier C** (implied total off > 50%) ⇒ `Excluded` — no
  section. Fixed step files graduate automatically at the next upload; no confirm table.
- **D9 — The pin gate is ±2% with proportional rescale.** Between the exclusion tiers and
  agreement sits the era-approximate middle. Within 2% of NoteCount, positions rescale
  (`J × implied/NoteCount`) and pins show — the residual drift is under half a pin cluster.
  Outside it: `StepsOnly` — the strip renders (taps are the trustworthy half; only 3 charts
  corpus-wide are arithmetically impossible), pins and death chips hide behind one caveat
  line. Altale S21, the site's #1 break chart, sits at 1.08% — a strict 1% gate would exclude
  it for 14 notes on 1,299.
- **D10 — Percent-linear placement is refuted, measured.** "X% of judgements at X% of the
  strip" misplaces Altale S21's 30 real deaths by median 6.9 s, max 10.1 s (the chart is 71%
  hold ticks; half its judgements land at 44% of its height), and the real ×3 Pass cluster at
  J=703/703/704 lands 9.6 s from the truth. Exact placement is one array index into data the
  payload already carries. Percent thinking survives only as D9's rescale.
- **D11 — The Web layer composes.** The failures endpoint dispatches Catalog's payload query
  and ScoreLedger's breaks query and runs the kernel solver, returning pre-placed pins
  (time, count, cause, yours). No Catalog→ScoreLedger project edge exists and none is added.
- **D12 — Client-side canvas, one module.** `wwwroot/js/step-chart.js` renders strip, rail,
  minimap and chips from two fetches. The static chart page loads it like the calculator
  modules (App.razor, content-hashed); the dialog's Steps tab imports it lazily and mounts.
  Canvas tiles stay under browser height limits; hueless of literals — every color reads a
  token.
- **D13 — Two endpoints, two cache lives.** The step payload changes only at ingest (ETag on
  vintage, long cache); the pins change with every import (short cache). Deliberately outside
  `api/*` — UI-support, no partner contract, no wire-shape test.
- **D14 — Dialog gets a fifth tab ("Steps"),** compact scale, no minimap, lazy-loaded on first
  activation. The page stays the whole record; "More info" lands on the section anchor.
- **D15 — Mode preference sticks per player** (`StepChart__ColorMode` UiSetting). Fixed zoom
  in v1 (page 200 px/s, dialog 110); a zoom control waits for field feedback.
- **D16 — Co-op charts get no section.** Their causes are never classified
  (pass-command-detection D35), their `Level` is a player count, and the corpus barely covers
  them. Skip outright rather than render a maybe.
- **D17 — Quick links cap at six**: crux, up to two of the snapshot's "eNPS ranges of
  interest", and the top death clusters by count. Structure first, then spikes.

## 3. Data model

One new table (Catalog's model contribution): **`scores.ChartStepChart`** — one row per chart.

| Column | What |
|---|---|
| `ChartId` (PK) | the catalog chart |
| `Vintage` | the snapshot release stamp (`version.txt`) the row was built from |
| `UpdatedAt` | ingest time |
| `Payload` | gzip JSON: panels, chart span, taps `[t, beat?, panel, limb]`, holds, tick times, segment spans + eNPS, ranges of interest, per-mix verdicts + rescale factors, tempo map when aligned |

Verdict per mix rides inside the payload: `Excluded | StepsOnly | Full`, with the implied
total and NoteCount it was judged against — the section server-renders nothing for `Excluded`,
strip-only for `StepsOnly`.

The journal gains one filtered covering index for the rail's read:
`(ChartId, MixId) WHERE IsStageBroken = 1` including the judgement columns,
`IsNonLifebarBreak` and `UserId` — every existing journal index leads with UserId, and the
limbo board's `(ChartId, MixId)` index deliberately excludes breaks-only columns. Built ONLINE
like its neighbours.

## 4. Where it lives

| Layer | What |
|---|---|
| `SharedKernel` | `BreakPositionSolver` — J + rescale + event times → seconds; the same pure-model family as `StageBreakCauseSolver` |
| `Domain` | `IStepFileStore` (put/get/list, vintage-keyed) in SecondaryPorts |
| `Data` | `AzureBlobStepFileStore` (parked when unconfigured, D7); `PiuCenterDataParser.ParseChartPageSteps` — the raw taps/holds/ticks/segments/ssc-path read beside the aggregate parse, same shared-adapter home |
| `Catalog` | the .ssc parser + alignment + verdict rules (internal Domain); ingest extension + reprocess consumer (Application); `ChartStepChartEntity` + repository + cache (Infrastructure); `GetChartStepChartQuery` + record, `ReprocessStepFilesCommand` (Contracts) |
| `ScoreLedger` | `GetChartStageBreaksQuery(chartId, mix)` → anonymized (J, cause, isViewer) rows; the repository read + index |
| `Web` | the two endpoints; the page section + dialog tab; `step-chart.js`; the mix-invariant token groups `--panel-*`, `--foot-*`, `--quant-*`, `--break-pass`; the admin reprocess button |

Nothing changes in the legacy `Application` project, `api/*`, Hangfire, or the importers.

## 5. Build order

Docs first, i18n last, one PR — each commit green:

1. this document, the schema rows, the pointers
2. `feat(kernel)` — `BreakPositionSolver` + tests
3. `feat(catalog)` — the .ssc parser + tests
4. `feat(catalog)` — alignment + verdict rules + tests
5. `feat(domain, data)` — `IStepFileStore` + the parked blob adapter
6. `feat(catalog)` — entity + repository + migration #1 + integration round-trip
7. `feat(catalog)` — ingest + reprocess consumer + wiring
8. `feat(ledger)` — breaks query + filtered index (migration #2)
9. `feat(catalog)` — the payload read contract
10. `feat(web)` — the token groups
11. `feat(web)` — the two endpoints
12. `feat(web)` — renderer core + the page section
13. `feat(web)` — the failure rail
14. `feat(web)` — Feet + Timing modes
15. `feat(web)` — the dialog's Steps tab
16. `feat(admin)` — the reprocess button
17. `i18n` — nine locales

## 6. Post-deploy

One step: regenerate the snapshot zip with the `stepfiles/` tree embedded (the generation
pipeline reads the local Rayden checkout it already provably reads) and upload it once at
`/Admin/PiuCenter`. That single upload banks the step payloads **and** discharges the
hold-chip re-upload the identity work already owed. Until it runs, every chart page simply
shows no step-chart section — absence, not breakage.

## 7. Not in this pass

- **Zoom controls and SCROLL-gimmick display.** The strip is time-spaced; how a gimmick chart
  *scrolls* is the gimmick-detection feature's axis, and it will share the .ssc parser.
- **Co-play detection, api/v2 exposure, any score-side backfill.**
- **A manual verdict override.** D8's computed verdict regenerates per vintage; if a repaired
  corpus ever needs a hand override, it is an additive column then.
