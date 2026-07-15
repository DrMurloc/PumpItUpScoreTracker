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

| Option | Source | Available |
|---|---|---|
| **No sorting** | the match order itself, strongest first | always |
| **Community** *(default)* | community tier lists + scoring level, closest to the anchor | always |
| **Pass · for me** | the reader's personalized Pass tier list | signed in |
| **Score · for me** | the reader's personalized Score tier list | signed in |

Pass and Score are both offered because they genuinely differ — a chart can be a wall to clear and
generous to score, or the reverse. "No sorting" is the only option that shows the true match
order; every difficulty sort reorders away from it.

**Filters** reduce the *target list* and then **recompute live** — they never filter the stored
top-20 (which would trivially return zero). Dimensions: step artist, BPM range, level range, mix
("from X or newer"). The reach must be stated: *"Compared **30 charts** by SPHAM within 2 levels —
**1 match**."* — that reads as a narrow filter rather than a broken feature.

**The D18→D23 case is a live call.** "I liked this D18, what D23s are like it" is a real use case
and is deliberately **outside** the precalculated ±2 window; it computes on demand.

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
  `(MixId, ChartId, SimilarChartId)`, index `(MixId, ChartId, Score DESC)`. `SharedScorers` is
  dropped with Players. `SignalsJson` carries skill, intensity, and the shared-badge breakdown.
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
| **Conflict ↔ Viyella's as the geometric's proof case** | This doc used to cite the pair as a false match rescued by coinciding NPS (10.7/10.5). It is not one, and the rework does not move it: measured on the real D21 cohort, **old .787 → new .783**. Their decomposed profiles are genuinely close — Conflict is ~1.65× Viyella's on *both* axes (sus .217/.133, burst .250/.148), so `s_sus .748 / s_burst .742` and there is no mismatched dimension for the geometric to catch. They are also adjacent rungs in the owner's own six-chart ordering, so ~.78 may simply be right. **What the owner's labels separate ("high stamina" vs "pure burst") is TutFrac — total load, .467 vs .281 — not composition**; note Viyella's has the *lower* burstFrac of the two. Use Slapstick Parfait ↔ Horang Pungryuga (§3.2) as the proof case instead. |
| **A BPM plateau (±10, decay beyond)** | Nominal BPM is **noise**: slow songs multiply. Altale 90 BPM → **12.0 nps**; Glimmer Gleam 85 → 8.8; TRICKL4SH 220 → 11.5 — all within ~2 of Rush-More's 10.7, while raw BPM said 60–75 apart. A ±10 band would have deleted two owner-approved matches and promoted THE REVOLUTION (exactly 160 BPM, *"absolutely not"*). **NPS already carries it.** BPM survives only as a filter. |

---

## 10. Open

- **The floor value.** 0.55 was calibrated against a five-signal arithmetic mean that no longer
  exists. Meaningless until a run with skill+intensity only; owner will run the analyzer and
  calibrate from the distribution. It is a render constant, so this is a redeploy, not a job run.
- **Static shelf vs interactive controls.** The shelf **is** the internal-link mesh and must sit in
  the anonymous output cache — but sort and filters are interactive, and an island renders nothing
  server-side (prerendering is off permanently). Settled shape: static server-rendered default list
  (real `<a href>`, crawlable) + an island for the controls that swaps the list on interaction,
  using the doc's `data-island-ready` pattern. The card's play button is a **sibling** of the `<a>`,
  never a child — a `<button>` inside an `<a>` is invalid and the click targets collide.
- **Video embed shape.** The card art is 16:9 so the video takes its exact box — no reflow, the grid
  never jumps. Nothing loads until asked (the hero's rule). Autoplay is unreliable unmuted, so
  expect a second click; don't let the UI promise playback.
- **`ChartVerdictHandler`'s folder read** — unrelated to similarity, same page, same branch, still
  there. `GetScores(mix, chartType, level)` materializes ~50k rows for Phoenix S20 and filters to
  one chart in memory. Its result is cached per (chart, mix) daily, so it is one read per chart per
  day — but the SEO goal is 4,213 crawled chart pages, each a cold verdict.

---

## 11. Future upgrades (recorded, out of scope)

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
