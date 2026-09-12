# Hardmode

A second PUMBILITY board, priced by the same formula over a different chart set: the rarest slice of
every folder — the charts almost nobody holds in a top 50. Phoenix 2 only. Lives as a fourth tab in the
PUMBILITY section (`/Pumbility/Hardmode`) while we find out whether the community bites.

The point is not a second skill ranking. It is a **reason to go play the charts nobody plays**, and the
board is shaped so that going and playing them is what moves you up.

---

## 1. What qualifies

Once a weekend, every folder (chart type × level) on Phoenix 2 gives up its rarest charts.

A chart's rarity is a **weighted hold count**: for every player with a full 50-chart pool, a chart scores
`51 − slot` where `slot` is its place in that pool — 50 points at slot 1, 1 point at slot 50. A chart
earns that credit in each of the three pools it can sit in (Combined, Singles, Doubles), and the holder
count beside it counts each player once.

**The cut** (D1): `max(unheld, min(25, ⌊folder ÷ 4⌋))`, where `unheld` is how many of the folder's charts
no full pool holds at all. So a folder gives up 25 charts, or a quarter of itself if that is fewer, or
**every unheld chart** when there are more of those than the cut would otherwise take. A folder of four
charts or fewer gives up exactly **one** (D2) — without that, ⌊2 ÷ 4⌋ = 0 silently excluded S26 entirely,
which is to say it excluded *1948*.

**Ordering and ties** (D3): weighted points ascending, then **scoring level descending**, then name.
94 doubles charts and 19 singles charts at level 20+ carry no usable `ChartScoringLevel`, so a missing
scoring level stands in as the chart's own nominal level rather than sorting last — a folder *is* one
level, so that is the neutral position, and two unrated charts then fall through to the name. Chart level
is not a tiebreak dimension of its own (it is constant inside a folder); it is only what a missing scoring
level reads as. Exact-points ties at the cut line are rare; this exists so the list is deterministic,
not because the order carries much.

**All levels are in** (D4). The census below level 20 is decided by a small group and says so on the
page — see §6.

On the measured 2026-09-06 population the list comes out at **1,211 charts**, 955 of them below level 20.

## 2. Who votes

Both populations, together, weighted identically (D5):

- **PIU Scores accounts** with a full 50-chart pool of the relevant type — 252 on Phoenix 2.
- **Official-board players** whose top 50 can be reconstructed from the mirrored per-chart boards — 1,216,
  deduped against the accounts they are linked to so a linked player votes once, through their site
  records.

A partial pool does not vote (D6): its slot 1 would carry 50 points off three charts. This is why a chart
can read **"held by nobody"** on a page where the viewer is holding it — the caption's tooltip names the
population.

⚠ The mirrored chart boards only exist at level 20 and up, so a board player's reconstructed pool is
level-20-plus by construction. On Phoenix 2 they are the large half of the electorate, which is one of the
two reasons the sub-20 census is thin.

## 3. The number

Your Hardmode PUMBILITY is the same formula (`ScoringConfiguration.PumbilityScoring(Phoenix2)`) over your
best qualifying charts, in three pools exactly as the official boards split them (D7):

| Pool | What it sums |
|---|---|
| Combined | your best 50 qualifying charts, either type |
| Singles | your best 50 qualifying singles |
| Doubles | your best 50 qualifying doubles |

**There is no pool-completeness gate** (D8) — no PUMBILITY board has ever had one. A partial pool simply
scores lower, which on this board is the mechanic rather than a defect: day one the average account holds
**7.6** of the 1,211, and the way up is to go and play more.

Measured consequence worth carrying: once a pool fills, a Hardmode total is **93–96%** of the same
player's real PUMBILITY, so the endgame board is close to the PUMBILITY board. The interesting phase is
the year spent filling pools, during which the board reorders hard — the site's number-one Phoenix 2
player sits at **#10** on day one with 25 qualifying charts played.

## 4. Two boards, not one

"PIU Scores" and "Official Boards" are separate tabs over the same three pools (D9). The top of the
official boards plays everything and would occupy the same places on both, and a board player has no site
profile to open — their rows carry the `*` mark the rivals and chart boards already use for mirrored data.

Board rows read: rank (coloured by percentile through the rarity ramp), player, playstyle chip, **Held**
(how many of that pool's 50 they hold) and one figure column, **PUMBILITY** — which on this page *is*
the Hardmode pool. The grid is `olb-grid-row` with the standard column template, so it sheds Held at
600px and the playstyle chip at 500px.

The board deliberately does **not** print the player's ordinary PUMBILITY beside it (owner,
2026-09-12). It did at first, and setting the two side by side invites reading them as rival measures
of the same player when one is a strict subset of the other: every qualifying chart is a chart ordinary
PUMBILITY already chose its own fifty from, so a Hardmode pool can only ever be **smaller**. Dropping
the column also spares the Official Boards tab a rating-board read on every load.

A pool total is the best **fifty** of that pool and nothing else. The first build of the weekly reprice
summed every qualifying chart an account had scored, which is not a pool but an unbounded count of
effort; it put the top of the board at 45,379 against that player's real PUMBILITY of 15,859 — the
impossibility above, printed. The held count hid it by being capped at fifty rather than measured, so
every inflated row still read "50 / 50". The site half and the page now price identically, and a test
asserts they agree on the same records.

