# PUMBILITY section (`/Pumbility`) — design

Rebuilds `/Pumbility` from a wall of chart cards into a planner, and replaces the projection
engine underneath it. Mocked against real data from the owner's own account before any code;
the mock is the visual authority for §3.

The **second round** (2026-08-08) splits the result into a three-page section sharing one frame,
and adds two things the first round never answered: what your number is *made of*, and what it is
*for*. §2 D13–D20 are that round's rulings.

The **third round** (2026-08-15) replaces who the Phoenix 2 projection asks. The engine in §4.1
was built on Phoenix 1 competitive levels and Phoenix 1 evidence; on Phoenix 2 that admitted
players two levels stronger as "peers" and printed SS on charts the viewer would not SS. Phoenix
2 now projects from **PUMBILITY peers** — the game's own PUMBILITY level ladder, Phoenix 2 data
alone — and the tier list's PUMBILITY lens shares the definition. §2 D21–D29 are that round's
rulings; §4.8 is the formula and the measurement; §6.6 the scope.

Mocks: round 1 https://claude.ai/code/artifact/5dded5e4-03d7-4d70-80ed-d5ecfac68aa2 ·
round 2 https://claude.ai/code/artifact/2196691e-b756-458c-b84c-229061046745

---

## 1. Why

The page opens with fifty chart cards grouped under eight headings — "Extremely High Rating",
"Standard Rating", "Very Low Rating" — that are never defined, then a gain calculator, then a
BETA projection list. Owner's read: *"the page is leading too much with the answer before it
gets to even define the question."*

Three specific failures behind that:

1. **On Phoenix 1 the page never prints your PUMBILITY at all.** The total/singles/doubles chips
   are gated `_currentMix == MixEnum.Phoenix2`. The page named after a number does not show the
   number to the majority of its visitors.
2. **The mechanic is invisible.** PUMBILITY is the sum of your top 50, so the only way to gain is
   to beat your 50th chart. That value is computed today as a local inside `CalculateNewScore`
   and thrown away. Nothing on the page says what a chart has to beat.
3. **The eight rating bands are a tier-list idiom on a page that is not a tier list.** They rank
   your pool against itself and never say what the ranking is for.

And the projection those cards feed was measurably wrong: it weights charts through the **chabala
skill rollup**, which the owner ruled out — and which, measured, correlates **0.071** with the
error it exists to correct (§4.3). The engine is replaced outright in §4.1.

## 2. Locked decisions

Owner rulings, 2026-08-05. These bind; do not re-litigate them in the build.

| # | Ruling |
|---|---|
| D1 | **The eleven chabala skills are gone.** *"Those 11 skills are gone. Kaput. Nowhere."* Where skills are used at all it is the granular piucenter badges — one badge = one observation, weighted by its own measured `badge_fraction:`, no mapping table, no rollup. The 33 badges are **distinct by domain ruling**; do not re-derive overlap from how the names read. ⚠ **The projection ended up carrying no skill term whatsoever** (§4.3), so this ruling now binds the *display* surfaces and the tier-list blend, not this estimator |
| D2 | **900,000 is the proficiency floor and it stays.** *"A score below 900k is 99 times out of 100 mashed for a pass. 900k is when scores start being scores and not just numbers attached to people who hit a lot of buttons."* ⚠ Moot for the estimator, which no longer measures deviations at all; still binding anywhere proficiency is computed (the thumbprint as displayed data, the tier-list blend) |
| D3 | **Recommending charts you have never played is the point**, not a risk to hedge. *"It's almost the entire point."* |
| D4 | **The Phoenix 2 carryover is the full repricing** — every Phoenix 1 score priced under Phoenix 2's rules, ranked by what it would be worth, for all three pools (All, Singles, Doubles) |
| D5 | **The pool selector re-ranks the whole page** — total, bar, curve, targets and pool board all scope to the chosen pool |
| D6 | **The page keeps its name.** It is still the PUMBILITY page |
| D7 | ~~**The top 50 is collapsed and below the fold.**~~ **Superseded by D14.** It was a fold because it shared a page with the answer; on a page of its own it is simply that page's last section |
| D8 | **No world-rank chip.** Offered and declined |
| D9 | **"Kind" is not a column.** It restated the cell beside it — see §3.3 |
| D10 | **A broken run plays no part in this page** (owner, 2026-08-07). *"Failed shit should not show anywhere on here."* A stage break rates zero, so it is not a score the player holds: it cannot occupy a pool slot, cannot set the bar, and does not count as having scored a chart. Enforced at the two top-50 reads rather than per call site, so the queries mean what their names say |
| D11 | **A carryover target is priced, not gated.** A chart already scored in Phoenix 2 stays a target when the Phoenix 1 repricing beats what it currently contributes — same floor as the peer projection, one ranked list, both sources priced identically |
| D12 | **The projection does not explain itself** (owner, 2026-08-07). Peer counts, effective voices and spread were printed beside every estimate and told a player nothing they could act on. What survives of the why-line is the **source** — carried from Phoenix 1, or projected — because that is the one thing about a number that changes how far to trust it; what the row is *to you* the card's border and the legend already say. A thin cohort remains a reason to **gate** a suggestion; it is not a caption |

Round two, 2026-08-08.

| # | Ruling |
|---|---|
| D13 | **⚠ A gain is measured against the bar as it stands now, and never against a running sequence.** An ordered "path to your next title" was designed and rejected: *"The top 10 PUMBILITY suggestions I have in my list are NOT ones I'm going to play because I'm out of shape, so you would be taking away meaningful information for me who is playing lower items on the To Play list."* Re-pricing row N as if rows 1…N−1 had already been cleared destroys the column for everyone who plays out of order, which is everyone. **"What to play next" is not to be touched.** Do not rebuild the ordered path — see §3.7 for what replaced it |
| D14 | **Three routed pages sharing one frame**, the Official Leaderboards pattern: **Play** `/Pumbility` · ~~**Your Pool** `/Pumbility/Pool`~~ **PUMBILITY Breakdown** `/Pumbility/Breakdown` (renamed in round six, D45; the old route still resolves) · **Phoenix 1** `/Pumbility/Phoenix1`. **One** menu entry, pointing at Play |
| D15 | **The frame carries the number, the pool selector and the bar**, because all three pages measure from them. Everything in it is **left-aligned** — a bar card pushed right with `margin-left:auto` strands itself against the far edge the moment the row wraps |
| D16 | **The breakdown measures from pure base (×1.00)**, not from a grade. Owner: *"Pure base is showing more real data on how distribution actually works."* ⚠ If it ever moves to a grade the reference is **900,000** — which is **AA on Phoenix 1 but A+ on Phoenix 2**, since P2 shifted the sub-AAA floors. One score, two grades |
| D17 | **"Your PUMBILITY titles", never "projected"** — that word is spent on the peer estimator (§8.1), and a title you actually hold is not a projection |
| D18 | **The what-if calculator is deleted**, moving to the rating-calculator overhaul. ⚠ It loses the "what would this **add**" framing in transit: it prices against *your bar* today, and `/RatingCalculator` has no pool |
| D19 | **`GetPumbilityPageQuery` stays one query.** Splitting the cheap pool read from the expensive projection was offered and declined — performance gets its own pass. This does **not** worsen the cold read (§6.5) |
| D20 | **A chart worth zero never occupies a pool slot** — the general rule D10 was one case of. See §3.8 |

Round three, 2026-08-15. Phoenix 2 only; Phoenix 1's projection is untouched.

| # | Ruling |
|---|---|
| D21 | **The Phoenix 2 projection is mix-independent.** *"Treat the projections as mix independent, then go off of pumbility level in phoenix 2 alone, NOT Phoenix 1."* Peers, evidence and levels come from Phoenix 2; the peer estimator performs no cross-mix read. §4.7's pooling of Phoenix 1 evidence is superseded for Phoenix 2 |
| D22 | **PUMBILITY peers** — the players within **±3 rungs** of you on the Phoenix 2 PUMBILITY level ladder (`Phoenix2PumbilityLevel`, the hidden five-levels-per-gem ladder: DIAMOND LV.4 reaches down to DIAMOND LV.1 and up to RED BERYL LV.2), where **you and each peer hold a full 50-chart pool of the chart type**. *"This would formally introduce the idea of 'PUMBILITY Peers'."* One rung, the total pool's, serves both chart types; the full-pool rule is per type. Measured against every alternative in §4.8 |
| D23 | **The tier list's PUMBILITY lens uses the same definition.** *"They need to be the same."* And the word is **peers**: *"please stop calling 'peers' cohorts."* The lens's keys, columns and code rename accordingly ([pumbility-tier-list.md §5](pumbility-tier-list.md)) |
| D24 | **A chart shows only when five or more peers have passed it.** No level window: *"That was already a bit flaky, I think the '5 or more peers have to have passed it' covers us on not showing unrealistic charts. This lets the occasional 'D23 that a level 18 player can actually pass if they spend a few minutes memorizing a single section' entry show."* The perfect-score-cannot-clear-your-bar filter (§6.5) stays — it is arithmetic, not a level |
| D25 | **No competitive level on this page.** *"Competitive level isn't really the answer here. It is built for score alone, it doesn't view scores <950k as valid ... pass pushing is a valid competency."* The peer band replaces it in who is asked, the five-peer floor replaces it in which charts, and the growth weighting — a competitive-level delta — is dropped: *"drop it."* Measured: on Phoenix 2 the weighting discounted 58% of the evidence, because everyone's level climbed three rungs in the first month |
| D26 | **The Phoenix 2 estimate is the median (p50), not p65.** Measured, not chosen: p65 was fitted on Phoenix 1 eventual bests; on Phoenix 2 evidence it reads +6k median, p50 reads +1.8k, and with the full-pool rule −44 (§4.8) |
| D27 | **The peer count is shown**, per type, in one clause: *"a disclaimer on 'this is how many PUMBILITY Peers you have' somewhere."* This is a section line, not the per-row evidence caption D12 removed |
| D28 | **A type without a full pool is dark.** *"Have peers/projections only light up/show once you're at 50 charts for that type."* Charurun plays no singles and sees no singles peers; a 29-double pool sees "29 of 50" |
| D29 | **The carried Phoenix 1 rows are untouched.** *"The projections we have in phoenix 2 that are just 'your phoenix scores, mapped to Phoenix 2 pumbility' — those should remain exactly as is. We are only changing projections."* `CarryoverTargets`, `/Pumbility/Phoenix1`, the pool, the bar, the titles and the ask do not move |

Round four, 2026-08-17, after the owner's field test of round three.

| # | Ruling |
|---|---|
| D30 | **A peer row carries a "Peers IQR"** — the peers' 25th and 75th percentile scores as two letter grades with a dash between them, beside the median. Owner: *"Can we do an estimated range? … If we do like 'Estimated P25 -> P75 letter grades'? It sort of helps visualize confidence levels a bit too. If like everyone in your pool is getting a SS, you'll probably get an SS. If P25 is A+ and P75 is SSS+, you know it's a specialized chart."* Named **IQR**, not "range": *"call it 'Peers IQR' so those who understand math nuance will know it's not a Min/Max range."* Grades only — no quartile numbers, no width figure — and *"at most a tooltip for 'From X peers'"*. **Compact is unchanged**: *"Only so much data we can fit there."* A carried Phoenix 1 row has no peers and prints a dash. This narrows D12: the numeric spread D12 removed stays removed; two grades that say whether the peers agree is the one per-row figure about the evidence that earned its place, because the field test read the median alone as a claim ("it's still giving me S22 SSs") when the peers behind it were split |
| D31 | **You are never one of your own peers** (owner, 2026-08-17). The projection has always drawn its band with the viewer removed; the tier lists' PUMBILITY lens now does the same at read time — the stored list is one per peer group and counts every member's pool, so the reader's own pool is taken back out (one from the peer count, one from every chart it holds, the bands redrawn), and both lenses' captions on Phoenix 2 name PUMBILITY peers. Nightly is the caveat: a pool that filled since the last build was never counted in, and for that day the subtraction runs one deep |

Round five, 2026-08-18. Phoenix 2 only; Phoenix 1's Play page is untouched. The page these rulings
describe is §3.10; the measurements behind them are §4.9.

