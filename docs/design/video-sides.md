# Singles video sides

Which half of a shared chart video is the chart you opened. Scoped 2026-08-24; UI settled the
same day (mock rev 3). Companion to [chart-details-overhaul.md](chart-details-overhaul.md) —
the indicator lands in `ChartDetailsDialog`, the overhaul's quick-look surface.

## The problem

Open Uh-Heung S22 and the video autoplays — a Nevsister recording with **two charts side by
side**: S17 on the left, S22 on the right. Nothing on screen says which half you came for.
Nevsister and the official PIU channel both publish singles this way; doubles never split
(one player fills the frame).

**The convention** (owner-attested, no counterexample seen): when a Nevsister or official PIU
video is shared by two singles charts, the **lower level plays on the left, the higher on the
right**. SinglePerformance counts as a single here.

## Data census (prod, 2026-08-24)

`scores.ChartVideo` is one row per chart (`ChartId` PK, `VideoUrl`, `ChannelName`); sharing =
the same `VideoUrl` on multiple rows. 5,561 rows, 4,504 distinct videos, all
`youtube.com/embed/<id>`:

- **3,451 solo videos** — no indicator ever.
- **1,034 same-song pairs of two singles-family charts** — the feature population: 1,021 S+S
  and 13 S+SP. Channel families: ~70% Nevsister, ~28% official PIU (each fragmented across
  mojibake variants of the same two names); the remainder are mislabeled rows of those same
  videos. `Unknown Channel` and BOSS_PIUVN rows never share a URL, so no other provider is in
  scope.
- **19 videos span multiple songs** — mislinks (recurring confusions: PICK ME ↔ Nekkoya,
  arcade ↔ `- FULL SONG -` titles). Every 3-charts-on-one-video case and both doubles
  "pairs" are inside this set. These get a data-fix script, not an indicator.

Level facts that shaped the design: on the modern mixes (XX / Phoenix / Phoenix 2) every S+S
pair has **strictly distinct levels and the same ordering on all three**, and every pair
exists on at least one of them. Ties and order flips exist only on pre-Fiesta legacy-mix
levels — and, in 3 of the 13 S+SP pairs, between the S and the SP.

## Decision: the side is stored data, never derived at render time

`scores.ChartVideo` gains a nullable `Side` (`Left`/`Right`). Rationale:

- A video's layout is fixed content. Deriving the side from the *selected mix's* levels would
  swap arrows on the handful of legacy-mix ties/flips — nonsense, since the video didn't
  change. Stored sides mean **a mix switch relabels the level tokens but never swaps the
  arrows**.
- S+SP pairs can't be ordered by level at all (SP sits below the S in 10 of 13, above in 1,
  tied in 2), and whether videos put the SP left is unverified. Their sides come from watching
  the videos (the research pass), which only a stored column can hold.
- `NULL` means "no known side" and renders nothing — which is also how bad data self-mutes
  (see the assigner rule below).

**Backfill** rides the migration: the 1,021 S+S pairs get sides by the convention, comparing
levels on the first modern mix carrying both charts (Phoenix 2 → Phoenix → XX). No per-video
verification — the convention is the owner's stated ground truth. The 13 S+SP pairs stay
`NULL` until the research pass sets them by hand.

## The assigner rule (`VideoSideAssigner`, Catalog-internal, pure)

Input: one song's charts with their video URLs. Output: a side per chart, or null.

- Group by URL. A group is **pairable** only when it holds exactly two singles-family charts
  (`Single` / `SinglePerformance`) of that one song.
- S+S (or SP+SP) with distinct levels → lower Left, higher Right. Equal levels → left
  untouched (never guess, never wipe).
- S+SP → left untouched always: those sides are hand-researched, and a level guess must never
  overwrite one.
- Anything else (solo, doubles, co-op, 3+ charts) → null.

Cross-song sharing never reaches the assigner — it sees one song at a time — so a mislinked
video's rows come out null and the UI stays silent about them until the fix script lands.

**Write-path registration**: `EFChartRepository.CreateChart` and `SetChartVideo` end by
recomputing the affected song's sides through the assigner (clearing a stale partner side when
a URL is edited away from a pair). Both admin flows (`/Admin/BulkAddCharts` and the chart admin
panel) call the repository directly, so side registration needs zero call-site changes — and
the bulk JSON contract is unchanged: two singles sharing a `youtubeHash` within one song is
the registration signal ([new-charts-json.md](new-charts-json.md)).

## The UI (settled: edge captions, arrows only — mock rev 3)

One caption row justified to the video iframe's edges, between the video and the title row,
~20px tall: `▲ S17` on the left edge, `S22 ▲` on the right. Each label sits under the half it
describes and its arrow points **up at that half** — a sideways arrow field-tested as "go to
the next chart" (owner, 2026-08-24) — with **no "left"/"right" words** (owner cut them), **no
difficulty bubbles** (owner cut those first — subtext scale, minimal vertical cost). The
opened chart's side carries the ink and the accent-colored arrow; the partner is muted.
Level tokens are the chart shorthand (`Type.GetShortHand()` + the **selected mix's** level).

States:

- Partner chart absent from the selected mix → your half renders alone.
- `Side` null (solo video, unresearched S+SP, mislink) → the row doesn't render at all.
- Rendered by the shared `VideoSideCaption` component; `ChartDetailsDialog` is the only host
  for now. The chart page's hero player and the similar-chart cards play the same videos and
  could host it later — deliberately out of this pass.

The only localized strings are the screen-reader labels (one sentence per side); the visible
row is arrows and level tokens.

## The data-fix script (outside the PR)

After a research pass (YouTube titles/oEmbed name both charts; a wrong chart's neighbors'
videos often contain it): one `Downloads` SQL script for prod that repoints the 19 cross-song
mislinks — including the doubles and DP ones — and sets the 13 S+SP sides. Where no correct
video exists for a chart, the row is **cleared to no-video** (missing beats wrong). Until it
runs, those videos simply show no caption.

## Out of scope

- A videos endpoint on `api/*` — chart videos aren't on the API surface today, and this
  feature doesn't add them.
- Side editing UI in the admin panel (the recompute covers the flows; hand overrides go
  through SQL like the S+SP research sides).
- The chart page hero + similar-card hosts (same component, later pass if wanted).
