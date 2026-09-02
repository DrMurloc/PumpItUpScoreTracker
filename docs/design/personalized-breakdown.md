# Personalized Breakdown

The page that explains the tier list's **Personalized** switch: what goes into the
blend, what each source said about every chart, and why your list disagrees with the
community's where it does. Workshopped 2026-07-12; decisions below are owner-locked.
Route: `/TierLists/{ChartType}/{Level}/Breakdown?Lens=Pass|Score`.

## Why it exists

Personalization was entirely behind the scenes: the blend combined community lists,
a skill estimate, and a similar-players aggregation, and nobody could see which
charts moved or why — nor that a starved source had silently degraded the whole
thing to the community list. The owner's bar for the page: *"a user should come out
of this going 'oh, that's what all that meant'"* — and the per-chart movement
attribution is the headline win.

## Decisions

1. **Both skill views, explained together.** The page shows the blend's real inputs
   (cross-folder deviations, the K7 inference) *and* the Folder Stats in-folder
   ability rows, with bridge copy connecting them. They answer different questions
   and will disagree; hiding one would just move the confusion.
2. **"Vs. Peers", from existing data.** The per-skill peer comparison is the
   coverage-weighted average of the player's per-chart "Better Than %" (players
   within ±0.5 competitive level — the tier page's Better Than population). No new
   cohort SQL (see the 2026-07-10 incident). "Vs. Cohort" was rejected as jargon.
3. **Contribution extraction.** The blend's internals moved into
   `TierListBlendBuilder` (ChartIntelligence, internal), shared by
   `GetBlendedTierListQuery` and the new `GetPersonalizedTierListBreakdownQuery`
   so the page and the real blend cannot drift. The breakdown returns per-chart
   per-source categories, pooled skill deviations + evidence, and source statuses.
4. **Movers: simple rows, tap for more.** Rows show jacket, tier movement in
   ramp-colored words, and top contribution chips (influence = coverage × your
   deviation). Tapping expands the full source strip (Community / Players like you /
   Your skills, with weights, → final). Always-visible strips were considered and
   rejected as too dense.
5. **Silent sources are diagnostics.** Each recipe card prints its status for this
   player ("Active — 214 scores nearby…" / "Silent — only 2 skills have enough
   evidence…") with what to do about it. Silence must never look like "covered".
6. **Honest remainder.** Unchanged and not-enough-data counts always print; the
   no-data charts are listed — they double as a what-to-play-next nudge.
7. **Similar Players = competitive-level cohort.** As part of this work the blend's
   neighbor selection moved from ±1 *title* level (unweighted) to ±1.0 *competitive*
   level for the folder's chart type, each neighbor's vote scaled by linear
   closeness falloff × rating agreement. Competitive level ≤ 1 is the no-data floor.
8. **Eligibility mirrors the switch.** Logged in, Pass/Score lens, non-CoOp,
   non-legacy mix; everything else soft-lands on the tier list. Entry points: the
   "How is this personalized?" caption link under both Personalized switches and
   the clickable "Personalized for …" chip.
9. **Score age diminishes outlier votes, never values (score-age workshop, same
   day).** A best attempt's age only means "time since last improved" — ceiling
   scores go stale by definition — so age reduces an observation's *evidence*, not
   its score. "Old" requires BOTH conditions (owner-corrected model): past the
   **30-day grace floor** (only there so a new account's three-week-old scores
   never read as outdated next to last week's — beyond a month, the player's own
   distribution rules) AND an **age outlier in the player's own record** — beyond
   mean + 1σ of their score ages, the same banding the Age lens uses. The target
   is the years-old one-and-done chart nobody revisits because the chart design is
   annoying — sitting 60k under what a bad day would score today.
   Outliers are diminished (half-voice per 180 days beyond the threshold, floored
   at 0.1), everything else keeps weight 1. A uniformly-old history has no spread,
   hence no outliers — a returning player is a coherent snapshot at full voice.
   Applied to the skill estimate's observations AND its folder baselines (or
   fresh-vs-stale reads as phantom deviation). The breakdown page discloses it at
   card level only — owner: "it's a disclaimer, not data" — never as per-row
   decoration.
