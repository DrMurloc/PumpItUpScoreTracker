# Share-card download settings

The Download buttons on `/TierLists` and `/Pumbility` (PUMBILITY Targets) stop rendering one
fixed picture and start rendering the picture the player asked for: Download opens a settings
dialog — the Export charts dialog's shape — with a live example, and the choices are remembered.
One option set serves both surfaces; the composer that applies it and the renderer that draws it
are shared, so the two pictures can never drift apart.

Decided across five workshop rounds (2026-08-30). This document is the spec of record.

## 1. Entry and persistence

- The Download icon **opens the dialog every time**, like Export charts. The example doubles as
  a confirmation; remembered choices make it one extra click.
- **One shared UiSetting** (`ShareCard__Options`) carries the choices across both surfaces — one
  picture language. The stored value is a `v1`-prefixed list of enabled flag names, so
  "everything off" is distinguishable from "never saved".
- Defaults are today-parity: letter grades, plates, the Pass and To Do boundaries and the broken
  border on; everything else off.

## 2. Chart-card options

| Option | Effect |
|---|---|
| Song names | A name band under the jacket. Grade/plate/score marks stay ON the jacket, above the name. |
| Letter grades | The grade art, in the corner chip's black box. Shares a row with the plate, **above the score**; without a score that row drops into the score's place. |
| Plates | The plate art, boxed the same way, to the right of the grade — or in the grade's slot when the grade is off. |
| Scores | The score as plain text in the same black box, **bottom-left** of the jacket. Never percentile-colored. |
| ↳ Include broken runs | A broken best's score prints (muted). Off: broken tiles print no score. |
| PUMBILITY | Bottom-right chip: the chart's current value for your score — computed with `ScoringConfiguration.PumbilityScoring(mix, false)` exactly as the pool page computes it, printed N2 (the pool corner's own format). Zero-value charts (broken, sub-10 P2, co-op in a no-co-op pool) show nothing. |
| ↳ Expected gains | Switches the chip to the **expected gain** (`+` chip, gold) with a small expected-grade image beside it — the blend of your repriced Phoenix 1 scores and your peers' numbers (`ProjectPumbilityGainsQuery`). Shown only where there IS a gain. |
| Skills | The chart's identity chips in a band below the card (below the name band when both are on) — the same chips the on-page card wears, identity claims only. |

Projected *scores* were cut in round 5 (UX judged awkward; may return later). Nothing projects a
score into the score slot; the projection surface is the Expected gains chip alone.

## 3. Boundaries

Each individually selectable; **one border per tile**, most specific enabled claim wins:

