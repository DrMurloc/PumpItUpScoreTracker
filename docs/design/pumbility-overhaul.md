# PUMBILITY page (`/Pumbility`) — design

Rebuilds `/Pumbility` from a wall of chart cards into a planner, and replaces the projection
engine underneath it. Mocked against real data from the owner's own account before any code;
the mock is the visual authority for §3.

Mock: https://claude.ai/code/artifact/5dded5e4-03d7-4d70-80ed-d5ecfac68aa2

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
| D7 | **The top 50 is collapsed and below the fold.** It is a reference, not the answer |
| D8 | **No world-rank chip.** Offered and declined |
| D9 | **"Kind" is not a column.** It restated the cell beside it — see §3.3 |

## 3. The page

### 3.1 The stack

```
your number  ·  the bar  ·  the pool curve        ← above the fold
what Phoenix 1 is worth here                       ← Phoenix 2 only
what to play next  (density trio)
your top 50                                        ← collapsed
what would a clear be worth?                       ← collapsed
```

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

Ranked by projected gain. Each row: jacket, difficulty bubble, chart name, what you hold now →
what you are projected to reach, the gain, and the badge chips explaining why.

**Two kinds, distinguished by the row itself and never by a column.** A chart with a grade in
the *You have* cell is an upgrade; a chart with a dash is new. A "Kind" column restated that on
Phoenix 1 and read the same value down every row on Phoenix 2. The coloured left rail stays as a
scan affordance, paired with the grade cell so nothing rides on colour alone (rule 8).

**In Phoenix 2 the list mixes two kinds of evidence, and says which is which.** A chart from
the player's Phoenix 1 pool that they have not scored here is not estimated — the score is on
record and repricing it is arithmetic. Those rows **replace** any peer estimate for the same
chart (owner, 2026-08-06: *"there is no better data than the actual scores you had before"*),
carry a **Phoenix 1** label, and are the only signal that works at a mix launch when there is
no peer data to estimate from. The label renders only where the list actually mixes sources —
on Phoenix 1 every row is a peer estimate, so it would be a column repeating itself.

⚠ **The "why" line is OPEN, and the mock's version is now dishonest.** The mock renders badge
chips ("▲ Anchor Runs · ▼ Twists") as the explanation for each target. That was drawn when the
estimator weighted charts by skill. **It no longer does** — §4.1 carries no skill term — so
printing badge chips beside a projection would claim a causal path that does not exist, and the
site does not do that (rule: say what you cannot compute).

What the estimator can honestly explain is *how many peers it heard from, how recent their
scores are, and how spread they were* — an evidence line, not an attribution line. What the page
may still show, separately and unattached to the projection, is the player's own thumbprint as
descriptive data (§4.3). **Needs an owner call before C6.**

Density trio via `Density__Pumbility`, governing the targets only.

### 3.4 The pool board

Collapsed by default (D7). Board rows wearing `.olb-rank-card` — a ranked list of entities gets
the leaderboard skin and **no density toggle** (rule 5). The bar renders as a rule in the list.

### 3.5 The what-if calculator

Unchanged in behaviour, moved to the bottom and collapsed. It is a what-if tool, not an answer;
today it sits between the pool and the projections, in the middle of the page's argument.

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
| Cohort window ±1.0 | **already optimal** — ±0.5 costs +3.1%, ±2.0 +3.1%, ±3.0 +12.4% |
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

## 5. Phoenix 2 carryover

Only on the Phoenix 2 view; Phoenix 1 has one pool and no per-type board, so offering a split
there would invent a stat.

Every Phoenix 1 score repriced under `Phoenix2PumbilityScoring` — singles priced one level up the
base curve, sub-10 charts at zero, broken plays at zero — then the top 50 taken for each of All,
Singles and Doubles.

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

**A chart with no Phoenix 2 appearance is a fact, not a target.** It is stated once in the fact
tile and never appears in the target list — you cannot go and play it.

## 6. Technical scope

### 6.1 Verticals and layers

The estimator's inputs collapsed once §4.3 removed every skill term. What it now needs is: peer
scores on a chart, peer competitive levels, each peer's level *when they set that score*, and
chart scoring levels. Three of those already have homes.

