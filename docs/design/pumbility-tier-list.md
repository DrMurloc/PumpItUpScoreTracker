# The PUMBILITY tier list

Replaces **Personalized Pass** with a census of what players actually keep in their top-50
PUMBILITY pool. Community and personalized are the same computation over different cohorts.

Mocks: <https://claude.ai/code/artifact/0c0cf3e8-7bd3-4097-86ec-da506c7ee8bf>

---

## 1. Why Personalized Pass had to go

A pass tier list assumes every player has played every chart once, at full effort. They
haven't, so a raw pass count carries three biases: popularity (unplayed ≠ hard), new charts
that haven't accumulated passes, and last-mix charts nobody plays any more. At the site's
scale that mostly launders itself, which is why **community Pass is decent and stays**.

Personalizing it was the broken half. Its recipe was `Pass Count 2 · Skill 2 · Similar
Players 1`, and **two of those three sources carried no pass signal at all** — Skill was
built from score proficiency in the 900k–1M band, Similar Players compared relative *score*
lists. The code said so outright: *"there is no pass-projection engine, so its personal half
is still the skill estimate and the similar-players aggregation."* Roughly 60% of
Personalized Pass was score inference wearing a pass label.

Separately, the owner measured the (then Chabala-sourced) skill tags making personalization
actively worse, and found similar-skilled-player aggregation more effective than the skill
algorithm. That measurement predates the PiuCenter migration, so it was measuring data that
no longer exists — but the verdict stands for other reasons and the decision is deletion.

## 2. Why the pool census is the right replacement

Progression titles are no longer folder-tied. "What can I pass in this folder" stopped being
the question; "what could I play that moves my PUMBILITY" is the question. That is
reverse-engineerable from what players at your level actually hold.

Three structural properties make it a better primitive than a pass count:

1. **Top-50 slots are rivalrous.** A pass count is a stock that only grows — a lifetime
   popularity integral. A pool slot has to displace something and can be lost, so the count
   is a *current-state census of the player population* rather than an accumulation.
2. **Level cancels inside a folder.** Across folders, higher levels crowd every pool
   regardless of ease. Within one folder every chart is the same level, so what is left is
   relative value. Tier lists are per-folder, so this works out.
3. **No tunable constants.** The Pass ladder has seven hand-picked cohort weights. The census
   has none — it is a `GROUP BY` over data already computed for `/Pumbility`.

**Popularity bias does not carry over, and this is the subtle part.** For Pass, play is
driven by fun, novelty and tournaments — orthogonal to difficulty, so popularity is a
confound. For pool membership, play is driven *by the thing being measured*: players hunt
good-PUMBILITY charts, and go out of their way for an old one that pays. "Nobody plays it"
is not missing data, it is the population having already done the search. **Counts, not
rates** (owner, measured previously).

## 3. Evidence

Measured against the prod-synced snapshot: 1,505 players, 937,604 non-broken Phoenix 1
scores priced under the Phoenix 2 PUMBILITY formula, top 50 per player per chart type. Pool
totals landed at avg ~16.5k / max ~19.8k, which is the right ballpark for the model to be
believed.

**Coverage is near-total.** 90–100% of Phoenix charts appear in at least one pool across
every folder 10–26. Only D27 (6 of 8) and D28 (1 of 3) are thin — 11 charts in total.

**It agrees with Pass broadly and disagrees usefully.** Spearman against the live Pass Count
order runs 0.84–0.96 (Doubles 16–24) and 0.79–0.92 (Singles), one outlier at S23 = 0.62. The
disagreements are systematic:

| Song type | charts | pool percentile − pass percentile | moving 2+ tiers |
|---|---|---|---|
| **ShortCut** | 159 | **+0.149** | **20.8%** |
| **FullSong** | 46 | **+0.137** | **19.6%** |
| Remix | 91 | +0.070 | 6.6% |
| Arcade | 2,059 | −0.030 | 6.9% |

The census independently rediscovers **short-cut farming** — a full tier of re-rating on a
fifth of all shortcuts, which pass counts cannot see because shortcuts are under-played
relative to how well they pay. The other direction is charts you can pass that do not pay:
Sorceress Elise D24 has *zero* pool appearances while Pass calls it Medium.

That profile — ~92% agreement, with the 8% it moves being exactly the "what should I play"
set — is the argument for it earning a lens slot rather than replacing Pass.

## 4. The algorithm

For a folder (mix, chart type, level) and a cohort:

1. Every player in the cohort has a top-50 pool per chart type, priced by
   `ScoringConfiguration.PumbilityScoring(mix, includeCoOp: false)`.
2. For each chart in the folder, count how many of those pools contain it. That count is the
   signal. No denominator, no rate.
3. Band the counts through `TierListProcessor` on a **log scale**: `log(1 + n)`, zeros
   excluded from the mean and standard deviation, cut at ±0.5σ / ±1σ / ±1.5σ as every other
   tier list is.
