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
round 2 https://claude.ai/code/artifact/2196691e-b756-458c-b84c-229061046745 ·
round 9 (the fifty back on the Breakdown page) https://claude.ai/code/artifact/265fba57-a486-46c3-bf97-30d4179198ac

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
| D22 | **Superseded by D53 (round eight): peers are drawn on the pool of the type, within 500 below and 250 above yours.** ~~**PUMBILITY peers** — the players within **±3 rungs**~~ of you on the Phoenix 2 PUMBILITY level ladder (`Phoenix2PumbilityLevel`, the hidden five-levels-per-gem ladder: DIAMOND LV.4 reaches down to DIAMOND LV.1 and up to RED BERYL LV.2), where **you and each peer hold a full 50-chart pool of the chart type**. *"This would formally introduce the idea of 'PUMBILITY Peers'."* One rung, the total pool's, serves both chart types; the full-pool rule is per type. Measured against every alternative in §4.8 |
| D23 | **The tier list's PUMBILITY lens uses the same definition.** *"They need to be the same."* And the word is **peers**: *"please stop calling 'peers' cohorts."* The lens's keys, columns and code rename accordingly ([pumbility-tier-list.md §5](pumbility-tier-list.md)) |
| D24 | **A chart shows only when five or more peers have passed it.** No level window: *"That was already a bit flaky, I think the '5 or more peers have to have passed it' covers us on not showing unrealistic charts. This lets the occasional 'D23 that a level 18 player can actually pass if they spend a few minutes memorizing a single section' entry show."* The perfect-score-cannot-clear-your-bar filter (§6.5) stays — it is arithmetic, not a level |
| D25 | **No competitive level on this page.** *"Competitive level isn't really the answer here. It is built for score alone, it doesn't view scores <950k as valid ... pass pushing is a valid competency."* The peer band replaces it in who is asked, the five-peer floor replaces it in which charts, and the growth weighting — a competitive-level delta — is dropped: *"drop it."* Measured: on Phoenix 2 the weighting discounted 58% of the evidence, because everyone's level climbed three rungs in the first month |
| D26 | **Superseded by D50 (round seven): one default, the 25th, on both mixes.** ~~**The Phoenix 2 estimate is the median (p50), not p65.**~~ Measured, not chosen: p65 was fitted on Phoenix 1 eventual bests; on Phoenix 2 evidence it reads +6k median, p50 reads +1.8k, and with the full-pool rule −44 (§4.8) |
| D27 | **The peer count is shown**, per type, in one clause: *"a disclaimer on 'this is how many PUMBILITY Peers you have' somewhere."* This is a section line, not the per-row evidence caption D12 removed |
| D28 | **A type without a full pool is dark.** *"Have peers/projections only light up/show once you're at 50 charts for that type."* Charurun plays no singles and sees no singles peers; a 29-double pool sees "29 of 50" |
| D29 | **The carried Phoenix 1 rows are untouched.** *"The projections we have in phoenix 2 that are just 'your phoenix scores, mapped to Phoenix 2 pumbility' — those should remain exactly as is. We are only changing projections."* `CarryoverTargets`, `/Pumbility/Phoenix1`, the pool, the bar, the titles and the ask do not move |

Round four, 2026-08-17, after the owner's field test of round three.

| # | Ruling |
|---|---|
| D30 | **Superseded by D52 (round seven).** ~~**A peer row carries a "Peers IQR"**~~ — the peers' 25th and 75th percentile scores as two letter grades with a dash between them, beside the median. Owner: *"Can we do an estimated range? … If we do like 'Estimated P25 -> P75 letter grades'? It sort of helps visualize confidence levels a bit too. If like everyone in your pool is getting a SS, you'll probably get an SS. If P25 is A+ and P75 is SSS+, you know it's a specialized chart."* Named **IQR**, not "range": *"call it 'Peers IQR' so those who understand math nuance will know it's not a Min/Max range."* Grades only — no quartile numbers, no width figure — and *"at most a tooltip for 'From X peers'"*. **Compact is unchanged**: *"Only so much data we can fit there."* A carried Phoenix 1 row has no peers and prints a dash. This narrows D12: the numeric spread D12 removed stays removed; two grades that say whether the peers agree is the one per-row figure about the evidence that earned its place, because the field test read the median alone as a claim ("it's still giving me S22 SSs") when the peers behind it were split |
| D31 | **You are never one of your own peers** (owner, 2026-08-17). The projection has always drawn its band with the viewer removed; the tier lists' PUMBILITY lens now does the same at read time — the stored list is one per peer group and counts every member's pool, so the reader's own pool is taken back out (one from the peer count, one from every chart it holds, the bands redrawn), and both lenses' captions on Phoenix 2 name PUMBILITY peers. Nightly is the caveat: a pool that filled since the last build was never counted in, and for that day the subtraction runs one deep |

Round five, 2026-08-18. Phoenix 2 only; Phoenix 1's Play page is untouched. The page these rulings
describe is §3.10; the measurements behind them are §4.9.