## 5. Titles

The existing Phoenix 2 ladders, asked of the Hardmode pool instead of the record book (D10) — the
`[P.B]` gem rungs on Combined, `[S]`/`[D]` on the per-type pools, drawn by the Breakdown page's own
`PumbilityTitleRails` with no new thresholds. About 30 of 301 accounts clear `[P.B] BRONZE` today, so
nearly every rail reads *not started* — which is the honest picture of a board nobody has played yet, and
the `pmb-ask` row beside it ("BRONZE asks 200.00 · your charts average 303.57 · charts held 11 / 50")
says exactly what is missing.

## 6. The sub-20 disclaimer

A Phoenix 2 chart is worth at most `base × 1.52`, and a player can only hold it if their own fiftieth
chart is worth less than that. That ceiling collapses the electorate going down: **1,412** pool holders
could hold an S22, **318** an S17, **ten** an S10.

So every folder at level 17 and below carries a warning in the qualifying-charts list (D11): *few players
hold a chart this low in a top 50, so this folder is decided by a small group — expect the list to change
substantially week to week as players join or their PUMBILITY climbs.* The threshold is the level, not a
computed electorate, because a fixed number is what a reader can check against the folder they are
looking at.

## 7. Card states

Three states in the qualifying list, in the site's owner-locked border language (D12):

| State | Border | Why |
|---|---|---|
| In your Hardmode pool | **solid `--rarity-gold`** | the share card's existing "Top 50 — combined" boundary; the same fact, the same colour |
| Scored, outside your fifty | **solid `--mud-palette-success`** (`tier-chart-card-pass`) | you cleared it and it does not count here |
| No score yet | default | the opportunity |

Gold beats green where both apply, matching the share card's precedence (Top 50 before Pass). The
qualifying list sections in that order reversed — **Not played yet → Scored, outside your 50 → In your
Hardmode pool** — so the opportunity is on top.

## 8. Shape

The reference graph decides most of this. `ChartIntelligence → Catalog, Data, Domain` only, and the chain
is `OfficialMirror → ScoreLedger → PlayerProgress → ChartIntelligence → Catalog` — so the census sits at
the bottom and cannot see the official board data, and the board half cannot see the census. Two Domain
ports carry both directions rather than adding project references (D13).

| Layer | What |
|---|---|
| **Domain** | `IOfficialPoolReader` (board pools, implemented in OfficialMirror) · `IHardmodeChartReader` (the week's list, implemented in ChartIntelligence) · `HardmodeChartsRebuiltEvent` — in `Domain/Events/` because its consumers live in verticals that cannot reference the publisher |
| **ChartIntelligence** | `HardmodeCut` (the rule) · `HardmodeCensusSaga` (the weekly sweep) · `HardmodeChart` table · `RebuildHardmodeChartsCommand` / `GetHardmodeChartsQuery` |
| **PlayerProgress** | `HardmodeSaga` (site ratings, the weekly sweep AND the per-import reprice) · six `PlayerStats` columns · `GetHardmodePageQuery` / `GetHardmodeBoardQuery` — render-time reads of your own pool and the board |
| **OfficialMirror** | `OfficialPoolReader` · `OfficialHardmodeRating` table + its writer |
| **Web** | `/Pumbility/Hardmode` · `HardmodeBoardSection` · `HardmodeQualifyingSection` · the gold card state · the `/Admin` backfill button |

Everything except the list and the ratings is computed at render time from the viewer's own records
against the week's frozen list, so your number moves with your imports while the list holds until Sunday.

**The board is live; only the chart list is weekly** (D15, owner 2026-09-12: *"the chart pool
re-calculates every sunday, but the leaderboard itself should be live updated as people import"*).
The stored totals exist because a leaderboard has to be an ordered read, not because the number is
a weekly fact — so `HardmodeSaga.RepriceHardmodePool` reprices ONE account against the current list
as a failure-isolated in-process step of `HighlightCaptureSaga`, beside the rating and title steps.
The page and the board therefore agree the moment an import lands, instead of the page being current
and the board being up to six days stale. The weekly sweep still exists, and is now the thing that
repopulates every account after the LIST changes under them.

**The weekly job** rebuilds the LIST (and reprices everyone against the new one). It is its own
Hangfire cron at `0 18 * * 0`, after the Sunday 16:30 Phoenix 2 import seals (D14). Chaining off `OfficialSnapshotSealedEvent` would be stricter, but that event is
OfficialMirror's contract and ChartIntelligence cannot see it; the cost of strictness is a project
reference, and a one-week-stale board population is acceptable on a board whose whole point is weekly
stability. `/Admin` carries a one-shot button for the first run.

## 9. Known limits

- **Phoenix 2 only.** Phoenix 1 would need its own census and has a different, much larger population.
- **The day-one board rewards breadth over difficulty.** With 955 sub-20 charts against 256 at level 20+,
  fifty slots are far easier to fill with level-14 charts, so accounts averaging level 14 outrank accounts
  averaging level 21 until the strong pools fill. Accepted (owner, 2026-09-12): it self-corrects, and the
  alternative was a board with one player on it.
- **A partial pool's rails read empty.** By design — see §5.
- Nothing here is seeded anywhere else on the site yet. If it takes, `IHardmodeChartReader` is how the
  next surface reads the list.
