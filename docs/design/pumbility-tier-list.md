# The PUMBILITY tier list

Replaces **Personalized Pass** with a count of what players actually keep in their top-50
PUMBILITY pool. Community and personalized are the same computation over different peer groups.

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

## 2. Why the pool count is the right replacement

Progression titles are no longer folder-tied. "What can I pass in this folder" stopped being
the question; "what could I play that moves my PUMBILITY" is the question. That is
reverse-engineerable from what players at your level actually hold.

Three structural properties make it a better primitive than a pass count:

1. **Top-50 slots are rivalrous.** A pass count is a stock that only grows — a lifetime
   popularity integral. A pool slot has to displace something and can be lost, so the count
   is a *current-state count across the player population* rather than an accumulation.
2. **Level cancels inside a folder.** Across folders, higher levels crowd every pool
   regardless of ease. Within one folder every chart is the same level, so what is left is
   relative value. Tier lists are per-folder, so this works out.
3. **No tunable constants.** The Pass ladder has seven hand-picked peer-group weights. The pool
   count has none — it is a `GROUP BY` over data already computed for `/Pumbility`.

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

The pool count independently rediscovers **short-cut farming** — a full tier of re-rating on a
fifth of all shortcuts, which pass counts cannot see because shortcuts are under-played
relative to how well they pay. The other direction is charts you can pass that do not pay:
Sorceress Elise D24 has *zero* pool appearances while Pass calls it Medium.

That profile — ~92% agreement, with the 8% it moves being exactly the "what should I play"
set — is the argument for it earning a lens slot rather than replacing Pass.

## 4. The algorithm

For a folder (mix, chart type, level) and a peer group:

1. Every player in the peer group has a **full** top-50 pool per chart type, priced by
   `ScoringConfiguration.PumbilityScoring(mix, includeCoOp: false)`. A player short of fifty is
   not counted at all — their total is low because they have imported little of the mix, not
   because they are weak, and letting that stand drops them into a group of genuinely weaker
   players *and* drags that group down with them. This is also what keeps Phoenix 2 dark until
   its pools are real, and lights it up on its own as they fill. The same gate applies to the
   reader: no pool, no personalized answer.
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

S20, mid ~17k peer group, 236 peers — the actual log-banded split:

| tier | Staple | Strong | Solid | Average | Modest | Slim | Poor | *not in any pool* |
|---|---|---|---|---|---|---|---|---|
| charts | 8 | 13 | 21 | 41 | 20 | 13 | 10 | 9 |
| peers | 101–175 | 53–86 | 29–50 | 8–28 | 4–7 | 2–3 | 1 | 0 |

Earendel tops it at 175 of 236.

## 5. PUMBILITY peers

*"Peers", not "cohorts" — owner, 2026-08-15: "please stop calling 'peers' cohorts."*

**Phoenix 2 (round eight of the PUMBILITY overhaul, [pumbility-overhaul.md D53/D55](pumbility-overhaul.md)):**
your PUMBILITY peers are the players whose pool of the chart type sits within **500 below and 250 above
yours** — your singles pool for a singles folder, doubles for doubles — each holding a **full 50-chart
pool of the type**, and you holding one too: no full pool of the type, no peers for it. **This is the
projector's own definition and the lens reads it from the projector**, by ruling (*"tier list should move
with this. we aren't keeping multiple pumbility peer groups across the site"*): `TierListBlendBuilder`
projects the folder with the catalog and counts `ScoreProjection.PeerPools` — the same holders-per-chart
the PUMBILITY page's Play list counts — banded with the same log-scaled processor the nightly writer
uses. Nothing is stored for a Phoenix 2 viewer; the read is one player's window (median 22 players) and
their records, held six hours with the rest of the blend.