| # | Ruling |
|---|---|
| D32 | **Your PUMBILITY peers replace "What to play next" on Phoenix 2.** Owner: *"And yes, this replaces 'what to play next'."* `/Pumbility` on Phoenix 2 renders the peers page in the target list's place; the section stays three tabs; ~~Phoenix 1 keeps the target list of §3.3 exactly as it is~~ — **that half was never asked for and is reversed by D43**: Phoenix 1 gets the page too, and the target list is retired as a component. Measured first (§4.9): every peer-projected target is a chart at least one peer holds, so nothing the old list said is lost — what changes is the order it says it in, and where the carried Phoenix 1 rows sit |
| D33 | **The grouping is called Prevalence** — *"Prevalence is good. I like that. Go with that."* — and it is a weighted count: a chart at #1 in a peer's pool scores 50, at #50 scores 1, summed over the peers (a Borda count; each peer casts an equal 1,275-point vote, so the band's strong tail cannot dominate the way a raw value sum would). **The hold count is what prints** — "17 of 23 peers" — and the weighted number rides a tooltip that says **"Weighted sum: 550"**: *"don't call it borda there."* Measured: the weighting barely reorders (top-50 overlap 39/50 against the plain count) and is not level-sorted inside a band |
| D34 | **It is a tier list and follows the tier list page's pattern** — *"Follow the Tier List page's pattern for all of this (UI/UX wise too)."* Staple · Strong · Solid · Average · Modest · Slim · Poor by the PUMBILITY lens's own log-scaled banding on the rarity ramp; collapsible sections with **Slim and Poor folded** by default; Comfortable / Compact / Table via the standard trio, which sits **right above the list, right-aligned**; the To-Do bookmark and its dashed-blue ring exactly as the tier list wears them. **No song-name filter, no other filters, no applied-filters row, no sorting** (*"No, no sorting"*) |
| D35 | **Variability, not IQR grades.** The Peers IQR (D30) survives as a data point but prints as a level: the peers' interquartile width in points, `log(1 + w/1000)`, banded ±0.5σ / ±1.5σ across the charts they play — five levels named **Very consistent · Consistent · Mixed · Split · Very split** (*"i like a"*), per chart type, only where five or more peers scored the chart. Measured: raw widths skew +1.10 and the bottom band never fires; on the log they skew −0.25 and all five assign (7/34/36/34/9 on the reporter's singles peers). The word always prints; **Compact carries a dot top-right and Comfortable does not** — *"Comfortable doesn't need the variability dot, only compact does"* |
| D36 | **Grouped by: Prevalence (default) · Projected gains.** Under Projected gains the list is only what pays, biggest first, in bands of PUMBILITY points, and carried Phoenix 1 rows interleave by gain exactly as the target list did — it *is* the old page. A **Project Phoenix 1 scores** switch appears there, and only there, and only *"when the player has phoenix 1 scores that would change the data"*: on (the default) is the carried-wins rule of D11/D29; off drops the carried rows and the peer projection shows through where one exists. Under Prevalence an **Only projected PUMBILITY gains** switch cuts the tiers to the charts that pay; the front page opens with it on |
| D37 | **The pool selector's All is one merged list** — *"merge into one"* — tiers and order computed per chart type (different electorates: 23 singles peers, 7 doubles), the sections interleaving both, ordered within a tier by each chart's share of its own electorate, and every card saying "5 of 7 peers" so the electorate is visible. Not stacked lists |
| D38 | **The jacket carries the gain and nothing else.** *"Switch out the X/Y peers for the green/blue outlined Projected PUMBILITY (if you're projected to have a gain)"*, then *"drop the X/Y peers number on the song jacket for both comfortable and compact."* The badge is the target list's own — mix-primary outline, `+18` — and the gain is the target list's own rule (D11/D29, carried wins, measured against the bar as it stands, D13), so the two pages can never disagree about a number. A chart that would not pay wears no corner at all; the hold count lives in the Comfortable body and the Compact tooltip. Compact's other bottom corner is the projected grade, as on the target list |
| D39 | **The peers get a roster** — *"give them a dedicated table at the bottom"* — name, PUMBILITY level, PUMBILITY total (N2, it is a pool), **competitive levels too** (*"maybe competitive level too"*), which type they are a peer for under All, and how many of your fifty they also hold; sorted by total with your own row highlighted where you would sit. **Private accounts are counted as peers and not named**, with *"X private accounts not shown"* beneath |
| D40 | **The chart leaderboard gets one Peers chip with a sub-row** — Competitive · PUMBILITY — *"for now"*; the sub-row appears only where both exist (Phoenix 2, the viewer's type lit), and the default when clicked cold is **Competitive** (*"do competitive for now"*). Hosts pass the sub-scope: a card on this page opens on PUMBILITY, a session score row on Competitive as before. Private peers stay on the board as Anonymous rows, exactly as Competitive Peers keeps them. This is the first surface presenting two of the [peer pools](peers-abstraction.md) as one control |
| D41 | ~~**The compare strip and the Yours-alone section ship, on the mock's evidence, as cuttable.**~~ **Ruled in field test round two: the In-common and Yours-alone tiles are cut** (*"not useful"*) and the strip is the **level bars alone**, sitting beside the list's lede on a wide screen and dropping below it once the viewport is square or taller. `PeerCompare` lost the three counts with them — a number nobody can act on is not worth computing. The Yours-alone *section* in the list survives |
| D46 | **The Your top 50 lens has its own tier vocabulary (field test round two).** Owner: *"Staple/Strong/etc. make no sense"* on your own fifty. Those bands are what a chart is worth to **you**, not how many players keep it, so they read as a magnitude: **Highest · Very High · High · Average · Low · Very Low · Lowest**. Average, Low and Very Low are the Score lens's own words, so the two ramps sound like one family; `PumbilityTierNames.PoolNameOf` is the second map beside `NameOf` |
| D42 | **What is assumed until the field test says otherwise:** the front page lands on **Prevalence with the gains switch on** (confirmed in round six: *"Stay on Prevalence"*); the block title is ~~**"Your PUMBILITY peers"**~~ **"Your peers"** on both mixes since round six (D43) and does not change with the grouping; the gain-band sections are fixed point bands (+25 and up · +15 to +25 · +10 to +15 · +5 to +10 · +2 to +5 · under +2), the unit the bar and the ask are read in, rather than σ-banded |
| D43 | **Phoenix 1 gets the page too, with the competitive band as its peers (round six).** Owner: *"has phoenix 1 not been getting the same exact treatment as 2? functionally it should be exactly the same for all the grouped by etc. right?"* `/Pumbility` renders the peers page on both mixes; `TargetList` is deleted, and what it ranked survives as the Projected gains lens (D38). A Phoenix 1 peer is the band the projection already draws from — players within one competitive level of you for the type (§4.7), the viewer out — which is also the cohort the leaderboard's Peers chip shows there, so the page, the chip and the gain agree about who "peers" are on both mixes. **No full-fifty gate on Phoenix 1**: the band is drawn from competitive level, which is real at any pool size, so a Phoenix 1 player is never dark (D28 stays Phoenix 2's) and a thin peer simply casts a shorter vote. The read widens from the targets' level band to the pool floor — level 10 is the formula's floor on both mixes, `BaseRating` is zero below it — roughly twice today's Phoenix 1 sweep, on the same 24-hour cache; a first visit already says it takes a moment. Stated, not hidden: Phoenix 1's gain is the discounted quantile of §4.1 and the card's "Peers' median" is the plain median of the same group, so the two can differ by a grade there — on Phoenix 2 they are one number (D26). The block is **Your peers** on both mixes and the lede names the cohort. **The roster is capped at fifty rows around you** with the counts above and below — a band is several hundred players, and the roster's question is "where do I sit", not "who is first" |
| D44 | **Your top 50 is the third lens of the Play list (round six).** Owner: *"What if we merge in the 'Your top 50' as one of the options to group by."* Under it the list is the pool the frame is scoped to — the selected pool's fifty, by place — split at the bar into two sections, **Your top 50** and **The waiting room**, each card carrying its place, the chart's value, your grade and score, and the peers line beneath (how many hold it, their median, how split); the table is the old pool board with the peers' columns added. No switches under this lens. The pool board of §3.4 and its no-density board skin retire with it — the fifty wear the tier-list card like everything else on the page. Web-only: the frame's record joined with the peers record by chart id |
| D45 | **The Pool page is the PUMBILITY Breakdown page.** Owner: *"keep a section for all of the 'Your Pool' data/distribution stuff and call it 'PUMBILITY Breakdown' instead of 'Your Pool', keeps it on its own dedicated page."* `/Pumbility/Breakdown` — your PUMBILITY titles, where your PUMBILITY comes from, the pool curve — with `/Pumbility/Pool` still resolving to it. The top 50 leaves it for the Play list (D44); the curve stays, because it is the bar's picture rather than the list's |
| D47 | **The way out of the section is a link on the eyebrow, not a chip in the nav.** Owner: *"It's a link, not a button… more out of the way, it bloats up the actionable field and clutters mobile too much."* `How it works →` sits at the right of the `PHOENIX 2 · PUMBILITY` line in `--mix-primary`, so the one thing on the line that leaves the section is the one thing on it that is not muted; the nav row keeps its three tabs and its width. The row is `space-between` rather than `margin-left:auto`, so a long locale drops to the left of the next line instead of stranding itself against the far edge (D15). It points at the viewer's own mix and renders once the mix is known, so it never flips under the reader. The calculator carries the return trip, `Your PUMBILITY →`, on its own eyebrow beside the cross-mix links — signed in only, since the frame sends an anonymous visitor to `/` |

## 3. The section

### 3.1 The three pages

One frame, the `OfficialSectionFrame` pattern: shared chrome, each page its own route and circuit,
nav links as real document loads.

```
FRAME   your number · pool selector · the bar        ← left-aligned, all three pages
        [ Play ]  [ PUMBILITY Breakdown ]  [ Phoenix 1 ]

Play          your peers, both mixes (§3.10)                      /Pumbility
              Grouped by: Prevalence · Projected gains · Your top 50
PUMBILITY     your PUMBILITY titles                               /Pumbility/Breakdown
Breakdown     where your PUMBILITY comes from · the pool curve    (/Pumbility/Pool still resolves)
Phoenix 1     what Phoenix 1 is worth here                       /Pumbility/Phoenix1
```

**Phoenix 1 is a two-page section.** No carryover, no PUMBILITY ladders
(`Phoenix2PumbilityTitle` is Phoenix-2-only), no pool selector — one pool, and splitting it would
invent a stat. The frame drops the third chip rather than showing an inert one, exactly as the
official frame drops What It Takes; `/Pumbility/Phoenix1` reached by URL redirects to Play. Since
round six the Play page is the same page on both mixes (D43): what differs is who a peer is, and the
page says so in its lede.

**The frame's nav renders before its data**, like `OfficialSectionFrame`, which skeletons only its
body. You can change tabs while the number is still arriving rather than facing an inert section.

**The pool selection is a `Pumbility__Pool` UiSetting**, not circuit state: tab links are real
navigations, so anything held in a circuit dies between them. It persists between visits too, which
the in-circuit selector never did.

### 3.2 The bar is the organising device

The hero prints your PUMBILITY, then immediately prints **the value of your 50th chart** and
names the chart holding it. Everything else on the page is positioned relative to that line: the
curve draws it, the target list only contains things that clear it, the pool board renders it as
a rule with the waiting room ghosted below.

Worked example (DrMurloc, Phoenix, prod-synced 2026-08-05): PUMBILITY **64,466.90** — the
official board reads 64,466 — and the bar is **1,207.50**, held by HTTP D24 at 925,308 (AA+).

### The pool curve

Fifty solid bars descending, coloured by chart type, with the bar as a dashed line and ranks
51–56 ghosted beneath it. It answers the one question the pool can actually answer: **how flat
is it**, which decides whether grinding pays.

On the same account that reads: top to bottom is **17%**, and four charts sit level with the
50th. That shape says grind volume, not one hero chart. Under Phoenix 2's much narrower base
curve the same pool reads **2.5%**.

The curve replaces the eight rating bands. They are deleted, not relocated.

### 3.3 Targets — "what to play next" (retired as a page in round six)

Ranked by projected gain. **Retired as a component in round six (D43)** — on both mixes the Play page
is §3.10 and this list is its Projected gains lens; everything below is the record of the target list
as it rendered from round one to round five, kept because the arithmetic still runs: §3.10 prices its
gains through exactly this list (D38), and the Phoenix 1 engine (§4.1) is unchanged.

**Comfortable and Compact are the tier list's chart card** (`TierListChartCard`), not a
lookalike — the same component, extended. A grid of chart cards is one concept, and the tier
list already owns it: jacket, difficulty bubble top-left, body below, To-Do and details in the
action row. What this page adds is generic and reusable — a printed **corner badge** bottom-right
of the jacket, a **body slot**, and an optional **play** affordance that opens the chart dialog
with the video already running. Cloning the card instead would have bought a second thing to
keep in sync, which is the drift the one-concept-one-component rule exists to prevent.

The corner badge is the **gain**, because a Compact card is 72px tall and prints exactly one
value — so it has to be the one the list is ranked by.