1. **To Do** — dashed info blue. Overrides everything.
2. **Top 50 PUMBILITY** — gold (the mix's rarity gold): **dotted** = in your top 50 of the
   chart's own type, **solid** = in your top 50 combined. Membership from
   `GetTop50ForPlayerQuery` (typed / merged). Mutually exclusive with the color modes.
3. **Pass** — solid success green, or, as sub-modes (require Pass; exclusive with each other and
   with Top 50 — last pressed wins):
   - **Color by letter grade** — border takes `MixThemes.GradeHex` for your grade.
   - **Color by plate** — border takes `MixThemes.PlateHex` for your plate.
   - In both modes a **Perfect Game glows** (halo on the border).
4. **Passed in other mixes** — dashed success green (the page's own `tier-chart-card-other-mix`
   treatment). Renamed from "Passed in another mix" — the main page's legend adopts the same
   wording.
5. **Broken with score** — dotted grey, **last**, exactly where the page's card priority puts
   broken. Fires when your best *registered* attempt is broken — never a journal lookup, so
   wiped/withdrawn history shows nothing.

## 4. The legend

The card's footer grows a legend row for **exactly the boundaries that are enabled** — a shared
image has to explain its own borders. The color modes collapse to a single entry ("Pass —
colored by letter grade · PG glows") rather than enumerating sixteen grades.

## 5. The bubble rule

Not a toggle. **Many folders feed the list → every tile wears its difficulty bubble** (the
Targets page). **One folder feeds it → the folder's bubble rides the header** and tiles carry
none (the tier list page).

## 6. The example

The dialog's preview is a **real render** — the same `GetTierListShareCardQuery`, fed a sample
of at most **six tiles** off the live list, debounced on toggle changes. Never the whole folder:
the renderer ingests every jacket into memory, so the preview stays small and the download is
where the full list gets paid for.

## 7. Where things live

- **Domain**: `TierListShareCard.Tile` carries the new optional fields (caption, score label,
  expected-grade art, skill chips, glow, the compact-marks flag) and the card carries the legend;
  everything defaults so an untaught caller renders exactly as before.
- **Data**: `SkiaShareCardRenderer` draws them. No new packages.
- **Web**: `ShareCardOptions` (persistence + interlocks), `ShareCardComposer` (facts + options →
  tile: the boundary ladder, score gating, chip switching, legend), and the shared
  `ShareCardSettingsDialog`. The two pages feed the composer from what they already load, plus
  two lazy reads when the relevant toggles are on: `GetTop50ForPlayerQuery` (membership) and
  `ProjectPumbilityGainsQuery` (gains + expected scores).
- **Verticals**: consumed read-only via existing contracts. No schema changes.

Signed-out visitors get only the impersonal options (song names, skills); the personal toggles
and boundaries need scores to mean anything.

## 8. Download progress (round 6)

The slow phase of a cold download is the renderer pulling every jacket into memory, and a
MediatR query cannot report mid-flight — so the **page drives the slow phase itself**: it
collects the composed card's distinct art URLs and warms the renderer's cache in **batches of
eight** (`PrefetchShareCardArtCommand` → `IShareCardRenderer.PrefetchImages`), ticking a
determinate bar per batch — "Fetching chart art — 34 of 61", real counts, never an invented
percentage — then sends the render, which is a short indeterminate "Rendering…" tail against a
warm cache. The renderer's own pre-load uses the same bounded batches, so nothing fans out
unbounded anymore.

While a download runs, **every control in the dialog disables** the moment the first batch is
sent. The Close action becomes **Cancel** — pressing it cancels the loop and returns the dialog
to its editable state — and **closing the dialog any other way (backdrop, escape) cancels too**.
Cancellation keeps whatever already landed in the cache: warming is harmless and makes the next
try faster. A warm cache flashes the bar to 100%.

The preview keeps its plain spinner — six tiles do not earn a bar.

## 9. The header names the rows (round 7)

Audit finding: the tier-list card put the **page's scope** in the title ("Singles 20") and the
**lens** in the subtitle, but on five of seven views the lens is not what the rows are grouped
by; the Targets card put the **page block's name** in the title and the real subject in the
subtitle. One rule now, composed by `ShareCardTitles` for both surfaces:

- **Title = what the rows are.** `{Folder} — {Lens}` on the tier views; `{Folder} — {Tag}'s Scores
  by {Grouping}` under My Scores (the title owns whose scores they are); `{Folder} — Speed`.
  Targets: `PUMBILITY Targets — {Grouping}`; the Breakdown page's fifty: `PUMBILITY Breakdown —
  Your top 50` (the lens's `PUMBILITY Pool — Top 50` retired with the lens, PUMBILITY doc D57).
- **Subtitle = how to read them.** `{Mix} · {date}` on the tier views (the lens is already the
  title). `Shown Difficulty: {lens} · {Mix} · {date}` under My Scores and Speed, where the lens
  only orders within sections — the personalized reading when the Shown Difficulty picker chose
  one. Targets: `Energy: {rung}`, then the pool scope on Phoenix 2 (All / Singles / Doubles
  pool), then `Only projected PUMBILITY gains` and `Phoenix 1 projected` when those switches are
  on, then mix and date. The Breakdown page's card carries no Energy — nothing on that page reads
  one — just the pool scope on Phoenix 2, then mix and date. No "n of m charts" line (owner: no need).
- **Stamp = whose reading it is** — `Crowd sourced` (never "Community", which names the
  Community entity) or `Personalized for {tag}` on the tier views, `Personalized for {tag}` on
  Targets, and **nothing** under My Scores and Speed, where the title already said.
- **Filenames carry the subject** so two downloads of one folder never collide:
  `TierList_{mix}_{type}{level}_{subject}_{date}.png` (`Pass`, `PersonalizedScore`,
  `ScoresByAge`, `Speed`) and `PumbilityTargets_{mix}_{grouping}_{energy}_{pool}_{date}.png`.

The renderer shrinks a long title toward 22px before ellipsizing and skips an empty stamp.

## 10. The example is scripted (round 7)

The dialog's example no longer shows the first six live tiles — that made seeing a download
mean fishing across folders for a Perfect Game or a broken run. `ShareCardSample` dresses the
list's first six jackets in a fixed set of states: a PG in the combined pool (the glow), a pass
in the type pool with a gain, a broken run, a To Do, a pass carried from another mix, a bare
chart. Every option in the dialog is visible in one picture, the section is labelled "Example",
and no personal read runs for a preview. A legacy mix keeps the states and drops the numbers.