| Mix | Peer key | Lists | Members |
|---|---|---|---|
| Phoenix 1 | highest **difficulty** title level (`UserHighestTitle`), `L{n}` | ~19 | 85–211 through levels 16–25; 11 at L10, 6 at L27, 1 at L28 |
| Phoenix 2 | none stored — the community `*` list only; the personalized lens is the projector's read | 1 per type | pools within −500..+250 of yours, full pool of the type both sides — median 22 players (p10 7, p90 39) |

Phoenix 1 has no per-type pool worth reading — its difficulty titles stand in. Not perfect, and
deliberately not worth more: Phoenix 1 PUMBILITY has a few weeks of relevance left.

- **Phoenix 2 does not materialize, and does not need to.** A window around the viewer's own pool is
  per viewer and per type, which is exactly the shape the first version's Singles∪Combined union could
  not store; the projector answers it at request time from one range read on `PlayerStats` and the
  peers' records, the read the PUMBILITY page already pays for. The community view still reads the
  nightly `*` list, so a signed-out visitor costs nothing new.
- **You are never one of your own peers** (owner, 2026-08-17; [pumbility-overhaul.md D31](pumbility-overhaul.md)).
  On Phoenix 2 the projector draws the window with the viewer removed, so there is nothing to take back
  out. On Phoenix 1 the stored list counts every member's pool, the reader's among them when they hold
  one, so the reader takes their own back out at read time (`TierListBlendBuilder.ComputePumbility`):
  one from the peer count, one from every chart their pool holds — the pool rebuilt from their records
  with the writer's own rule, `PumbilityPeers.TopPool` — and the bands redrawn with the writer's own
  processor. Nightly is the caveat there: a pool that filled since the last build was never counted in.
- **History.** Round three keyed Phoenix 2 on the viewer's rung of the combined total (`R{index}`, a
  list per rung counted over the players within ±3 rungs), which materialized because everyone on one
  rung shared one list. It was type-blind — a singles-carried DIAMOND's doubles peers were doubles
  specialists — and skewed upward at every rung, which is what round eight measured and replaced
  ([pumbility-overhaul.md §4.11](pumbility-overhaul.md)). The type-track refinement deferred here for
  three rounds is what D53 is: the pool of the type IS the type-aware definition, and it needed no
  title track to exist.
- Phoenix 1 needs no new read: `ITitleRepository.GetUserIdsOnHighestLevel` already exists and
  community Pass's difficulty-title groups already call it on Phoenix 1 (§10a). Phoenix 2's nightly
  pass reads no stats at all now.

## 6. Coverage, and what happens outside your band

Personalized PUMBILITY only speaks for a **3–4 level band** — the folders your peers' pools reach — and the walls are absolute, not
sloped. (On Phoenix 2 since round eight the folder picker lists the levels the projector's peers' pools reach, the same walls read live.) Measured on the first version's singles groups at ±250 PUMBILITY, charts with ≥3 peer appearances:

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
  tie, and that group indeed holds *zero* S16 charts.
- **Folders with no data are disabled in the folder selector**, and a direct URL **silently
  redirects** to the nearest folder that has data. No explanation, no dead-end page. There is
  no "fall back to community" state.
- **Above range is still ranked normally.** No shortlist mode, no special casing. For the
  ~15k group, S22 has exactly two charts anyone holds — *Hardkore of the North* and *Vector*,
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
- **Peer line**, on *every* personalized tier list, not just this one:
  - PUMBILITY — "Ranked against **N PUMBILITY peers** — within 3 levels of you with a full singles pool" (Phoenix 2); "Ranked against **N players** on your difficulty title level" (Phoenix 1)
  - Score — "Ranked against **N players** of similar competitive level"

  Score's number is already plumbed (`TierListResult.PeerCount` off the projection); it has
  simply never been rendered.

## 8. Storage and scheduling

