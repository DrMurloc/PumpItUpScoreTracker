# PUMBILITY section (`/Pumbility`) — design

Rebuilds `/Pumbility` from a wall of chart cards into a planner, and replaces the projection
engine underneath it. Mocked against real data from the owner's own account before any code;
the mock is the visual authority for §3.

The **second round** (2026-08-08) splits the result into a three-page section sharing one frame,
and adds two things the first round never answered: what your number is *made of*, and what it is
*for*. §2 D13–D20 are that round's rulings.

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
| D14 | **Three routed pages sharing one frame**, the Official Leaderboards pattern: **Play** `/Pumbility` · **Your Pool** `/Pumbility/Pool` · **Phoenix 1** `/Pumbility/Phoenix1`. **One** menu entry, pointing at Play |
| D15 | **The frame carries the number, the pool selector and the bar**, because all three pages measure from them. Everything in it is **left-aligned** — a bar card pushed right with `margin-left:auto` strands itself against the far edge the moment the row wraps |
| D16 | **The breakdown measures from pure base (×1.00)**, not from a grade. Owner: *"Pure base is showing more real data on how distribution actually works."* ⚠ If it ever moves to a grade the reference is **900,000** — which is **AA on Phoenix 1 but A+ on Phoenix 2**, since P2 shifted the sub-AAA floors. One score, two grades |
| D17 | **"Your PUMBILITY titles", never "projected"** — that word is spent on the peer estimator (§8.1), and a title you actually hold is not a projection |
| D18 | **The what-if calculator is deleted**, moving to the rating-calculator overhaul. ⚠ It loses the "what would this **add**" framing in transit: it prices against *your bar* today, and `/RatingCalculator` has no pool |
| D19 | **`GetPumbilityPageQuery` stays one query.** Splitting the cheap pool read from the expensive projection was offered and declined — performance gets its own pass. This does **not** worsen the cold read (§6.5) |
| D20 | **A chart worth zero never occupies a pool slot** — the general rule D10 was one case of. See §3.8 |

## 3. The section

### 3.1 The three pages

One frame, the `OfficialSectionFrame` pattern: shared chrome, each page its own route and circuit,
nav links as real document loads.

```
FRAME   your number · pool selector · the bar        ← left-aligned, all three pages
        [ Play ]  [ Your Pool ]  [ Phoenix 1 ]

Play          what to play next  (density trio)                  /Pumbility
Your Pool     where your PUMBILITY comes from                    /Pumbility/Pool
              your PUMBILITY titles
              the pool curve  ·  your top 50
Phoenix 1     what Phoenix 1 is worth here                       /Pumbility/Phoenix1
```

**Phoenix 1 is a two-page section.** No carryover, no PUMBILITY ladders
(`Phoenix2PumbilityTitle` is Phoenix-2-only), no pool selector — one pool, and splitting it would
invent a stat. The frame drops the third chip rather than showing an inert one, exactly as the
official frame drops What It Takes; `/Pumbility/Phoenix1` reached by URL redirects to Play.

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

### 3.3 Targets — "what to play next"

Ranked by projected gain.

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
scores are, and how spread they were* — an evidence line, not an attribution line. What the page
may still show, separately and unattached to the projection, is the player's own thumbprint as
descriptive data (§4.3). **Still open** — see §9.

Density trio via `Density__Pumbility`, governing the targets only, using the site's standard
control — a `MudButtonGroup` of `ViewComfy` / `GridView` / `TableRows` icon buttons, the same
one the tier list carries.

**Pagination** sized by density (Comfortable 24, Compact 60, Table 50) — one page size would be
wrong at two of the three. A shorter list **clamps** the current page rather than resetting it,
so a density flip or a pool switch keeps you roughly where you were instead of throwing you back
to the top.

### 3.4 The pool board

Board rows wearing `.olb-rank-card` — a ranked list of entities gets the leaderboard skin and **no
density toggle** (rule 5). The bar renders as a rule in the list, with the waiting room ghosted
beneath it.

Open, not folded (D14). It sits on Your Pool beside the curve, which is the same data in the other
form — and the frame's bar card and the highlighted 50th row are now visibly the same number on the
same screen, which they never were across a fold.

### 3.5 The what-if calculator — deleted

Moved to the rating-calculator overhaul (D18). What it loses in transit is worth naming: today it
prices against **your bar**, so it answers *"what would this add"*. `/RatingCalculator` already
builds a `PumbilityScoring(Phoenix2, false)` config but holds no pool, so it can only answer *"what
is this worth"* until it picks up the pool read.

### 3.6 Where your PUMBILITY comes from

A band on Your Pool. **The split is exact, not modelled.** A pool entry is
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
against grade multipliers of 0.90–1.50, so a plate is worth at most **1.3% of a chart**.

Two devices carry that without exaggerating it:

- **The stack is true to scale.** The plate segment renders as a hairline because it *is* a hairline.
  It is never widened to be visible.
- **The plate gets its own magnified rail**, running from Rough Game on all fifty to Perfect Game on
  all fifty, so the sliver can be read on a scale where it has room. The line under it prices the
  ceiling: Perfect-Gaming every chart in the owner's pool is **+174, or 0.97%** — about **twelve
  chart swaps**. On Phoenix 1 that line reads *nothing*, flatly.

⚠ **The reference is load-bearing and the note has to stay honest.** Pure base measures from ×1.00,
which on Phoenix 2 is exactly what a **D** pays — every grade above it adds and only an F (×0.90)
subtracts, so the Score segment carries a floor all but the worst play gets.
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

- **A thin pool.** *"Your pool holds 7 of 50. BRONZE asks 200.00 a chart across fifty."* Under what
  any level-10 chart pays at AA, so until the pool is full the ask is the pool itself. This is the
  common case for months after a mix launch, not an afterthought.
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

The rule is `Rank(s) > 0`, which subsumes all four. Nothing legitimate is caught: the worst grade
multiplier is ×0.4 on Phoenix 1 and ×0.90 on Phoenix 2, and `MinimumScore` is 0 in both PUMBILITY
configs. `FolderTitleTrack.Compute` already builds its pool this way (`if (value <= 0) continue;`) —
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

## 4. The projection engine

Rebuilt from scratch and measured before anything was written. **§4.1 is the formula.**
Everything after it is the evidence, including four approaches that were tried and rejected —
read §4.3 before re-proposing any of them.

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

### 4.7 The peer cohort pools Phoenix 1 and Phoenix 2

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
in R5 divides by fifty — an average over a pool holding a passed S9 is wrong by construction.

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
be the one that waits — land on Your Pool first and you sit behind the `PatienceCard` for a result
that page never renders. An inelegance, not a regression, and the reason the card belongs to the
frame rather than to Play.

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
4. **A title you hold is not a projection.** The rails on Your Pool state what you have and what the
   next rung asks. The chips on Phoenix 1 state where a record *would* land you. The one word
   between them is the whole difference, and it is why D17 refuses to spend "projected" twice.

## 9. Open

- **The page's truth horizon.** Bias is strongly horizon-dependent (the old formula ran +207 at 30
  days against −2,300 at a year), and §4.1's `p65` is fitted at **one year**. *"What would you score
  if you played this now"* implies a much shorter horizon. Nobody has ruled on it, so every projected
  score on the page is calibrated to *what you would eventually score*, and reads **high** for a
  near-term reading. `CohortEstimator.Quantile` is one constant in one pure class — re-fitting is a
  one-line change and a re-run, and nothing built on top of it has to move.
- **The harness was never ported.** §4.2's numbers come from the scratchpad scripts in
  `Downloads/pumbility-harness/` on levels 19–21. Nothing in the repo can reproduce them, and the
  ratchet that would catch a drift between the harness and `CohortEstimator` does not exist. Its home
  when it lands is `ScoreTracker.ExplorationTests/Pumbility/`, config-gated like the catalog probes,
  with one pin fact asserting the real estimator and the harness's agree — a genuine equivalence
  check, since `CohortEstimator` is pure. Metrics ρ ahead of MAE: §4.5 is the cautionary tale, where
  every constant moved MAE and none moved ρ.
- **Two data limits, stated not fixed.** `PlayerHistory` begins **2024-06-04**, so a score older than
  that resolves to the player's earliest known level and the growth weight under-states how much they
  improved on the site's oldest records. And `PhoenixRecord` stores only the current best, so a chart
  improved after the backtest cutoff reads as unplayed at it; the journal's 29,153 multi-event pairs
  are the subset with true history.
- **The "why" line on a target row.** §3.3. The estimator carries no skill term, so badge chips would
  assert a causal path that does not exist. Evidence line instead, or nothing?
- **Does the page show the thumbprint as descriptive data?** It is a real, stable trait (§4.3)
  with genuine display value and zero predictive value. If yes it needs its own placement and
  copy, and must never sit adjacent to a projection where it reads as the explanation.
- **A Singles/Doubles filter on Phoenix 1 targets** was raised and not decided. It is additive
  and does not invent a stat, unlike a Phoenix 1 pool split.
- **Is ρ ≈ 0.66 good enough to print a point estimate?** Nothing tried moved it far, and the
  ceiling looks structural rather than tunable. If the answer is no, the target list shows
  ranges rather than numbers — a page decision, not an algorithm one.
- **What the tier list's Skill source is worth**, which this harness cannot measure — its output
  is a difficulty ordering with no equivalent ground truth. The 20% degradation figure for
  suggestions comes from separate work and is not reproduced here.
- **N5 (the badge re-key) is now unblocked from this page** and should ship on its own schedule
  for the tier-list blend. It is no longer a dependency of anything here.
