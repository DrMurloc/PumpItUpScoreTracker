# Chart similarity graph — design & settled formula

Companion to [chart-details-overhaul.md](chart-details-overhaul.md). A **database graph** (owner
call, 2026-07-14: nodes = charts, edges = evidence-weighted similarity — no visualization in this
wave). Owned by **ChartIntelligence**, following its existing nightly `Recalculate*` pattern.

**Status (2026-07-15).** V1 settled at the 07-15 workshop after a full night of calibration
against real data. This is a **rewrite of the shipped B1–B2 formula, not an extension** — three of
its five signals leave the score entirely. Read §"Rejected, with evidence" before changing
anything: nearly every alternative here was tried, measured on the dev corpus, and killed for a
recorded reason.

---

## 1. Three questions, three mechanisms

The shipped formula flattened to `sd = 0.030` across an entire folder — it could not rank
anything. The cause was structural, not tuning: **it was averaging a chart property with a viewer
property and calling the result one number.** V1 splits them (owner, 2026-07-15):

| Question | Nature | Mechanism |
|---|---|---|
| **Which charts have matching profiles?** | property of the *chart pair* | **precalculated** — skill + intensity, nightly |
| **How hard will it be?** | property of the *viewer* | **ordering**, at read time |
| **Which charts do I want to see?** | property of the *request* | **filters**, computed live |

Corollaries, all of which the data had been shouting:
- **Difficulty is personalized.** Community tier lists and scoring level are a sane *default* order
  for anonymous readers; the personalized tier lists are what actually answer "will this be hard
  **for me**". It is never a match criterion.
- **Metadata is a filter, never a score.** "By this artist", "at this BPM", "from X mix or newer",
  "in this level range" are things a user *asks for*. Meta out-discriminated both Players and
  Difficulty in S20 and D23 precisely because it is filter-shaped (binary, high variance) — that
  was the tell, not a mystery.
- **Similarity means "same kind of problem", full stop.** Not "similar and also equally hard".

---

## 2. Gates (hard filters, before any scoring)