| # | Ruling |
|---|---|
| D32 | **Your PUMBILITY peers replace "What to play next" on Phoenix 2.** Owner: *"And yes, this replaces 'what to play next'."* `/Pumbility` on Phoenix 2 renders the peers page in the target list's place; the section stays three tabs; ~~Phoenix 1 keeps the target list of §3.3 exactly as it is~~ — **that half was never asked for and is reversed by D43**: Phoenix 1 gets the page too, and the target list is retired as a component. Measured first (§4.9): every peer-projected target is a chart at least one peer holds, so nothing the old list said is lost — what changes is the order it says it in, and where the carried Phoenix 1 rows sit |
| D33 | **The grouping is called Prevalence** — *"Prevalence is good. I like that. Go with that."* — and it is a weighted count: a chart at #1 in a peer's pool scores 50, at #50 scores 1, summed over the peers (a Borda count; each peer casts an equal 1,275-point vote, so the band's strong tail cannot dominate the way a raw value sum would). **The hold count is what prints** — "17 of 23 peers" — and the weighted number rides a tooltip that says **"Weighted sum: 550"**: *"don't call it borda there."* Measured: the weighting barely reorders (top-50 overlap 39/50 against the plain count) and is not level-sorted inside a band |
| D34 | **It is a tier list and follows the tier list page's pattern** — *"Follow the Tier List page's pattern for all of this (UI/UX wise too)."* Staple · Strong · Solid · Average · Modest · Slim · Poor by the PUMBILITY lens's own log-scaled banding on the rarity ramp; collapsible sections with **Slim and Poor folded** by default; Comfortable / Compact / Table via the standard trio, which sits **right above the list, right-aligned**; the To-Do bookmark and its dashed-blue ring exactly as the tier list wears them. **No song-name filter, no other filters, no applied-filters row, no sorting** (*"No, no sorting"*) |
| D35 | **Superseded by D52 (round seven).** ~~**Variability, not IQR grades.**~~ The Peers IQR (D30) survives as a data point but prints as a level: the peers' interquartile width in points, `log(1 + w/1000)`, banded ±0.5σ / ±1.5σ across the charts they play — five levels named **Very consistent · Consistent · Mixed · Split · Very split** (*"i like a"*), per chart type, only where five or more peers scored the chart. Measured: raw widths skew +1.10 and the bottom band never fires; on the log they skew −0.25 and all five assign (7/34/36/34/9 on the reporter's singles peers). The word always prints; **Compact carries a dot top-right and Comfortable does not** — *"Comfortable doesn't need the variability dot, only compact does"* |
| D36 | **Grouped by: Prevalence (default) · Projected gains.** Under Projected gains the list is only what pays, biggest first, in bands of PUMBILITY points, and carried Phoenix 1 rows interleave by gain exactly as the target list did — it *is* the old page. A **Project Phoenix 1 scores** switch appears there, and only there, and only *"when the player has phoenix 1 scores that would change the data"*: on (the default) is the carried-wins rule of D11/D29; off drops the carried rows and the peer projection shows through where one exists. Under Prevalence an **Only projected PUMBILITY gains** switch cuts the tiers to the charts that pay; the front page opens with it on |
| D37 | **The pool selector's All is one merged list** — *"merge into one"* — tiers and order computed per chart type (different electorates: 23 singles peers, 7 doubles), the sections interleaving both, ordered within a tier by each chart's share of its own electorate, and every card saying "5 of 7 peers" so the electorate is visible. Not stacked lists |
| D38 | **The jacket carries the gain and nothing else.** *"Switch out the X/Y peers for the green/blue outlined Projected PUMBILITY (if you're projected to have a gain)"*, then *"drop the X/Y peers number on the song jacket for both comfortable and compact."* The badge is the target list's own — mix-primary outline, `+18` — and the gain is the target list's own rule (D11/D29, carried wins, measured against the bar as it stands, D13), so the two pages can never disagree about a number. A chart that would not pay wears no corner at all; the hold count lives in the Comfortable body and the Compact tooltip. Compact's other bottom corner is the projected grade, as on the target list |
| D39 | **The peers get a roster** — *"give them a dedicated table at the bottom"* — name, PUMBILITY level, PUMBILITY total (N2, it is a pool), **competitive levels too** (*"maybe competitive level too"*), which type they are a peer for under All, and how many of your fifty they also hold; sorted by total with your own row highlighted where you would sit. **Private accounts are counted as peers and not named**, with *"X private accounts not shown"* beneath |
| D40 | **The chart leaderboard gets one Peers chip with a sub-row** — Competitive · PUMBILITY — *"for now"*; the sub-row appears only where both exist (Phoenix 2, the viewer's type lit), and the default when clicked cold is **Competitive** (*"do competitive for now"*). Hosts pass the sub-scope: a card on this page opens on PUMBILITY, a session score row on Competitive as before. Private peers stay on the board as Anonymous rows, exactly as Competitive Peers keeps them. This is the first surface presenting two of the [peer pools](peers-abstraction.md) as one control **Your own row is on the PUMBILITY board** (owner, 2026-09-01: *"we should make sure you, yourself, show in the pumbility peers leaderboard. it's weird not having yourself on there, even if you're not technically your peer"*): the peer read leaves the viewer out (D31), the board adds their World row back, and the standing line counts them |
| D41 | ~~**The compare strip and the Yours-alone section ship, on the mock's evidence, as cuttable.**~~ **Ruled in field test round two: the In-common and Yours-alone tiles are cut** (*"not useful"*) and the strip is the **level bars alone**, sitting beside the list's lede on a wide screen and dropping below it once the viewport is square or taller. `PeerCompare` lost the three counts with them — a number nobody can act on is not worth computing. The Yours-alone *section* in the list survives. **Round nine, part two (D58): the level bars leave the Play lede for the Breakdown page's card** |
| D46 | **The Your top 50 list has its own tier vocabulary (field test round two).** Owner: *"Staple/Strong/etc. make no sense"* on your own fifty. Those bands are what a chart is worth to **you**, not how many players keep it, so they read as a magnitude: **Highest · Very High · High · Average · Low · Very Low · Lowest**. Average, Low and Very Low are the Score lens's own words, so the two ramps sound like one family; `PumbilityTierNames.PoolNameOf` is the second map beside `NameOf`. Since round nine those names band the Breakdown page's top-50 sections (D57, §3.11) |
| D42 | **What is assumed until the field test says otherwise:** the front page lands on **Prevalence with the gains switch on** (confirmed in round six: *"Stay on Prevalence"*); the block title is ~~**"Your PUMBILITY peers"**~~ **"Your peers"** on both mixes since round six (D43) and does not change with the grouping; the gain-band sections are fixed point bands (+25 and up · +15 to +25 · +10 to +15 · +5 to +10 · +2 to +5 · under +2), the unit the bar and the ask are read in, rather than σ-banded |
| D43 | **Phoenix 1 gets the page too, with the competitive band as its peers (round six).** Owner: *"has phoenix 1 not been getting the same exact treatment as 2? functionally it should be exactly the same for all the grouped by etc. right?"* `/Pumbility` renders the peers page on both mixes; `TargetList` is deleted, and what it ranked survives as the Projected gains lens (D38). A Phoenix 1 peer is the band the projection already draws from — players within one competitive level of you for the type (§4.7), the viewer out — which is also the cohort the leaderboard's Peers chip shows there, so the page, the chip and the gain agree about who "peers" are on both mixes. **No full-fifty gate on Phoenix 1**: the band is drawn from competitive level, which is real at any pool size, so a Phoenix 1 player is never dark (D28 stays Phoenix 2's) and a thin peer simply casts a shorter vote. The read widens from the targets' level band to the pool floor — level 10 is the formula's floor on both mixes, `BaseRating` is zero below it — roughly twice today's Phoenix 1 sweep, on the same 24-hour cache; a first visit already says it takes a moment. Stated, not hidden: Phoenix 1's gain is the discounted quantile of §4.1 and the card's "Peers' median" is the plain median of the same group, so the two can differ by a grade there — on Phoenix 2 they are one number (D26). The block is **Your peers** on both mixes and the lede names the cohort. **The roster is capped at fifty rows around you** with the counts above and below — a band is several hundred players, and the roster's question is "where do I sit", not "who is first" |
| D44 | **Superseded by D57 (round nine): the fifty are back on the Breakdown page.** ~~**Your top 50 is the third lens of the Play list (round six).**~~ Owner: *"What if we merge in the 'Your top 50' as one of the options to group by."* Under it the list is the pool the frame is scoped to — the selected pool's fifty, by place — split at the bar into two sections, **Your top 50** and **The waiting room**, each card carrying its place, the chart's value, your grade and score, and the peers line beneath (how many hold it, their median, how split); the table is the old pool board with the peers' columns added. No switches under this lens. The pool board of §3.4 and its no-density board skin retire with it — the fifty wear the tier-list card like everything else on the page. Web-only: the frame's record joined with the peers record by chart id |
| D45 | **The Pool page is the PUMBILITY Breakdown page.** Owner: *"keep a section for all of the 'Your Pool' data/distribution stuff and call it 'PUMBILITY Breakdown' instead of 'Your Pool', keeps it on its own dedicated page."* `/Pumbility/Breakdown` — your PUMBILITY titles, where your PUMBILITY comes from, the pool curve — with `/Pumbility/Pool` still resolving to it. ~~The top 50 leaves it for the Play list (D44); the curve stays, because it is the bar's picture rather than the list's~~ — the fifty return in round nine (D57), the curve inside their block |
| D47 | **A band too thin to meet the five-peer floor answers on what it has.** Owner: *"if someone has ZERO net results because of the 5+ peers rule, drop it. This will likely be important for super high and super low levels."* The floor (D24) asks for five peers **on a chart**, not five in the band, so at a band of five it demands unanimity and below three it can never be met — a cliff, not a filter, and it lands exactly where a band is thinnest. Where it leaves a run with **nothing at all**, the same records answer again with no floor; a band that produced anything is untouched. Opt-in per caller on `ScoreProjectionRequest`: the PUMBILITY page and the home widget ask, because they suggest charts and one peer's score beats an empty board, while the personalized tier list does not — it falls back to the community list, which is a better answer than a folder ranked on single scores. `PumbilityPeerPools.MinimumScored` deliberately does **not** relax with it, so a rescued row carries a gain beside "Fewer than 5 peers scored it" and a blank IQR — that pairing is the per-row disclaimer, already written and already localized. Measured on the 2026-08-20 snapshot: of 216 Phoenix 2 accounts, 121 saw an empty board; the two with a full pool and a thin band are the shape the Discord report came from, and this rescues one of them with 155 charts. Evidence depth across every rescued row: 71% rest on one peer, 24% on two. The lede note that explains it counts the peers who hold a full pool — the players actually lending scores, and the roster's own rows — so it names them that way rather than as the band. **The note fires on whether the fallback RAN, never on the band's size** (2026-08-21): a band too small to put five voices anywhere is only the obvious case, and a band of nine whose charts were each scored by two or three relaxes identically while a size test stays silent through the whole board. `PeerGroup.AnsweredBelowFloor` carries the projector's own answer out to the page, set only when the second pass actually produced rows — a band that scored none of these charts has an empty board and no thin evidence to warn about. Unchanged by D53: the floor and the fallback apply to the pool window exactly as they did to the band |
| D48 | **Twenty charts is enough to project from, against your estimated finish.** Owner: *"what's the end results if we cause it to start projecting for you once you have 20 charts, based on your current projected title (assuming you maintain your current PUMBILITY average)."* The VIEWER's pool gate drops from fifty to twenty; a PEER's stays at fifty, because their pool is the evidence and half a pool is half a vote. The two halves are one decision and ship as one: a twenty-chart pool's own total is the sum of the charts it happens to hold, so seating a player by it puts a strong one at the bottom of the ladder among peers who tell them nothing — which is why supplying `ProjectedTotal` is what lowers the gate, and a caller with no answer to "where will they finish" keeps the full-fifty gate (the tier list's own call does). The estimate keeps every chart held at what it is worth and prices only the EMPTY slots, at the weakest chart in the pool — the standard the player is already holding at its bottom. **Backtested on the 2026-08-20 snapshot**, 111 full-pool accounts, and 84.7% of the true peer set falls inside the projected ±3 band. The estimator shipped on 2026-08-21 as the pool's top-twenty AVERAGE out to fifty, and averaging is upward-biased by construction — the mean of a descending list's head can only exceed the mean of the whole — and biased by the same amount however much the player holds, because an average over the top twenty discards slots 21 and up: +1.69% mean at every pool size, the exact rung 8 times in 111, up to four rungs high, and never low. Filling from the pool's floor instead lands the exact rung 39 times in 111 at a twenty-chart pool and 111 in 111 at forty-eight, converging as the pool fills rather than staying flat (within two rungs 110/111 at twenty). It still reads high — +0.89% at twenty, +0.01% at forty-eight — because the slots a player adds next are usually worth a little less than the one they hold last. That residual is **one-directional**, so the ±3 band is not symmetric around the truth: at a twenty-chart pool it reaches roughly [−2, +4]. Accepted as the cost of placing a short pool at all; owner call, no calibration constant. Coverage: of 118 dark Phoenix 2 accounts, 57 reach twenty charts in some type and 51 of those get a full board (median 111 charts clear the five-peer floor); 61 hold under twenty anywhere and no gate change reaches them. The projected rung is what does the work — placed by their standing total instead, 37 of 65 (account, type) pairs get fewer than five peers; placed by the finish, 63 of 65 clear it. **The bar stays 0 for a short pool**: a chart displaces nothing until the pool holds fifty, so a gain is the chart's whole value and the list ranks by what a chart is worth — `PoolCurve` already says exactly this. `PeerGroup.PoolSize` carries the gate the run was made under, so the dark chip counts to twenty on this page and to fifty on the Personalized Breakdown, and the chart leaderboard's empty PUMBILITY-peers state names the same constant. **A short pool of ONE TYPE is not a projection**: the rung is read off the MERGED pool, so a player with a full fifty and twenty-odd doubles is placed by a settled number even though the doubles band lights on the shorter gate — `ScoreProjectionRequest.ProjectedTotalIsEstimate` rides out on `PeerGroup.PlacedByEstimate`, and the note keys on that rather than on a pool count. **Under D53 the finish is the pool OF THE TYPE**: the peers are drawn around it, so the empty slots of that type's pool are filled at its weakest held chart, and a short pool of one type IS placed by an estimate now, whatever the merged pool holds — the merged-pool nuance above is history |
| D49 | **The way out of the section is a link on the eyebrow, not a chip in the nav.** Owner: *"It's a link, not a button… more out of the way, it bloats up the actionable field and clutters mobile too much."* `How it works →` sits at the right of the `PHOENIX 2 · PUMBILITY` line in `--mix-primary`, so the one thing on the line that leaves the section is the one thing on it that is not muted; the nav row keeps its three tabs and its width. The row is `space-between` rather than `margin-left:auto`, so a long locale drops to the left of the next line instead of stranding itself against the far edge (D15). It points at the viewer's own mix and renders once the mix is known, so it never flips under the reader. The calculator carries the return trip, `Your PUMBILITY →`, on its own eyebrow beside the cross-mix links — signed in only, since the frame sends an anonymous visitor to `/` |
| D50 | **Superseded by D54 (round eight): the median is the default again, on D53's peers.** ~~**The projection reads the 25th percentile by default, on both mixes (round seven, 2026-09-01).**~~ Owner: *"I think we should bump it down to 25th percentile for predicting scores as a default"* → *"lets try 25th, yeah"*. The median was centered over every pair a backtest can score and off by half a grade exactly where a player looks: the top ten of a gain-sorted list is selected for the charts whose estimate ran high, and at the median those rows read +4,728 with their SS calls right 53% of the time (§4.10). Owner: *"I don't think I've EVER come close to ANY of the medians recommended … EVERYONE talks about how it over-estimates. Even if it's hitting 50% of the time, it throwing an SS in your face when you feesibly won't be able to pull that SS off is going to feel infinitely worse than the 20 charts it got right."* Phoenix 1's 65th retires with it — measured +7,359 median against today's frozen records (§4.10) — so `PeerEstimator` carries one default and the two constants of D26 are gone. Every surface that is not the PUMBILITY page reads this default and nothing else |
| D51 | **Energy — the PUMBILITY page's own read of the projection.** Owner: *"a drop down for 'How are you playing today?' … 'Good' (25th) 'Great' (50th) and 'Top of my Game' (75th)"*; the label word is **Energy** (*"'Today' doesn't read well, do 'Energy' or something"* → *"Energy feels good"*), a **dropdown, not a slider** (*"drop down for now"*). ~~Placed as a chip on the block head right of the *PUMBILITY Targets* title~~ — **on seeing the build the owner moved it into the control row, a select between Grouped by and the grouping's switch** (*"move that select down between the group by and the 'project phoenix1' toggle"*), **labelled Energy with the options Good, Great and Top of my game exactly as the approved mock had them.** The build had reworded the options to dodge the judgement keys `Good`/`Great`; ruling: *"Please do not change entire approved UX in mocks over a low level key collision problem"* — the resx keys carry the register (`Energy: Good`, `Energy: Great`), the copy does not. It re-reads **every projected score and gain on the PUMBILITY page** and nothing off it: *"all projected scores on the PUMBILITY page for now. default all the others to 25 percentile still though"*. Persisted per player as the `Pumbility__Energy` UiSetting, ~~Good~~ **Great** by default (D54). The projector hands back a ladder of the three rungs per chart over the same voices and weights, so a change of Energy is a lookup, never a second sweep, and the 24-hour cache stays keyed on player and mix. No summary line about the setting anywhere on the page — a *"Reading at Good · 25th percentile"* strip was mocked and cut as *"all noise"*. No Auto: a per-player quantile fitted from the viewer's own standing measured 7% better on held-out charts (§4.10) and was declined — *"no. no auto."* |
| D52 | **Projected replaces the median, and the spread is gone (round seven).** The Table column is **Projected** — *"Just 'Projected' is fine"* — the peers' score at the viewer's Energy, and the Comfortable body line and Compact corner grade print the same value. The **Peers IQR (D30) and the Variability meter (D35) are retired**, with the dot, the word, the legend row and the `--vary-*` tokens: *"get rid of range bar and variability please"* — *"that was sort of trying to accomplish this and wasn't doing it well."* The Energy read IS the answer to "where in the range would I land"; a second visualisation of the same range beside it was noise. `PeerPoolChart` keeps every scorer's score and answers a quantile on demand, so nothing about the peers is lost — only the two columns that printed a shape of it |
| D53 | **PUMBILITY peers are drawn on the pool of the type: players whose singles pool sits within 500 below and 250 above yours for a singles chart, doubles for doubles (round eight, 2026-09-01).** Owner: *"What happens if we remove combined pumbility as a factor and switch to only including peers in your singles or doubles pumbility range … +/- 500 of your doubles pumbility for measuring doubles"* → *"move to -500 and +250 singles/doubles, let's roll that out and see what people think."* The rung band on the combined total (D22) was type-blind — a singles-carried DIAMOND's doubles peers were doubles specialists — and skewed upward at every rung: the charts a player has not played are the ones players above them hold (§4.11: for viewers on rungs 16–20, 62% of every voice the band heard sat above the viewer, and the median of the scorers sat above the viewer on 73% of pairs). Measured on the same 11,480 pairs (§4.11): the −500..+250 window at the median answers 75% of the band's pairs, and on those the **top ten of a gain-sorted list read −1,611 against the band's +1,974**, coverage 57% against 43%, an SS+ it calls landing 76% of the time against 65%; the symmetric ±500 moved the same figures a fifth as far. Asymmetric because the skew is. The full-pool gate on both sides (D28), the five-voice floor (D24), the thin fallback (D47) and the short-pool finish (D48) all stand, the finish now being the pool OF THE TYPE. Groups are smaller — median 22 players against the band's 36 (p10 7, p90 39) — and one viewer in 185 drops under five peers. The window is a range read on the stats row's per-type top-fifty sum (`SinglesRating`/`DoublesRating`, stored unrounded since 2026-08-09), so nothing new is stored: `IPlayerStatsReader.GetPlayersByPoolOfType` replaces `GetPlayersByPumbilityRange`, and `PeerGroup` carries the pool and the two distances instead of a rung and a half-width |
| D54 | **Great — the median — is the default read everywhere (round eight).** Owner: *"switch it to great as default. i'll run it and see how it feels."* … *"Default to great there and home page too for now too."* `PeerEstimator.DefaultQuantile` is the median again, so the tier list's projected scores and the home widget's pushes read it; Energy opens on Great, the setting still the player's. Supersedes D50, which chose the 25th against the rung band's overshoot at the top of the list — under D53's peers the top ten at the median reads −1.6k rather than +4.7k, so the reason for the lower rung went with the band. Field test, not a ruling on the arithmetic: *"i'll run it and see how it feels"* |
| D55 | **One PUMBILITY peer group across the site (round eight).** Owner: *"tier list should move with this. we aren't keeping multiple pumbility peer groups across the site."* The tier list's PUMBILITY lens for a signed-in Phoenix 2 viewer reads the projector's peers' pools — `ScoreProjection.PeerPools`, the same holders-per-chart the Play page counts — banded per folder with the same log-scaled processor the nightly writer uses, and the folder picker lists the folders those pools reach. The nightly job stops writing Phoenix 2's per-rung `R{index}` lists and keeps the community `*` list; Phoenix 1's `L{n}` lists are untouched. D23 holds by construction: the lens no longer has a definition of its own to keep in step. The read-time viewer subtraction of D31 is moot on Phoenix 2 — the projector leaves the viewer out — and stays on Phoenix 1. Rows written under the old keys are harmless leftovers ([pumbility-tier-list.md §11](pumbility-tier-list.md)) |
| D56 | **A re-read of the Play list holds its shape (round eight, field test).** Owner: *"do we really need the loading animation for switching energy or group-bys? it shifts the entire page, it feels pretty jarring. Versus just disabling inputs for the half second it takes … Possibly a good case for disabling inputs, but hiding the actual chart cards (so shows tier list skeleton while it loads the next one in)."* The patience card is for the **first paint** only. A change of Energy or pool keeps the ledes, the peer line, the control row and the legend where they are — the two selects, the switch, the density buttons and Download **disabled** for the moment the read takes — and keeps the previous list on screen under a pulse, every card and table cell blanked at its own size, so the page's height and the scroll position hold; everything flips once when the new record lands. The list renders from the record it was built with, one read behind the frame's, so the old peers are never laid against the new gains. **Grouped by is untouched**: it is a re-sort of what is already on screen and loads nothing (*"instant re sort is fine"*). The chart-identity chips are read once per page rather than on every energy change. **The flip waits for both halves** (bug, 2026-09-02): the frame renders once with the new pool or energy beside the record it still holds and again when the re-priced record lands, and the peers read usually lands in between — flipping on the peers alone captured the old record and then ignored the new one, since by then pool and energy already matched, so the list sat on the old pool's bar and targets for good. The record now carries the energy it was priced at (`PumbilityPageRecord.Energy`, beside its pool) and the block flips only when the peers and a record for the same pool and energy are both in hand; a change that arrives while a read is in flight supersedes it |
| D57 | **Your top 50 leaves the Play list for the PUMBILITY Breakdown page (round nine, 2026-09-05).** Owner: *"move the 'your top 50' back out of 'Play' on the pumbility page and onto the 'Your pool' page. Basically anything breaking down YOUR CHARTs should be there, suggested charts are all on Play … keep using the same tier list elements, just switch it to top 50 and don't give people the 'Your Top 50' option on the Play page."* The rule that decides the section: **Play holds what is projected, the Breakdown page holds what you hold.** The fifty become the Breakdown page's last block (§3.11) — the pool curve, then the fifty as tier-list cards banded by what each is worth to you under the D46 vocabulary, with the density trio and Download above the list — and Grouped by on Play is Prevalence · Projected gains again. **No peers' data on those rows** (*"don't worry about peers details on top 50"*): no projected grade, no hold count, no Better Than, and no gain corner, since a held chart that would pay is already a Play row that names its slot; the value rides the jacket corner in both densities and Compact's other corner is your grade. **No waiting room** (*"No waiting room"*): the curve keeps ghosting the six, the list stops at the fiftieth. Both mixes. Presentation only (§6.11): the frame's record already carries the fifty with place, value, score, plate and date, so the Breakdown page reads nothing it did not read before, and a saved `Pumbility__GroupBy` of `YourTop50` simply fails to parse and lands on Prevalence |
| D58 | **Where your PUMBILITY comes from grows two sections: you against your peers by chart type, and by level (round nine, part two, 2026-09-05).** Owner: *"move the 'where the level sits' to the 'Your pumbility page' and give an additional card for 'your singles versus doubles' (similar to 'Phoenix 2 · same scores, repriced' but with your actual phoenix 2 scores) … fold all of that into the 'where your pumbility comes from'"*, then *"make 'Singles and Doubles' read '<Pumbility> (<chart count>)', get rid of the individual sub-cards … add a second bar beneath that that gives you a comparison of your peer's average break down"*. **Singles and doubles**: two of the Phoenix 1 card's bars, one above the other — your real fifty and your peers' average fifty — each segment reading its PUMBILITY whole with the chart count (owner, 2026-09-05: *"drop the decimal for the bar"* — the one whole number on the section, for the segment's width), doubles then singles, sized by value like every stack on the card (the Phoenix 1 card sizes by count). **Your peers** are the union of your singles peers and your doubles peers (D53; one definition, D55), and a peer's breakdown is their merged fifty — their records of both types priced, merged, the top fifty taken — which nothing stores, so it is read once and cached beside the sweep for its day; only a full fifty counts. The bars show only for the merged fifty — All on Phoenix 2, the one pool on Phoenix 1 — and your bar stands alone while no type has peers. **Where the levels sit** moves off the Play lede into the card as the section after, unchanged in what it draws (D41), one tile per lit type in the selected pool. Both arrive by one query off the cached sweep (§6.12), so Play reads nothing new and the Breakdown page reads one thing more, after the frame has warmed the sweep |

| D59 | **Official board players are PUMBILITY peers (round ten, 2026-09-06).** Owner: *"we have a couple high level players now … what's the practicality for using official leaderboard players for them as pumbility peers"* → *"I don't think we want to do pumbility gate. I think we just naturally start mixing them in"* → *"for all intents and purposes, just like we do with rivals, these are your peers."* The top of the site is peer-starved by arithmetic rather than taste: its strongest singles account holds **four** peers under D53, and four can never satisfy the five-voice floor, so its Play list is empty and always would be (§4.12). The official per-type PUMBILITY boards publish the same quantity `SinglesRating`/`DoublesRating` holds, for ~1,145 players a mix, so a board player's membership needs no estimate; the per-chart boards carry their scores. They enter the same window (D53), count toward the same five-voice floor (D24), and are peers on every surface a site peer is — the projection, the tier-list lens (D55), the standing that colors your own scores, Hot Streak, the roster, and the board a source line opens. **No strength gate**: the per-chart boards are blind below level 20 and a pool short of fifty prices low, so the window and the fifty check (D60) exclude the middle on their own. Measured ungated on 13,547 pairs, MAE by viewer rung: PLATINUM 9,720, DIAMOND 8,401–9,285, RED BERYL 8,308–9,626, ALEXANDRITE 6,533 — against the site-only baseline's 9,174 — and the bias turns conservative at the top (ALEXANDRITE −4,052). Phoenix 2 only: Phoenix 1's PUMBILITY board is not per-type, so there is no window to read |
| D60 | **A board player counts only when we can see their fifty, and the tolerance is 270 (round ten).** piugame prints each player's per-type pool on its own board, so a fifty rebuilt from the chart rows the mirror holds can be checked against it — and the gap runs one way only, a chart we cannot see being a chart missing from our copy. The tolerance is **270**, which is what the plate costs across a whole pool: Phoenix 2 prices a chart Base(level) × (grade + plateBonus), Base(25) is 260 for a Double and 270 for a Single (which prices one level up), and a Perfect Game's plate bonus is 0.020 — so 5.20 a chart, **260.00 across a full fifty of doubles, 270.00 of singles**. A board row carries a score and no plate, so that band is exactly what cannot be known, and no smaller tolerance is reachable; it is also under one whole chart, worth 373 at the bottom of a pool that size. Owner: *"just set the tolerance to 270. That's less than one chart so won't pick up 'PG'd 49 but missing 1' (which is unrealistic anyways, TG to UG is going to be the standard for most of these). keeps it simple."* The realistic TG→UG spread is 0.012 × 260 × 50 = 156, so 270 clears it with room. **This never reaches a page** — it decides who is on the roster, and the roster lists them (D62). Nearly all-or-nothing in practice: of 863 board-only players on the Phoenix 2 singles board, 336 of the 629 between 17,000 and 18,000 can fill a fifty at all and 330 of those pass; above 18,000, 212 of 219 fill and all 212 pass |
| D61 | **One board row, one person — and a private account is read as a board player (round ten).** A row is matched to an account by its link or by game tag with spacing and case ignored: the site stores `NAME #1234` and the board stores `NAME#1234`, so an exact compare finds **zero** matches and a normalised one finds 300 accounts the link column never caught, 55 of them on the Phoenix 2 singles board. Twelve accounts own more than one row after a rename (`EUPHO#5163` → `EUPHO#6352`) and fold into one voice, best score per chart across the rows. A **public** account is then judged by its own record and its board copy dropped — `ELIJAHTS#6216` reads 628 above his real pool, which is what a board number would use to drag him into a window his record puts him outside of. A **private** account that qualifies on the board is a **board player**: named by its public tag, scored from public rows, membership from the board's number, and nothing of its PIU Scores record read. Owner: *"if someone has a private piuscores account but matches as a board qualifier, treat them as a board account, not a piuscores account. That'll keep them from showing scores outside of what's publicly visible already"* and, on where the rule lives, *"mirror should not be pretending there's a linked account when that linked account is private."* It costs nothing measured: the two private accounts in the reporter's own window cover 41 and 42 of his fifty from board rows against 41 and 32 from their records. A private account **not** on the board is unchanged — counted in every number, named nowhere |
| D62 | **The roster gains a chip, not a column (round ten).** Owner: *"players don't need to see all the evidence of WHY someone was included. Just who the matches are. The board chip is prob good enough."* `PeerRoster` keeps its own columns; a board row wears the **BOARD** chip beside the name and an em-dash in the two competitive-level cells, which the boards do not publish. The fifty check, the rebuild shortfall and the count of charts seen are all membership arithmetic and are rendered nowhere. `/Account` is not reworded either — *"PUMBILITY page including those i think is more than enough"* — its PUMBILITY peers row keeps its definition, which board players satisfy as written, and only the count moves |
| D63 | **A PUMBILITY row's score is coloured, and its popover is real (round ten, 2026-09-06).** Owner, on the Breakdown page: *"pumbility page (pumbility breakdown specifically) is still just… hanging? I don't see activity in the console logs either."* Neither PUMBILITY list had ever asked for a peer standing — not on this branch and not before it — so every score there painted plain and its popover had nothing to say, which after the D62 round's "Still working out where this sits." read as a page still loading. Both lists read `GetPeerStandingsQuery` for the charts on screen now, beside the identity chips they already read. **The caption stays off** (`TierListChartCard.ShowStandingText`): a Better Than line under a Breakdown row is exactly what D57 excluded, while the colour is the site-wide `PeerScore` treatment every other list gets rather than peers' detail. **And the popover's source lines open their board** (D12), which is the other half of the same field test: wired to nothing they render inert, and a row that reads as a link and does nothing is worse than plain text. Both pages open the chart dialog on that source's scope, on the Leaderboard tab — the Breakdown page falling back to the World and Play to the mix's own board when a card is opened plainly |

## 3. The section

### 3.1 The three pages

One frame, the `OfficialSectionFrame` pattern: shared chrome, each page its own route and circuit,
nav links as real document loads.

```
FRAME   your number · pool selector · the bar        ← left-aligned, all three pages
        [ Play ]  [ PUMBILITY Breakdown ]  [ Phoenix 1 ]

Play          your peers, both mixes (§3.10)                      /Pumbility
              Grouped by: Prevalence · Projected gains
PUMBILITY     your PUMBILITY titles                               /Pumbility/Breakdown
Breakdown     where your PUMBILITY comes from                     (/Pumbility/Pool still resolves)
              your top 50: the pool curve, then the fifty (§3.11)
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

### 3.4 The pool board — superseded by the Your top 50 lens (D44), then by the Breakdown page's own list (D57)

~~Board rows wearing `.olb-rank-card` — a ranked list of entities gets the leaderboard skin and **no
density toggle** (rule 5). The bar renders as a rule in the list, with the waiting room ghosted
beneath it.~~ Since round six the fifty are the **Your top 50** lens of the Play list (§3.10): the
same ranked pool, split at the bar into the fifty and the waiting room, wearing the tier-list card
with the peers' data on every row. The board skin and its no-density rule went with the board —
the list has one density setting and the fifty obey it. The curve stays on the Breakdown page,
because it draws the bar, not the list. Since round nine the fifty are the Breakdown page's own
list again (§3.11, D57): the same card and the same density setting, with no peers' data and no
waiting room — the curve keeps ghosting those six.

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

**Singles and doubles (D58, round nine part two).** Two of the Phoenix 1 card's bars, one above the
other beneath the plate rail: **your real fifty** and **your peers' average fifty**, each split by chart
type, doubles then singles, sized by value like every stack on this card, each segment reading its
PUMBILITY whole with the chart count — *12,773 (36)* (owner, 2026-09-05: *"drop the decimal for the
bar"*; the segment is too narrow for two decimals, and it is the one whole number on the section). The peers are the union of your singles peers and
your doubles peers (D53; one definition, D55), and a peer's breakdown is their merged fifty — their
records of both types priced under the mix's formula, merged, the top fifty taken
(`PumbilityPoolSplit`), only a full fifty counting. Nothing stores that, so it is read once per viewer
and mix and cached beside the sweep for its day. The bars show only for the merged fifty — All on
Phoenix 2, the one pool on Phoenix 1 — since a singles or doubles pool is one type by definition; your
bar stands alone while no type has peers, and the line under the bars names the peers the mix
has — the pool window on Phoenix 2, the competitive band on Phoenix 1 (D43). On the owner's account (2026-09-05): 36 singles worth
12,773.08 against the peers' average 34 worth 12,085.84, and 14 doubles worth 4,958.59 against 16 worth
5,508.22, over 64 peers.

**Where the levels sit (D41, moved here by D58).** The Play lede's tiles, unchanged in what they draw —
your fifty per level against the peers' prevalence per level — as the card's last section, one tile per
lit type in the selected pool, labelled by type. They come with the split, in one query off the cached
sweep (§6.12), so Play reads nothing new and this page reads one thing more, after the frame has warmed
the sweep.

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
the selector itself say what they are worth. The rung bar was the device `PumbilityTitleTrack` drew on
the tier list too, until 2026-09-05, when the tier list gave it up for a pointer here: this section
is the rung bar's one home now.

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
`MinimumScore` is 0 in both PUMBILITY configs. `FolderTitleTrack.HasTitleProgress`, the tier list's pointer gate, builds its pool the same way —
so the page and the tier list agree about what a pool is.

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
rather than what the page is for). Every chart your peers (D53, D43) hold in their top-50 pool of the
type, tiered by prevalence (D33, D34): how many hold it, weighted by how high. Each card says what the peers do on
it — the grade you are projected to land at your Energy (D51, D52), ~~how far apart they are (D35)~~, how many hold it — and where you stand: your
score and its percentile among them, whether it is in your pool and at what slot, or that you have
never played it. Under it, who they are (D39). Above it, the chips line — **the dark state only** since field test round two, because a lit type's
count is what the roster prints below (D27/D28) ~~— and, beside the lede, the level bars saying where
your pool sits against theirs (D41)~~ — the level bars moved to the Breakdown page's card in round
nine, part two (D58), and the lede stands alone.

**Two groupings, one control (D36).** *Prevalence* is the page's own order. *Projected gains* is the
old page: only what pays, biggest first, the carried Phoenix 1 rows interleaved (D29), in bands of
points rather than one paginated list, with each card annotated *"Staple · 17 of 23 peers"* so the
prevalence travels along, and its section prints no per-tier pool count — *"X of Y in your pool"* read
as a claim about the tier rather than about you (field test round one). The `Grouped by` select is the
tier list's own control, capped in width so the density trio stays beside it; the two switches
beside it (Only projected PUMBILITY gains under Prevalence; Project Phoenix 1 scores under Projected
gains) are the page's only filters, and each shows only where it means something.

**Energy (D51, round seven).** A select in the control row, between Grouped by and the grouping's own switch,
labelled *Energy* and reading *Good* by default with *Great* and *Top of my game* behind it — the peers' 25th, 50th and
75th percentile. It is the page's one read of the projection: every projected grade and every gain on the page, under
every lens and density, moves with it, and the share card follows because it is drawn from the rendered list. It is a
UiSetting like the pool and the grouping, so the frame reloads on a change the way it does for the pool selector; a
change costs no sweep, because the sweep already holds the three rungs (§6.9), and the block holds its shape while it
runs — controls disabled, the old list pulsing in place, one flip when the new record lands (D56). Nothing else on the site reads it (D50):
the home widget's PUMBILITY pushes and the tier list's projected scores stay at the 25th. The select explains itself on
hover and each option says what its percentile means — *a score three in four of your peers reach* / *the middle of your
peers* / *a score only one in four of your peers beat*. No line on the page restates the setting.

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
(`LetterGradeIcon` for the projected grade at your Energy · the count — ~~the variability meter and its word~~, D52), the pool line,
and the tier list's border language with the tier list's precedence — passed, To-Do, carried. Compact
prints no words: its tooltip carries the tier, the count, the weighted sum, the projected grade and
your state; ~~a top-right dot is the variability (D35)~~ — retired with D52, the tile's top-right corner is empty. ~~and, under Projected gains, a
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
was the same card model all along. **The tile carries its own difficulty bubble** (field test round
six): a tier list's card prints its bubble once, in the header, because the whole list is one folder,
and this list is every difficulty at once — without it the picture cannot say what any tile is. It is
`Tile.BubbleUrl`, drawn in the jacket's top-left where the page's card wears it, null by default so
the tier-list card is unchanged; the URL comes from `ShareCardImages`, which now owns the page's own
spelling of it (flat art for SP/DP and the legacy sets, none where the page draws a legacy chip),
so the card and the screen can never ask for different pictures.

**Table** — Song · Chart · Peers (`17 of 23`, weighted sum in the title) · Projected (the grade and score at your
Energy, D52) · Gain · My Score · Better Than · Your pool · the two actions. ~~Peers' median · Variability~~ (D52).

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

**Your top 50 (D44, round six) — retired from this page in round nine (D57).** ~~The third lens: the
selected pool's fifty by place, split at the bar into *Your top 50* and *The waiting room*, each card
carrying its place and the chart's value, your grade and score, and the peers line beneath; the table
is the old pool board (§3.4) plus the peers' columns. No switches under it. The data is a Web-side
join of the frame's `PumbilityPageRecord.Pool` / `WaitingRoom` with the peers record's entries by
chart id — nothing is read twice.~~ The fifty are the Breakdown page's list now (§3.11) and Grouped
by is two options again: this page holds what is projected, that one holds what you hold.

**The roster cap (D43).** A competitive band is several hundred players. The roster keeps the fifty
rows nearest you in the sort — you in place — with a line above and below saying how many more there
are; under fifty it is the whole roster as before.

### 3.11 Your top 50 — the Breakdown page's last block (round nine)

The owner's framing: *"anything breaking down YOUR CHARTs should be there, suggested charts are all on
Play."* The fifty left this page for the Play list in round six (D44) and come back in round nine (D57)
as the page's last block, under the titles and the breakdown band. Mock: the round-9 artifact in the
header, drawn from the owner's real pools.

**What it is.** One block titled **Your top 50**, with the lede *"The fifty charts your PUMBILITY is
the sum of, grouped by what each is worth to you — its place, its value, and the score that earned
it."* The pool curve (§3.2) sits inside the block first, because it is the summary of the same fifty
the list details; under it the control row — Download and the density trio at the far end, nothing on
the left, there being no grouping to choose — and then the list.

**The list.** The selected pool's fifty, banded by what each chart is worth with the shared
processor's standard-deviation cuts and named as a magnitude — Highest · Very High · High · Average ·
Low · Very Low · Lowest (D46) — on the rarity ramp, sections collapsible, nothing folded by default.
The card is `TierListChartCard` as the Play list wears it: jacket, bubble, **the value in the corner in
both densities**, your grade in Compact's other corner, the score line with grade and plate, the
identity chips, and one body line, *In your pool #12*. Every card is a pass, so every card wears the
pass border. The table is `#` · Song · Chart · My Score · Value · the two actions. Compact's tooltip
carries the name, the place, the value and the score.

**What is deliberately not here.** No peers' data — no projected grade, no hold count, no Better Than
— and no gain corner: those are Play's, and a held chart that would pay is already a Play row naming
its slot (*"don't worry about peers details on top 50"*). No waiting room (*"No waiting room"*): the
curve ghosts the six as before and the list ends at the fiftieth. No switches. The empty state is the
sentence the lens used: *Nothing in your pool yet — every non-broken pass at level 10 or above takes
a slot.*

**Download.** The tier list's own share card, from the rendered sections, titled `PUMBILITY Breakdown
— Your top 50` with the pool scope on Phoenix 2, the mix and the date beneath, `Personalized for
{tag}` as the stamp; no Energy clarifier, because nothing on this page reads one. The value a tile
prints under the PUMBILITY option is the pool's own, so the picture and the page cannot disagree.

**Settings.** `Density__PumbilityBreakdown` and `PumbilityBreakdown__CollapsedTiers`, per page like
every list on the site. Play's `Pumbility__GroupBy` keeps its key; a stored `YourTop50` fails to parse
and reads as Prevalence.

**Both mixes, one shape.** On Phoenix 1 there is no pool selector and the block simply lists the one
pool.

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

⚠ **Step 1 is superseded by D53 (§4.11, round eight):** the peers are the players whose pool of the type sits within
500 below and 250 above the viewer's, not a rung band on the combined total. Steps 2–4, the floor and every
measurement below stand as written.

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

### 4.10 Round seven — which percentile, measured where a player looks (2026-09-01)

The owner asked for the 25th percentile as the default and a per-page choice of percentile. Before
either was drawn, the shipping harness (`ScoreTracker.ExplorationTests/Pumbility/PumbilityProjectionBacktestTests`)
was extended to read the same pairs at the peers' first and third quartiles — the projector already
computed both for the Peers IQR — and to add coverage (the share of actual scores at or above the
estimate), the never-overstated rate (the estimate's letter grade no higher than the actual one), a
sampled Phoenix 1 backtest, the **top of the list**, and a split-half per-player quantile. Truth is the
player's actual best on charts they have played, which favours every percentile alike: nobody plays
the charts they are worst at.

**Over every pair, Phoenix 2** (289 players, 11,480 pairs) and **Phoenix 1** (every eighth account,
150 with a level ≥ 15 type, 32,423 pairs in the page's ±2 window):

| read | P2 median bias | P2 coverage | P2 grade never overstated | P2 SS+ calls right | P1 median bias | P1 coverage | P1 SS+ calls right |
|---|---|---|---|---|---|---|---|
| 25th | −6,734 | 76% | 83% | 90% | −10,939 | 75% | 88% |
| 50th | 0 | 50% | 62% | 81% | — | — | — |
| 65th (P1 shipped) | — | — | — | — | **+7,359** | 30% | 53% |
| 75th | +5,189 | 25% | 41% | 70% | +11,700 | 20% | 43% |

So the median was centered over everything on Phoenix 2 — and the Phoenix 1 constant, fitted in
January on a forward-looking test of levels 19–21, reads seven thousand high against today's frozen
records, where the band a player is matched to is whoever kept grinding. "Centered over everything"
is the wrong test, though, and the owner said so before the numbers did: *"I don't think I've EVER
come close to ANY of the medians recommended."*

**The top of the list.** What a player sees is the top of a list sorted by projected gain, and sorting
by a noisy estimate selects the charts where it ran high — the winner's curse. Per player-type, the
answered charts ranked by projected PUMBILITY value (the page's own order; the bar is one number
within a player), the top ten read against the rest, each percentile ranking its own list:

| Phoenix 2, 176 lists | median bias | coverage | grade overstated | SS+ calls on rows | those right |
|---|---|---|---|---|---|
| top 10 at the 50th | **+4,728** | 35% | 56% | 44% | **53%** |
| the rest at the 50th | −298 | 53% | 35% | 71% | 84% |
| top 10 at the 25th | −4,123 | 63% | 29% | 21% | 73% |
| top 10 at the 75th | +14,214 | 13% | 81% | 70% | 35% |

| Phoenix 1, 251 lists | median bias | coverage | grade overstated | SS+ calls on rows | those right |
|---|---|---|---|---|---|
| top 10 at the 65th | +4,693 | 36% | 49% | 18% | 62% |
| the rest at the 65th | +7,676 | 30% | 56% | 16% | 52% |
| top 10 at the 25th | −10,314 | 76% | 13% | 3% | 90% |

At the median the top ten overshoots by half a grade and its SS calls are a coin flip; at the 25th it
reads modestly low, overstates the grade on 29% of rows instead of 56%, and an SS it does call lands
three times in four. The cost is asymmetric — an SS a player cannot hit poisons trust in the whole
list, an undershoot is invisible — which is D50. On Phoenix 1 the whole list overshoots, not just the
top, so one literal ladder on both mixes holds.

**A per-player quantile, measured and declined.** Where a player's own scores sit among their peers
(midpoint rank of the actual among the voices on each chart) spans the 24th to the 73rd percentile
across players (p10–p90 of 183 player-types), and is noisy within one (median within-player IQR of the
percentile 0.37, where 0.5 is "anywhere"). Fitted on the odd half of each player's charts and scored on
the even half: personal quantile MAE 8,604 against the median's 9,277 (−7%), bias 0, grade-exact 33.3%
vs 30.6%, SS+ calls right 83.6% vs 81.0%; a half-shrunk version 8,651. The first personalisation on
this page to measure positive (§4.3's four were ≤ 0.3%; §4.5's per-player offset was +9.1% worse) —
and declined as a product (*"no. no auto."*): the knob is the player's, and it answers *how am I
playing today*, not *where do I stand*. Recorded so it is not re-derived.

**The reporter's own list**, singles pool, bar 346.68, at the three rungs: 46 / 100 / 100 of the
hundred listed rows still clear the bar; SS+ calls 9 / 38 / 77. His own Phoenix 2 form on the S22
charts (961k–967k) sits at the peers' 25th. ⚠ Those figures are a 2026-09-01 snapshot of a live
account — re-run the probe rather than quoting them.

### 4.11 Round eight — who the peers are, measured on the same pairs (2026-09-01)

The owner's report after round seven: the charts the page kept pushing were charts only players ABOVE
him held — King's Tomb D24 was held by peers on higher rungs, every one of them scoring it well — and
the question was whether that was his account or the population. Three questions, in turn, on the
round-seven harness and the same 289 players / 11,480 pairs (`Phoenix2_scorers_level_offset_against_the_bias`,
`Phoenix2_pertype_pool_window_against_the_rung_band`).

**It is the population.** For every pair the band answers, the rung offset of every voice heard
against the viewer's own rung, grouped by where the viewer stands:

| viewer's rung | pairs | voices below / level / above the viewer | pairs whose scorers' median sits above / below | top 10 at the median, bias |
|---|---|---|---|---|
| 11–15 | 569 | 24% / 18% / **58%** | **62%** / 11% | +7,962 |
| 16–20 | 2,132 | 25% / 13% / **62%** | **73%** / 10% | +9,142 |
| 21–25 | 4,900 | 43% / 15% / 42% | 29% / 34% | +4,490 |
| 26–30 | 3,653 | 52% / 16% / 31% | 15% / 57% | +1,578 |
| 31–36 | 226 | 90% / 10% / 0% | 0% / 100% | −2,296 |

The skew is structural, not personal: the charts a player has not played are the ones the players
above them hold, so a symmetric band around the viewer hears the room above more than the room below,
worst in the middle of the population where the rungs are thickest above. Only at the top of the
ladder, where nobody stands above, does the median read low.

**Asymmetric rung windows**, re-read off the same voices (strict: under five voices in the window the
chart is not shown) — −2..+1 answered 77% of the band's pairs and cut the top ten's median-read bias to
−665 (coverage 52%, SS+ calls right 70%); −3..+1 answered 85% at −1,728 (56%, 73%); rung weighting by
`exp(−|offset|)` barely moved (+4,085 at the 75th, 0 at the median over every pair). Enough to show the
direction, not chosen: the owner's next question was whether the combined total should be a factor at all.

**Windows on the pool of the type.** Peers drawn on the viewer's pool OF THE TYPE — the stats row's
per-type top-fifty sum — within a PUMBILITY distance of it, full pool of the type on both sides, the
five-voice floor, the viewer out. Head to head against the shipping band on the pairs both answer:

| window on the pool of the type | pairs answered | median group (p10–p90) | viewers under five peers | top 10 · median read: bias | coverage | SS+ calls right | all pairs · median: bias / coverage |
|---|---|---|---|---|---|---|---|
| ±3 rungs on the total (shipping) | 100% | 36 (17–61) | — | +1,974 to +4,728 | 35–43% | 53–65% | 0 / 50% |
| ±250 | 58% | 16 (5–27) | 17 of 185 | +944 | 46% | 73% | 0 / 51% |
| ±500 | 88% | 26 (10–51) | 0 | +2,664 | 41% | 63% | 0 / 50% |
| ±1000 | 113% | 48 (20–88) | 0 | +8,802 | 28% | 43% | +168 / 49% |
| **−500..+250** (D53) | 75% | 22 (7–39) | 1 of 185 | **−1,611** | **57%** | **76%** | −1,214 / 59% |
| −750..+250 | 86% | 26 (10–51) | 1 of 185 | −3,024 | 61% | 78% | −2,572 / 65% |
| −500..0 | — | 14 (5–27) | 17 of 185 | — | — | — | — |

(The band's own top-ten figures differ per row because each is measured on the pairs that window also
answers.) A symmetric window does not fix the skew — ±500 still overshoots the top ten by +2.7k, ±1000
reads worse than the band — because a symmetric window reaches as far into the room above as below and
the room above is fuller. Reaching further down than up is what moves it, and −500..+250 is where the
top ten stops overshooting without the group thinning out: one viewer in 185 drops under five peers,
against seventeen at ±250. The wider −750..+250 pushes the median read down another 1.4k for eleven
more points of pairs answered; the owner chose the narrower one to roll out and *"see what people think"*.

**The reporter's own case.** Singles pool 17,648, doubles 17,318: 37 singles peers and 22 doubles peers
under −500..+250 against the band's 61 and 37. King's Tomb D24, the chart the round began on, has
**two** voices among his doubles peers under ±500 — under the five-voice floor, so it is not shown —
where the band read it off higher-rung specialists and pushed it. ⚠ Snapshot figures of a live account;
re-run the probe rather than quoting them.

### 4.12 Round ten — official board players as peers, measured (2026-09-06)

Measured against the prod-synced local database, official snapshots through 30 Aug 2026.

**The starvation is arithmetic.** Site peers under D53, and the charts that reach the five-voice
floor, against what the official per-type boards would add in the same window:

| | S peers site→board | S charts at the floor | D peers site→board | D charts |
|---|---|---|---|---|
| RSS (S 19,200 / D 19,444) | 4 → 40 | **0 → 280** | 5 → 32 | 33 → 270 |
| ORIU #9860 | 6 → 48 | 44 → 304 | 6 → 34 | 56 → 288 |
| SUNMU #7646 | 10 → 118 | 102 → 471 | 8 → 48 | 85 → 352 |
| SANDEX | 19 → 163 | 231 → 494 | 8 → 71 | 67 → 480 |
| Tomatonium | 28 → 283 | 279 → 511 | 7 → 64 | 64 → 421 |

Site players holding a full Phoenix 2 pool: 162 singles, 107 doubles. Official players with fifty or
more charts visible: 702 singles, 590 doubles.

**Board evidence is clean and level-truncated.** On 14,676 rows shared with linked accounts' ledgers:
**95.4% exact score match** (the rest is snapshot lag, both directions), **17 broken rows — 0.12%**,
so a placement is a pass for D9's purposes. Coverage by singles level: **S18 0%** of 3,259 ledger
rows, S19 5%, S20 73%, S21 80%, S22 80%, S23 87%, S24 89%, S25 94%. Within a level the visible slice
averages 3.5k–6.5k above the hidden slice, netting roughly **+1.5k on the visible sample** — small
against a 9k MAE, and in a known direction.

**Why nothing below level 20 needs a rule.** The lowest chart in a player's own top fifty climbs with
their pool — level 15 at singles pool 17,000, 17 at 17,500, **19 at 18,000**, 20 at 18,500, 22 at
19,000 — so a pool rises above the boards' visibility floor exactly where the boards start seeing it.
The share of a player's own fifty the boards show, by gem rung (singles/doubles): PLATINUM 15%/18%,
DIAMOND 1–2 47%/51%, DIAMOND 3–5 72%/60%, RED BERYL 1–2 85%/74%, RED BERYL 3–5 88%/84%,
ALEXANDRITE **96%/92%**.

**A per-peer coverage filter earns nothing.** Restricting board peers to those seen on 100+ charts,
against no restriction: DIAMOND MAE 9,055 vs 8,876, RED BERYL **9,471 vs 8,838 — worse** — PLATINUM
identical, and the pair count barely moves (6,257 → 6,111). The estimator never reads a peer's pool,
only their score on the chart in front of it; a peer seen on forty charts votes on forty and is silent
on the rest, and silence is not a wrong vote. What the fifty check (D60) protects is everything that
reads a **pool** — the roster's overlap, the Breakdown card's peer average (D58), the prevalence
grouping — not the estimate.

**Supply per viewer rung**, new board peers beside the site peers they join, under D60: PLATINUM 25
site / ~0 board · DIAMOND 1–2 46 / ~0 · DIAMOND 3–5 50 / ~5 · RED BERYL 1–2 41 / 117 · RED BERYL 3–5
28 / 160 · ALEXANDRITE 12 / 99. Nothing below RED BERYL 3–5 is starved, and nothing below it gains.

⚠ Every figure here is a snapshot of a live population read through a weekly mirror. Re-run the probe
(§6.13) rather than quoting them.


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

### 6.9 Round seven — Energy

**Still no new table, port, package, migration or job.** No API surface reads a projection.

| Vertical / layer | Change |
|---|---|
| **Domain** | `PeerEstimator`: one `DefaultQuantile = 0.25` replaces `Quantile` (0.65) and `Phoenix2Quantile` (0.50); `Median`, `LowerQuartile`, `UpperQuartile` stay as named rungs. `ScoreProjectionRequest.Quantiles` — the rungs a caller wants, the default alone when omitted. `ScoreProjection.Ladders`: per chart a `PeerLadder` of those rungs plus the peer count, both branches over the same voices and weights the estimate uses; `Scores` stays as the first rung, so a caller that wants one number is untouched. `PeerPoolChart` loses `Median`/`Quartile1`/`Quartile3` and answers `ProjectedAt(quantile)` from the `Scores` it already carries, null under the five-scorer floor. `PeerSpread`, `ScoreProjection.Spreads` and `PeerVariability` are deleted |
| **PlayerProgress** | `Contracts/Energy.cs`: the enum and its rung. `GetPumbilityPageQuery`, `GetPumbilityPeersPageQuery`, `ProjectPumbilityGainsQuery` gain `Energy Energy = Energy.Good`. `ProjectionSweep` caches ladders; `PumbilityProjectionSaga.Estimate` asks for the three rungs and `Price` reads the request's; the peers-page handler fills `PeerPoolEntry.Projected` from the pool chart and drops the variability banding. `PumbilityPageSaga` threads Energy. `PumbilityProjection.Spreads`, `PumbilityTarget.Spread`, `PeerPoolEntry.Median`/`Quartile1`/`Quartile3`/`Variability` go. `PumbilityProjectionCache` is untouched — same key, same lifetime, same size limit. `RecommendedChartsSaga` is untouched: the widget reads the default |
| **ChartIntelligence** | No code change. `TierListBlendBuilder` and `ProjectedScoresHandler` inherit the 25th through `Scores` |
| **Web** | `PumbilitySectionFrame` reads `Pumbility__Energy`, carries it in `SectionContext`, reloads on change like the pool. `PeersSection`: the Energy select in the control row (a `MudSelect` beside Grouped by; its copy in `Services/EnergyLabels`), Energy on both reads, a re-fetch on change; the variability row leaves `PeerPoolLegend`. `PeerPoolList`: the Table column is **Projected**, reading the entry's projected score with the target's as fallback; the Comfortable peers line and the Compact corner grade read the same; the Variability column, the meter, the top-right dot and the legend row go. `VariabilityMeter.razor` deleted; `TierListChartCard` loses its top-right dot parameters with their only consumer; `ThemeScales.VariabilityColor`, the `--vary-1..5` tokens and the `.pmb-vary*` rules come out |
| **Localization** | New: `Energy`, `Energy: Good` and `Energy: Great` (keys only — the English copy is *Good* and *Great*; `Good` and `Great` are the judgement names' keys, reusing them would print judgement translations in Korean and Japanese, and a case variant is forbidden), `Top of my game`, the select's sentence and one per option. `Projected` is reused. Retired: `Variability`, the five level words, `variability, consistent to split`, `Peers' median`, `From {0} peers` |

**Performance.** The cached payload does not grow: three rungs per chart replace the estimate plus two
quartiles it held already. Database reads are unchanged. A change of Energy is one setting write and
the two reads a pool change already costs, both served from the cached sweep.

**Two things that stay true.** On Phoenix 1 the Projected column reads the band's plain voices while
the gain reads them growth-weighted, so the two can still differ by a grade there (D43's caveat). And
the home widget's pushes read the default whatever the chip says, so a player on Great can see a chart
on the page the widget does not offer (D51).

**Tests.** `PeerEstimatorTests` (the default; the rungs); `ScoreProjectorTests` (ladders on both
branches, the default as `Scores`); `PumbilityPeerPoolsTests` (`ProjectedAt`); `PeerVariabilityTests`
deleted; `PumbilityProjectionSagaTests` / `PumbilityProjectionSagaPeersTests` / `PumbilityPageSagaTests`
(Energy pricing, the sweep shape, `Projected`); `Tests.Components`: `PeerPoolListTests` (the Projected
column, no Variability), `PeersSectionTests` (the chip persists its setting), `PeerRosterTests` fixture.
The harness gains the quartile rows, coverage, the Phoenix 1 sample, the top-of-list read and the
split-half quantile (§4.10).

**Build order** — docs first, i18n last, pushed per commit: (1) this section, D50–D52, §4.10;
(2) the harness; (3) Domain; (4) PlayerProgress; (5) the chip and the column; (6) the removal of the
median, the spread and the variability meter; (7) nine locales.

### 6.10 Round eight — the pool of the type, Great, one peer group

**Still no new table, package, migration or job.** One reader method replaces another on an existing port;
the nightly job does less.

| Vertical / layer | Change |
|---|---|
| **Domain** | `IPlayerStatsReader.GetPlayersByPoolOfType(mix, chartType, minimumPool, maximumPool)` — inclusive both ends, a range read on the per-type pool column — replaces `GetPlayersByPumbilityRange`. `PeerGroup`: `Kind` is `PumbilityPeers`, `Center` is the viewer's pool of the type (settled, or the D48 finish), `Below`/`Above` replace `HalfWidth` (500/250 on PUMBILITY peers, the window twice on a competitive band; `Lowest`/`Highest` derived); `PumbilityWindowBelow`/`PumbilityWindowAbove` replace `PumbilityRungWindow` and `PumbilityBand(rung)`. `ScoreProjector`'s Phoenix 2 branch draws candidates on the window around `SinglesRating`/`DoublesRating` (or the caller's finish), the viewer out; the pool gates, the floor, the fallback and the pools ride unchanged. `PeerEstimator.DefaultQuantile` is `Median` |
| **PlayerProgress** | `PumbilityProjectionSaga.Estimate` computes the D48 finish per type from the per-type pools it already builds for the bar (one `GetTop50ForPlayerQuery` fewer per sweep — the merged read goes) and hands each type its own. `Energy.Great` is the default on the three queries; `EnergyRungs` unchanged. `EFPlayerStatsRepository` gains the range read on the per-type column |
| **ChartIntelligence** | `TierListBlendBuilder.ComputePumbility` on Phoenix 2 for a signed-in viewer projects the folder with the catalog and counts `PeerPools` holders per folder chart, banded with `TierListProcessor.ProcessIntoLogScaledTierList` — no stored list, no viewer subtraction. `PumbilityFoldersHandler` lists a Phoenix 2 viewer's folders from the same pools (every level a peer's pool reaches). `TierListSaga.ResolvePeers` writes only `*` on Phoenix 2. `PumbilityPeers` loses `ForPhoenix2Rung`, `ForPhoenix2Total`, `Phoenix2Band`. Phoenix 1's title-level lists and the Score lens are untouched |
| **Web** | `PersonalizedBreakdown`: the Phoenix 2 stat block prints the pool window and the viewer's pool of the type instead of the rung band. `PeersSection`: the three ledes and the short-pool note name the window; the Energy select opens on Great; `PumbilitySectionFrame` defaults to Great. `ChartSkills`' peer caption names the window. `ChartLeaderboardScopes` unchanged (it reads the sweep) |
| **Localization** | Retired: the three rung-band ledes, `within 3 levels of you with a full pool`, `you stand on {0}`, `Ranked against {0} PUMBILITY peers within 3 levels of you`, the merged-pool short-pool note. New: the three window ledes, `pools within {0} below and {1} above yours, each a full pool of the type`, `Pool window`, `your singles pool is {0}` / `your doubles pool is {0}`, the window caption, the per-type short-pool note. `Level band` stays for Phoenix 1 |

**Performance.** The candidate read is a two-column range on the stats table where it was a one-column
range — same cost, a smaller group (median 22 against 36) and so a smaller records read. The Play page's
sweep drops one top-fifty query. The tier list's PUMBILITY lens for a signed-in Phoenix 2 viewer becomes
a projector run per folder inside the blend's six-hour cache, where it was a table read; the folder
picker is one run per type, cached the same way. The nightly job writes up to 37 fewer row sets per
folder per type on Phoenix 2.

**Tests.** `ScoreProjectorTests`: the window edges per type, the viewer out, the finish placing a short
pool, no ladder clip. `PumbilityProjectionSagaTests`: the finish per type, the group's centre as a pool
total, a short pool of one type placed by its own finish. `TierListSagaPumbilityTests`: Phoenix 2 writes
only the community key. `BlendedTierListHandlerTests`: the lens counts the projector's pools, the viewer
out, dark without a full pool. `PeerEstimatorTests` and every default-read assertion move to the median.
`Tests.Components`: the Breakdown's pool window, the peer-line fixtures. `Tests.Integration`: the inclusive
per-type range read. The exploration probes compile against the new port.

**Build order** — docs first, i18n last, pushed per commit: (1) this section, D53–D55, §4.11, the tier-list
and breakdown docs, the schema and jobs rows; (2) the definition — Domain, PlayerProgress, ChartIntelligence,
Web, tests; (3) Great as the default; (4) nine locales.

### 6.11 Round nine — the fifty move to the Breakdown page

**Presentation only.** No new query, port, table, migration, job or cache; the frame's record already
carries the fifty (`PumbilityPageRecord.Pool`: place, value, score, plate, date). Application, Domain,
Data and every vertical are untouched.

| Layer | Change |
|---|---|
| **Web** | `PeerGrouping` loses `YourTop50`. `PeersSection`: the third select item, the lens lede and the lens's share-card header go; its download machinery moves to a shared `PumbilityShareCard` helper both pages call. `PeerPoolList`: the pool branch, `PoolRecord`, the row's slot, the `#`/Value columns and the value corner go; its share tile keeps `PoolValue`, which the Breakdown list fills. New: `PumbilityPoolList` (the fifty as sections of `TierListChartCard`, banded with `TierListProcessor.ProcessIntoTierList` and named by `PumbilityTierNames.PoolNameOf`) and `PumbilityPoolSection` (the block: title, lede, the curve, the control row, the two settings, the identity read for the fifty, Download). `PumbilityBreakdown.razor` renders the block last and picks up the To-Do set and the chart-details dialog the Play page has. `ShareCardTitles.Pool` is the card's header; `Targets` loses its pool flag |
| **Localization** | New: the block's lede. Retired: the lens lede (*"Your pool by place, with what your peers do on each chart…"*) and `PUMBILITY Pool`; `Top 50` stays, the rankings page uses it |

**Tests.** `Tests.Components`: `PumbilityPoolListTests` (the bands and their names, the corner value
and Compact's grade, the empty state — the three lens cases, without the peers' data),
`PumbilityPoolSectionTests` (the block renders the curve then the list; density and folds persist),
`ShareCardTitlesTests` (the pool header); `PeerPoolListTests` loses its three lens cases;
`PeersSectionTests` untouched.

**Build order** — docs first, i18n last, pushed per commit: (1) this section, D57, D44–D46, §3.1,
§3.4, §3.10, §3.11, the architecture page map; (2) the shared download helper; (3) the list and its
tests; (4) the block, the page, its tests; (5) the lens out of Play; (6) nine locales.

### 6.12 Round nine, part two — you against your peers, inside the card

**No new table, migration or job.** One new read, cached beside the sweep; one new query; one pure
Domain function.

| Layer | Change |
|---|---|
| **Domain** | `PumbilityPoolSplit` (pure): `Of` takes a player's priced records of both types (`PricedRecord`) to their merged fifty split by type — `PoolTypeSplit`: counts and values per type — and `Average` takes a set of players to the mean over those holding a full fifty, or null. The pricing and the reads are the caller's |
| **PlayerProgress** | `GetPumbilityPoolCompareQuery(user, mix, pool)` → `PumbilityPoolCompareRecord(Levels, Peers)` on `PumbilityProjectionSaga`, off the cached sweep: `Levels` per lit type through the existing level comparison; `Peers` — the average split — only for the merged scope, from two `IScoreReader.GetPlayerScoresInLevelRange` reads over the union of the lit types' peers, priced with the mix's scoring, cached by `PumbilityProjectionCache` beside the sweep for its day and evicted with it. `PeerCompare` moves to the new record's file; `PumbilityPeersPageRecord` drops `Compare`, which nothing else read |
| **Web** | `PumbilityBreakdown` (the card) takes the frame's record and the charts, dispatches the query when its scope changes, and grows two sections: *Singles and doubles* (your bar off the record, the peers' bar off the answer) and *Where the levels sit* (`PeerCompareStrip`, rendering from the levels dictionary, labelled by type). `PeersSection` loses the strip and its head row. New site.css rules for the sub-sections and the two-row bars, tokens only |
| **Localization** | New: the two section heads and their captions, the peers line. Retired: the two *Where the levels sit · singles / doubles* labels |

**Tests.** `DomainTests/PumbilityPoolSplitTests` (the fifty across both types, a zero never takes a slot, only full
fifties average, the mean, nobody full). `PumbilityProjectionSagaPeersTests`: the compare query answers the
levels and the peers' average off one sweep, nothing for a type scope, empty for a dark viewer; the peers page no
longer carries a comparison. `Tests.Components`: `PumbilityComponentTests` — the bars only for the merged scope,
the segment text, value sizing, your bar alone without peers, one tile per lit type; `PeersSectionTests` — no
strip. `Tests.Integration`: none — the reads are existing ones.

**Build order** — docs first, i18n last, pushed per commit: (1) this section, D58, D41, §3.6, §3.10, the page
map; (2) the probe's merged-fifty export; (3) Domain; (4) PlayerProgress; (5) Web; (6) nine locales.

### 6.13 Round ten — the mirror's board players

**Forced by the layer graph.** `ScoreProjector` lives in `Domain/Services/`, so it cannot reference a
vertical and the board's evidence has to arrive through a Domain secondary port. Half of that port
already exists: `IOfficialPlacementReader` (`Domain/SecondaryPorts/ISessionCaptureReaders.cs`),
implemented by OfficialMirror's `Infrastructure/OfficialPlacementReader`, already answers
`GetPumbilityBoard(mix, boardName)` — but `OfficialBoardReading` carries values and no player ids,
because its job is "where would my pool rank". Membership needs ids, pools and identity, so these are
new reads beside it rather than a change to that record.

**Domain** — `GetBoardPeers` and `GetBoardScores` on `IOfficialPlacementReader`, with
`BoardPeerReading` (tag, pool, the account it resolves to when the mirror may speak for one) and
`BoardScoreReading`; `PeerVoice`, the site-or-board identity a projection and a standing both count;
`PeerGroup` and `ScoreProjection` carry the board half and the mirror's `AsOf`;
`ScoreProjector.ProjectFromPumbilityPeers` merges the two evidence sources, one voice per identity.
`PeerEstimator` is untouched — it takes scores, not players.

**OfficialMirror** — the two reads, the identity resolution of D61 (link, then normalised tag, then
the many-to-one fold, then the private rule) and the fifty rebuild of D60, which prices board scores
with `ScoringConfiguration.PumbilityScoring(Phoenix2)` and `ExpectedPlateForScore`. The private rule
lives here rather than in Rivals because the mirror should not report a linked account it is not
entitled to speak for; one answer, and no drift between the projection's path and the standing's.

**PlayerProgress** — the sweep gains the board source; `ProjectionSweep`, `PumbilityProjection` and
`PumbilityPageRecord` carry board peers; `GetPumbilityPeersQuery` stops returning `Guid`.
`PumbilityProjectionCache` is untouched: it caches the sweep, and the sweep is what grew — peers'
scores moving deliberately does not evict (§6.5), and a weekly mirror is less volatile than the daily
imports that already do not.

**Rivals** — `PeerStandingReader.PumbilityPeers()`, `PeerStandingCalculator.SourceMembers`, and the
roster and catalog contracts. **ChartIntelligence** — the lens reads the sweep at request time (D55),
so it needs only to understand a board voice. **Web** — `ChartLeaderboardScopes` builds the board,
`PeerRoster` gains the chip (D62), `PeersAndColorsPanel` the count, `PeerStandingPopover` and
`PeerBoardRequest` the mirror's asterisk and date, as `RivalScoreReader` already does for a ghost.

**Nothing else.** No migration, no table, no index —
`IX_OfficialLeaderboardPlacement_PlayerId_SnapshotId` covers the evidence read and the clustered key
covers the board read — no scheduled job, no cache hook, no new vertical. Measured on the local
database: one viewer's peer evidence is 8,776 rows in 122 ms, and the entire eligible population's
every chart is 68,769 rows in 136 ms.

**Tests** — `DomainTests`: the fifty rebuild and its 270 gate, the voice merge. `ApplicationTests`:
the three dedupe legs, the private flip, the projector's mixed pool, the standing's counts.
`Tests.Components`: the roster chip, the board's rows and asterisk, the popover line, the account
count. `Tests.Integration`: both reads against a real migrated database with seeded placements — a
mocked repository cannot catch a wrong join across snapshots. `ExplorationTests`: the census re-run on
the shipping code.

**Build order** — docs first, i18n last, pushed per commit: (1) this section, D59–D62,
[peers-abstraction.md](peers-abstraction.md) D36–D38; (2) the two reads; (3) identity; (4) the fifty
check; (5) the Domain voice; (6) the contract change; (7) the projector; (8) Rivals; (9) the chart
dialog; (10) the roster chip and the account count; (11) the tier-list lens; (12) integration;
(13) the census probe; (14) nine locales.


### 6.14 Round ten, part two — the peer scores are held in memory

**The bug this fixes was already in production.** The PUMBILITY page draws several folders and asks
for a peer group's scores once per folder; the tier list draws one and never showed it. Adding board
peers made it visible rather than causing it — the first sweep after a restart was measured at 7.3
seconds, of which 2.5 was the fifty check, per chart type, recomputed for every viewer in the band.
Score colouring reads the same standings, which is why the page said "none of your peers have passed
this" on scores that a hundred peers had passed: nothing was wrong with the answer, it just had not
arrived yet.

**Two stores, and they are not the same shape**, because what makes them stale is not the same.

| | `BoardScoreStore` (OfficialMirror) | `PeerScoreStore` (ScoreLedger) |
|---|---|---|
| Holds | every board player's best per chart, per mix | every player's passing bests, per mix |
| Keyed by | the latest sealed snapshot | the player |
| Released by | a new sweep — the key changes | the player's own import, per player |
| Size | Phoenix 2 191,746 rows, Phoenix 1 124,300 | Phoenix 2 41,706 rows, Phoenix 1 1,039,022 |

The board store **never needs invalidating**: the set is stamped with the snapshot it was built
from, a sweep produces a new one, and the old is dropped. Nothing has to remember to evict it, and a
second app instance builds its own and is correct by construction. Which snapshot is current is
itself re-read at most once a minute, which is as often as a weekly sweep can matter.

The site store is **per player**, which is the shape both eviction and arrival want: a player who
imports has their own slice dropped and rebuilt on next use, a player nobody has asked about yet is
fetched when they are, and nobody else moves either way. `PeerScoreCacheConsumer` drops a slice off
`PlayerScoresUpdatedEvent` and `PlayerScoreDataDeletedEvent` — the same two the projection cache has
always used, for the same reason and in both directions. There is deliberately **no whole-set
expiry**: that would put a multi-second rebuild in front of one unlucky viewer for a staleness that
is per player in the first place. The twelve-hour per-player backstop is for an event that never
arrived, not the mechanism.

**⚠ The scale-out caveat.** The board store is snapshot-keyed and safe on any number of instances.
The site store's eviction rides the in-memory bus, so it reaches only the instance that ran the
import. On one instance that is exact; before this app is ever scaled out the site store needs a
cross-instance signal, or a second instance serves its own stale copy of a peer's scores until the
backstop. This is the one thing in this section that a second instance breaks.

**Neither store changes an answer.** Both hold what their SQL held: the site store keeps broken runs
out, masks a private player's name and says `IsPublic` outright; the board store keeps supplemented
rows out and collapses to each player's best across every sealed snapshot. Both hold **every type
and level**, not only what prices into a pool — the same reads answer a chart dialog, and a CO-OP
chart has a board and no pool. Trimming the board to singles and doubles above level 10 would have
saved 5% and turned a board peer into silence on every CO-OP page.

**What is not held with the scores.** A name and a public flag change without a score changing, so
they ride a separate five-minute read rather than the slice — otherwise a player who goes private
stays named for twelve hours. `BoardPeerReader`'s folded board carries the same fact (whether the
mirror may name the account behind a row, D61) and is held for the same five minutes.

