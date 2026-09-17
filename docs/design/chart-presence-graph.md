# PUMBILITY presence

A graph on the chart page and in the chart details dialog that answers two questions about one chart,
title by title up the Phoenix 2 PUMBILITY ladder: **how many players on that title hold it in their top
50**, and **where it sits in those top 50s**. A chart's life on the ladder reads straight off it — the
title where it starts showing up near the top of people's lists, the title where the most players hold
it, and the title where it slides to the bottom and drops out.

Phoenix 2 only, and the gem ladder only (`[P.B]` BRONZE through ABYSS ABSOLUTE, not the `[S]` / `[D]`
rungs). Workshopped with the owner on 2026-09-16 over four mock rounds, and a fifth on 2026-09-17 for the
note on charts the official rankings undercount (§8): https://claude.ai/artifact/RUjWAzfF752sqXh1tYXJXz

---

## 1. What it shows

**The Split graph** (D2). One column per title, two panels sharing it:

- **% hold it** — a bar: the share of the players on that title whose top 50 holds the chart.
- **Spot in top 50** — where it sits for the players who hold it: a box over the middle half of their
  spots, a line at the median, whiskers out to the highest and the lowest spot. Under five holders there
  is no box; each holder is a dot.
- **The folder's shadow** (D4) — a grey box behind each title's box: where the *other* charts of the same
  type and level (other D23s, for a D23) sit in top 50s on that title. A box above its shadow rates higher
  than its folder there.

Above the graph, a sentence and up to two short lines:

- `Most held at RED BERYL LV.1: 27% hold it, typically around #21 in their top 50.` — the title with the
  highest share among titles with 25 or more players (a thinner title only when nothing else holds it).
- `Rates higher than other D23s at DIAMOND, about the same at RED BERYL.` — the folder comparison summed
  up per gem (§5).
- `You're DIAMOND LV.4: it's #11 in your top 50.` — for a signed-in player on a gem; their title is tinted
  and their own spot is a diamond. The diamond marks a spot, so it draws only when the chart is in their top
  50 (D16): otherwise the title keeps its tint, the across flag says `You`, and the line reads `You're
  DIAMOND LV.4: it isn't in your top 50.`