| Vertical / layer | Change |
|---|---|
| **PlayerProgress** | **Owns everything.** `PumbilityProjectionSaga` replaced by the §4.1 estimator. `ProjectPumbilityGainsQuery` gains a pool scope (`ChartType?`). New `GetPumbilityPageQuery` — **one read for the page**, so the hero does not assemble itself from six dispatches. New `ProjectPhoenix2CarryoverQuery` (§5). Also owns `IPlayerHistoryRepository`, which is where the growth weight's raw material already lives |
| **ScoreLedger** | **Nothing new.** `IScoreReader.GetScores(mix, users, type, lo, hi)` already returns exactly the cohort read step 1 needs |
| **ChartIntelligence** | **Nothing.** The estimator no longer consumes `PlayerSkillDeviations`. ⚠ N5 (the badge re-key) is still worth doing on D1 and for the tier-list blend — it is simply **no longer this page's dependency**, and should ship on its own schedule |
| **Catalog** | Chart metadata and `ChartScoringLevel` reads only. **No badge query.** `GetChartBadgeChipsQuery` is needed only if the page shows the thumbprint as descriptive data (§3.3, open) |
| **Domain** | `PumbilityProjection` loses `SkillAdjustments` entirely; `SkillAdjustmentRecord` is **deleted**, not re-keyed. ⚠ Both live in `Domain/Records/` and should **move to `PlayerProgress/Contracts/`** — a vertical's projection contract has no business in shared Domain |
| **SharedKernel** | Untouched. N8's deletion of `Skill`/`SkillCategory` is the nuke's tail and out of scope here |
| **Data** | **Nothing — no migration.** See §6.3; the growth weight is derived at read time from an existing table |
| **Web** | The page and its components (§6.2) |
| **ExplorationTests** | The harness (§6.4) |

**No new ports.** Every cross-vertical read is an existing published contract query or an
existing Domain port.

### 6.2 Classes

**PlayerProgress**
`Contracts/Queries/`: `ProjectPumbilityGainsQuery` (+ pool), `GetPumbilityPageQuery` →
`PumbilityPageRecord`, `ProjectPhoenix2CarryoverQuery` → `Phoenix2CarryoverRecord`.
`Contracts/`: `PumbilityProjection` relocated from Domain and slimmed.
`Application/`: `PumbilityProjectionSaga` (rewritten), new `PumbilityPageSaga`.
`Domain/`: new **`CohortEstimator.cs`** — pure, no I/O: takes (peer scores, peer levels now,
peer levels at record, τ, q) and returns the estimate. The harness and the app share this one
implementation, which is what stops them drifting.
`Infrastructure/`: `EFPlayerHistoryRepository` gains a **bulk** read —
`GetHistory(mix, userIds, ct)` — so a cohort's level series arrives in one query instead of
N. Additive to `IPlayerHistoryRepository`, which PlayerProgress already owns.

**Web** — `Pages/Progress/Pumbility.razor` rewritten. New in `Components/Pumbility/`:
`PumbilityHero`, `PoolCurve`, `PoolSelector`, `TargetCard`, `TargetStickerSheet`, `TargetTable`,
`PoolBoard`, `CarryoverPanel`, `GainCalculator`. Reuses `DifficultyBubble`, `LetterGradeIcon`,
`SongImage`, `ScoreBreakdown` (rule 3 — no page-local restyles).

**Retired**: the eight `GetTierListName`/`GetTierListOrder` band helpers, the
`TierListProcessor.ProcessIntoTierList("PUMBILITY", …)` call, and the whole skill-adjustment path
in the saga.

### 6.3 No migration — the join is per-peer, not per-score

An earlier draft of this doc specified a `CompetitiveLevelAtRecord` column on
`scores.PhoenixRecord`, on the reasoning that resolving "their level when they set it" at query
time meant joining every candidate score against `PlayerHistory`.

**That was wrong about the shape of the join.** A projection touches the peers inside a ±1
competitive band — on real data, 150–300 players — and `PlayerHistory` holds ~27 rows per player
per mix (40,795 rows over 1,509 Phoenix users). So the whole history for a cohort is **one read
of roughly 8,000 narrow rows**, bisected in memory per score. That is an order of magnitude
smaller than the cohort score read the estimator already performs.

Storing it would also have coupled the write path across verticals: `PhoenixRecordEntity` belongs
to **ScoreLedger**, while competitive level is **PlayerProgress** data, so every import path
would have had to ask another vertical for a number at insert time — to denormalize something
already derivable.

So: **no migration, no backfill, no nullable-column semantics, no write-path change.** One
additive method on an existing port (§6.2).

⚠ **`PlayerHistory` begins 2024-06-04.** A score older than that resolves to the player's
earliest known level, so the growth weight *under-states* how much they have improved on the
site's oldest records. Same limitation the column would have had; stated, not fixed.

### 6.4 The harness

`ScoreTracker.ExplorationTests/Pumbility/`, config-gated on the existing
`CatalogProbe:ConnectionString` pattern. Never CI, never a feature guarantee.

**Port the scratchpad harness rather than re-deriving it** — `Downloads/pumbility-harness/`
holds the scripts that produced every number in §4, including the rejected arms. The C# version
must reproduce §4.2's table on the same cutoff before it is trusted.

**One pin fact** runs the real `CohortEstimator` against the harness's own for a handful of
players and asserts they agree. Because `CohortEstimator` is pure, this is a genuine equivalence
check rather than an end-to-end smoke test.

**Metrics: ρ ahead of MAE**, plus signed bias and coverage. The page's job is ordering what to
play next; §4.5 is the cautionary tale — every constant in the old formula moved MAE and none
moved ρ.

⚠ **Known bias, stated not fixed.** `PhoenixRecord` stores only the current best, so a chart
improved after `T` reads as unplayed at `T` even if it was played earlier. The journal's 29,153
multi-event pairs are the subset with true history.