**Two more caches fell out of the same read.** The fifty check is a property of the player and the
snapshot, not of the viewer, so it is answered once per snapshot and shared. So is everything else
in `GetBoardPeers` except the window itself, so the board is **folded to people once** and the
window filters the fold — which is also the more honest order, since a person's pool is their best
row, so a person whose best sits above the window is above it even when a lesser row of theirs would
have fit inside.

**The two stores read differently, on purpose.** The board store asks
`IOfficialSnapshotRepository.GetEveryChartHistory` — a new bulk form of the two reads it replaces —
because the placement table has exactly one reader and a `PlacementScope` with no default, so that a
supplemented row cannot enter an official reading by an author forgetting a predicate
([supplemented-leaderboards.md §7](supplemented-leaderboards.md)); an architecture ratchet holds that
line and caught the first attempt at this. The site store reads its own table directly, because the
Ledger has no such flag to get wrong and the read it needs is one EF cannot afford:

**A million rows will not go through EF.** The site store reads its rows off a `DbDataReader`
straight into the structs it keeps, and the chart's level and type come from a four-thousand-row
dimension held beside the scores rather than two columns repeated a million times down the wire. A
player's rows are then kept in `(type, level)` order, so a folder read is two binary searches rather
than a walk of everything that player has ever passed.