- Same mix (the graph is per-mix; edges never cross mixes).
- Same chart type (Single↔Single, Double↔Double). **Co-Op excluded v1.**
- Level within **±2 folders** of the anchor — **a reach limiter, not a difficulty statement**.
  Distance inside the window costs **nothing** (owner, 2026-07-15: the folder level is Andamiro's
  *passing* level, applied inconsistently; it is not a truth source). The 0.15-per-folder affinity
  penalty this replaces was the single largest reordering force in the old formula and it fired
  blind — on Rush-More D23 it buried Meteo5cience (3rd-best skill+intensity in the folder, and the
  owner's pick for one of the best matches in the list) to 12th for being D22, alongside two D22s
  that deserved it. Folder level cannot tell those apart, so it must not try.
  **Folder, not scoring level:** the gate is baked into the precalc, so it must be cheap,
  universal, and dependency-free. Scoring level is a nightly-job output — gating on it would
  re-couple similarity to the analytics chain it currently escapes (§7), and ~46% of the catalog
  has none. Scoring level does its work in the **sort** (§5), where it is free.
- **Same-song charts excluded** — siblings are navigation (the hero's sibling bubbles), not
  discovery; they would otherwise dominate every shelf.

---

## 3. The formula

```
S_skill      = Bray-Curtis over gamma-shaped raw badge coverage        (§3.1)
S_intensity  = geometric mean over three z-scored scalars              (§3.2)

score        = S_skill^0.75 · S_intensity^0.25
```

Both signals are mandatory: a chart missing either gets no edge. In practice they co-occur — both
come from the same piucenter crawl (4,411 charts each) — but the old `nonMetaAvailable >= 2` gate
meant "some real evidence" when there were four signals and now means "everything". Say what it
means.

Mandatory is also what makes the score that clean product: the weights sum to 1 and both terms are
always present, so there is nothing to renormalize and no mean to take. Skill leads 3:1 because two
charts that demand different things are not alike however similarly they exhaust you.

### 3.1 S_skill — Bray-Curtis over gamma-shaped raw badge coverage

Vector over piucenter's **own** badge vocabulary via `GetChartBadgeCoverageQuery` (~29 badges,
banked as measured). Component = fraction of the chart's segments carrying that badge.

```
cov'    = cov ^ γ                                   γ = 2 (CoverageGamma)
S_skill = 1 − Σ|cov'_a − cov'_b| / Σ(cov'_a + cov'_b)          over the UNION
```

A badge one side never carries is zero coverage — a real difference, not missing data.

**Why mass normalization.** Dividing by the pair's own coverage mass rather than a dimension count
makes the profile's *shape* decide the score: a badge neither chart carries costs nothing on
either side of the ratio (shared absence is free, and the sparse tail cannot dilute a real gap); a
badge one chart is built on and the other never touches contributes its whole magnitude; shared
high coverage lands in the denominator and argues *for* the pair.

**Why gamma.** The common badges average ~0.3 coverage across the corpus (`doublestep` .38,
`jump` .36, `jack` .33, `twist_90` .33) — every chart runs, jumps and jacks a bit. That is the
baseline, not a property. Squaring keeps a 0.9 badge at 0.81 while a 0.3 drops to 0.09, so a badge
a chart is **built on** outweighs one it brushes past **52:1 instead of 7:1**. γ = 1 disables it.

**Explainability is free** — and this is load-bearing, see §4:

```
S_skill = Σ 2·min(cov'_a, cov'_b) / Σ(cov'_a + cov'_b)
```

`min(a,b)` **is** the shared coverage of that badge. The match reasons fall out of the formula;
nothing extra is computed or stored to explain a match.

### 3.2 S_intensity — geometric over three z-scored scalars

From piucenter step analysis (`nps`, `sustain_time`, `time_under_tension`) plus `Song.Duration`:

```
susFrac    = sustain_time / duration                              the grind
burstFrac  = (time_under_tension − sustain_time) / duration       the spikes
nps                                                               does it feel fast

per dimension d:  z_d  = z-score within the (type, level) cohort   (population sd)
                  s_d  = clamp01(1 − |z_dA − z_dB| / K)            K = 3

S_intensity = exp( Σ w_d·ln(max(s_d, 0.01)) / Σ w_d )
              burstFrac 0.40 · susFrac 0.40 · nps 0.20
```

**Why burst and sustain weigh the same.** They are the two halves of one decomposition, and the
owner's taxonomy turns on both ends of it: Gargoyle is all sustain, Viyella's is all burst, and
neither is the senior partner. NPS trails at 0.20 because it is the corroborating axis, not a
verdict — a chart's feel-fast is real evidence but it is the thing most likely to coincide by
accident (Conflict and Viyella's, 10.7 against 10.5). Weights renormalize over whichever dimensions
a pair has, so a chart with no duration still scores on NPS alone.

**Why decompose tension.** `sustain_time ⊆ time_under_tension`, **always** — Gargoyle proves it at
`sustain 362 / TUT 362`. So the old `susFrac`/`tensionFrac` pair double-counted sustain and gave
burst no dimension of its own. `susFrac` + `burstFrac` are the two halves, independent, and they
map onto the owner's taxonomy directly (§8).

**Why geometric, and why NPS is only 0.20.** NPS is **orthogonal** to the stamina axis, not
redundant with it — and that is a feature, not a reason to drop it. It is what correctly saw
through Altale's 90 BPM (12.0 nps) and TRICKL4SH's 220 BPM (11.5 nps) when nominal BPM said they
were 60–75 apart. But averaging lets the dimensions that agree pay for the one that does not.
Measured on the real D21 cohort (n=128, 2026-07-15):

| | NPS | susFrac | burstFrac | TutFrac |
|---|---|---|---|---|
| Slapstick Parfait D21 | 10.70 | .109 | **.118** | .227 |
| Horang Pungryuga D21 | 10.70 | .095 | **.516** | .611 |

Same NPS to the decimal, sustain within .014 — and one of them spends half its length bursting.
`s_nps 1.000 · s_sus 0.958 · s_burst 0.000`. **The old formula scored this pair .751 and would have
shipped it**; decomposed and geometric it scores **.156**. A flat arithmetic mean over the *same*
decomposed dimensions still reads .583, so the decomposition and the geometric mean are both
load-bearing — neither fixes this alone. Across the cohort, **34 pairs that scored ≥.75 now score
<.55**.

**Why z-scores here and absolute coverage in skill — the asymmetry is deliberate.** Low intensity
is a *valid property*: two charts both unusually chill for their level genuinely are alike, so
z-scores are centred and `|Δz| ≈ 0` → similarity 1.0. Low skill coverage is *absence*: two charts
both lacking brackets are not thereby alike, so Bray-Curtis makes shared absence free. Never
gamma or mass-normalize intensity; never z-score skill.

**K = 3 is provisional.** Typical `|Δz|` is ~0.6, so the divisor only uses a fifth of its range —
but the bug was dilution, not scale, and geometric fixes dilution. Dropping K *and* going geometric
at once would prune twice and teach us nothing. One-constant retune after a calibration run.

### 3.3 Why geometric and not a weighted mean

An arithmetic mean **squares its weights** — `Var(Σw_i·S_i) = Σw_i²·Var(S_i)` — so a signal at 0.25
weight contributes a *sixteenth* of its variance and agreeing signals bury the one that dissents.
Measured on S20, the five old signals' spreads ran through that algebra to a combined
`sd ≈ 0.062`, and the floor truncated it to the **0.030** actually observed. That is why Players at
0.251 with **478 shared scorers** could not kill an edge, and why Meta — the weakest prior, at 0.10
— out-discriminated Players *and* Difficulty. In log space a low signal drags the score down and
cannot be outvoted: "alike in **every** way that matters", not "alike on average".

`SignalFloor = 0.01` exists because `ln(0)` is `−∞`, which would hand any single zeroed signal a
hard veto it has not earned.

---

## 4. Match reasons (explainability is the product)

**Owner, 2026-07-15, binding:** *"I want to see specifically the skills and intensity metrics that
the chart matched on — saying 'skills match' is going to be confusing as shit and make people think
it's broken."*

Per edge, persist the top shared badges — the `min(cov'_a, cov'_b)` terms from §3.1, rendered as
raw (un-gamma'd) coverage so the number means something to a human:

> **Matched on** · `Brackets 50%` · `Bracket jumps 37%` · `Anchor runs 25%`

The `%` is the **shared** coverage, so "Brackets 50%" honestly means *both* are at least half
brackets. Intensity contributes its own chip in a distinct colour (`NPS 10.7 → 11.9`) — a different
*kind* of reason, and it must not read as a skill.

This is not decoration. Conflict↔Viyella's would have rendered **"matched on: NPS (10.7 vs 10.5)"**
— and a human reads that as a coincidence in two seconds. The chip catches what three separate
analyses missed.

---

## 5. Ordering (difficulty), filtering (metadata)

**Order by** — a runtime concern over the precalculated set:

| Option | Source |
|---|---|
| **No sorting** | the match order itself, strongest first |
| **Pass Difficulty** *(default)* | the community Pass tier list, projected onto the level scale |
| **Score Difficulty** | scoring level, which already *is* an effective level |

Pass and Score are both offered because they genuinely differ — a chart can be a wall to clear and
generous to score, or the reverse. "No sorting" is the only option that shows the true match
order; every difficulty sort reorders away from it.

**Personalized** is a checkbox *across* the two lenses, not a third and fourth option: it is a
property of the difficulty question rather than a different answer to it, and it has nothing to
modify while nothing is being ordered (so it only appears once a lens is picked). It swaps the
community list for the reader's own blend, and needs a sign-in; the community lenses never do.

A tier list ranks charts *within* one folder, so its `Order` is 0..N per folder and its category
means "hard for a D21" rather than "hard" — neither survives a shelf that reaches across folders.
Nudging each chart's own level by its category is what makes them commensurable, deliberately by
under half a level (`EffectiveLevel`): the list is saying a chart is misplaced *within* its folder,
never that it belongs in a different one. Charts the list cannot place sort last rather than
pretending to be easy.

**Filters** reduce the *target list* and then **recompute live** — they never filter the stored
top-20 (which would trivially return zero). The reach must be stated: *"Compared **30 charts**
(Folder D18–D22) — **1 match**."* — that reads as a narrow filter rather than a broken feature.

**Four dimensions, and that is the whole set** (owner, 2026-07-15). Two the chart declares and two
measured from it:

| Filter | Seeded at | Unit |
|---|---|---|
| **Folder** | anchor ±2 | whole levels |
| **Scoring level** | anchor ±2 | tenths |
| **BPM** | anchor ±10 | whole BPM, and a range **overlaps** rather than contains — a 150–190 chart is "at 180" |
| **NPS** | anchor ±1 | tenths |

Every one is a **range**, and every one is something the reader can already see on this page. Step
artist and debut mix used to be here and are gone: neither is a range, and "charts by SPHAM" is a
question the chart browser answers better than a similarity shelf can.

Three rules the panel exists to enforce:

- **Nothing is on by default.** An unfiltered shelf is already the answer to "what plays like this",
  so four ranges pre-applied would be four decisions in front of a reader who wanted none.
- **Switching one on opens it on the anchor's own neighbourhood**, because "near this chart" is the
  only range anyone starts from — never a bare track to go find the anchor on.
- **The live count is computed from the same numbers the server filters on**, so what it promises is
  what comes back. It costs no database: the charts and their crawl metrics are cached
  repository-side and the scoring levels per circuit, so the panel assembles the pool once (lazily,
  on first open — most readers never open it) and a thumb-drag counts an array in memory.

**Scoring level always answers.** A chart nothing has measured filters at its listed level, which is
what `GetChartScoringLevelsQuery` reports for it everywhere else — otherwise switching the filter on
would silently drop the ~13% with no measurement, and the count would not be the list. This is
deliberately *not* the same reading as the gate's `WithinReach` (§2), which stays measured-only: the
gate asks whether there is evidence two charts score alike, a filter asks whether a chart is in the
range someone pointed at. Feeding the fallback to the gate would apply the ±1.25 test to the charts
that currently escape it on a folder-only pass and quietly re-cut every chart's suggestions. **NPS
has no such fallback** — nothing is listed to fall back to, so a chart without one is excluded by an
NPS filter rather than admitted at unknown speed.

**The D18→D23 case is a live call.** "I liked this D18, what D23s are like it" is a real use case
and is deliberately **outside** the precalculated ±1 window; it computes on demand. The folder
track spans what the pool actually holds, so the reach is "every level that exists", not a constant.

### 5.1 The least similar charts

`GetLeastSimilarChartsQuery` — the **6** charts in reach that pose the *least* similar problem,
**worst first** (owner, 2026-07-15: *"do top (bottom) 6 matches"*). A novelty (*"that'll just be
used for memes/fun"*), so it is not held to the shelf's standard: nothing is stored for it, and it
needs to be funny and defensible rather than right. Computed live because the graph banks the twenty
**nearest**, and the furthest are by construction what ranking never keeps.

It lives as a toggle **inside the filter panel, under a rule, after the ranges** (owner:
*"technically not a filter but i don't want it top level"*). Four consequences worth keeping
straight:

- **It is a mode, not a filter.** It asks the opposite question rather than narrowing this one.
- **It cannot honour the ranges**, because they are picked from everything in reach — so switching
  it on disables the four toggles rather than leaving them implying they apply.
- **It bypasses the floor** — but nothing else. They are the furthest charts in reach, so every one
  is under any bar worth having and the normal split would file the whole joke under *"Didn't quite
  make the cut"*. They still take the **difficulty lens** like any other list: "least similar first"
  is just another match order, and a lens reorders away from it exactly as it does on the real
  shelf.
- **Two flags, not one** — what the panel has *selected* vs. what the shelf is *showing*. Without
  the split, a sort click made after ticking the box but before pressing Apply re-files the real
  matches as opposites.

---

## 6. Degradation and near-misses

The floor is a **render-time constant, not a storage decision** (§7) — which makes both of these
free, and lets the floor be retuned without a job run.

- **Near-misses.** Even with matches, show *"Didn't quite make the cut"* — the rows below the
  floor. People are curious, and it is the same stored data.
- **No matches.** *"No significant matches — here's the closest we could find."* Never an empty
  box. In the filtered case say what was searched: *"Compared 128 charts at 195–225 BPM — none
  cleared the bar."*

---

## 7. Storage & compute

- **Table `ChartSimilarity`** (ChartIntelligence contribution, internal entity):
  `(MixId, ChartId, SimilarChartId, Score, SignalsJson, ComputedAt)`, PK
  `(MixId, ChartId, SimilarChartId)`. `SharedScorers` is dropped with Players. `SignalsJson`
  carries skill, intensity, and the shared-badge breakdown — a blob because it is read whole,
  only by this vertical, and never queried into.
  **No `Score DESC` index** (this doc used to spec one): the read is a PK-prefix seek on
  `(MixId, ChartId)` returning ≤20 rows, and ordering twenty rows in memory is free. An index
  whose only job is to sort a set that small earns nothing.
- **Top-20 per (mix, chart), floor-free.** ~84k rows at 4,213 charts, against 18k today. Trivial,
  and it is what buys near-misses, the degraded state, and a retunable floor.
- **Nightly** `RecalculateChartSimilarityCommand` — **no longer has a cron dependency.** With
  Players, Difficulty and Meta gone, it reads *only* badge coverage and step analyses, i.e. the
  piucenter crawl. It was pinned at `0 12 * * *` "deliberately after the analytics chain it reads";
  that chain is no longer an input. It also loses `IScoreReader.GetScores` (a ~50k-row folder read
  per level) and the `IPlayerStatsReader` fan-out entirely.
- **Read**: `GetSimilarChartsQuery(chartId, mix)` → a PK-prefix seek returning ≤20 rows. Filtered
  and out-of-range reads compute live from badge coverage — one metrics read plus dictionary math.

---

## 8. Reference fixtures (owner-verified — use these, don't invent new ones)

**The stamina axis.** `TutFrac` orders the owner's entire taxonomy, verified against six
independently-labelled charts (2026-07-15):

| label | chart | NPS | susFrac | burstFrac |
|---|---|---|---|---|
| full stamina (extreme) | Gargoyle - FULL SONG - D25 | 10.0 | **.958** | **.000** |
| sustain + big bursts | Papa Gonzales S22 | 13.0 | .242 | **.440** |
| high stamina | Conflict D21 | 10.7 | .217 | .250 |
| pure burst | Viyella's Nightmare D21 | 10.5 | .133 | .148 |
| big burst moments | MilK S20 | 10.2 | .065 | .154 |
| chill / "fake fast runs" | Hymn of Golden Glory S22 | 13.2 | .033 | .033 |

**Grind and spikes are style; NPS is difficulty.** Measured across every Singles folder 15–25
(2026-07-15) — this is *why* the decomposition carries information NPS cannot:

| level | 15 | 18 | 21 | 23 | 25 |
|---|---|---|---|---|---|
| **NPS** | 7.81 | 10.10 | 11.74 | 13.52 | **14.46** |
| **susFrac** | .140 | .126 | .144 | .141 | **.134** |
| **burstFrac** | .205 | .159 | .184 | .120 | **.154** |

**NPS nearly doubles; sustain and burst do not move.** An S15 is as likely to be 14% sustain as an
S25 — how much of a chart is grind is a *decision the stepmaker made*, not a consequence of its
level. That orthogonality is the whole reason `susFrac`/`burstFrac` earn dimensions of their own,
and it is also why their z-score cohort is nearly a no-op (§9 — do **not** "fix" NPS the same way).

**The two axes are orthogonal — measure both before concluding anything.** Conflict D21 and
Viyella's D21 are the case that proves it (2026-07-15, real D21 badge data):

| badge | Conflict | Viyella's | |
|---|---|---|---|
| `run` | **.333** | **.000** | 16.8% of the skill distance |
| `drill` | **.000** | **.286** | 12.4% |
| `doublestep` | **.000** | **.286** | 12.4% |
| `twist_90` | .333 | .143 | 13.7% |

**Conflict is a run chart with no drills; Viyella's is a drill chart with no runs** — near-disjoint
on what defines each. Yet their `susFrac`/`burstFrac` are within a hair (`s_sus .748 / s_burst
.742`), because *intensity* asks **how much of the song taxes you** (TutFrac .467 vs .281) while
*skill* asks **what the taxing parts are made of**. Both are real; neither substitutes for the
other. `S_skill .6498 · S_intensity .7830 → .6808`.

This vindicates the owner's taxonomy rather than contradicting it: **drills are bursts, runs are
stamina**, and "pure burst" means *low total load, and what load exists is drills*. An earlier
revision of §9 concluded the opposite ("what the labels separate is TutFrac, not composition")
from an intensity-only measurement. **Do not diagnose a pair on one signal.**

- **Gargoyle - FULL SONG - D25 is the sentinel**: `sustain 362 / TUT 362` — every second of tension
  is sustained, zero bursts, and the only chart in the set where `top3:sustained` fires at #1.
- **Hymn of Golden Glory S19 vs S22 is the controlled inversion**: same song, same 121s, adjacent
  rungs — `S19 nps 10.2 / TutFrac .579` (real runs) vs `S22 nps 13.2 / TutFrac .066` (triplet
  "fake fast runs" — faster *and* less taxing). Every variable except the one being measured is
  identical by construction. **Any intensity formula that calls S19 and S22 similar is broken**, and
  an averaging one does.

**The graded shelf.** Rush-More D23 (SPHAM, 160 BPM, 10.7 nps, bracket .50 / anchor_run .50 /
drill .375), owner-graded 2026-07-15 — top 8 all good, precision@8 effectively 100%:

- **Good:** Dream To Nightmare, TRICKL4SH 220, Your Mind, What Happened, Glimmer Gleam, Altale
  (*"really good"*), Pop Sequence, Demon of Laplace, **Meteo5cience** (*"one of the best matches in
  the list"* — the chart the level tax buried to 12th).
- **Bad:** Passacaglia D22, THE REVOLUTION D22 (*"absolutely not"*), Chase Me (*"nope, try again"*).
- **Baroque Virus - FULL SONG - D23:** owner says *"actually perfect match"* — **but piucenter has
  the wrong chart** (the one they hold no longer exists; owner raising it upstream). Its
  `sustain 35 / TUT 156` against Rush-More's `13/33` is independent confirmation. **Do not calibrate
  against this chart.**

---

## 9. Rejected, with evidence (do not re-derive)

Measured on the Curiosity Overdrive ↔ BOOOM!! pair and the S20/D23 folders unless noted.

| Rejected | Why |
|---|---|
| **Cosine** for skill | Scale-invariant — reads only the angle. 10%-brackets and 95%-brackets score a perfect **1.0**. All 11 original fixtures used *identical* vectors, so it was never tested. |
| **Mean over the union** | Rewards sparsity — drifts to 1.0 as dimensions are added. Raw badges + mean = **0.81** vs coarse chips' 0.75: *worse with better data*. |
| **Magnitude-weighted mean** | **0.79** — the weights land in the denominator and cancel. Any weighted-average form has this. |
| **IDF on badge rarity** | Owner: a badge riding many charts doesn't make those charts *about* it — mass normalization already handles it. |
| **The display chips** (`GetChartSkillChipsQuery`) | Max-per-mapped-skill + per-badge thresholds. Collapses `bracket .43 + bracket_jump .43 + staggered_bracket .14` vs a lone `bracket_jump .38` into `0.43 vs 0.38`; a chart with four sub-threshold twist badges gets **no Twists tag at all**. Right for chips, lossy for comparison. Also drops `doublestep` (3,848 charts) and `side3_singles` (1,644). |
| **Arithmetic combination** | Squares the weights → `sd 0.030` across a folder. §3.3. |
| **`top3:` dominance picks** | **59.1% of gated S20 pairs have zero overlap; median 0, mean .079.** Zero overlap is the *norm*, so as a geometric factor it would cost ×0.50 on the majority case. It does track owner grades (9/9 of >0 overlaps were good matches) and it explains Dream To Nightmare (its top3 is `anchor_run, bursty, split` — **no bracket**, despite .625 bracket coverage: *made of* brackets, *about* splits) — so it may return as a **bonus**, never a penalty. `GetChartDominancePicksQuery` was built and deleted; resurrect from git if needed. |
| **Players (residual correlation)** | Out of scope V1 (owner, 2026-07-15) pending a "player type" project. It was honest — Rush-More's pair had a Pearson of **0.26 across 478 shared scorers**, and score-age analysis ruled out era mismatch (56% played both within a month; pair ages 470/478d vs their own 459d average). It just cannot be *acted on* without knowing what kind of player is correlating. Removing it also removes a ~50k-row folder read from the nightly job. |
| **Difficulty as a score** | It is a **viewer** property. Measured pre-floor across all 99,422 gated S20 pairs: `avg .649, sd .150` — a healthy, well-scaled signal. Its post-floor `avg .854, sd .084` was **selection-on-the-outcome** (the floor cuts on the score; the score is made of these signals), not a pedestal. Any signal looks flat through that lens. |
| **Meta as a score** | Filter-shaped. It out-discriminated Players *and* Difficulty in both S20 and D23 at a third of their weight — because binary facts have high variance, not because it knows anything. |
| **Note count in intensity** | Andamiro pads charts toward a per-folder note-count norm, so within-cohort spread is tiny and z-scoring *divides by it*, amplifying differences the padding was designed to erase. Also `NPS × duration = note count` — never independent. |
| **The level-affinity penalty** | §2. |
| **Dropping the z-score cohort for sustain and burst** | **Over-engineered, correctly, and still not worth unwinding.** Measured across Singles 15–25 (2026-07-15), the mean shift per one-level step — the only distance the ±1 gate ever spans — is `NPS 0.512 SD · sustain 0.116 SD · burst 0.194 SD`. When two folders share a mean and a spread the folder terms cancel and `\|z_a − z_b\|` collapses to `\|x_a − x_b\| / σ`, so for sustain and burst the cohort lookup computes a division by a near-constant ~0.15. **But removing it buys nothing**: NPS still needs the cohort, so the folder's scalars are read regardless, and the read is the only thing that costs. It would change every score, invalidate §8's fixtures, and move a floor pinned inside a 0.013-wide window (§10). Real regression risk to delete a constant divisor. |
| **A global SD for NPS** (the tempting corollary) | **No — this is the trap the row above sets.** "If sustain doesn't need the folder, nothing does" is exactly backwards. NPS climbs **7.81 → 14.46 monotonically** across S15–S25; its variance is *dominated* by between-folder differences, so a corpus-wide SD (~2.5) would flatten a real 2-NPS gap to 0.8σ where the folder's own ~1.3 correctly calls it 1.5σ. The folder is the honest ruler for NPS precisely *because* it is redundant for the other two. |
| **Conflict ↔ Viyella's as the geometric's proof case** | This doc used to cite the pair as a false match rescued by coinciding NPS (10.7/10.5). It is not one — but *not* for the reason first recorded here, and the correction matters (§8, "the two axes"). **Intensity cannot separate them**: `s_sus .748 / s_burst .742 / s_nps .953`, and old-intensity .787 → new-intensity .783, so the decomposition and the geometric both find nothing to catch. **Skill separates them decisively**: `S_skill = .6498`, and since skill carries three-quarters of the weight the full V1 score is **.6808**, not the ~.78 an intensity-only reading implies. Measuring one signal and generalizing to "the formula" is the mistake to avoid here. Use Slapstick Parfait ↔ Horang Pungryuga (§3.2) as the *geometric's* proof case, because that pair is one; this pair proves something else. |
| **A BPM plateau (±10, decay beyond)** | Nominal BPM is **noise**: slow songs multiply. Altale 90 BPM → **12.0 nps**; Glimmer Gleam 85 → 8.8; TRICKL4SH 220 → 11.5 — all within ~2 of Rush-More's 10.7, while raw BPM said 60–75 apart. A ±10 band would have deleted two owner-approved matches and promoted THE REVOLUTION (exactly 160 BPM, *"absolutely not"*). **NPS already carries it.** BPM survives only as a filter. |

---

## 10. Open

- **The floor value — CALIBRATED 2026-07-15, and it holds on a knife edge.** The first real run
  of the V1 formula (87,612 edges over 4,409 charts) put **0.55 exactly between the owner's good
  and bad matches** on his graded shelf:

  | # | Rush-More D23's neighbour | score | owner |
  |---|---|---|---|
  | 1 | Meteo5cience | .5859 | *"one of the best matches"* |
  | 2 | Your Mind | .5762 | good |
  | 3 | TRICKL4SH 220 | .5626 | good |
  | | *— floor 0.55 —* | | |
  | 4 | THE REVOLUTION | **.5496** | *"absolutely not"* |
  | 5 | Passacaglia | .5471 | bad |

  **The window is 0.013 wide and THE REVOLUTION misses by 0.0004.** The floor cannot rise above
  .5626 without cutting a good match, nor fall to .5496 without admitting the rejected one. It is
  landing there by luck, not by design: **any retune — K, the weights, γ — puts THE REVOLUTION
  back on that shelf.** Treat 0.55 as pinned, and re-check this table after touching any constant.

  **Coverage:** 139 of 4,426 Phoenix S/D charts (3.1%) have nothing clearing the floor, 17 have
  no edges at all — so 96.9% show a real match, and the 139 still get "closest we could find".

  **Rush-More is atypical.** Shelf sizes at 0.55: 61% of charts show 16–20 cards, 7.6% show 1–3.
  Owner's read (2026-07-15): *"that 60% with a high chart rate feels like a legitimate read on how
  much of the game feels like practically the same chart"* — so the fat end is honest, not broken.
  It does mean one global floor cannot both keep Rush-More's three and thin a 20-card S20 shelf;
  it is already pinned to ±0.006 by the table above and has no freedom left to try.
- **Static shelf vs interactive controls — settled, and gated on Stage 2.** The shelf **is** the
  internal-link mesh and must sit in the anonymous output cache — but sort and filters are
  interactive, and an island renders nothing server-side (prerendering is off permanently). Settled
  shape: static server-rendered default list (real `<a href>`, crawlable) + an island for the
  controls that swaps the list on interaction, using the `data-island-ready` pattern. The card's
  play button is a **sibling** of the `<a>`, never a child — a `<button>` inside an `<a>` is invalid
  and the click targets collide.
  **The split is not built and cannot be on this branch.** `data-island-ready` exists only in these
  design docs; the app is still classic Blazor Server (`AddServerSideBlazor`, no `@rendermode`
  anywhere), so there is no render-mode infrastructure to island *into*. R6 therefore built the
  controls as circuit components — which is what the whole page already is — and the packaging
  rides with **P3**, once Stage 2 (`claude/static-shell-wave2-746ce2`) merges forward. What R6 did
  guarantee is the part that would be expensive to retrofit: the cards are already real `<a href>`s
  with the play button as a sibling, bUnit-pinned both ways.
- **Video embed shape.** The card art is 16:9 so the video takes its exact box — no reflow, the grid
  never jumps. Nothing loads until asked (the hero's rule). Autoplay is unreliable unmuted, so
  expect a second click; don't let the UI promise playback.
- **`ChartVerdictHandler`'s folder read** — unrelated to similarity, same page, same branch, still
  there. `GetScores(mix, chartType, level)` materializes ~50k rows for Phoenix S20 and filters to
  one chart in memory. Its result is cached per (chart, mix) daily, so it is one read per chart per
  day — but the SEO goal is 4,213 crawled chart pages, each a cold verdict.

---

## 11. Future upgrades (recorded, out of scope)

- **A projected passing level** (a `ChartPassingLevel` to sit beside `ChartScoringLevel`, mapping to
  real levels the way scoring level does — *"there are 23s that pass like a 20"*). **Owner revisits
  this roughly every six months; it dies for the same reasons each time. Read this before spending
  another round on it.**
  - **Don't interpolate the pass total.** `TierListSaga` builds the Pass Count list from
    `Σ log(28 − competitiveLevel)` over passers — a *sum*, so it scales with how many people played
    the chart. Scoring level interpolates an *average* score, which is why popularity cancels there
    and would not here. Popularity is its own tier list for a reason.
  - **Nor does a rate fix it.** `P(pass | competitive level)` from `IsBroken` looks native to the
    level scale and needs no conversion constant — but every objection below lives in its
    denominator, which is "players who have a record on this chart", and that set is never a random
    sample.
  - **Three biases, all owner-raised, all structural:** broken-score import is optional, so a fail
    exists only if someone bothered to upload it; retired players leave *negative space* on charts
    released after they quit, so a new chart is measured against a different population than an old
    one; and popularity picks who plays an obscure chart at all (enthusiasts), which reads as easy.
  - **And the signal is poisoned at the source, which is the one that actually settles it:**
    players walk off the pads to kill a PG attempt the moment they drop a Great. A broken record
    from a strong player chasing perfection is *indistinguishable* from one from a player who
    cannot clear the chart. Unlike the other three, **collecting better data prospectively does not
    fix this** — the measurement itself is ambiguous.
  - So the fix is not a better statistic over this data. It is different data, or a model that
    estimates the selection rather than assuming it away — its own project.
  - **Meanwhile the Pass Count tier list is a "good enough" state (owner, 2026-07-15)**, and the
    similarity gate uses scoring level, which is measured and has none of this.
- **Player type** — the prerequisite for Players returning. Needs patterns that identify *what kind
  of player* is correlating; its own project.
- **`top3:` as a bonus** — see §9. High precision, low recall; can confirm a match, never refute.
- **Rare-pattern signals.** `rare:*` (3,464 rows) and `practice_rank:*` (14,533 rows) are banked
  from piucenter and read by **nothing**. `badge_fraction` measures *pervasiveness*; `rare:split-2`
  means "there is a run of 2+ splits" and `practice_rank:split = 2` means it is the #2 thing to
  practice. Dream To Nightmare's uniqueness lives entirely there. Four skills — `bursty`,
  `sustained`, `run_without_twists`, `twists` — have **zero** coverage fractions corpus-wide and are
  reachable only this way.
- **"Most opposite chart"** — a live query, no storage, **1.66s** in raw SQL over all 1,731 Doubles
  (milliseconds in-process). Owner-verified as a fun/meme feature: the most opposite chart to
  Rush-More is **Hymn of Golden Glory - SHORT CUT - D20** at **0.0000** — zero shared mass, all
  twists against all brackets. Only 4 charts tie under .02, so the answer is essentially unique.
  Caveat: Bray-Curtis punishes sparsity, so the tail fills with thin old ANDAMIRO charts — needs a
  minimum-badge guard or same-level scoping. Bonus finding: the *maximum* similarity across every
  Double is **0.6154**, so the shelf's 49–62% range is genuinely the top of the distribution.
- **Judgment distributions**, **piucenter section density**, **cross-mix edges** — as before.