`TierListEntry` cannot hold this — the cards show a peer count and that table has no count
column — so the PUMBILITY tier lists get **their own table**, `scores.PumbilityTierListEntry`,
keyed (MixId, ChartType, Level, ChartId, PeerKey) with the appearance count, the peer count, category
and order (`PeerKey`/`PeerCount` were `CohortKey`/`CohortSize` until round three; renamed by migration). It lives beside `TierListEntry` on `ITierListRepository` — one repository owns the
tier-list family — and the computation is a consumer on `TierListSaga`, where tier list
calculations live.

Written by the nightly `process-pumbility-tier-list` job, **once per mix**, one row set per
peer key plus an "everyone" key for the community view. Per-key rather than per-user is
what makes materializing tractable: ~34 folders × ~30 keys.

**No Phoenix 2 fallback.** `GetTierListWithFallbackQuery` falls back P2 → P1 for stored lists,
and that is *wrong* here: the two mixes price charts under completely different formulas and
338 shared charts were rerated between them, so a P1 pool count is a wrong answer for a P2 folder
rather than a stale one. On P2 the lens is absent until P2 pools exist.

**Phoenix 2 stores only the community list (round eight, D55).** The personalized Phoenix 2 lens is the
projector's read at request time (§5), so the nightly job writes the `*` key alone on that mix — roughly
one row set per folder per type instead of up to thirty-eight. Phoenix 1 still writes its `L{n}` lists.

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
- **Community Pass's inverted peer-group weights** — level+1 counts 1, level+2 counts 2, level+3
  counts 3, so a player three levels stronger than the folder counts triple one a single level
  stronger. Below the folder it is correctly monotonic (7/6/5/4). Its own session. Phoenix 1 still
  places players by their highest difficulty title; since 2026-10-03 a mix without difficulty titles
  (Phoenix 2, RISE, RISE Arcade) places them by their competitive level for the folder's chart type
  instead (§10a). Both use the same weights, so the inversion stands on both.
- **In-title levels for Phoenix 1** (the in-game Diamond 1–5 rungs that were never ingested).
- **Cross-folder "charts above my comfort zone that people like me hold"** — really a
  Suggested Charts question, not a tier-list one.

### 10a. Community Pass groups players by competitive level where a mix has no difficulty titles (2026-10-03)

Owner, 2026-10-03: *"Pass ranking on phoenix 2 still not working (we might need to just turn this on
until we find a better pass difficulty algorithm)"*. Owner, 2026-10-04, on a first cut that regrouped
Phoenix 1 as well: *"Yeah, phoenix 1 should stay the same as today"*.

**Why it was dark.** The nightly Pass job (`TierListSaga`, the stored `Pass Count`
list) built its seven weighted groups from `ITierListRepository.GetUsersOnLevel`, which read each
player's highest **difficulty** title off `UserHighestTitle`. Only a `PhoenixDifficultyTitle` writes that
table. Phoenix 2's title list holds none, and RISE and RISE Arcade have no title list at all, so on those
three mixes every group was empty, every chart summed to zero, and the whole folder was saved as
`Unrecorded` — *Not Rated* on the Pass lens, and on every surface that reads the list (the Charts SRP
facet and sort, the chart-page PassVsScore verdict, the details placement chip, the Discord chart card,
`api/v2` `pass-difficulty` and v1 tier lists, the PUMBILITY Play targets' difficulty, approachable charts
in Recommended Charts). A full folder of `Unrecorded` rows also kept the Phoenix 2 → Phoenix 1
provisional fallback from ever firing. Nothing switched Pass off, so "turn this on" could not be a flag;
it is read as *make the existing algorithm produce answers now, flaws accepted*.

**What changed.** Which grouping a mix gets is the domain's answer to whether it has chart-level
difficulty titles, `TitleLists.HasDifficultyTitles(mix)` — not a mix check, so it flips on its own if
Phoenix 2's title list ever gains them.

- **A mix with difficulty titles — Phoenix 1 — is unchanged.** Its groups are still the players' highest
  difficulty titles from `ITierListRepository.GetUsersOnLevel`, with the same weights, the same guards at
  the top of the ladder, the same seven title reads and seven score reads per folder and the repository's
  own 120-day activity read, so the same data gives the same list it gave before 2026-10-03.