**The jacket's other bottom corner is the grade the projection lands on** (owner, 2026-08-07).
The difficulty bubble owns the top edge, so the two badges share the bottom: how much on the
end, what you would come away with on the start. It is a picture rather than a second number,
so "Compact prints one value" still holds — and the tooltip says it in words, because Compact
has no body to put them in (rule 7).

**The border says which kind, the number says how much** (owner, 2026-08-06). Both jacket
badges are one treatment — the **mix accent** on the mix accent over the black backdrop — and
carry no meaning beyond what they state. Deliberately not the pass green: the border language
below owns green, and `MixPalette.Success` is one constant across every mix, so a badge painted
with it says nothing about where you are. The kind rides the **card border**, in the tier list's
own language rather than a second one invented here:

| border | meaning |
|---|---|
| solid success (`.tier-chart-card-pass`) | you hold a score on this chart and would beat it |
| dashed success (`.tier-chart-card-other-mix`) | you hold it in *another mix* (§5) |
| dotted grey (`.tier-chart-card-broken`) | you played it here and broke |
| none | nobody has seen you play it |

The fourth state is what D10 costs. Once a stage break holds no score, a chart the player broke
on falls through the first two — and *no* border would claim they had never touched it, on
exactly the charts the rule changed. It ranks below both pass states on purpose: where a chart is
attempted here and passed in Phoenix 1, where the number came from is the more useful thing to
say. Grey rather than the pass green because the run earned nothing, dotted rather than dashed so
the three "we have seen you here" states stay separable at 72px.

Same classes as the tier list, so the two pages cannot drift apart. A "Kind" column restated
what the card already says, and read the same value down every row on Phoenix 1.

**Compact only.** Comfortable says the kind in words on every card's why-line, and its To-Do
bookmark would otherwise paint a fourth ring competing with the three that mean something — so
it turns the state border off (`ShowStateBorder="false"`, a flag that defaults *on* so the tier
list is unaffected). Compact has no room for a word, so the **legend prints every kind the grid
contains** — and only those, since a swatch for an absent state reads as one you failed to find.
Rule 8 is satisfied at both densities without a colour travelling alone at either.

**One ranked list, two sources of evidence.** Not two lists stapled together: up to 100 peer
estimates and up to 100 carried Phoenix 1 scores, merged and cut to 100. The cut happens *after*
the merge, so a chart both sources name cannot spend two slots.

A chart from the player's Phoenix 1 record that they have not scored here is not estimated — the
score is on record and repricing it is arithmetic. Those rows **replace** any peer estimate for
the same chart (owner: *"there is no better data than the actual scores you had before"*) and are
the only signal that works at a mix launch. Table density keeps a **Based on** column with the
word spelled out, because a table cell can hold a word where a 72px card cannot; it renders only
where the list actually mixes sources.

**No filters on the list.** A filter that re-runs the query is what the hero's pool selector
already is, and two controls driving one piece of state is one too many. The max-level filter
was dropped outright — narrow use case, and it cost a control on every visit.

⚠ **The "why" line is OPEN, and the mock's version is now dishonest.** The mock renders badge
chips ("▲ Anchor Runs · ▼ Twists") as the explanation for each target. That was drawn when the
estimator weighted charts by skill. **It no longer does** — §4.1 carries no skill term — so
printing badge chips beside a projection would claim a causal path that does not exist, and the
site does not do that (rule: say what you cannot compute).

What the estimator can honestly explain is *how many peers it heard from, how recent their
scores are, and how spread they were* — an evidence line, not an attribution line. Two of the
three have since landed, in a different shape than a line: the peer count as a section chip (D27)
and the spread as the Peers IQR grades (D30). What the page may still show, separately and
unattached to the projection, is the player's own thumbprint as descriptive data (§4.3). **Still
open** — see §9.

**Phoenix 2, round three (D22, D24, D27, D28).** The list carries one **peer line** above it: a
chip per chart type in the selected pool — *"23 PUMBILITY peers"* for a lit type, *"Doubles:
29/50 charts"* for one whose pool is not yet full (on All, *"Singles: 23 PUMBILITY peers ·
Doubles: 29/50 charts"*). The definition ("within 3 levels of you with a full pool") and the
five-peer clause live in the chip's tooltip, not on the line — the first version spelled both out
and the owner's field test read it as *"wordy af and filling up the screen"* (2026-08-16); a
count, not a paragraph. Peer rows exist only for charts five or more of those peers have passed.
Carried Phoenix 1 rows are unaffected by either state (D29): a dark doubles pool still lists your
Phoenix 1 doubles repriced.

**Round four: the Peers IQR (D30).** One thing per row did change after the field test. Beside
the projected grade and number, a peer row prints the peers' **first and third quartiles as two
grades** with a dash between them — *AAA — SSS* on a chart that splits the peers, *AAA+ — AAA+*
on one they agree about — under a **Peers IQR** column in Table and a **Peers IQR** line in the
Comfortable card body. **The dash is one treatment on every row.** The first cut drew it as a
connector that encoded the width (short and solid inside 10,000 points, long and dashed past
25,000) and the owner's field test read it as noise the same day: *"why is it dotted lines vs a
solid line for different entries? just make it the dash."* The grades already say the width; a
second encoding of the same fact is a thing to explain. The tooltip says *"From 13 peers"* and
nothing else. Compact prints nothing new — a 72px card has one value and one picture. Carried
Phoenix 1 rows have no peers and print a dash in Table, no line in Comfortable. Phoenix 1 rows
get the same column from the same arithmetic (its quartiles are growth-weighted like its p65),
because `ScoreProjection.Spreads` is filled by both branches of the projector — the IQR is read
over exactly the voices and weights the median was, so the two cannot disagree about who was
heard. The why-line stays what D12 left it: the source, in words.

Density trio via `Density__Pumbility`, governing the targets only, using the site's standard
control — a `MudButtonGroup` of `ViewComfy` / `GridView` / `TableRows` icon buttons, the same
one the tier list carries.

**Pagination** sized by density (Comfortable 24, Compact 60, Table 50) — one page size would be
wrong at two of the three. A shorter list **clamps** the current page rather than resetting it,
so a density flip or a pool switch keeps you roughly where you were instead of throwing you back
to the top.

### 3.4 The pool board — superseded by the Your top 50 lens (D44)

~~Board rows wearing `.olb-rank-card` — a ranked list of entities gets the leaderboard skin and **no
density toggle** (rule 5). The bar renders as a rule in the list, with the waiting room ghosted
beneath it.~~ Since round six the fifty are the **Your top 50** lens of the Play list (§3.10): the
same ranked pool, split at the bar into the fifty and the waiting room, wearing the tier-list card
with the peers' data on every row. The board skin and its no-density rule went with the board —
the list has one density setting and the fifty obey it. The curve stays on the Breakdown page,
because it draws the bar, not the list.

### 3.5 The what-if calculator — deleted

Moved to the rating-calculator overhaul (D18). What it loses in transit is worth naming: today it
prices against **your bar**, so it answers *"what would this add"*. `/RatingCalculator` already
builds a `PumbilityScoring(Phoenix2, false)` config but holds no pool, so it can only answer *"what
is this worth"* until it picks up the pool read.

### 3.6 Where your PUMBILITY comes from