**Tooltips are short** (D3), in the owner's words: `27% hold it` · `Typically #13–#30 in top 50, centered
around #21` · `Rates higher than other D23s` · `Low data count for this title, read loosely` · `You: #11 in
your top 50`. Nothing anywhere says how many of the players came from the official ranking — the owner:
*"We don't need to know how much of the data is from official boards."*

## 2. Who counts

**The players on a title are the Breakdown card's title cohort** (D8,
[pumbility-overhaul.md](pumbility-overhaul.md) D68), read the same way:

- **PIU Scores accounts**, placed on the ladder by their stored PUMBILITY, with their top 50 built from
  their own records.
- **Players on piugame's official PUMBILITY ranking**, placed by the number the ranking publishes, with
  their top 50 rebuilt from the official chart rankings. A ranking row that resolves to an account is left
  out — that person is already counted through their account (D61) — and so is anyone the mirror cannot
  rebuild a fifty for (D60).
- **Private accounts count.** This is a census: nobody is named, and leaving them out would disagree with
  every other count of a title.

**A chart with no official chart ranking counts PIU Scores players only** (D11). piugame publishes chart
rankings from level 20 up, so an official-ranking player can never be seen holding a 19 — counting them
in the denominator would divide a site-only numerator by the whole ladder. The census asks the mirror which
charts have a ranking rather than assuming the floor, so the rule stays true if piugame adds more.

## 3. Columns

**A gem opens into its five levels once it holds 125 players** — five times the 25 a level needs to be
read in its own right on the Breakdown card (D9). Measured on the local copy (2026-09-16): BRONZE 23 ·
SILVER 30 · GOLD 38 · PLATINUM 59 · DIAMOND 477 · RED BERYL 478 · ALEXANDRITE 40 · ABYSS ABSOLUTE 2, so
DIAMOND and RED BERYL open and the graph has sixteen columns. The rule opens more gems as Phoenix 2 fills
up, with no code change.

- **A chart counted over PIU Scores players only opens its gems by those players alone** (D13). The ranking
  players are what carry DIAMOND and RED BERYL past 125. Opened by everyone, a level-19 chart's DIAMOND levels
  held 13–27 PIU Scores players each, so every level after LV.1 drew faded and got no folder call. Opened by
  its own players, the same chart reads DIAMOND whole on about 95. So the columns of a chart below 20 can
  differ from a chart above it.
- **A title with fewer than 25 players draws faded**, and its tooltip adds the low-data line. DIAMOND LV.2
  had 19 that day.
- **Under five holders, each holder is a dot** — a box needs five.
- **Every title always draws**, held or not, so the axis is the same on every chart and a reader learns where
  the charts they care about live.

Why levels matter, from the same data: Digitalis D21 is held by 44% of DIAMOND LV.1 around #12, by 20% of
DIAMOND LV.5 around #37, and by 2% of RED BERYL LV.2 around #48. At gem resolution the same chart reads
"DIAMOND 32%, #27" and the slide disappears.

## 4. What a spot is

**A spot is the chart's place in that player's top 50, #1 being the chart worth the most** (D10). Phoenix
2 prices discretely — every SSS+ S21 is worth the same — and **81% of spots are tied** with another chart
in the same fifty (average tie group 4.8, largest 27). **Tied charts share the middle spot**: five charts
tied at #12–#16 are all #14. Ordering ties by chart id would push the same charts up in every player's list,
every day.

## 5. Rates higher or lower

The folder comparison (D4) puts a chart's typical spot on a title against the other charts of its folder
held on that title — every spot any other chart of the same type and level takes in those players' top 50s.

- **Rates higher** when its median spot is **5 or more spots better** than theirs, **lower** when 5 or more
  worse, **about the same** otherwise.
- **No call** without 25 players on the title, 5 holders of the chart, and 10 spots from the rest of the
  folder.
- **The summary line** takes, per gem, the call most of that gem's holders sit under, and merges neighbouring
  gems that agree: `Rates higher than other D23s at DIAMOND, about the same at RED BERYL.`

Measured across the mock's charts, the typical gap on a title with a call is **10 spots** (quartiles 4.5 and
13.8). Charts players keep usually rate higher than their folder — they are the ones people score well on —
and a filler chart like T.B.H S20 comes out about the same or lower. That is the comparison working, not a
bias to correct.

## 6. Layout and placement

**Titles across on screens 900px and wider; titles down below that, and always in the dialog** (D5). 900
is the site's Desktop rung ([UX-GUIDELINES.md](../UX-GUIDELINES.md)), which is also where the chart page's
hero goes two-column. In the owner's words: *"I think tablet is horizontal"* — a tablet held sideways is
1024px and up — and *"vertical for Fold"*. It is a media query rather than a render decision, so unfolding a
Fold mid-visit turns the graph without a reload.

- **Titles across** keeps #1 at the top. The x labels read the way a title is named (D6): every gem name on
  one row, a bracket under DIAMOND and RED BERYL's columns, their level numbers on the row below.
- **Titles down** runs the spot axis **#50 on the left to #1 on the right** — the further right a box sits,
  the higher the chart ranks in top 50s (owner: *"we want that on the right"*). The share prints at the end
  of every row, which is what a phone gets instead of hovering.
- **The dialog** is never wider than 572px, so it is always titles down.

**Placement** (D7). On the chart page the graph sits **in the hero, under the Core Skills and Features
chips**, above the PIU Center link — static SSR, so the headline sentence is real text a crawler reads. The
mock first put it among the Chart Stats cards; those cards are an interactive island, and the owner moved it
(*"just beneath core skills"*). In the dialog it sits in the same place on the Stats tab: under the chips,
above "Charts like this".

## 7. The census

**A stored daily census, not a read on view** (D12). The chart page is the site's public, crawled page, and
building every title's top 50s on the first view after each day or deploy would put a sweep of the whole
ladder in front of that request.

- **Job**: `rebuild-chart-pumbility-presence`, daily 12:30 UTC, `RebuildChartPresenceCommand(Phoenix2)` →
  `ChartPresenceSaga` (ChartIntelligence). After the nightly chart jobs' 07:00–12:00 window.
- **Reads**: two small SQL reads — the PIU Scores accounts on a gem and their stats, and the official
  ranking's band — while the scores themselves come from the two in-memory stores the peers features already
  keep warm (`PeerScoreStore`, `BoardScoreStore`).
- **Work**: price every score, build every top 50 through `PumbilityPeerPools` (the Breakdown card's
  definition), spot each chart, and fold the spots into per-chart, per-title spreads with the folder's
  shadow beside them.
- **Writes**: `scores.ChartPumbilityPresenceColumn` (one row per column: the title, its players, how many
  are PIU Scores accounts) and `scores.ChartPumbilityPresence` (one row per chart per column where the chart
  or its folder is held), both replaced per mix in one transaction: a page read during the write still
  gets the previous census, and a write that fails leaves the previous census in place.

Measured on the local copy (scores through 2026-09-15): 297 PIU Scores accounts and 1,000 ranking rows on a
gem; 43,469 site scores and 140,442 ranking bests priced; 56,866 held spots folded (47 ms in a prototype);
**28,647 rows written** — 2,519 of 3,644 scoreable charts are held on some gem, 9,844 rows where the chart
itself is held and the rest its folder's shadow. Lighter than the daily chart-similarity rewrite.

**Until the first run the card does not render.** Trigger the job once in `/hangfire` after deploy, or wait
for 12:30 UTC.

## 8. Held by more than shown

**piugame's chart rankings stop at 300 places, and a crowded one hides holders** (D14). An official-ranking
player's fifty is rebuilt from the chart rankings (§2). When a chart's ranking is full and even its last place
is a high score, a ranking player who holds the chart in their top 50 with a lower score is on no ranking of it
at all. Their fifty is rebuilt without it and still passes the total check
([pumbility-overhaul.md](pumbility-overhaul.md) D60), because the fifty-first chart slides in and fills the
total back up. The census counts them as not holding it, so from DIAMOND up, where the ranking players are, the
graph reads low. Measured on the local copy against the 2026-09-06 sweep: of 81 linked DIAMOND-and-up accounts
that pass the check, 42 hold at least one chart below a full ranking's last place, 2.5 each on average. The Last
Rebellion D23 is held by 54% of them, and the rankings see 37%.

**A ranking is crowded** when all 300 places are taken and its last place scores at or above its folder's bar. A
single reads one level up, the way it prices:

| Folders | Last place at or above |
|---|---|
| D20 · D21 · S20 | S+ · 975,000 |
| D22 · D23 · S21 · S22 | S · 970,000 |
| D24 and up · S23 and up | AAA+ · 960,000 |

That flags 81 charts on the 2026-09-06 sweep: S20 22 · S21 15 · S22 10 · S23 3 · D20 11 · D21 8 · D22 5 · D23 4 ·
D24 3. The bars are high on purpose. In the owner's words: *"I want to make sure we're not just overlabelling like
1/4 of a folder as 'too easy' simply because of a few hidden scores."* D20 was not in the owner's list; the
approved mock grouped it with D21.

**What a crowded chart's graph adds** (D15, the mock's Callout + titles with wording 1):

- **A callout** under the sentences, above the graph. Heading: `Likely more common than shown`. Body: `This chart
  is popular or easy enough that its official top 300 only reaches down to SSS (990,893). From DIAMOND up,
  players who hold it in their top 50 with a lower score can't be counted, so it's likely in more top 50s than
  this shows.` The grade and score are the ranking's last place. The gem is the lowest title whose players
  include official-ranking players.
- **A dashed extension past the share bar** on every title whose players include official-ranking players: above
  the bar when titles run across, right of it when they run down. The title's tooltip adds `Likely held by more
  players than shown`. A title counted over PIU Scores accounts alone is never marked, because their fifties
  come from their own records.
- **A legend key**, `Likely more than shown`.

**Read when the chart is viewed, not stored.** The ranking's depth and last place come from the mirror's latest
sealed sweep, cached per sweep, so a new sweep moves the note without waiting for the census.

**The Play page sets these charts apart too**, in a section called *Held by more than shown*
([pumbility-overhaul.md](pumbility-overhaul.md) D70).

## 9. Decisions

| # | Decision | Why |
|---|---|---|
| D1 | **A presence graph on the chart page and in the details dialog; Phoenix 2, gem ladder only.** | Owner, 2026-09-16: "a chart's presence in PUMBILITY (gemstone only) across the various levels". |
| D2 | **The Split graph** — a share bar above a spot box per title. | Owner picked it from three (Split, Fat boxes, Slot bands): "I like the split mock you have up front". It is the only one that shows every number asked for. |
| D3 | **Short tooltips in player words**; no official-ranking counts. | Owner: "those tooltips feel super wordy and math-majory … We don't need to know how much of the data is from official boards." |
| D4 | **The folder shadow**, and "rates higher/lower" at 5 spots with 25 players, 5 holders and 10 folder spots. | Owner asked for "does this rate higher or lower than other charts in this folder"; picked the Shadow over an arrow row. The 5-spot gap and floors are decided unless objected. |
| D5 | **Titles across at 900px and up; titles down below and in the dialog; #1 on the right when down.** | Owner: "tablet is horizontal", "vertical for Fold", "we want that on the right". |
| D6 | **Gem names on the first label row, level numbers under a bracket below.** | Owner: "you have the x labels ordered wrong" — the three-baseline staircase put levels above their own gem. |
| D7 | **In the hero under the chips; same spot in the dialog.** | Owner: "just beneath core skills". |
| D8 | **The Breakdown card's title cohort is the population.** | One definition of "the players on a title"; decided unless objected. |
| D9 | **A gem opens at 125 players; under 25 faded; under 5 dots.** | Decided unless objected — reuses D68's 25. |
| D10 | **Spots are places in the top 50; ties share the middle spot.** | Decided unless objected; 81% of spots tie. |
| D11 | **A chart with no official chart ranking counts PIU Scores players only.** | Decided unless objected; the ranking cannot show those charts. |
| D12 | **A stored daily census.** | Owner: "table with job sounds fine". |
| D13 | **A chart counted over PIU Scores players only opens its gems by those players, not by everyone.** | A bug check found a chart below 20 fading every DIAMOND level after LV.1; owner, 2026-09-16: "k". |
| D14 | **A chart's official ranking is crowded when all 300 places are taken and its last place is at or above its folder's bar: S+ for D20, D21 and S20; S for D22, D23, S21 and S22; AAA+ from D24 and S23.** | Owner, 2026-09-17: "D21/S20 - S+, D22/S21 - S, D23/S22 - S, D24/S23 - AAA+, D25/S24 and above - AAA+". D20 follows the approved mock. |
| D15 | **A crowded chart gets a callout, plus a dashed extension and a tooltip line on every title that counts official-ranking players.** | Owner, on the round five mock: "I like Callout + titles. Wording 1.", with *hold* for *keep*: "we're using 'hold it' not 'keep it' as the terminology". |
| D16 | **Your diamond draws only when the chart is in your top 50.** Titles down used to draw it beside your title's name either way, just left of #50, where it read as a spot. | Owner, 2026-09-17: "don't place a dot for you if you don't have the chart in your top 50. I think right now it's putting the dot at the far left." |

## 10. Known limits

- **The Fold's real width is unmeasured.** Published Z Fold 7 viewports are arithmetic and disagree. D5
  does not lean on it: a Fold unfolded is under the 900 rung on every figure published.
- **Official-ranking players are placed by the published number, their fifty rebuilt.** A rebuilt fifty can
  sit a little under the number it was placed by. Same trade the Breakdown card makes.
- **A tie at #50** is broken by chart id when a fifty is chosen, as everywhere else a top 50 is built.
- **A crowded ranking is a sign, not a count** (§8). It marks the charts where holders are most likely hidden;
  it says nothing about how many. A chart under its bar can still hide a few holders, which is the price of
  keeping the note rare.