4. A chart with zero appearances is `Unrecorded` and renders as **"Not in anyone's
   PUMBILITY"**.

### 4a. Why the log, since it is not cosmetic

σ-banding assumes the input is roughly symmetric about its mean — that is what μ±0.5σ/±1σ/±1.5σ
*is*. Raw pool counts are not: measured skewness runs **+0.8 to +2.85** (typically ~+2.0)
across folders, while `log(1+n)` runs **−0.76 to +0.19**, i.e. symmetric. The log satisfies
an assumption the existing rule already makes rather than adding one.

The consequence of skipping it is not ugliness, it is **two tiers that can never be
assigned**: on raw counts the μ−1.5σ cutoff lands at a negative appearance count in **33 of
34 folders** (D20's is −51.1), and μ−1σ in 25 of 34. A chart cannot appear −51 times. The
list would ship with 7 tiers and structurally use 5, dropping the two a player scanning for
"what is hard here" reads first.

The log changes **no rankings** — it is monotonic, so chart order is identical and nothing
bad for PUMBILITY gets rescued. It changes only where the tier lines fall. Nor does it force
an even distribution (that would be percentile banding): S20 comes out 8/13/21/41/20/13/10,
a fat middle and thin ends, and different folders keep different shapes.

Why the data is multiplicative in the first place: a chart's count is roughly *(how many
players' 50th-place bar sits below this chart's value)* × *(what fraction of those played
it)*, and both terms rise together with how good the chart is. Two multiplied effects
compound, so the spread is naturally in ratios.

### 4b. Real output

S20, mid ~17k cohort, 236 peers — the actual log-banded split:

| tier | Staple | Strong | Solid | Average | Modest | Slim | Poor | *not in any pool* |
|---|---|---|---|---|---|---|---|---|
| charts | 8 | 13 | 21 | 41 | 20 | 13 | 10 | 9 |
| peers | 101–175 | 53–86 | 29–50 | 8–28 | 4–7 | 2–3 | 1 | 0 |

Earendel tops it at 175 of 236.

## 5. Cohorts — "PUMBILITY peers"

A sibling to competitive peers, and a concept intended for reuse. Keyed on **title**, and
resolved per mix:

| Mix | Cohort key | Buckets | Cohort sizes |
|---|---|---|---|
| Phoenix 1 | highest **difficulty** title level (`UserHighestTitle`) | ~19 | 85–211 through levels 16–25; 11 at L10, 6 at L27, 1 at L28 |
| Phoenix 2 | **PUMBILITY** title rung (`Phoenix2PumbilityTitle`) | ~30 | measured 50–126 per 250-point band |

Phoenix 1 has no PUMBILITY-threshold titles — its difficulty titles stand in. Not perfect,
and deliberately not worth more: Phoenix 1 PUMBILITY has a few weeks of relevance left.

The P2 ladder already carries in-title levels — `[S] ADVANCED LV.1` at 15,000 through
`LV.10` at 17,250, in 250-point steps — which is exactly the band width the cohort sizes
above were measured at.

- A player belongs to **one** cohort per chart type — cohorts are a partition, which is what
  makes them materializable.
- Phoenix 1 needs no new read: `ITitleRepository.GetUserIdsOnHighestLevel` already exists and
  `ProcessPassTierList` already calls it.

⚠️ **Deviation from the original intent, shipped deliberately.** The rule was "Singles reads
Singles title ∪ Combined title, Doubles reads Doubles ∪ Combined, counted once". Implemented,
that union is *per viewer* — my cohort is the union of the two ladders I personally sit on, and
the player next to me sits on a different pair, so no two readers share a cohort and none of it
can be materialized per cohort. Phoenix 2 therefore reads the **own-type ladder only**, and the
Combined ladder is unused. Revisit when Phoenix 2 has score volume; the cheap version is to key
a cohort on the *pair* of rungs, which stays a partition.

## 6. Coverage, and what happens outside your band

Personalized PUMBILITY only speaks for a **3–4 level band**, and the walls are absolute, not
sloped. Singles cohorts at ±250 PUMBILITY, charts with ≥3 peer appearances:

| folder | low ~15.0k (91 peers) | mid ~17.0k (236) | high ~18.5k (62) |
|---|---|---|---|
| S15 | **89** / 132 | 0 | 0 |
| S16 | **88** / 189 | 0 | 0 |
| S17 | 47 / 196 | 40 / 196 | 0 |
| S18 | 7 / 189 | **103** / 189 | 0 |
| S20 | 0 / 135 | **108** / 135 | 0 |
| S22 | 0 / 97 | 54 / 97 | **78** / 97 |
| S24 | 0 / 23 | 0 / 23 | **22** / 23 |

That is correct rather than a defect: no S22 will ever enter a 15k player's pool, so "which
S22 is best for my PUMBILITY" genuinely has no answer.

**Handling (owner-locked):**