**Measured**, on the prod-synced local database, one viewer's peer group of 200 players over 14
folders — every number the *whole sweep*, not one read:

| | straight to the database | store, first call | store, warm |
|---|---|---|---|
| Phoenix 2, 15,460 rows | 240 ms | 1,649 ms | **9 ms** |
| Phoenix 1, 128,333 rows | 265 ms | 8,582 ms | **57 ms** |
| Board peers, singles | — | 4,997 ms | **12 ms** |
| Board peers, doubles | — | 1,822 ms | **2 ms** |

Row counts are identical before and after in every case, which is the check that matters more than
the milliseconds.

**The warm-up is Phoenix 2 only** (`PeerScoreCacheWarmer`, background and fully swallowed like the
chart-page warmer). Phoenix 2 is the mix the peers page runs on and its whole population is forty
thousand scores — about six seconds for both stores together, paid at startup where nobody waits.
Phoenix 1 is a million, and preloading all of it at every deploy would spend minutes of a small
instance on players nobody is going to look at; there the store fills a peer group at a time. Note
that whole-mix load times measured locally run through the Docker port proxy and are a ceiling, not
a forecast — the numbers worth trusting are the warm ones above.

**Memory.** About 40 MB for Phoenix 1 held whole, 2 MB for Phoenix 2, and roughly 12 MB for both
boards — against a 1.5 GB working set on a 3.5 GB B2. Restricting the site store to players who
qualify as somebody's peer was measured at a 21% saving and is not worth the coupling.