- **A mix without them — Phoenix 2, RISE, RISE Arcade — groups by competitive level.** The groups come
  from the player's own **competitive level for the folder's chart type** — singles for a singles folder,
  doubles for a doubles folder, so RISE's half-doubles read the doubles level (D20 in [rise.md](rise.md)) —
  floored to a whole level, minus the folder's level. The weights and the activity rule are Phoenix 1's
  (`PassPeerWeights` in ChartIntelligence's Domain):

| Offset from the folder | −3 | −2 | −1 | 0 | +1 | +2 | +3 | anything else |
|---|---|---|---|---|---|---|---|---|
| Weight | 7 | 6 | 5 | 4 | 1 | 2 | 3 | 0 |
| Must be active (a best in the last 120 days) | yes | yes | yes | yes | no | no | no | — |

On the competitive-level path one bulk folder read and one stats read replace the title and score reads,
and the activity set is read once per mix on the injected clock, only for a mix that takes this path. The
Pass list there skips a passer with no stats row instead of throwing. The PG list the same job builds just
before it in every folder skips a holder with no row on every mix — a PG holder with no row would otherwise
stop the rest of that mix's run before the Pass list was reached (the local copy had no such holder on any
mix, 2026-10-03). `GetUsersOnLevel` stays: it is the Phoenix 1 path's read.

Options weighed and not taken:

- **Let the Phoenix 2 → Phoenix 1 fallback fire.** The fallback was for *"until P2 data accumulates"*
  ([phoenix2-implementation.md](phoenix2-implementation.md)) and it has — roughly 45k unbroken Phoenix 2
  passes in folders 10–29 from 375 players. The bands are folder-relative and 338 charts changed level
  between the mixes, the Charts SRP facet reads the repository directly with no fallback, and it would
  never heal.