10. **Neighbors fade by entry, never by membership.** The owner rejected an
   activity filter: an inactive player's folder record is a coherent snapshot and a
   valid witness. Instead each materialized `UserTierListEntry` carries a
   `Freshness` weight from the same grace-floor + outlier formula, scoped **within
   that player's own folder** — era-mixed entries whisper, uniform snapshots
   (including quit players') keep full voice. Computed in `UserTierListSaga`; the
   Backfill User Tier Lists run re-stamps existing rows (default 1.0 =
   pre-backfill behavior unchanged). Known second-order effect, deliberately
   unaddressed in v1: the relative *categories* are still bucketed against a mean
   that old scores drag down; the freshness weight mutes most of the distortion.

## The two windows (deliberate)

- **Vs. Peers column**: ±0.5 competitive level — reuses the Better-Than data.
- **Players Like You blend source** (Pass only): ±1.0 competitive level with closeness falloff.
- **Projection source** (Score only): ±0.5, the window the rest of the site means by a
  competitive peer.

Same spirit, different sources; aligning the first two would mean new cohort
aggregation for no user-visible gain.

## The two lenses stopped sharing a recipe (2026-08-11)

Score's personal half is now a single source: what players within ±0.5 competitive
level actually score on the folder's charts, growth-discounted and σ-bucketed like
every other tier list — `IScoreProjector`, shared with the PUMBILITY page so the two
surfaces cannot answer "what would you score here" differently.

It replaced both of Score's old personal sources. The Skill nudge measured **0.071**
correlation with the residual it existed to correct (pumbility-overhaul.md §4.3 —
worse than not adjusting), and Similar Players read the same competitive cohort the
projection reads, then discarded the scores in favour of the tier buckets they had
been sorted into. **Pass keeps both, unchanged**: there is no pass-projection engine,
so the Skill source is still the only thing doing per-player work there.

Two consequences the page has to carry:

- **Each recipe card follows its own weight**, never a lens name, so the page tracks
  the modifier table instead of a snapshot of it. The skill profile section goes with
  the Skill source — on Score those deviations feed nothing.
- **The claim is bounded.** The estimator depends on the player only through their
  competitive level, so the card says *what players at your level score here* and may
  not say *this chart suits you*.

### Score dropped its community half (2026-08-11)

Personalized Score is the projection **alone** — the stored score lists no longer vote.
Blending them back in counted the same evidence twice: the projection is built from
peers' actual scores, so those lists are an echo of its own input, bucketed, not a
second opinion. It also means the standard-deviation banding happens once inside the
projection rather than being averaged with other bandings and re-cut.

Two consequences:

- **No fallback.** A chart no peer near the player's level has played is Not Rated
  rather than quietly the community's tier, and under the floor the whole folder is —
  so the page says so, with the Community view one button away, instead of showing a
  screen of grey cards.
- **The breakdown's community column is computed as the Community view**, not as the
  personalized recipe filtered to its stored sources. That filter now yields nothing on
  Score, which would have blanked the moved-charts diff the page exists for.
  `CommunityWeight` means the community share of *your* list — 0 on Score — so the
  recipe card follows it.

### The page rebuilt on the number (2026-08-11)

With one source there is no blend to explain, so the page stopped explaining one.

- **The recipe collapses to a card.** One source is not a weighing, and a weight bar
  with a single bar in it says nothing. What matters is what the source is and how
  much of the folder it reached.
- **The spread replaces the skill profile.** `ProjectionSpread` (a component, so its
  CSS can live in `site.css` — a Razor `<style>` block is page-scoped) lays every
  chart out on what this level would score, with the tier bands drawn behind it. The
  cuts come from `TierListProcessor.StdDev`, the same function the bucketing runs, so
  a band edge cannot sit where the tier list disagrees. (Below 700px the title gave
  way to the tier printed per row — superseded by the vertical band labels below,
  which fit at every width.)
- **An unplayed chart keeps its position and loses only its fill.** The projection is
  exactly as real for a chart nobody has touched; what is missing is the player's
  marker, not the number. It is also the case the number is most useful for, so there
  is a *Not played yet* list beside the gap list.
- **Mover rows carry the projection, with no editorial clause.** Four rows reading
  "above the folder mean for your level" would be filler.
- **Two comparison sections, labelled as such**, because the player's own scores never
  enter the ranking — plus the note that stops a column of red reading as a verdict:
  nobody is above the line on every chart, because if they were their competitive
  level would climb and the line would move with them.
- **Retired with the skill profile:** the Vs. Peers column and the whole dependency
  chain behind it, including the `GetChartSkillChipsQuery` and
  `GetPlayerScoreQualityQuery` reads the page made on every visit to feed it.

### Figures instead of prose (2026-08-11)

The page as first drawn carried ~200 words of prose on the Score path. Most of it
was *asserting* something the page had a number for, or could have had. Roughly 140
of them are gone.

| Was | Now |
|---|---|
| 60 words describing the cohort | Four figures: **players**, **level band**, **coverage**, **stale weight** |
| 28-word spread caption | Nothing. The bands are drawn over the dots |
| 30 words insisting the gap list is not an input | A `not an input` chip |
| 34 words on the moving bar | The **balance figure** — *4 above · 7 below* — plus one clause |
| 12-word movers caption | Nothing. Two headings with counts |
| "Players at your level score 971,200 · you 968,000" | `peers: 971,200 [S]` · `you: 968,000 [AAA+]` |

Three things this settled:

- **The cohort figures are real data, not a restatement.** `ScoreProjector` already
  computed the peer count, the matched level and the growth discount and threw all
  three away, so `Project` returns them alongside the scores rather than a bare
  dictionary. The count is players who **reached an estimate**, not the several
  hundred in the band — most of a band has played none of a given folder, and the
  band figure would overstate the evidence by that share. Freshness averages per
  *score*, because a peer who lent five scores lent five pieces of evidence.
- **The one surviving explanation waits to be asked.** "Nothing else votes, because
  the community lists read the same scores" answers a question a reader either has or
  does not. Behind a `?`, it is available; leading with it made the page argue with
  itself.
- **The window travels on the contract.** The page states a band, and it has no
  business knowing that a tier list reads ±0.5 while PUMBILITY reads ±1.0. A page
  that hardcoded one would print the wrong band the day the other moved.

**The value column went and the numbers moved onto the markers** (`MudTooltip` with
`ShowOnClick`, carrying score + tier + `LetterGradeIcon`). Two things that cost a
debugging round each: a tooltip renders its content only once *shown*, so a bUnit
assertion on a readout has to open it first; and `ShowOnClick` binds `onpointerup`,
so `ClickAsync` fails there claiming the element has no `onclick` handler.

**The axis spans the projections and nothing else.** It is the spread of *those*
numbers being explained. One score a hundred thousand points under the folder used to
drag the low end with it and squash every chart into the right-hand third; an
off-scale score now keeps its row, clamps to the edge, and draws as an arrow so it
still reads as "off the bottom" rather than "at the bottom".

**Band labels turned vertical, and therefore stayed at every width.** Rotated they
need ~12px instead of ~90, so they survive a phone — which is what let the row drop
the per-row tier name the 700px rule previously required. The picture carries the
tier at every width now, so colour is never the only channel (rule 8) without a
second copy of the same information on every line.

**Pass is on hold** (owner, 2026-08-11): it renders a coming-soon state rather than
explaining a blend that is about to be reworked. The handler still computes it and the
contract still carries it — that work is early, not wrong — so the follow-up session
starts from a page that is honest about not being ready.

A projection reaching fewer than **3** of a folder's charts stays silent. Tier bands
are cut from the spread of the values handed in, so one projection has a standard
deviation of zero and lands on the easiest band by construction — at full weight, off
one peer's one score. The floor is provisional; `ScoreProjectionCostProbeTests`
(exploration) exists to settle it along with the on-demand cost.

## Data flow

One new contract query (cached 6h/1h sliding per user+folder+lens, like the blend):
`GetPersonalizedTierListBreakdownQuery` → per-chart `BreakdownChartRecord`
(community = stored sources combined alone, personalized, skill, similar players,
plus the projected score itself) + `BreakdownSkillRecord` (deviation, evidence,
usable) + statuses/weights + the cohort figures (`PeerCount`, `CompetitiveLevel`,
`CompetitiveWindow`, `MeanFreshness`). The page composes the rest from published
contracts it already dispatches (`GetChartsQuery`, `GetPhoenixRecordsQuery`).
No new tables, ports, or jobs; nothing in `dev/export/*` or `api/*` changed.

`IScoreProjector.Project` returns `ScoreProjection` (scores + cohort) rather than a
bare dictionary — four call sites, two verticals. `PeerEstimator` (then `CohortEstimator`) is untouched:
`GrowthWeight` was already public for the exploration harness, so the freshness mean
is computed where the peers already are and the PUMBILITY arithmetic cannot move.

## Phoenix 2 reads PUMBILITY peers (2026-08-15)

On Phoenix 2 the Score projection no longer matches by competitive level: its peers are the
player's **PUMBILITY peers** — the players whose pool of the type sits within 500 below and 250 above
the viewer's, full 50-chart pool of the type on both sides (D53, round eight; ±3 rungs on the combined
total until then), Phoenix 2 scores only, the default rung, no growth weighting, hidden under five
peers ([pumbility-overhaul.md §4.8, §4.11](pumbility-overhaul.md)). The four figures survive with one
swap: **players** is the peer count, **level band** becomes the **pool window** ("17,110 – 17,860",
with *your doubles pool is 17,610* under it, `N0` off the PUMBILITY pages), **coverage** is unchanged,
and **stale weight** is not shown — nothing is discounted. `ScoreProjection` carries a `PeerGroup` naming which kind of band it is, so the page
renders by kind rather than by mix. Phoenix 1 keeps the competitive band, `p65` and the growth
discount, unchanged. A type without a full pool has no personalized Score source at all — the
blend falls to the community lists exactly as it does for a thin peer group today.