**Classes** — `ScoreLedger/Infrastructure/PeerScoreStore` (singleton) and
`ScoreLedger/Application/PeerScoreCacheConsumer`; `OfficialMirror/Infrastructure/BoardScoreStore`
(singleton). `EFPhoenixRecordsRepository`'s two cohort reads and `EFOfficialSnapshotRepository`'s two
history reads delegate to them and hold no SQL of their own any more. One command per vertical
(`WarmPeerScoresCommand`, `WarmBoardScoresCommand`) exists because a vertical may not reference the
hosting abstractions and the stores are internal, so the host asks through the MediatR seam.

**Tests** — `Tests.Integration/PeerScoreStoreTests`: that a slice really is held between reads, that
eviction is what releases it, that a one-mix eviction leaves the other, and that none of the SQL's
answers moved. `Tests.Integration/BoardPeerReadTests`: the CO-OP and low-level rows the store must
still carry. `ExplorationTests/Pumbility/PeerCacheProbeTests`: the table above, re-runnable.

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

- **Round eight, awaiting the field test (D53, D54).** Whether −500..+250 on the pool of the type holds
  (*"let's roll that out and see what people think"*) or the wider −750..+250 — another 1.4k lower at the
  median for eleven more points of pairs answered (§4.11) — reads better; whether Great as the default
  holds on the new peers (*"i'll run it and see how it feels"*); and whether the roster should print
  the pool of the type beside the gem, now that the type's pool is what makes a peer a peer.
- ~~**The page's truth horizon — answered for Phoenix 2, open for Phoenix 1.**~~ **Closed in round seven (D50, §4.10):**
  one default, the 25th, on both mixes — chosen on the top of the list rather than on the mean of every pair, and
  Phoenix 1's p65 measured +7k against today's records. The original note, kept for the reasoning: bias is strongly
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