### 6.5 Tests

| Suite | What |
|---|---|
| `ScoreTracker.Tests/DomainTests` | **`CohortEstimatorTests`** — the weighted quantile (including a left-skewed fixture where mean and p65 diverge), the growth weight's self-conditioning (flat player → all weights 1.0), the ±1 gate, empty-cohort behaviour |
| `ScoreTracker.Tests/ApplicationTests` | `PumbilityProjectionSagaTests` rewritten: pool scoping, no skill dependency, carryover repricing |
| `ScoreTracker.Tests.Components` | Page and components with mocked data — hero prints the bar, curve renders 50 + waiting room, targets shed by density, carryover hides on Phoenix 1, pool selector re-ranks everything |
| `ScoreTracker.Tests.Integration` | The bulk history read against a real migrated DB |
| `ScoreTracker.Tests.E2E` | Nothing new — not a critical whole-workflow path (owner's granularity ladder) |
| `ScoreTracker.ExplorationTests` | §6.4 |

### 6.6 Build order

| # | Commit | Contents | |
|---|---|---|---|
| C1 | Bulk history read | `IPlayerHistoryRepository.GetHistory(mix, userIds)` + EF implementation. Additive, no schema change | ✅ |
| C2 | `CohortEstimator` | Pure domain class + `DomainTests` | ✅ |
| C5 | Saga rewrite | `PumbilityProjectionSaga` on `CohortEstimator`; skill path deleted; `PumbilityProjection` relocated + slimmed; pool scoping; scoping moved to **scoring** level | ✅ |
| C6 | Page read model | `GetPumbilityPageQuery`, `ProjectPhoenix2CarryoverQuery`, `PumbilityPageSaga` | ✅ |
| C7 | The page | Rewrite + `Components/Pumbility/*` + density + the responsive ladder + bUnit facts | ✅ |
| C8 | l10n | 36 keys × 9 locales, alphabetical, two case collisions caught | ✅ |
| C9 | Docs | This section, `ARCHITECTURE.md` page row | ✅ |
| **C3** | **The harness** | Port from `Downloads/pumbility-harness/`, reproduce §4.2, pin fact against C2 | **not done** |
| **C4** | **Re-fit the calibrations** | p65 against the page's stated truth horizon (§4.4), confirmed at levels 17–19 and 22–24 | **not done** |

⚠ **C3 and C4 did not ship and the page is live without them.** C4 was specified as a gate on
C5 onward and that gate was not honoured — the build ran C5–C9 while the quantile is still the
one fitted to a **one-year** truth horizon. Consequences, stated plainly:

- Every projected score on the page is calibrated to *"what you would eventually score"*, not
  *"what you would score now"*. Measured on the old formula, that difference was worth ~2,500
  points of bias. The direction is known — projections read **high** for a near-term reading.
- `CohortEstimator.Quantile` is one constant in one pure class, so re-fitting is a one-line
  change plus a re-run. Nothing built on top of it needs to move.
- §4.2's numbers are from the scratchpad harness on levels 19–21. Until C3 ports it into
  `ExplorationTests`, nothing in CI or in the repo can reproduce them, and the ratchet that
  would catch a drift between the harness and `CohortEstimator` does not exist yet.

### 6.7 What the build changed about the design

- **The migration was dropped** (§6.3). The join is per-peer, not per-score.
- **Scoping moved from printed level to scoring level.** A chart printed 24 and scored like a 21
  belongs in a level-20 player's window; the reverse was cluttering the list. Pinned by
  `AChartIsScopedByItsScoringLevelNotItsPrintedLevel`.
- **`PumbilityProjection` gained `Evidence`** — peer count, effective peers after growth
  weighting, and the 10th-to-90th spread — because the page still owed the player a "why" and
  the honest one is what the estimate heard, not what it attributed (§3.3).
- **`ProjectionEvidence.Spread` and `EffectivePeers` are computed but not yet rendered.** The
  comfortable card prints the peer count only. The other two are the raw material for a
  confidence treatment nobody has designed.

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

## 8. Honesty boundaries

1. **A projection is a projection.** The targets list says what you are *projected* to reach, and
   the page must not print it in the same register as a score you actually hold.
2. **The carryover is not your Phoenix 2 PUMBILITY.** It is what your Phoenix 1 record would be
   worth here. The hero says so, and the count of scores you actually have in Phoenix 2 sits
   beside it.
3. **The bar moves under you.** Every gain figure is against the bar as it stands now; clearing
   one target raises it for all the others. The page should not imply the gains sum.

## 9. Open

- **The page's truth horizon — blocks C4.** Bias is strongly horizon-dependent (the old formula
  ran +207 at 30 days against −2,300 at a year), and §4.1's `p65` is fitted at one year. *"What
  would you score if you played this now"* implies a much shorter horizon. Nobody has ruled on
  it, and the quantile cannot be finalised until someone does.
- **The "why" line on a target row — blocks C7.** §3.3. The estimator carries no skill term, so
  badge chips would assert a causal path that does not exist. Evidence line instead, or nothing?
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