- **Group by the PUMBILITY title ladder.** No rung-to-folder mapping exists, it is type-blind (round
  three's problem, §5 History), Phoenix 2 only, and RISE has no PUMBILITY.
- **A plain pass count.** Drops the weighting, which turns the list into a popularity count (§1).
- **Regroup Phoenix 1 on competitive level too.** The first cut (2026-10-03) ran the competitive-level
  rule on every mix. Phoenix 1 stays on its difficulty titles (owner, 2026-10-04, quoted above); what the
  regrouping would have moved is below.

**What regrouping Phoenix 1 would have moved** (local prod-synced copy, 2026-10-03; not shipped). It is
a re-keying, not a transcription: competitive level runs about two levels below the difficulty title
(title 22 ≈ 20.75 singles / 20.39 doubles; title 26 ≈ 23.41 / 25.07), and the floor takes about another
half level, so the same −3..+3 window sits about two title levels higher. A SQL rebuild of the title
grouping matched the stored list on 3,451 of 3,501 charts; regrouping on competitive level would have
moved 686 of 3,501 Phoenix 1 charts by one tier and 8 by two or more. Rank agreement with the title list
was 0.95–0.996 in every Single and Double folder from 14 up and 0.78–0.93 in D10–D13. Shifting by +2 to
line the scales up did not help (625 moved, 15 by two or more). For scale, a plain unweighted pass count
agrees with the title list at 0.93–0.997: the seven weights do little beyond counting passes.

**Known gap: low Phoenix 2 folders stay partly Not Rated.** Most Phoenix 2 players who pass S10–S13 sit
more than three levels above the folder, and those players weigh 0. Charts rated against charts with at
least one pass, on the same local data:

| Folder | Rated | With a pass |
|---|---|---|
| S10 | 64 | 101 |
| S11 | 61 | 118 |
| S12 | 83 | 123 |
| S13 | 68 | 84 |
| D10 | 27 | 35 |
| D11 | 35 | 50 |

Folders 14 and up are 80–100% rated. It is a population effect and fills in as weaker players arrive.
Widening the window changes the weights the §10 bullet above defers, so it waits for that session; the
cheap version, if wanted, clamps offsets above +3 to weight 3.

**Known gap: the hard end of a Phoenix 2 folder is mostly empty, so its folders read easier than they
are.** The bands are cut at the folder's mean sum minus one and one and a half standard deviations
(`TierListProcessor.ProcessIntoTierList`), and every Not Rated chart's zero counts toward both. Phoenix 2's
sums spread about as wide as their own mean (mean ÷ SD 0.49–1.22 in every folder from S10 to S22 and D10
to D24, against 1.33–2.69 on Phoenix 1 in the same folders, measured with Phoenix 1 regrouped the same way
in the first cut), so no chart in those folders lands in **Very Hard** or **Underrated**: 10 of 3,644
Phoenix 2 charts do, all in folders 23 and up, against 530 of 3,501 on that regrouped Phoenix 1. **Hard**
is still reached, except in S10, S11, D10, D19 and the
ten-chart D27, where the zeros pull the mean down far enough that every rated chart lands Medium or
easier. Same local data, same date. Two ways out, neither taken here because both change the algorithm
the brief accepted as it is:

- Leave the zeros out of the cut-offs. Hard comes back to those five folders; Very Hard newly appears
  only in D11 and D13.
- Band on the log scale the PUMBILITY list already uses (`ProcessIntoLogScaledTierList`, §4a), which
  also leaves the zeros out. 523 Phoenix 2 charts land in Very Hard or Underrated. The banding is shared,
  so it moves Phoenix 1 as well: against the regrouped first cut, 975 of 3,501 charts by one band, none
  by two.

**Neither is taken: Phoenix 2's Pass list bands exactly as Phoenix 1's does** (owner, 2026-10-03: *"just
match phoenix 1 for now. I don't want to make adjustments until I get some dedicated measurement time
in."*). The banding, the zeros and the weights wait for that measurement session, together with §10's
deferred weights question.

**Post-deploy, once:** trigger `process-pass-tier-list` in `/hangfire` (or wait for the 09:30 UTC
nightly). The nightly upsert overwrites the all-`Unrecorded` rows on Phoenix 2, Rise and Rise Arcade in
place — no SQL; Phoenix 1's rows come out as they do today. The Charts SRP
community bundle and the chart verdicts pick the new list up at their next recompute; **Clear Cache**
shows it sooner.

## 11. Post-deploy, once

**Round three re-keys Phoenix 2.** The migration renames the columns only; Phoenix 2 rows written
under the old own-type title keys are stale until the job runs again — press the button once (or
wait for the nightly). Until then the Phoenix 2 lens is dark and falls to Pass, exactly as it does
for any thin peer group. Phoenix 1 rows keep their `L{n}` keys and are unaffected.

The `PumbilityTierListEntry` table ships empty, so the lens shows nothing until the nightly job first runs.
Press **Rebuild {mix} PUMBILITY tier lists** on `/Admin`, per mix. `/hangfire` works too, except
locally where `PreventRecurringJobs` parks the job on a yearly cron the dashboard cannot fire.

**Importing scores does not rebuild the PUMBILITY tier lists**, and deliberately: it is a population-wide
count, not a per-player derivation, so one import cannot meaningfully move it. Your own new
scores show up in the lens on the next nightly run — or immediately, if you press the button.

**Round eight leaves the Phoenix 2 rung rows behind.** The job no longer writes `R{index}` keys on Phoenix 2
and nothing reads them, so the rows written before 2026-09-01 are harmless leftovers; the next nightly run
does not remove them, because `SavePumbilityTierLists` replaces the keys it is handed and is handed only
`*`. Delete them by hand if the table's size ever matters (the Phoenix 2 mix's rows whose `PeerKey` starts
with `R`); nothing depends on it either way.