A band on the PUMBILITY Breakdown page (Your Pool until round six, D45). **The split is exact, not modelled.** A pool entry is
`Base(level) × grade × plate` and nothing else — `AdjustToTime` is off in both PUMBILITY configs,
every `SongTypeModifier` is 1.0, and `ChartLevelSnapshot` is null, so the base is a pure function of
level (plus Phoenix 2's singles bump). The three parts therefore **sum to the real total**, which is
the invariant the tests pin.

Measured from **pure base, ×1.00** (D16):

| | Level | Score | Plate |
|---|---|---|---|
| Phoenix 1, the owner's pool | 58,242 · 90.3% | +6,225 · 9.7% | **0 · exactly zero** |
| Phoenix 2, the same 50 repriced | 12,442 · 69.0% | +5,524 · 30.6% | +75 · **0.4%** |

**Plate is the reason this exists.** Phoenix 1's plate modifiers are all exactly 1.0 — the plate you
walked away with never entered the number at all. Phoenix 2's are additive bonuses of 0.000–0.020
against grade multipliers of 1.00–1.50, so a plate is worth at most **1.3% of a chart**.

Two devices carry that without exaggerating it:

- **The stack is true to scale.** The plate segment renders as a hairline because it *is* a hairline.
  It is never widened to be visible.
- **The plate gets its own magnified rail**, running from Rough Game on all fifty to Perfect Game on
  all fifty, so the sliver can be read on a scale where it has room. The line under it prices the
  ceiling: Perfect-Gaming every chart in the owner's pool is **+174, or 0.97%** — about **twelve
  chart swaps**. On Phoenix 1 that line reads *nothing*, flatly.

⚠ **The reference is load-bearing and the note has to stay honest.** Pure base measures from ×1.00,
which on Phoenix 2 is exactly what a Single's **D** pays — every grade above it adds, and an F never
appears at all because it contributes nothing, so the Score segment never subtracts.
Measured from AA instead, the same pool reads 93.8 / 5.8 / 0.4. On Phoenix 1 the question does not
arise: AA's modifier is exactly ×1.00, so pure base *is* AA-neutral there. That is also why Level
reads 90% on one mix and 69% on the other — same pool shape, different zero point.

### 3.7 Your PUMBILITY titles

The section that answers *what is the number for*, and the replacement for the ordered path D13
killed.

**The device is the ask.** A pool is fifty charts, so a threshold is a flat per-chart value: a title
at 19,000 asks **380.00 of every chart you hold**. Against the owner's carryover pool averaging
360.82, that is **+19.18 on every one of fifty**. No ordering, no counting, no reaching into the
target list.

```
TOTAL   18,041                              [P.B] RED BERYL   held since 18,000
[▓░░░░░░░░░░░░░░░░░░░░░░░░░]  RED BERYL 18,000 ──── ALEXANDRITE 19,000
ALEXANDRITE asks 380.00 │ your charts average 360.82 │ your bar 358.08 │ +19.18 each
380 a chart is an S25 at AAA — or an S23 played perfectly.
```

**One rail, following the pool selector** (owner, 2026-08-08). Drawn as three at first, on the
reasoning that a player holds all three ladders at once — but the selector already re-ranks the
total, the bar, the curve, the board and the targets, and a control that moves everything in the
section except this one reads as broken. The other two ladders are one click away, and the totals on
the selector itself say what they are worth. The rung bar is the device `PumbilityTitleTrack` already
draws on the tier list, so the two surfaces stay legible as the same idea.

**The ask names three charts, not one**, at SSS+, AAA and A — the shape the title drawer already
settled ([PR #234](https://github.com/DrMurloc/PumpItUpScoreTracker/pull/234)). Play quality moves the
answer by several levels, so one reference is right only for the player already performing at it.
⚠ **A is the floor because it is the lowest multiplier this site has verified**; B and below are the
unverified −0.05 extrapolation in `Phoenix2PumbilityScoring`, so never anchor lower without live data.
Best grade first, so levels ascend and the low one reads as the hard one, and the grade stays on the
same line as its level — in a shared caption underneath, three levels read as a path.

**The fourth cell is the realism check.** `ProjectedAverage` is what the fifty would average if every
suggestion landed on its projection; read against the ask beside it, that says whether the list on
Play reaches the rung at all. ⚠ It is a **merge, not a sum** — a chart already in the pool keeps the
better of its held and projected value — so no gain is ever added to another, which is precisely what
§8.3 forbids.

**Why the ask and not a count.** It reads correctly at every distance without changing shape: +19.18
a chart is a whole grade band on all fifty and says so; +2.62 a chart reads as *basically there*.
Neither number needs a play order to be true, which is exactly what D13 forbids. The translation into
a real chart — *an S25 at AAA, or an S23 played perfectly* — is the actionable half, and both figures
are exact against the formula.

**Edge states, both drawn:**

- **A thin pool.** The ask keeps its fifty-denominator — a threshold is a demand on a full pool
  however much of one exists — but **your charts average divides by the charts actually held**
  (owner, 2026-08-13): a ten-chart pool averaging 400 reads 400, not 80. The number moves as the
  pool fills, which is accepted. (An earlier draft of this state reworded the whole cell row —
  *"Your pool holds 7 of 50. BRONZE asks 200.00 a chart across fifty."* — and was never built;
  dividing the average by fifty in the meantime is what a player reported as a wrong number.)
  This is the common case for months after a mix launch, not an afterthought.
- **The top of a ladder.** The rail states that nothing sits above ABYSS ABSOLUTE rather than
  vanishing, so the section never disappears on the one player it should be congratulating.

### 3.8 A chart worth zero never occupies a pool slot

D10 dropped broken runs from the two top-50 reads. **It was one case of a general rule, and the other
three were never covered.** Four kinds of chart rate exactly zero and still hold a slot:

| | why it is zero | filtered before? |
|---|---|---|
| a broken run | `StageBreakModifier = 0` | yes (D10) |
| CO-OP | `ChartTypeModifiers[CoOp] = 0` | yes, explicitly |
| half-double / performance | `ChartTypeModifiers = 0.0` | **no** |
| **anything below level 10** | `DifficultyLevel.BaseRating` is `_level < 10 ? 0 : …` | **no** |

Zeros sort last, so they surface the moment a player has fifty scores but fewer than fifty that
count — and then **the 50th slot is the bar**, which reads `0`, and every projected gain prints as if
it displaced nothing. Confirmed live on the owner's account: the bar was held by a *passed S9*.

The rule is `Rank(s) > 0`, which subsumes all four — and, on Phoenix 2, a fifth: a **passing F**
prices at zero there, so it is excluded by the same test. Nothing legitimate is caught: the worst
grade multiplier that still pays is ×0.4 on Phoenix 1 and ×1.00 on Phoenix 2 (a Single's D), and
`MinimumScore` is 0 in both PUMBILITY configs. `FolderTitleTrack.Compute` already builds its pool this way (`if (value <= 0) continue;`) —
this makes the page agree with the tier list's folder track.

⚠ **It is two places, not one.** `ProjectPhoenix2CarryoverQuery` builds `repriced` and `phoenix1Pool`
with no such filter at all, so an account with fewer than fifty counting Phoenix 1 charts gets a
corrupted carryover bar and singles/doubles split. The same rule, applied twice.

### 3.9 Precision — the page and the card disagreed by 22

Reported by a player 2026-08-09: the session card read **17,195** and this page read **17,173** for
the same pool on the same account. Neither staleness nor the formula. **The two rounded at different
points.** `PlayerRatingSaga` summed fifty doubles and truncated once; this page truncated each of the
fifty and then summed, so it lost the sum of fifty discarded fractions — always low, never high,
around 20–25 for any full pool.

Four more places had independently grown the same defect, and every suite was green throughout:

| where | what it cost |
|---|---|
| `Phoenix2TitleList.BuildProgress` | ladders gated on a truncated pool, so a rung inside the discarded fraction read as unreached here while `/Titles` had already awarded it |
| `PumbilityProjectionSaga` | every projected gain truncated |
| `LeaderboardHubSaga` | each board row truncated before the top-50 sum |
| `PumbilityAttribution` | each per-chart gain rounded to whole **and dropped under a point** — which is also why a fractional gain could never be displayed: it did not survive to reach a badge |

The standing rule that came out of it is in [UX-GUIDELINES §2](../UX-GUIDELINES.md): **nothing below
the presentation layer rounds a PUMBILITY value**, gains go through `PumbilityFormat`, and
`PumbilityPrecisionTests` is the ratchet.

⚠ **Two decimals are this section's, not the site's** (owner, 2026-08-09, reversing his own
one-day-old sitewide rule). Rendering `N2` everywhere was built and then walked back: a pool total
is a five-figure number, and three more glyphs is a real layout cost in a dashboard tile, a board
cell and the session ceremony band. **These three pages** print the total and each chart's
contribution at `N2` — this is where the number is being explained, so the precision earns its
space. Every other surface prints `N0`. The Official Leaderboards pages keep the `N2` they already
had, because they quote piugame's board. The storage rule is untouched: one unrounded double feeds
both. Two traps worth remembering —
`TiedAtBar` compared pool values to the bar with `==`, which on doubles is a coin toss and would have
taken the count silently to zero; and three sites rendered a raw `@value` with no format string at
all, harmless while the value was an int and full-precision noise the moment it stopped being one.

### 3.10 PUMBILITY Targets — the Play page (round five, both mixes since round six)

The owner's framing: *"a page/tab for the PUMBILITY page called 'Your Peers' … breaks down the peer
based suggestions and creates a cross-folder tier list based on how many charts show up in how many
peers … weighted, so a chart in 1st place on a peer scores 50 points … a chart in 50th place is 1
point. It can then show more data on the score variability within peers."* It began as a fourth tab
and ended as the Play page (D32), because a filter for "only charts that pay" over it reproduced the
target list's peer half exactly (§4.9) — at which point two pages were one page with a switch.

**What it is.** Titled **PUMBILITY Targets** (field test round two — "Your peers" named the evidence
rather than what the page is for). Every chart your peers (D22, D43) hold in their top-50 pool of the
type, tiered by prevalence (D33, D34): how many hold it, weighted by how high. Each card says what the peers do on
it — their median grade, how far apart they are (D35), how many hold it — and where you stand: your
score and its percentile among them, whether it is in your pool and at what slot, or that you have
never played it. Under it, who they are (D39). Above it, the chips line — **the dark state only** since field test round two, because a lit type's
count is what the roster prints below (D27/D28) — and, beside the lede, the level bars saying where
your pool sits against theirs (D41).

**Two groupings, one control (D36).** *Prevalence* is the page's own order. *Projected gains* is the
old page: only what pays, biggest first, the carried Phoenix 1 rows interleaved (D29), in bands of
points rather than one paginated list, with each card annotated *"Staple · 17 of 23 peers"* so the
prevalence travels along, and its section prints no per-tier pool count — *"X of Y in your pool"* read
as a claim about the tier rather than about you (field test round one). The `Grouped by` select is the
tier list's own control, capped in width so the density trio stays beside it; the two switches
beside it (Only projected PUMBILITY gains under Prevalence; Project Phoenix 1 scores under Projected
gains) are the page's only filters, and each shows only where it means something.

**Where the gain comes from (D38).** Not recomputed. `PumbilityPageRecord.Targets` is the target
list's own merged, carried-wins, top-100 answer, and that is the badge; with the Phoenix 1 switch off
the badge is `ProjectPumbilityGainsQuery`'s peer-only gain instead. Both already exist. Consequence:
a paying chart past the hundredth wears no badge here either. Under Prevalence with the gains switch
on, the carried rows no peer holds simply do not appear: they have no prevalence to sort on, and the
switch that produced them is not shown under this grouping. ~~they get a **Carried from Phoenix 1**
section of their own, dashed-green as the target list drew them~~ — cut in field test round one. They
are still what Projected gains is for, where §4.9 says why they are no edge case: 28 of the reporter's
74 carried rows are held by nobody.

**The card.** `TierListChartCard`, extended nowhere: jacket, bubble, the gain corner when it pays
(D38), the projected grade in Compact's other corner, the body slot carrying the peers line
(`LetterGradeIcon` for the median · the variability meter and its word · the count), the pool line,
and the tier list's border language with the tier list's precedence — passed, To-Do, carried. Compact
prints no words: its tooltip carries the tier, the count, the weighted sum, the peers' median and
variability, and your state; a top-right dot is the variability (D35). ~~and, under Projected gains, a
top-edge stripe is the prevalence tier~~ — the stripe is **cut** (field test round one): the dot, the
corner and the section already annotate one tile, and a fourth mark made the grid read as a chart. The
legend under the trio names every mark the grid contains, as the target list's did.

**The controls** are left-aligned as one group — the select, then the grouping's own switch — with
the **Download** button and the density trio at the far end (field test rounds two and three). The
download is the tier list's own share card (`GetTierListShareCardQuery`, `TierListShareCard`), fed
from the rendered list's sections and their ramp colours, so the picture cannot disagree with the
screen it was taken from; folded sections are in it, because a fold is a reading convenience rather
than a filter. **A tile is drawn as the Compact tile is** (field test round four): the border is
the state — solid passed, dashed To-Do or carried, dotted broken — the printed corner is the gain,
or what the chart is worth under the pool lens, and a tile carrying a corner value shows the grade
in the other corner instead of plate art. `Tile` grew `CornerLabel`/`CornerHex`/`Outline` for it,
defaulting to the dot it drew before, and **the tier-list download speaks the same language** — it
was the same card model all along.

**Table** — Song · Chart · Peers (`17 of 23`, weighted sum in the title) · Peers' median · Variability
· Gain · My Score · Your pool · the two actions.

**Roster (D39)** — `#` · Player · Level (gem + LV) · PUMBILITY · Singles · Doubles (competitive levels)
· Peer for (All only) · Overlap with you (a bar and the count, of your fifty of that type). Your own
row rides in the sort, unnumbered and highlighted, so the table also answers "where do I sit". Private
peers are counted in every number on the page and named nowhere.

**Dark types (D28)** — a type whose pool is short shows its chip and nothing else; under All that
type's peers, sections and roster rows are simply absent. Nothing here changes what dark means.

**The leaderboard (D40)** — `ChartLeaderboardScopes` gains a *PUMBILITY* peer scope under one *Peers*
chip. The board is the World breakdown filtered to your peers of the chart's type, private peers kept
as Anonymous rows (a peer group is a cohort, not a roster — the same reasoning that keeps them on
Competitive Peers), your standing above it as `#9 of 10 · PUMBILITY peers`.

**Phoenix 1 (D43, round six).** The same page, with the competitive band as the peers: players within
one competitive level of you for the type, which is the band §4.1 projects from and the board the
Peers chip shows on Phoenix 1 — one cohort for the page, the chip and the gain. No full-fifty gate and
no dark state; the pool selector is absent there, so the page is always the All shape — one list, a
peer group per type. The roster drops the gem column (no ladder to read it from) and the lede says
"within one competitive level" instead of "within three levels on the PUMBILITY ladder". The Project
Phoenix 1 switch never shows, there being nothing to carry. The chips line prints the band sizes.

**Your top 50 (D44, round six).** The third lens: the selected pool's fifty by place, split at the bar
into *Your top 50* and *The waiting room*, each card carrying its place and the chart's value, your
grade and score, and the peers line beneath; the table is the old pool board (§3.4) plus the peers'
columns. No switches under it. The data is a Web-side join of the frame's `PumbilityPageRecord.Pool` /
`WaitingRoom` with the peers record's entries by chart id — nothing is read twice.

**The roster cap (D43).** A competitive band is several hundred players. The roster keeps the fifty
rows nearest you in the sort — you in place — with a line above and below saying how many more there
are; under fifty it is the whole roster as before.

## 4. The projection engine

Rebuilt from scratch and measured before anything was written. **§4.1 is the formula on
Phoenix 1; §4.8 is the formula on Phoenix 2** (round three — the two share the estimator's
arithmetic and nothing about who is asked). Everything after §4.1 is the evidence, including
four approaches that were tried and rejected — read §4.3 before re-proposing any of them.

Harness in `Downloads/pumbility-harness/`. Backtest shape: cutoff **T = 2026-01-01**, player
state = scores before T, ground truth = their eventual best on charts they had **not** scored at
T. Cohort competitive levels read as-of from `PlayerHistory`, never from today's `PlayerStats`.

### 4.1 The formula

For a player **P** and a target chart **C** they have not played:

**1 — Who counts as a peer.** Every player whose competitive level *for C's chart type* is within
**±1.0** of P's, and who has a non-broken score on C. A hard gate, ranked by nothing (§4.3).
It is also what keeps the query from scoring P against three thousand people.

**2 — How much each peer's score counts.** For each peer, compare their competitive level *at the
moment they set that score* against their level now:

```
weight = exp( −(level_now − level_when_set) / τ )        τ = 1.0 levels
```

A peer whose level never moved counts at full voice; one who has grown two levels since counts at
about an eighth. **Self-conditioning** — no phase detection, no threshold, no "was this player
improving" classifier (§4.2c).

**3 — The prediction.** The **weighted 65th percentile** of those peers' scores on C.

Not the mean. Per-chart score distributions are left-skewed by a tail of barely-passed attempts,
and a mean aims at the middle of that tail: measured, the mean carries **−8,319** bias against
p65's **+180**. ⚠ **65 is fitted to a one-year truth horizon and must be re-fitted** — see §4.4.

**4 — Scope.** Only charts whose scoring level sits within ±2 of P's competitive level.

That is the whole estimator. There is no per-player percentile, no `×0.95`, no skill adjustment,
no proficiency band, no chart similarity, no neighbour ranking.

**The property to understand before building it:** the prediction depends on P **only through
their competitive level**. Two players at the same competitive level get the same number for the
same chart. That is not an oversight — it is the measured result (§4.3), and it has a copy
consequence: the page may honestly say *"players at your level score about this here"* and may
**not** say *"this one suits you."*

### 4.2 What it is worth

Measured on **competitive levels 19–21: 153 players, 14,240 targets**, identical target set for
every model.

| | coverage | MAE | bias | ρ per player |
|---|---|---|---|---|
| shipping percentile estimator | 58.0% | 13,239 | −3,970 | 0.6137 |
| **this formula** | **99.7%** | **12,506** | **~0** | **0.6607** |

- **Coverage and bias are the transformative parts.** The shipping estimator silently declines
  42% of targets — its depth and overlap gates are unmet — and when it does answer it lands
  ~4,000 points low, systematically. Both defects are gone.
- **ρ +0.047 is the number to sell.** Ranking is what a "what should I grind" list needs, and
  §4.5 shows *nothing* in the old formula's entire parameter space moved ρ at all.
- **MAE −5.5% is real but modest** — 733 points on a million-point scale. Most of the error is
  variance no per-chart model reached, and that ceiling did not move.

**Ingredient ablation** (drop one at a time):

| ingredient | worth |
|---|---|
| Coverage — no depth/overlap gates | 58% → 99.7% of targets answered |
| Quantile over the cohort, replacing percentile interpolation | most of MAE −5.5%, ρ +0.045 |
| Growth weighting (§4.1 step 2) | MAE −1.6% |
| p65 instead of the mean | MAE −17%, bias −8,319 → +180 |
| *Neighbour weighting, any flavour* | *~0.3% — cut, see §4.3* |

#### 4.2c Why growth weighting and not score age

Owner's worry: a player who levelled up leaves a tail of stale scores that misrepresent them.
Confirmed, and it is specific to levellers:

| player | level rise | within-level percentile sd | **score age explains** |
|---|---|---|---|
| DrMurloc (volatile) | **+1.71** | 0.247 | **11.7% / 13.3%** |
| jaime CIFA (stable) | +0.41 | 0.198 | −2.1% / −0.9% |
| Guilherme (stable) | +0.47 | 0.257 | −3.8% / −0.9% |

Clock age predicts nothing for a stable player — their old scores still describe them. It
predicts 12–13% for the leveller. **Same clock age, opposite meaning**, so a global half-life is
wrong: it would discount jaime's 336-day-old scores for no benefit.

Level-delta barely out-predicts clock age head to head (12.8% vs 11.7%) — for a monotone riser
they are collinear. **The reason to prefer it is that it conditions itself.** DrMurloc's median
doubles delta is **+1.71**; jaime's is **+0.13**. The weight switches on for a leveller and off
for a stable player with no detector at all.

⚠ **It also replaces `ScoreAgePolicy` in this estimator, deliberately.** That policy asks whether
a score is an age *outlier within the player's own record*. DrMurloc's ages cluster tightly — IQR
175 days at a median of 610 — so nothing is an outlier and everything keeps full weight, while
his record is simultaneously the stalest of the three. It reads a uniformly-old record as a
coherent snapshot, which is right for a returning player and wrong for a levelled-up one.
`ScoreAgePolicy` stays correct where it is used elsewhere; it is the wrong instrument here.

### 4.3 Four rejected approaches, with evidence

All four tried to personalize the estimate beyond competitive level. **Every one measured ≤0.3%,
and the one that ships today is worse than not personalizing at all.** Do not re-propose without
reading this.

| approach | result |
|---|---|
| **The chabala skill nudge** (ships) | Correlation **0.071** between its adjustment and the residual it exists to correct. Applying it is **worse than not adjusting**: +0.04% MAE against a no-skill baseline |
| **Chart-similarity residual transfer** — your own residual on badge-similar charts, carried onto the target | Worth **0.68pp** of a 2.94pp gain; scoring level and score recency each mattered more |
| **Skill thumbprint matching** — fingerprint players on per-badge deviation, weight peers by similarity | **+0.09%** as quantile weights, **+0.24%** as an additive deviation transfer |
| **Direct score-pattern matching** — weight peers by the correlation of your deviations on commonly-played charts | **+0.07%** MAE. Best ρ of the three (0.6613) and it does zero the bias, but +0.006 ρ |

**Why they all fail, and it is one reason.** The peers' scores on chart C **already encode who
that chart suits** — the people who scored well on it are disproportionately the people it fits.
Re-weighting those observations by an estimate of who resembles P is re-deriving, with more
noise, information the observations already carry. It is redundancy, not absence of signal.

**The signal is real; it is just not additional.** Three separate measurements confirm skills
relate to scores: the thumbprint is a stable trait (split-half **r 0.5–0.7** once computed over a
player's whole record), thumbprint similarity predicts actual score agreement (**+0.28 singles /
+0.48 doubles**), and the most-similar peers agree +0.41 against the least-similar +0.13.
**Reliability is not validity is not marginal value**, and this is a clean case of all three
coming apart.

⚠ **Two traps found while measuring these, worth keeping:**
- **A thumbprint computed inside a narrow window is noise.** Restricted to ±2 scoring levels,
  split-half reliability ran −0.20 to +0.52; over the whole record, +0.5 to +0.7. The first
  measurement nearly killed the idea for the wrong reason.
- **In-sample R² on 33 badge predictors reads 10–15% and holds out at ~0.** Every skill claim
  here is held out on charts the fit never saw.

**The thumbprint is still worth having as data.** It is stable, it is true about a player, and it
is already rendered on the Personalized Breakdown page. If it earns wider placement it is as
something the site **shows** — a profile surface, a rival comparison — never as a predictor. That
is a product decision and must not be justified with prediction numbers.

### 4.4 Two calibrations that must be redone before shipping

1. **The 65th percentile is fitted to a one-year truth horizon.** It is the same species of
   constant as the `×0.95` it replaces, and it moves bias by ~5,000 points across the range
   tested (mean → p75). The page asks *"what would you score if you played this now"*, which
   implies a much shorter horizon. Precedent: the old formula's bias ran **+207 at 30 days**
   against **−2,300 at a year**. Re-fit against whatever horizon the page claims, and state it.
2. **Everything above is measured on competitive levels 19–21.** Confirm at 17–19 and 22–24
   before calling it general.

### 4.5 The old formula's ceiling, for the record

Swept every constant the shipping estimator exposes, 485 players / 87,520 targets. Baseline —
cohort percentile interpolation, no skill adjustment — MAE 10,491, bias −2,847, ρ 0.674.

| lever | result |
|---|---|
| Cohort window ±1.0 | **already optimal** — ±0.5 costs +3.1%, ±2.0 +3.1%, ±3.0 +12.4%. ⚠ Measured on **accuracy of the number**, which is this page's job. A tier list only ranks a folder against itself, so the personalized Score lens asks for ±0.5 — the window the rest of the site means by a competitive peer — and the window is a parameter of `IScoreProjector` rather than a site-wide constant (owner, 2026-08-11) |
| Percentile `×0.95` | moves MAE ±8% and bias by 5,000, but **ρ never leaves 0.680–0.682**. Pure calibration |
| Minimum cohort depth | **selection, not accuracy** — like-for-like on the same targets, a raised gate is *worse* |
| Per-player offset calibration | **+9.1% worse**. Charts you have played are self-selected, so the offset does not transfer |
| Skill adjustment | see §4.3 |

**No parameter moved ranking.** That is why the replacement is a different estimator rather than
a retune, and why ±1.0 survives into §4.1 unchanged — it was the one constant that was already
right.

### 4.6 The 900k floor costs nothing — measured

An earlier draft worried that D2's floor silenced the skill nudge across many targets. **155 of
87,520 targets project below 900,000 — 0.2%.** Moot either way now that the estimator carries no
skill term, but recorded so the concern is not re-raised.

### 4.7 The peer cohort pools Phoenix 1 and Phoenix 2 — ⚠ superseded for Phoenix 2 by §4.8

⚠ **Round three reversed this for Phoenix 2 (D21).** Everything below was measured and true, and it
is kept because it says why the pooling looked right; what it did not foresee is in §4.8: the
Phoenix 2 competitive bands were built from thin, warm-up-heavy pools, so a "peer" drawn by
Phoenix 2 band at face value was routinely a Phoenix 1 24 who had played forty charts. Phoenix 2
now reads Phoenix 2 alone. **Phoenix 1 never read Phoenix 2, and still does not.**

Phoenix 2 has scores from **74 players**. Phoenix 1 has **1,529**. A cohort drawn from the
launch mix alone is too thin to estimate from, and stays thin for as long as it takes the
player base to re-grind — which is most of the window in which the page is useful.

The mixes share their charts: ~4,367 chart IDs appear in both, because Phoenix 2 **rerated**
Phoenix 1's charts rather than restepping them. So the only question is whether a *score* means
the same thing on either side. Measured on the 2,241 player-chart pairs scored in both mixes,
across 62 players:

| | |
|---|---|
| median difference (P2 − P1) | **0** |
| P2 higher / equal / lower | **976 / 271 / 994** |
| p25 → p75 | −4,955 → +6,458 |
| within-player sd | ~16,000 |

Symmetric and centred on zero. A changed scoring formula would show a consistent offset; this
is practice noise. (The −3,989 mean on pairs where the Phoenix 1 score is ≥ 990,000 is
regression to the mean — a near-max score has nowhere to go but down. And the "P2 is lower 85%
of the time" figure from the leaderboard work describes a different population: the elite, who
ground Phoenix 1 for years and have played Phoenix 2 once.)

So for a Phoenix 2 projection the peer side reads **both** mixes and takes each peer's better
attempt per chart, and the cohort is the union of both mixes' competitive-range queries. Level
history comes from Phoenix 1, which is where the series actually runs.

**Only the peer side pools.** The player's own pool, bar, current scores and competitive level
are read from the mix they are looking at and nowhere else — what the page shows them is what
they have done *here*. The one exception is an account with no Phoenix 2 scores at all: it has
no competitive level to match peers on, so the other mix names one rather than the page
projecting nothing. Their own Phoenix 1 scores still reach the page, but as **carryover rows**
(§5), labelled as such — not silently blended into an estimate.

The reference mix runs one way only. A Phoenix 1 projection never reads Phoenix 2: it would add
nothing, and it would make the older page's numbers drift as the newer mix fills up.

### 4.8 Phoenix 2 — PUMBILITY peers (round three, 2026-08-15)

Owner report: the Phoenix 2 list recommended SS on charts he had not played, and no peer he could
see held one. Reproduced exactly against the prod-synced database (BLAZOR S20 → 995,948, Rise Up
D20 → 996,935), then every candidate rule was measured on the same backtest before one was picked.
This section is the formula, the measurement, and the reason.

**The formula, Phoenix 2 only.** For a player **P** and a chart **C** of type **T**:

1. **Who counts as a peer.** Every player within **±3 rungs** of P on the PUMBILITY level ladder
   (`Phoenix2PumbilityLevel.From(total pool)`, 37 rungs, five per gem) who holds a **full 50-chart
   pool of type T** — and P holds one too, or T is dark (D28). One rung, from the total pool, serves
   both types. Read from `PlayerStats.SkillRating`; the peers' pool fullness is the count of their
   Phoenix 2 records of the type in the very read that fetches the evidence, so it needs no column.
2. **The evidence.** Each peer's best non-broken Phoenix 2 score on C. Nothing from Phoenix 1; no
   growth weighting (D25).
3. **The prediction.** The **median** of those scores (D26).
4. **The floor.** Fewer than **five** peers with a score on C → no opinion; the row does not exist
   (D24). There is no level window (D24) — every chart of the type is a candidate, and the bar
   arithmetic of §6.5 removes the ones that cannot pay.

**Why the Phoenix 1 machinery failed here.** Competitive level is the mean Fung score of the top
fifty; a Phoenix 2 pool of forty charts averages the warm-ups. Every launch player's Phoenix 2 level
was therefore deflated — across the dual-mix population by 1.45 levels singles, 2.63 doubles — and
the union cohort admitted by that band at face value. The reporter (Phoenix 2 S 21.40 / D 21.12) was
handed XIUMIN99 (Phoenix 1 24.11, Phoenix 2 22.04) and Tomatonium (24.12 / 21.97) as singles
peers, and GODDISH (Phoenix 1 doubles 25.34) as a doubles peer: BANG BANG D23 → 977,897 from three
of them; INFiNiTE ENERZY S23 → 980,198 from six players at Phoenix 1 22.9–24.1. On the P2 chart
boards those SS holders read as the strong names they are — which is exactly why "no peers with SSs"
was what he saw. His own Phoenix 2 form on the same levels: S22 965k, S23 935k, D22 974k, D23 964k.

**The launch backtest** — every Phoenix 2 player, every chart in their window they actually hold a
Phoenix 2 score on, cohort minus themselves, truth = that score. Self-selected, so it *favours* an
estimator. Shipped (Phoenix 1 machinery): centered overall by offsetting errors — median −988, MAE
12,364, SS calls right 69% — and wrong exactly where the ranking looks: P2-debut charts **+4,952**,
rows carrying ≥50% of their weight from above-band players **+7,771** (SS calls right 58%), fewer
than five peers **+6,382**. Shared charts with deep Phoenix 1 evidence −2,856. The quantile is not
the lever: p50 pushes everything to −6k. Level-based repairs (gate peers on the mature-scale level;
use the Phoenix 1 level while the pool is thin) each traded the over-estimates for under-estimates
without moving accuracy — and were wrong in principle for a returning player, whose Phoenix 1 level
is a frozen peak, not a strength.

| rule (Phoenix 2 alone unless noted) | pairs answered | bias, median | MAE | says SS+ | of which true | >20k high |
|---|---|---|---|---|---|---|
| shipped — Phoenix 1 levels and evidence, p65 | 4,674 | −1.7k | 12,194 | 39% | 71% | 10.0% |
| gem rung ±3 with Phoenix 1 evidence, p65 | 4,323 | **+9.6k** | 15,981 | 82% | 52% | 28.5% |
| gem rung ±3, p65 | 2,018 | +6.0k | 12,607 | 81% | 63% | 20.1% |
| gem rung ±3, p50 | 2,018 | +1.8k | 11,867 | 67% | 66% | 14.3% |
| P2 competitive level ±1, p50 | 1,822 | +0.4k | 11,173 | 60% | 72% | 11.4% |
| **gem rung ±3, full pools both sides, p50, ≥5, no window, no growth** | 2,020 | **−44** | **9,174** | 78% | **80%** | 9.8% |

The last row is D22–D26. Growth weighting on Phoenix 2 discounted 58% of the reporter's singles
evidence below 0.7 (levels climbed three rungs in a month) and turning it off was marginally more
accurate (MAE 9,637 vs 9,930). Removing the level window raised the share of players' own records
the estimator can answer from 21% to 37% and stayed centered. Under the final rule 49 of 92 ranked
players light up for singles and 16 for doubles today — that is the state of Phoenix 2 doubles, not
the rule — and the reporter's singles list reads 23 peers, 86 rows, S18–S21 SSS rows that are simply
correct, and the soft S22 debut charts at "your peers SS'd it".

**Two rules that were argued for and measured out.** The **overlap of the type title track and the
gem group** (both ±3) fixes type-blindness in principle, but the type track inherits the viewer's own
thin pool — a 29-double player's [D] rung is INTERMEDIATE LV.5, so "within 3 titles" is players with
6–12k doubles pools; 71 of 92 ranked players sit there today, and the doubles list blanks. Members
also score no more alike than the gem group alone (per-chart spread 25.6k vs 26.5k, against 21.3k
for a level band). Once per-type pools are real it is the right refinement. **Pool-shape peers**
(±1 on the pool's average level and ±12k on its average score) measured best of everything on the
Phoenix 1-referenced population (MAE 10,631) and separate pass-pushers from scorers — the group the
level-based estimator serves worst (+10.1k, and 20.6% of their Phoenix 2 plays sit above the ±2
window) — but it read each player's shape from a Phoenix 1 pool, i.e. a peak-era shape. Shelved
until it can be re-measured on Phoenix 2 pools.

**What Phoenix 2 alone cannot see, stated.** A rusty 24 whose Phoenix 2 stats read 21–22 is invisible
to any Phoenix 2 rule; the ladder does better than competitive level here — XIUMIN99's re-grinding
already carried him to RED BERYL LV.3, out of a DIAMOND LV.4 band — but the total ladder still admits
a singles-carried doubles-25 to a doubles band. Those rows are no longer fabricated: *"9 of your 23
peers played this; the median is 982k"* is a fact the P2 board confirms, and it self-corrects as the
strong player's rung leaves yours. That is the residual, and it was accepted with eyes open.

**Naming.** The projection's peers and the tier list's PUMBILITY lens are one definition (D23) —
[pumbility-tier-list.md §5](pumbility-tier-list.md). "Cohort" is not used for either.

### 4.9 Round five — what the peers' pools look like, measured

Every number here is from the prod-synced snapshot on 2026-08-18, the reporter's account (DIAMOND
LV.4, band rungs 21–27: 23 singles peers, 7 doubles, his doubles pool dark at 29/50), through
`ScoreTracker.ExplorationTests/Pumbility/PumbilityPeerPoolProbeTests` — the same peer definition,
the viewer excluded, pools rebuilt with the writer's own rule.

**Prevalence is not level-sorted inside a band.** 79% of the singles Borda mass sits at S20/S21/S22
(26 / 26 / 27%) and the top fifty spans S19–S23 with the levels interleaved: 404 (New Era) S21 is #1
(17 of 23 hold it, 550 points), We Love Your Step S20 #10 above most S22s. The tier-list doc's worry
that higher levels crowd every pool ([pumbility-tier-list.md §2](pumbility-tier-list.md)) is real
across the population and moot inside a ±3-rung band.

**The weighting barely reorders, and that is fine.** Top-50 overlap: Borda ∩ plain hold count 39/50,
Borda ∩ raw value sum 39/50, count ∩ value 50/50 (doubles: 46 and 38). Borda earns its place on a
different property — every peer casts the same 1,275 points, so a RED BERYL 2's #50 counts one and a
DIAMOND 1's #1 counts fifty, and the strong tail cannot dominate the list the way a value sum lets it.

**The overlay is the story.** 9 of his 50 pool charts are in the band's Borda top 50; 21 are held by
at most one peer and 10 by nobody (Paradoxx S26 at his #3, Achluoias S24, Neo Catharsis S25, HTTP,
Vacuum …). Same levels — 35 of his 50 are S20/S21 too — different songs. The 41 consensus charts he
does not hold are all unplayed in Phoenix 2.

**Variability bands.** Over the 120 singles charts five or more peers scored, IQR width runs 455 →
44,771 points, median 12,235. Raw widths skew +1.10 and a ±0.5σ/±1.5σ cut leaves the bottom band
empty; `log(1 + w/1000)` skews −0.25 and gives 7 / 34 / 36 / 34 / 9. Solve My Hurt S21 (4k) reads
very consistent, Crash-Landing Rendezvous S19 (5k) consistent, 404 S21 (12k) mixed, OVERNIGHT FLOWER
S22 (23k) split, Freedom Dive S22 (43k) very split — which is what the owner's own field-test words
described (*"if P25 is A+ and P75 is SSS+, you know it's a specialized chart"*).

**The merge question, answered before it was decided.** From the peers' evidence 86 singles charts
clear his bar (peers' median priced as the projection prices it, against his singles bar of 337.18)
and **all 86 are held by at least one peer** — a "gains only" cut of the prevalence list is the
target list's peer half exactly. What the shipping target list actually held for him: **100 rows =
26 peer-projected + 74 carried Phoenix 1** (doubles: 100 of 100 carried), the carried rows winning
where both exist; **28 of the 74 carried rows are held by no peer**. That is why the merged page needs
a Carried section under Prevalence (§3.10) and why the Projected-gains grouping keeps the carried rows
interleaved: it reproduces the shipping order row for row (OVERNIGHT FLOWER +26.9, QUATTUORUX +26.9,
Cleaner, Cross Over, Aragami +24.9). Under All the same charts re-base against the merged bar (345.94)
and read +18 / +18 / +16.

## 5. Phoenix 2 carryover — the Phoenix 1 page

Its own route since round two (`/Pumbility/Phoenix1`, D14), and only on the Phoenix 2 view; Phoenix 1
has one pool and no per-type board, so offering a split there would invent a stat.

Every Phoenix 1 score repriced under `Phoenix2PumbilityScoring` — singles priced one level up the
base curve, sub-10 charts at zero, broken plays at zero — then the top 50 taken for each of All,
Singles and Doubles.

⚠ **Repriced means the Phoenix 2 level, not the level the score was set against.** Phoenix 2
*rerated* the charts it inherited rather than restepping them: 338 of the 4,367 shared charts carry
a different level here — 302 up, 36 down — so the same chart id resolves to a different `Chart` in
each catalog and only the price moves, never the steps. Reading the Phoenix 1 level pays a
downrated chart a base the mix has taken away from it, and short-changes the 302 uprates by the
same arithmetic (found by the owner 2026-08-08 on Spooky Macaron S23 → S22, suggested at +372 where
the chart is worth +365 — over the bar on a rating it no longer has). A chart with **no** Phoenix 2
row has no Phoenix 2 level to read, so it keeps its own; it still counts toward the pool, and it can
never become a target. `Phoenix2ProjectionCalculator` (the recap) always did this correctly and is
the reference. The peer side handles the same fact separately — see `ReferenceLevelSlack` in §4.

**The panel's fifty is the definition; the suggestions are not bound by it** (owner, 2026-08-06).
PUMBILITY *is* the top fifty, so `Entries` and every figure in the table below come from exactly
that. But capping *suggestions* at the pool hid the rows carrying the best evidence the site has:
against a thin Phoenix 2 pool a repriced **#73** clears the bar as surely as a #3 does, and it is
still a score the player has actually hit. So the repricing is kept to `CandidateDepth` (200) and
split — `Entries` is the pool, `Candidates` is what ranks behind it, each carrying its real place
so a row can say it was your #73. This costs nothing: every score was already being repriced
before the `Take(50)`.

Under 50 Phoenix 2 scores the bar is **zero**, so every candidate qualifies and the 100-target cap
is what actually limits the list. That is the launch case and it is correct — *"50 scores takes 3
play sessions."*

**The finding this section exists for.** On the owner's account the Phoenix 1 top 50 is **46
Doubles / 4 Singles**. The same fifty scores under Phoenix 2's rules are **18 Doubles / 32
Singles**. The singles-level-up rule inverts which charts are worth grinding, and no other
surface on the site can tell a player that.

Supporting facts the section renders, all real:

| Fact | Value |
|---|---|
| Repriced pools | All 18,041.16 · Singles 17,969.21 · Doubles 17,863.80 |
| Bars | 358.08 · 356.26 · 354.00 |
| Not yet scored in Phoenix 2 | 49 of 50 |
| No Phoenix 2 chart at all | 1 — Uh-Heung S22, the account's best |
| Re-played charts scoring lower in P2 | 85% — 2,803 of 3,313 pairs scored on both mixes (owner's 2026-08-01 board recon) |

**A chart with no Phoenix 2 appearance never appears in the target list** — you cannot go and play
it. ⚠ It is no longer *stated* either: the "No Phoenix 2 chart" fact tile was **cut** in round two
(owner: it is not actionable information, and it reads as a problem). `Phoenix2CarryoverRecord.Unavailable`
goes with it — target filtering uses the per-entry `AvailableInPhoenix2` flag, not that list.

**The titles this record would land you** close the panel: three chips, one per ladder, computed from
the three repriced pools the handler already has in memory. On the owner's account that is
`[P.B] RED BERYL` · `[S] EXPERT LV.3` · `[D] ADVANCED LV.10`.

⚠ **They must never read as titles held**, which is §8.2 with teeth: the wording is *"where this
record **would** land you"* and every chip carries its underlying pool value. At a mix launch these
chips and the §3.7 rails say opposite things about the same three ladders — you hold nothing yet,
and your Phoenix 1 record is worth RED BERYL — and that contrast is the panel's whole argument, so
the two surfaces share a chip language deliberately and differ only in that one word.

**A chart already scored here is still a target** (D11). Carryover used to admit only charts with
no Phoenix 2 score at all, which dropped 985k-there-against-900k-here — a real gain, resting on
the best evidence the page has — for the sole reason that the chart had been touched. It now asks
the projection's question with the projection's floor: `Phoenix2Value − max(what you already get
from the chart, the bar)`, kept when positive. A stage break here contributes nothing and so reads
as unscored, which was the compounding half of the same bug: a chart the player broke on was
excluded for having been "scored" while adding nothing to the pool.

Note what this does *not* change: `Entries`, `ScoredHere`, `NotYetScored` and every figure in the
table above are still the pool's fifty. The repricing is the same arithmetic it always was — only
which rows become suggestions moved.

## 6. Technical scope

Round two. The first round's scope has shipped and is not reproduced here; what it decided that
still binds lives in §2, §4 and §6.5.

### 6.1 Verticals and layers

**No new table, no new port, no new package, no migration.** Two assemblies move, plus Web.

| Vertical / layer | Change |
|---|---|
| **SharedKernel** | `ScoringConfiguration` gains **`Decompose`** and **`PlateHeadroom`** — the §3.6 split and its ceiling. They belong with the formula because that is the only way they cannot drift from it: the Phoenix 2 grade table still carries unverified TODOs at B and below, and a decomposition written anywhere else would go on answering with the old shape, silently and plausibly |
| **Domain** | Nothing. `Phoenix2TitleList` / `Phoenix2PumbilityTitle` are read exactly as they stand |
| **PlayerProgress** | Owns the rest. `PumbilityPageRecord` gains the breakdown, the three pool totals and the title rails; `Phoenix2CarryoverRecord` gains three projected titles and loses `Unavailable`. Two one-line fixes for §3.8 |
| **ScoreLedger · ChartIntelligence · Catalog · Randomizer** | Nothing |
| **Data** | Nothing — no schema change, no new repository, no migration |
| **Web** | The frame, three routes, two new components, the CSS, the deletions |

**No new ports and no new cross-vertical reads.** Everything flows through contracts PlayerProgress
already owns, over reads it already performs.

### 6.2 Classes

**SharedKernel** — `ScoringConfiguration.Decompose(chart, score, plate, isBroken)` →
`ScoreContribution(Base, FromGrade, FromPlate)`, and `PlateHeadroom(chart, score, plate)`. Both pure.
Both formulas decompose exactly, and `Base + FromGrade + FromPlate == GetScore(…)` is the invariant:

- `Default` (Phoenix 1): base = scoreless, grade = scoreless × (g − 1), plate = scoreless × g ×
  (p − 1) — **identically zero**, because every Phoenix 1 plate modifier is 1.0.
- `GradePlusPlate` (Phoenix 2): base = scoreless′ (singles bump and sub-10 zeroing already applied),
  grade = scoreless′ × (g − 1), plate = scoreless′ × p.

`PlateHeadroom` asks the config for the best-plate score and subtracts the held one, so it needs no
knowledge of whether plates multiply or add — the one thing that differs between the two mixes.

**PlayerProgress**
`Contracts/`: `PumbilityPageRecord` gains `PoolBreakdown`, `PoolTotals` and `IReadOnlyList<TitleRail>`;
`Phoenix2CarryoverRecord` gains `ProjectedTitles` and drops `Unavailable`.
`Application/PumbilityPageSaga`: builds the breakdown and the rails, and computes **all three pool
totals in one pass**. The page currently fills the selector with two extra `GetPumbilityPageQuery`
dispatches (the third short-circuits on the pool already loaded) — folding them in is strictly fewer
round trips and feeds the rails for free. It is not the split D19 declined; it is the opposite.
`Application/PlayerRatingSaga`: `Rank(s) > 0` in `GetTop50ForPlayerQuery` (§3.8), and the same rule
on `repriced` and `phoenix1Pool` in `ProjectPhoenix2CarryoverQuery`.

**Web** — `Pages/Progress/`: `PumbilitySectionFrame.razor` (a component, so **no** `@rendermode`),
then `Pumbility.razor` `/Pumbility`, `PumbilityPool.razor` `/Pumbility/Pool` and
`PumbilityPhoenix1.razor` `/Pumbility/Phoenix1`, each declaring
`@rendermode RenderModes.Interactive` (`RenderModeDeclarationTests`). Flat rather than a
`Pumbility/` subfolder, and prefixed rather than named `Play`/`Pool`/`Phoenix1`: components resolve
by name inside a namespace, and three generic ones in `Pages.Progress` is a collision waiting to
happen.
`Components/Pumbility/`: new `PumbilityBreakdown` and `PumbilityTitleRails`; `PumbilityHero` becomes
the frame's horizontal band; `PoolCurve`, `PoolBoard`, `TargetList`, `CarryoverPanel` move unchanged.
New `CarryoverSection` wraps the last of those with its own read — the pool it is scoped to arrives
from the frame, so it is a **parameter**, which is not something the page can dispatch on in
`OnInitializedAsync`. Re-fetching on a pool change falls out of that for free.
`site.css`: the `pmb-*` block gains frame, breakdown and rail classes — `var(--mix-*)` only, no
literals (`UiColorTokenTests`).
UiSettings: **`Pumbility__Pool`** new. **`Density__Pumbility` unchanged** — the convention is per
page, but renaming it silently resets the preference for everyone who holds one, and Play is the only
tab with a toggle.

**Deleted** — the what-if fold with its `Recalculate` and `_whatIf*` state; both
`<details class="pmb-fold">` wrappers; `CarryoverPanel`'s unavailable tile and `LostName`;
`Phoenix2CarryoverRecord.Unavailable`; the `.pmb-whatif*`, `.pmb-field` and `.pmb-fact-warn` CSS.

### 6.3 Tests

| Suite | What |
|---|---|
| `ScoreTracker.Tests/DomainTests` | `ScoringConfigurationTests` — the decomposition sums to `GetScore` on **both** formulas across levels, grades and plates; `FromPlate` is exactly 0 for every Phoenix 1 plate; `PlateHeadroom` is 0 on Phoenix 1 and equals the RG→PG span on Phoenix 2; the ask (`threshold / 50`) and the rung lookup against the real ladders |
| `ScoreTracker.Tests/ApplicationTests` | `PumbilityPageSagaTests` — a sub-10 chart, a half-double and a broken run each **never** enter the pool and never set the bar, on both mixes; the carryover's two pools exclude them too; the three pool totals; each rail's held rung, next rung and ask; the top-of-ladder and thin-pool rails |
| `ScoreTracker.Tests.Components` | The breakdown draws its plate segment true to scale; a plateless mix says so rather than drawing an empty rail; an empty pool renders no band; the carryover chips say "would" and skip a ladder not yet reached; the unplayable tile is gone |
| `ScoreTracker.Tests.Integration` | Nothing new — the change is saga logic over reads that already have coverage |
| `ScoreTracker.Tests.E2E` | Nothing new. Not a critical whole-workflow path (owner's granularity ladder) |

⚠ **The frame itself has no component test.** Nav-before-data, the Phoenix 1 tab and selector coming
out, and the redirect behind them are all page-level behaviour needing `IMediator`,
`IUiSettingsAccessor` and `ICurrentUserAccessor` mocked together, and no page in this section has
that harness yet. The mix-dependent half is covered one layer down — the saga returns no rails and no
`PoolTotals` on Phoenix — but the rendering half is currently owner field-testing, not a ratchet.

### 6.4 Build order

| # | Commit | Contents |
|---|---|---|
| R1 | **The zero-value fix, alone** | `Rank(s) > 0` in `GetTop50ForPlayerQuery`, and the same rule on the carryover's two pools (§3.8). No UI — it fixes the page as it stands today, and it can ship on its own |
| R2 | The frame and the routes | `PumbilitySectionFrame` + three pages; `Pumbility__Pool`; the top 50 out of its fold; the what-if deleted; Phoenix 1 dropping its third tab and its selector. Existing content moved, nothing new |
| R3 | The decomposition | `ScoringConfiguration.Decompose` / `PlateHeadroom` + `DomainTests` |
| R4 | Where your PUMBILITY comes from | `PumbilityBreakdown` + the record fields (§3.6) |
| R5 | Your PUMBILITY titles | `PumbilityTitleRails` + the rails and pool totals (§3.7) |
| R6 | Phoenix 1 | The three chips in, the unplayable tile and `Unavailable` out (§5) |
| R7 | l10n | ~30 keys × 9 locales, alphabetically inserted, no case variants |
| R8 | Docs | This section, `ARCHITECTURE.md`'s Progress row |

R1 is deliberately first and deliberately alone: it is a live bug, it needs no UI, and every figure
in R5 is computed off the pool — an average over a pool holding a passed S9 is wrong by construction.

### 6.5 How a projection is held

The cached artifact used to be the whole `PumbilityProjection`, keyed by `(user, mix, pool)`.
That bundled four things with nothing in common:

| | Cost | Moves when |
|---|---|---|
| the cohort sweep | seconds, sized by the player population | peers play |
| the gains | arithmetic over the viewer's own top hundred | **the viewer plays** |
| the top-hundred cut | derived from those gains | as above |
| the Pass Count tier list | one read, **identical for every player in the mix** | the nightly job runs |

The consequence was a per-pool key, so Phoenix 2's three selector positions each paid for their
own sweep, and the same tier list was copied into every player's entry — about five-sixths of
the bytes, with most of the rest being the evidence D12 removed.

**Only the sweep is cached now, and it is pool-free.** Which pool you are looking at changes the
bar an estimate is measured against, never the estimate, so all three positions share one sweep.
Everything else is priced on each visit, from reads the page was already doing. The public
contract did not change, so nothing downstream moved.

Three properties that had to be got right, none of them obvious:

- **The task is cached, not the result.** The dashboard's suggestion widget and the page ask for
  the same sweep seconds apart — the design, not an edge case — and caching the result lets the
  second arrival start a second sweep while the first is still running.
- **A failure is never cached**, in either ordering. A sweep that fails before its first real
  await is already a faulted task when control returns, so its own cleanup has run before the
  store could happen; one that fails later has to clean up after. Handling only one of the two
  leaves a stored failure that outlives its cause by the whole lifetime.
- **The cache owns a bounded instance of its own.** Setting a `SizeLimit` on the app-wide
  `IMemoryCache` would throw for every other caller in the solution that omits an entry size.

Held for 24 hours, and dropped when the viewer's own scores move. Peers' play does not evict —
a sweep a few hours behind on other people is indistinguishable from one that is not, and
watching every import would evict continuously and cache nothing.

⚠ **The scoping prefilter now uses the most permissive bar of the two per-type pools**, because
one estimate set has to serve all three selector positions. A merged top fifty is drawn from a
superset of either single type's, so it never sits below both.

**This is why splitting the page into three did not have to split the query** (D19). The cache keys
on `(userId, mix)` and is pool-free, so all three pages share one entry: still one sweep per player
per mix per day, the same wait the single page had. What the section changes is only *which* page can
be the one that waits — land on the Breakdown page first and you sit behind the `PatienceCard` for a result
that page never renders. An inelegance, not a regression, and the reason the card belongs to the
frame rather than to Play.

### 6.6 Round three — PUMBILITY peers

**No new table, no new package. One new port read, one rename migration, and a deletion.**

| Vertical / layer | Change |
|---|---|
| **Domain** | `PeerEstimator` (was `CohortEstimator`): a `minimumPeers` floor (default 1 — Phoenix 1 unchanged) and `Phoenix2Quantile = 0.50` beside `Quantile = 0.65`. `ScoreProjector` gains the Phoenix 2 branch of §4.8 and **loses** the cross-mix plumbing — `ReferenceMixFor`, `BestAcrossMixes`, the union cohort, the reference-side stats and history reads, `ReferenceLevelSlack` — which nothing uses once Phoenix 2 stops. `ScoreProjection` carries a `PeerGroup` (a competitive band or a rung band, its bounds, count, and the viewer's pool count against 50) so a surface can name the group without knowing the mix. `IPlayerStatsReader.GetPlayersByPumbilityRange(mix, min, maxExclusive)` — the only new port read; `IScoreReader` already answers "every Phoenix 2 record of the type" (`GetPlayerScoresInLevelRange` at 10..Max) and that one read yields both the evidence and each peer's pool fullness |
| **PlayerProgress** | `EFPlayerStatsRepository`: the range read on `SkillRating`. `PumbilityProjectionSaga`: no window and no `CompetitiveLevel` call on Phoenix 2; a type dark while its pool is short of fifty; the per-type peer summary on `PumbilityProjection` and through `GetPumbilityPageQuery`. `PumbilityPageSaga` passes it through; `CarryoverTargets` untouched (D29). The projection cache is unchanged and gets cheaper |
| **ChartIntelligence** | The lens and the projection share the definition (D23): `PumbilityPeers` (was `PumbilityCohortKeys`); `TierListSaga` writes one list per **viewer rung** — members are the players within ±3 with a full pool of the type, rung from `SkillRating` — which still materializes (37 keys per type per mix at most) because every viewer at rung *r* reads the same list; the reader gates on a full pool of the **type** and resolves `R{rung}`. `PersonalizedTierListBreakdown` carries the peer group so the breakdown page stops printing a competitive band on Phoenix 2. Phoenix 1 keeps `L{n}` |
| **Data** | Migration `PumbilityTierListPeerColumns`: `CohortKey` → `PeerKey`, `CohortSize` → `PeerCount` on `scores.PumbilityTierListEntry`. Rename only |
| **Web** | `Pumbility.razor`: the peer line, the dark type, the five-peer clause (§3.3). `PersonalizedBreakdown.razor`: the peer block by kind. Nine locales |
| **ExplorationTests** | `Pumbility/`: the launch backtest and the reporter's-list reproduction, config-gated on the catalog connection, with one pin fact asserting `PeerEstimator` and the harness agree — the ratchet §9 asked for |

Not in this round: the other "cohort" family — `CohortScoreProvider`, `FolderCohortStats*`, the score-quality percentiles — a different feature, its own sweep.

**Post-deploy:** press *Rebuild Phoenix 2 PUMBILITY tier lists* once, or wait for the nightly — Phoenix 2 rows re-key from title names to rungs; until then the lens is dark and falls to Pass as it does for any thin peer group. Phoenix 1 rows are unaffected. The projection cache is in-process and clears with the deploy.

### 6.7 Round five — Your PUMBILITY peers

**No new table, no new port, no new package, no migration, no job.** The peers' pools come out of the
read the Phoenix 2 projector already does; everything else is arithmetic over it and reads the site
already performs.

| Vertical / layer | Change |
|---|---|
| **Domain** | `ScoreProjectionRequest` gains an optional `Charts` lookup; when it is present the Phoenix 2 branch of `ScoreProjector` prices every band record and fills `ScoreProjection.PeerPools` — a `PeerPoolSummary` (the peer ids, each peer's top-50 set, and per chart the holders, the weighted sum, how many scored it and their median and quartiles where five or more did). Pure `PumbilityPeerPools.Build` does the arithmetic; pure `PeerVariability` bands quartile widths into the five levels. The tier list's Score-lens caller passes no charts and pays nothing; Phoenix 1 never fills it. Prevalence tiers use `TierListProcessor.ProcessIntoLogScaledTierList` as they stand |
| **PlayerProgress** | `ProjectionSweep` carries the pools per type; the sweep passes charts on Phoenix 2. Two new contracts on `PumbilityProjectionSaga`, which owns the cache: `GetPumbilityPeersPageQuery(UserId, Mix, Pool)` → `PumbilityPeersPageRecord` (per type the group; the prevalence entries with tier, order, count, weighted sum, scored, median, quartiles, variability, and the viewer's slot / score / percentile; the yours-alone list; the roster from `IUserReader` + `IPlayerStatsReader` with overlap and the private count; the compare figures) and `GetPumbilityPeersQuery(Mix, ChartType)` → the peer ids, for the leaderboard chip. Gains are **not** computed here (D38). `PumbilityPageSaga`, `CarryoverTargets`, `TargetList`'s data path and Phoenix 1's projector are untouched. **A judgment call, flagged:** the tier-list family rule puts tier-list *calculations* in `TierListSaga`; this is a PUMBILITY-page product banded with the shared Domain processor and never stored, so it stays in PlayerProgress |
| **Data · Catalog · ScoreLedger · ChartIntelligence** | Nothing |
| **Web** | `Pumbility.razor` branches on the mix: Phoenix 2 renders the peers block, Phoenix 1 keeps `TargetList`. `Components/Pumbility/`: `PeerPoolList` (sections + the three densities over `TierSection` and `TierListChartCard`), `VariabilityMeter`, `PeerRoster`, `PeerPoolLegend`, `PeerCompareStrip`. `MixThemes` emits a mix-invariant `--vary-1..5` group with a `ThemeScales.VariabilityColor` accessor ([UX-GUIDELINES.md](../UX-GUIDELINES.md)). `ChartLeaderboardScopes`: the Peers chip, the sub-row, the `PumbilityPeers` scope; `ChartDetailsDialog.BoardScope` carries the sub-scope. UiSettings: `Density__Pumbility` kept, `Pumbility__GroupBy`, `Pumbility__ProjectPhoenix1`, `Pumbility__GainsOnly`, `Pumbility__CollapsedTiers` new. Nine locales |
| **ExplorationTests** | `Pumbility/PumbilityPeerPoolProbeTests` — the §4.9 measurement and the JSON export the mock is fed from |

**Tests.** `DomainTests`: `PumbilityPeerPoolsTests` (points, holders, the viewer excluded, the
five-peer gate, quartiles, per-peer sets), `PeerVariabilityTests` (log bands, cut points, under five →
none); `ScoreProjectorTests` (pools with `Charts`, none without, never on Phoenix 1).
`ApplicationTests`: `PumbilityPeersPageTests` (tiers through the processor, yours-alone, roster,
private count, overlap, the ids from a warm and a cold cache). `Tests.Components`: `PeerPoolListTests`
(grouping → tiers vs bands, ring precedence, gain-only jacket, dot Compact-only, table columns),
`PeerRosterTests`, `VariabilityMeterTests`, `PumbilityComponentTests` (the Phoenix 2 branch, the
switch visibility rules, Phoenix 1 unchanged), `ChartLeaderboardScopesTests` (the chip, the sub-row,
the default, the host-passed sub-scope, Anonymous rows kept). Ratchets that bite: `PumbilityPrecisionTests`,
`UiColorTokenTests`, `RenderModeDeclarationTests`, `MessageTaxonomyTests`, the resx tests. Nothing in
Integration or E2E — nothing new below the port line, and this is component-level UI by the ladder.

**Build order** — docs first, i18n last, pushed per commit: (1) this section and D32–D42; (2) Domain;
(3) PlayerProgress; (4) the theme tokens; (5) the components; (6) the page swap; (7) the compare strip,
alone so it can be cut; (8) the leaderboard chip; (9) the probe; (10) nine locales.

**Post-deploy:** nothing. The sweep cache is in-process; the first Phoenix 2 visit after the deploy
builds the pools with the sweep it was going to run anyway.

### 6.8 Round six — Phoenix 1 gets the page; Your top 50; the Breakdown page

**Still no new table, port, package, migration or job.**

| Vertical / layer | Change |
|---|---|
| **Domain** | The competitive branch of `ScoreProjector` does what the Phoenix 2 branch did in round five: when the request carries `Charts`, the band's records are read from the pool floor rather than the targets' level band, `PumbilityPeerPools.Build` prices them with Phoenix 1 scoring, and `ScoreProjection.PeerPools` is filled. The estimate still runs over the targets only and the history is still read for the voices that turned up on them, so the projection is unchanged to the score. The group's size becomes the band's size (the page prints "17 of N peers" from it and the summary's peer ids are the band) |
| **PlayerProgress** | The sweep passes the catalog on both mixes; the three `Phoenix2` gates in `PumbilityProjectionSaga` come out; `PeerRosterEntry.RungIndex` is nullable and null where there is no ladder |
| **Web** | `Pumbility.razor` loses its branch; `TargetList` and `CornerLegend` are deleted with their tests. `PeersSection`: the title is *Your peers*, the ledes branch on the group's kind, the third lens. `PeerPoolList`: the Your top 50 model — rows from the frame's pool and waiting room, the peers' entry joined by chart id. `PeerRoster`: no gem column without a rung; the fifty-around-you window. `PumbilityPeerLine` prints a competitive band's sizes. `PumbilityPool.razor` → `PumbilityBreakdown.razor` at `/Pumbility/Breakdown` (the old route kept as a second `@page`), the board section gone, `PoolBoard` deleted. The frame's second chip reads *PUMBILITY Breakdown* |

**Tests.** `ScoreProjectorTests` (Phoenix 1 pools with `Charts`, none without, the estimate unchanged
by the wider read); `PumbilityProjectionSagaPeersTests` (Phoenix 1 answered, the null rung);
`Tests.Components`: `PeerPoolListTests` (the lens: the bar split, place and value, the peers line
on a pool row, a waiting-room row), `PeerRosterTests` (the window, no gem column), `PeersSectionTests`
(Phoenix 1 renders the page, the lede, no Phoenix 1 switch), `PumbilityPeerLineTests` (a band prints
its sizes), `PumbilityComponentTests` (the target-list cases deleted, the board case deleted).

**Build order** — docs first, i18n last, pushed per commit, on PR #285: (1) this section and D43–D45;
(2) Domain; (3) PlayerProgress; (4) the page on both mixes; (5) the Your top 50 lens; (6) the
Breakdown page; (7) nine locales.

## 7. Responsive

The class ladder in [UX-GUIDELINES.md §1](../UX-GUIDELINES.md), no new numbers:

| Class | Rung | What this page drops |
|---|---|---|
| Desktop | ≥ 900 | — |
| Tablet | 700–900 | the Why chips |
| Fold | 500–700 | the song name — jacket + bubble identify the chart |
| Mobile | < 500 | the score digits, leaving grade art |

Plus `max-height: 520px` for the landscape phone, where the hero **compresses rather than
stacking** — height is the scarce axis and no width rule can say so.

The hero goes single-column at **860**, deliberately in the gap between the real tablets (820,
834) and the 900 rung so a scrollbar cannot flip the same device by platform.

⚠ **The frame wraps rather than reflowing, so everything in it starts at one left edge** (D15). The
bar card was drawn pushed right with `margin-left:auto`, which reads fine on one line and strands it
against the far edge with nothing beneath it the moment the row breaks. Left-aligned it simply stacks
under the number. The nav row underneath fills the air that costs at desktop width.

## 8. Honesty boundaries

1. **A projection is a projection.** The targets list says what you are *projected* to reach, and
   the page must not print it in the same register as a score you actually hold.
2. **The carryover is not your Phoenix 2 PUMBILITY.** It is what your Phoenix 1 record would be
   worth here. The hero says so, and the count of scores you actually have in Phoenix 2 sits
   beside it.
3. **The bar moves under you.** Every gain figure is against the bar as it stands now; clearing
   one target raises it for all the others. The page should not imply the gains sum.
   **Resolved by the ask** (§3.7), not by a caveat: the reason a title cannot be expressed as a
   number of charts is exactly that the gains do not sum, so it is expressed as a per-chart value
   instead — which is order-free and therefore true however you play. D13 is this boundary enforced
   rather than annotated.
4. **A title you hold is not a projection.** The rails on the Breakdown page state what you have and what the
   next rung asks. The chips on Phoenix 1 state where a record *would* land you. The one word
   between them is the whole difference, and it is why D17 refuses to spend "projected" twice.

## 9. Open

- **The page's truth horizon — answered for Phoenix 2, open for Phoenix 1.** Bias is strongly
  horizon-dependent (the old formula ran +207 at 30 days against −2,300 at a year), and §4.1's `p65`
  is fitted at **one year**. On Phoenix 2 the question was measured (§4.8): against players' actual
  Phoenix 2 scores the shipped `p65` was centered only by offsetting errors, and on Phoenix 2 evidence
  it read +6k; the median is the Phoenix 2 constant (D26). Phoenix 1 keeps `p65` and the open question
  with it. `PeerEstimator.Quantile` / `Phoenix2Quantile` are two constants in one pure class.
- **The harness is ported** (round three): `ScoreTracker.ExplorationTests/Pumbility/`, config-gated
  like the catalog probes, with the pin fact asserting the real estimator and the harness agree. The
  original Phoenix 1 backtest (§4.2, levels 19–21) is still not in it — that harness lived in
  `Downloads/pumbility-harness/` and its numbers are recorded here only. Metrics ρ ahead of MAE: §4.5
  is the cautionary tale, where every constant moved MAE and none moved ρ.
- **Two data limits, stated not fixed.** `PlayerHistory` begins **2024-06-04**, so a score older than
  that resolves to the player's earliest known level and the growth weight under-states how much they
  improved on the site's oldest records. And `PhoenixRecord` stores only the current best, so a chart
  improved after the backtest cutoff reads as unplayed at it; the journal's 29,153 multi-event pairs
  are the subset with true history.
- **The "why" line on a target row.** §3.3. The estimator carries no skill term, so badge chips would
  assert a causal path that does not exist. Evidence line instead, or nothing? The answer became a
  **section** chip for the count (D27) and a **column** for the spread — the Peers IQR, two grades
  (D30) — with the row's why-line still carrying only the source (D12). ~~What is left open is only
  whether Phoenix 1 gets a peer-count chip too; its rows carry the IQR already.~~ Answered in round
  six: Phoenix 1 gets the whole page, chips included (D43).
- **Does the page show the thumbprint as descriptive data?** It is a real, stable trait (§4.3)
  with genuine display value and zero predictive value. If yes it needs its own placement and
  copy, and must never sit adjacent to a projection where it reads as the explanation.
- **A Singles/Doubles filter on Phoenix 1 targets** was raised and not decided. It is additive
  and does not invent a stat, unlike a Phoenix 1 pool split.
- **Is ρ ≈ 0.66 good enough to print a point estimate?** Nothing tried moved it far, and the
  ceiling looks structural rather than tunable. Round four's answer is *both*: the point estimate
  stays, and the Peers IQR beside it (D30) prints the range as grades — so a reader can see when
  the point is standing on agreement and when it is a median of a split field.
- **What the tier list's Skill source is worth**, which this harness cannot measure — its output
  is a difficulty ordering with no equivalent ground truth. The 20% degradation figure for
  suggestions comes from separate work and is not reproduced here.
- **N5 (the badge re-key) is now unblocked from this page** and should ship on its own schedule
  for the tier-list blend. It is no longer a dependency of anything here.
- **Round five, awaiting the field test (D41, D42).** Whether the compare strip and the Yours-alone
  section survive; whether the front page should open on the gains switch or on the whole prevalence
  list; whether the gain bands stay fixed points or go σ-relative; and whether the target list's
  hundred-row cap, which the badge inherits (D38), should lift now that the page has no pagination.
- **Two peer pools in one chip (D40)** is the first place the [peers abstraction](peers-abstraction.md)
  shows through the UI. When Peers becomes a vertical, the sub-row is where "which pool am I measured
  against" would surface — nothing here anticipates that beyond keeping the two scopes as one enum.