- **Below range** — the gate is arithmetic, not peer data: compare your 50th-place pool value
  against what the folder can pay at best (`PricedBase(type, level) × 1.52`). Verified against
  observation — a ~17k player's bar sits near 300 and S16 prices at 215, so S16 needs AA+ to
  tie, and that cohort indeed holds *zero* S16 charts.
- **Folders with no data are disabled in the folder selector**, and a direct URL **silently
  redirects** to the nearest folder that has data. No explanation, no dead-end page. There is
  no "fall back to community" state.
- **Above range is still ranked normally.** No shortlist mode, no special casing. For the
  ~15k cohort, S22 has exactly two charts anyone holds — *Hardkore of the North* and *Vector*,
  one peer each — and they go through the same banding as any folder. Two equal values give a
  standard deviation of zero, so both land in the top tier and read as **Staple**. That is a
  feature: they are the only two charts in S22 that can do anything for you.

## 7. Presentation

- **Lens name**: PUMBILITY, joining Pass Difficulty and Score Difficulty in "Ranked by".
  Chabala and PG stay.
- **Tier names**, best to worst: **Staple · Strong · Solid · Average · Modest · Slim · Poor**,
  then "Not in anyone's PUMBILITY" for zero.
- **Ramp**: the **rarity** ramp, not difficulty — this measures *worth playing*, and rarity is
  the ramp whose meaning is "better". Prism · Sapphire · Gold · Emerald · Silver · Common for
  the top six; **Poor and "Not in anyone's PUMBILITY" share the existing unrecorded grey**,
  because the rarity ramp is six stops against seven tiers and growing a shared token for this
  was rejected.
- **Peer count on every card** — "175 peers", "1 peer". Plain, no special phrasing. It rides the
  card's existing tier-label slot and is shown on every view, because the count *is* the lens and
  a tier section heading cannot carry it; the tier name joins it only where the section is not
  already saying it.
- **Cohort line**, on *every* personalized tier list, not just this one:
  - PUMBILITY — "Ranked against **N players** in your PUMBILITY title ranges"
  - Score — "Ranked against **N players** of similar competitive level"

  Score's number is already plumbed (`TierListResult.PeerCount` off the projection); it has
  simply never been rendered.

## 8. Storage and scheduling

`TierListEntry` cannot hold this — the cards show a peer count and that table has no count
column — so the census gets **its own table**, keyed
(MixId, ChartType, Level, ChartId, CohortKey) with the appearance count, category and order.

Written by a **nightly Hangfire job** in the ChartIntelligence vertical, one row set per
cohort plus an "everyone" cohort for the community view. Per-cohort rather than per-user is
what makes materializing tractable: ~34 folders × ~30 cohorts.

**No Phoenix 2 fallback.** `GetTierListWithFallbackQuery` falls back P2 → P1 for stored lists,
and that is *wrong* here: the two mixes price charts under completely different formulas and
338 shared charts were rerated between them, so a P1 census is a wrong answer for a P2 folder
rather than a stale one. On P2 the lens is absent until P2 pools exist.

## 9. What this deletes

- The **Skill** source — `ComputeSkillEvidence`, `ComputeSkillSource`, the ±3-folder pooling,
  proficiency deviations, folder decay, `SkillEvidencePool` / `SkillSourceComputation`.
- The **Similar Players** source — `ComputeSimilarPlayers`, `SimilarPlayersComputation`.
- `UserTierListSaga`, `IUserTierListRepository`, `EFUserTierListRepository`,
  `UserTierListEntryEntity`.
- `scores.UserTierListEntry` (~1.02M rows) — rows purged, then
  `ALTER SCHEMA archive TRANSFER` per the never-drop-tables standard. Purging costs nothing
  recoverable: the rows are derived from scores via `GetMyRelativeTierListQuery`.

⚠️ The table is named by the account-purge manifest **and** by
`ContributionDeletionItems.TierListVotes` on `/Account/Data/Delete`. Both must be updated in
the same commit as the manifest change, or `AccountPurgeCoverageTests` fails.

`ScoreAgePolicy` **survives** — `RecommendedChartsSaga` still uses it.

## 10. Deferred, deliberately

- **The Personalized Breakdown page** keeps its Score half; its Pass half evaporates. Whether
  it gains a PUMBILITY mode is a later session. The `PersonalizedTierListBreakdown` contract
  keeps its now-always-empty skill and similar-players fields until then.
- **Community Pass's inverted cohort weights** — level+1 counts 1, level+2 counts 2, level+3
  counts 3, so a player three levels stronger than the folder counts triple one a single level
  stronger. Below the folder it is correctly monotonic (7/6/5/4). Its own session.
- **In-title levels for Phoenix 1** (the in-game Diamond 1–5 rungs that were never ingested).
- **Cross-folder "charts above my comfort zone that people like me hold"** — really a
  Suggested Charts question, not a tier-list one.

## 11. Post-deploy, once

The census table ships empty, so the lens shows nothing until the nightly job first runs.
Trigger it per mix from `/hangfire` (or the admin Rebuild button) rather than waiting a day.
