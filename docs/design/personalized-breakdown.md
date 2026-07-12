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
- **Players Like You blend source**: ±1.0 competitive level with closeness falloff.

Same spirit, different sources; aligning them would mean new cohort aggregation for
no user-visible gain.

## Data flow

One new contract query (cached 6h/1h sliding per user+folder+lens, like the blend):
`GetPersonalizedTierListBreakdownQuery` → per-chart `BreakdownChartRecord`
(community = stored sources combined alone, personalized, skill, similar players)
+ `BreakdownSkillRecord` (deviation, evidence, usable) + statuses/weights. The page
composes the rest from published contracts it already dispatches (`GetChartsQuery`,
`GetChartSkillChipsQuery`, `GetPlayerScoreQualityQuery`, `GetPhoenixRecordsQuery`).
No new tables, ports, or jobs; nothing in `dev/export/*` or `api/*` changed.
