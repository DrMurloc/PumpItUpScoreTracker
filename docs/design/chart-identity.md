# Chart identity — Speed, geometry, identity vs features

Owner-approved 2026-08-25, extended and corrected 2026-08-26. This doc is the buildable spec for
the chart-identity system that replaces the deleted Chabala skill rollup
([nuke-old-skill-categories.md](nuke-old-skill-categories.md), whose §7 addendum defers to this
doc for everything additive). It was designed against real data — the full Phoenix 2 folders and
the complete piucenter snapshot — and the golden examples in §8 are the acceptance bar: an
implementation that does not reproduce them is wrong, not differently-tuned.

Everything here derives from data already on disk. **No crawl, no new upstream dependency**:
piucenter is winding down (050726 = Phoenix final) and the snapshot zip
(`Downloads\piucenter-snapshot-050726.zip`, all 4,414 charts) is the corpus. The one-time
runbook is §9.

## 1. The model

A chart's identity is the set of **claims it earns about itself**, each measured **relative to
its folder** (mix, type, level). Absolute percentages are banned from the default display —
"bracket_run: 55%" means nothing without the folder around it (owner). Raw coverage stays
available behind the existing "Show Skill Metric" switch and on the chart page.

Two tiers, and the distinction is the whole feature (owner, 2026-08-26): *"a half double that
features bracket twists is a very different thing than a bracket twist chart that features
mid-6 patterns"*.

- **Identity** — what the chart *is*. Rendered filled, first, under a printed `Identity` label.
- **Features** — what the chart *also has*. Rendered as today's quiet outline, under `Features`.

**Identity is uncapped** (owner: *"Core identity should not be limited… if you got a deep twisty
half double with sustained bracket runs and anchor shuffles, I want that shit called out"*), and
**a chart may earn none** (*"if a chart is like… just a chart, it's fine for it to be nothing"*).
Claims stack; they are not a ladder where the first match wins.

Four axes were investigated. Three survive:

| Axis | Verdict |
|---|---|
| Composition (badge coverages) | ✅ banked already (`badge_fraction:*`, `top3:*`) |
| Crux (the chart at its hardest) | ✅ in the snapshot's per-segment data, banked by this build (§4) |
| **Geometry (where you stand, how you turn, whether you bracket)** | ✅ **new** — computed from the snapshot's limb-annotated arrows (§4b). This is the axis that answers "is it a half-double" and "how twisty is it" |
| Tempo / gimmicks | ❌ **dead — do not build.** piucenter's per-section "bpm" is back-computed from note density through a guessed note resolution; classifier wobble prints phantom tempo changes (CHAOS AGAIN D26 "shows" 30 tempos). Speed changes are also genuinely rare in PIU (owner: *"This isn't DDR"*). Real gimmick data needs .ssc tempo maps (the Resistance pack); separate effort, owner-gated |

## 2. The Speed tier list

A stored tier list named **`Speed`** in the tier-list family — the Popularity precedent: not a
difficulty judgement, same table, same machinery (`TierListSaga` computes, `ITierListRepository`
stores, per tier-list-family ownership rules).

- Per (mix, type, level): collect each chart's banked `nps` metric; μ and σ over the folder.
- Bands at z: `< −1.5` Very Slow · `< −0.5` Slow · `≤ +0.5` Mid Tempo · `≤ +1.5` Fast · else Very Fast.
- **Tail merge**: only the two extreme bands merge, once each, into the nearest **occupied**
  band. Merging into an adjacent *empty* band, or cascading merges, collapses a small folder to
  one section — both were live bugs.
- The middle band's localization key is **`Mid Tempo`**, never `Moderate` — that key is the
  comment-moderation button (ko `관리`), and reusing it renders "Manage" in every non-English
  locale while English looks perfect ([locale trap](../../CLAUDE.md#ui-conventions)).
- A chart with no `nps` metric → `Unrecorded` (renders as an "Unmeasured" section).
- `Order` = round(nps × 100) so the raw value survives; `Category` carries the band via
  `TierListCategory` ordinals mapped in presentation.
- Recompute: after a snapshot import / crawl completes, plus an /Admin rebuild button.
- Band labels are computed folder-relative labels with the NPS range printed beside them
  ("Slow · 10–11.3 NPS"). NOT published via api/v2 (Popularity/Chabala precedent).

**Speed as an identity claim**: only the extremes. `|z| ≥ 1.5` — i.e. exactly the Very Fast /
Very Slow bands — renders as an identity chip carrying the band *word*. Every other band renders
nothing. Owner, 2026-08-26: *"We don't want to soften charts in the 'very fast' or 'very slow'
category, we want those to truly be Very Fast and Very Slow."* A Site De La Rue D20 sits at
z = 1.46 and deliberately does not qualify.

**There is no NPS chip.** The raw number is meaningless at a glance (owner: *"NPS is a
meaningless number to most people. Good on chart details, not for quick info"*), so the
comfortable card carries no NPS chip at all; the number lives on the chart page and dialog.

## 3. Identity and features

Every rule below is expressed against the **folder baseline** (§5). Constants live in one place
(`ChartIdentityRules`) and are owner-tunable.

### 3.1 Presence — the only door

A badge earns a chip only by clearing its **presence bar**, and that bar is **per technique, per
folder, scaled to how common the technique is there** (owner, 2026-08-26: *"A d26 with a handful
of brackets but overall just being a run shouldn't even mention the thought of brackets. A S18
with brackets probably should at least feature them."*).

The bar is a **budget**: a technique may be claimed by about `PresenceBudget ÷ prevalence` of its
folder, capped at the prevalence itself, where prevalence is the share of the folder's charts
carrying it **at all** (any nonzero coverage). `PresenceBudget = 0.10`. So the bar rises with how
common the technique is, and collapses to "carries any at all" where it is rare.

A fixed bar could not work, and the spread is why: brackets sit on **13.7% of Phoenix 2 S14 and
79.4% of D26**. One number read those identically — at S14 it sat above the entire folder and not
one chart could say it had brackets, while at D26 a run chart with a handful of them could.

This also fixes the **exotic vocabulary**, which was structurally invisible. The rarest badges
have folder MAXIMUMS below the old fixed bar, so **no chart in any folder could ever say it had
one** — a chart could carry piucenter's own #1 and #2 picks and show neither.

The hand-tuned raised bars (`jack .40, jump .50, run .40, twist_90 .40`) are **deleted**, not
kept. They were raised by hand because those four ride nearly every chart, and their prevalence —
stable at 56–78% in every folder — now says so on its own. One mechanism, no lookup table.

Two consequences that are load-bearing:

- **Prevalence counts any nonzero coverage, never what clears the bar.** The bar is derived from
  prevalence, so reading rarity back off the bar's own output is circular — it made doublesteps,
  on 88% of some folders, read as rare.
- **The claim margin (§3.2) applies only where the budget bound the bar.** Below √`PresenceBudget`
  prevalence every carrier already clears it, so asking 1.25× of it asks for more than any chart
  has — the same above-the-maximum failure the drenched rule had, moved into the margin. There,
  carrying the technique at all *is* the claim.

Whole-chart qualities (`bursty`, `sustained`) carry no coverage and are admitted by piucenter's
dominance pick alone; they have no prevalence and never come through the rare rule.

Two hard rules on top:

- **The Achluoias rule** — a dominance-only top-3 pick with sub-threshold coverage establishes
  nothing. piucenter listed `bracket_drill` #3 on Achluoias D24 at 12.5% measured brackets; that
  must not make it a bracket chart.
- **Dominance picks admit nothing at all** (owner field test, 2026-08-26). The earlier rule let a
  pick under the bar become a chip "because their pick is a real signal about emphasis". Every
  chip the owner could not find on the pad walked in through that clause — A Site's Bracket Runs,
  Heliosphere's Bracket Jumps, 4NT's Bracket Twists, BSPower's Brackets — and it *crowded out*
  real coverage via the cap. Picks now only break ties in ordering.

### 3.2 Identity claims

A chart earns each of these independently; all that fire are shown, in this display order.

| # | Claim | Test | Renders |
|---|---|---|---|
| 1 | **Width** | mid-4 note-share ≥ 99.5% → `Quarter Double`; else mid-6 note-share ≥ 99.5% → `Half-Double`; else mid-6 ≤ folder p10 → `Wide`. Doubles only | geometry hue |
| 2 | **Twist extreme** | side-on share ≥ folder p90 → `Twist-heavy`; or side-on ≤ **5%** (singles) / **10%** (doubles) **and** crossed ≤ **2%** → `Twistless` | geometry hue |
| 3 | **Speed extreme** | `|z| ≥ 1.5` → `Very Fast` / `Very Slow` (§2) | geometry hue |
| 4 | **✦ Rare** | present, **coverage ≥ 1.25 × the qualifying bar**, and ≤ **12%** of the folder qualifies for that badge. Rarest first | family tint + ✦ |
| 5 | **Drenched** | present, **coverage ≥ the folder's p90 for that badge**, and ≥ 1.25 × the qualifying bar | family tint |
| 6 | **Difficulty Spike** | crux peakiness ≥ **+0.7** | amber, `Difficulty Spike +1.2` |
| 7 | **Hardest _X_s** | crux peakiness ≥ **+0.5** — the crux badges, as **one merged chip** | neutral body, per-badge family text |
| 8 | **Sustained** | the `sustained` dominance pick **and** `time_under_tension` ≥ folder p90 | family tint |

Everything else that cleared presence is a **feature**. Features are allowed to be common —
that was only ever a problem because common chips shouted at identity volume.

Three corrections the data forced, each recorded so it is not re-broken:

- **✦ needs margin, not just a pass.** Coverage steps by 1/segments (a 7-segment chart moves in
  .143 jumps), so "just over the bar" and "way over" were indistinguishable. That Kitty D22 cleared
  jack's .40 bar by **.029**, and because jacks are rare in D22 (6.3%) that promoted it to the
  loudest chip on the card. The 1.25× margin kills it and touches nothing the owner approved.
- **Drenched is a percentile, not a multiple.** The original rule (`≥ 2 × the p75 cutoff`) was
  **unsatisfiable for 31% of badge/folder pairs — 108 of 345 had `2 × p75` above their own folder
  maximum**. Doublestep in D20: p75 .375, folder max .714, rule demanded .727. Nakaka sits at the
  folder maximum and still failed, as did every doublestep chart everywhere. p90 always exists and
  always scales to the badge.
- **The 1.25× guard applies to drenched too.** Without it, p90 lets That Kitty's jacks back in —
  D22's jack p90 is only .393 *because* jacks are rare there. Rarity must not lower the bar.

### 3.3 The hard-section chip

One chip per chart, never two: it is one window, so the duration was always the same number
printed twice (owner). Carries up to two crux badges, each keeping its own family colour inside
a neutral chip body — the body names a *stretch*, not a skill, the same reasoning that keeps
Difficulty Spike out of the family palette.

- **Feature tier at peakiness ≥ .25, identity tier at ≥ .5, and the separate `Difficulty Spike`
  chip at ≥ .7.** Three gates because they answer different questions. Calibration comes from the
  owner's own reports: That Kitty (.17) stays silent, Windmill (.21) stays clean, New Rose (.29)
  speaks, BSPower (.62) headlines.
- `Difficulty Spike` and `Hardest _X_s` are **different claims about the same stretch** and both
  ship. Spike is an *elevation* claim (this plays above its rating); Hardest-_X_s is a
  *composition* claim (whatever the hardest part is, it's runs). Most charts are flat and have no
  spike but still have a hardest stretch — New Rose's 23-second coda is that case.
- The duration is the payload: Nakaka's `Hardest 6s` is a stumble to survive, New Rose's
  `Hardest 23s` is an ordeal to pace. Same chip, opposite advice.
- A badge already shown as an identity chip is **excluded** from the hard-section chip, and an
  identity claim **absorbs** its badge's feature chip. Both prevent printing a badge twice.
- **`mid4_doubles` / `mid6_doubles` are never hard-section badges** — geography is the width
  chip's job. Without this, Burn Out's crux (which ranks mid-4 #2) resurrects the exact chip the
  owner rejected.

### 3.4 The bracket veto

**Bracket-family badges** (`bracket`, `bracket_run`, `bracket_drill`, `bracket_jump`,
`bracket_twist`, `staggered_bracket`) require the chart's **bracket-row share ≥ 3%** — including
as hard-section badges.

Piucenter's bracket detection is a limb-assignment model and it mis-reads ordinary jumps.
Heliosphere D20 has **11 bracket rows in 845 (1.3%)**, every one a centre-plus-adjacent pair,
five of them in the final section — which is why it carried `last_segment_badge:bracket_jump`
and why the owner, watching the video, could not find a single bracket. The veto is calibrated
against the owner's own verdicts and separates them cleanly:

| Chart | Bracket-row share | Owner's verdict | Result |
|---|---|---|---|
| Nakaka D20 | 6.31% | "nailed it" | keeps |
| STAGER D20 | 5.65% | "nailed it" | keeps |
| Windmill D23 | 4.29% | "its entire thing" | keeps |
| BSPower D20 | 2.99% | "shouldn't be labeled" | **vetoed** |
| Heliosphere D20 | 1.30% | "not a single bracket" | **vetoed** |
| 4NT D20 | 0.56% | "there's like none" | **vetoed** |

### 3.5 Geography as a feature

`mid4_doubles` / `mid6_doubles` are **feature-only** (width owns the claim) and gate on
**note-share ≥ folder p75**, not on segment coverage. This is what reconciles the owner's
verdicts: Burn Out's segment-derived "71% mid-4" is 68.0% by actual notes against a p75 of
72.3 — folder-normal, chip correctly gone — while That Kitty (95.8% vs 93.7%) and New Rose
(96.8% vs 93.1%) keep theirs.

## 4. Crux metrics (banked from the snapshot)

Source: each chart JSON's `Segments` and `Segment metadata`, present for **all 4,414 charts**.
Crux = the **first segment achieving the maximum `level`**. New rows in `scores.ChartSkillMetric`
(Source=`PiuCenter`):

| Metric | Value |
|---|---|
| `crux_level` | the crux segment's `level` |
| `crux_peakiness` | `crux_level − METER` (negative = endurance grind — also identity) |
| `crux_position` | (crux start − chart start) ÷ (chart end − chart start), 0–1 |
| `crux_duration` | crux segment seconds |
| `crux_enps` | the crux segment's eNPS |
| `crux_badge:{badge}` | rank 1..3 for the crux segment's top badges |

Caveats measured, not guessed: per-segment `level` is piucenter's model against the printed
METER — good for banding, not arithmetic. Their `nps_summary` is already peak-ish — do **not**
build a "crux is faster" ratio on it. Catalog-wide sanity: median 7 segments/chart; crux position
median 0.76; peakiness p50 +0.23, p90 +0.89.

The window is recoverable and worth verifying against: `crux_position × span` is the window
**start**, and replaying the arrows in it reproduces `crux_enps` to within 0.1 (BSPower D20:
43.6–53.3s, computed 12.0 against a recorded 12.1).

## 4b. Geometry metrics (new, computed from the arrows)

The snapshot's per-chart JSON carries **every arrow with panel, timestamp and piucenter's limb
assignment** — part 0 is taps `[panel, time, limb]`, part 1 is holds `[panel, t0, t1, limb]`.
`StanceAnalyzer` (Catalog `Domain/`, pure) replays them: taps and hold-heads in time order, a
bracket averaging its two panels, and a stance measured on every row where both feet are planted
on distinct spots. τ = |atan2(dy, dx)| of the left→right foot vector; 0° square to the screen,
90° fully side-on, past 90° crossed over.

| Metric | Value |
|---|---|
| `pad_share_mid4` / `pad_share_mid6` | note-share on the middle 4 / middle 6 panels (a hold counts once, at its head) |
| `stance_diagonal` | share of stances at τ ≥ 44° |
| `stance_side_on` | share at τ ≥ 89° |
| `stance_crossed` | share at τ > 91° |
| `bracket_row_share` | rows where one limb hits 2+ panels, ÷ all rows |

Two measured facts that pin the thresholds:

- **The diagonal share is noise** — median ~78% at every level S15–S24, because the pad's corners
  make diagonal stance just "walking in Pump". It is drawer detail, never a chip.
- **Side-on share is the axis** — p10 ≈ 4–6%, median ≈ 10–15%, p90 ≈ 21–31% in singles, a 10×
  spread per folder. Doubles run structurally higher (median ~22%) because travelling between
  pads passes through side-on stances, hence the separate 10% Twistless floor.
- **The crossed guard is load-bearing.** Vook D20 measures 8.8% side-on of which **7.8% is
  crossovers** — a chart that barely rotates but crosses hard when it does is not twistless.

Definitional note, owner-acknowledged: a **vertical drill** (UL/DL or UR/DR on one pad) measures
90° and does count as side-on. Standing in that column you *are* square to the side of the pad.
A horizontal drill (UL/UR) measures 0° and does not. Changing this would require re-running every
folder distribution.

Validation the measure passes unaided: the most side-on singles are Mr. Larpus (55.4%), Poseidon
SC, Gothique Resonance, Rolling Christmas — the BanYa twist canon in order; the least are
Doppelganger (3.6%), Dead End, Naissance, Aragami. Hymn of Golden Glory sweeps its own spectrum
by chart — D17 3.6%, D20 44.9%, D21 33.9%, D25 13.7% — four identities from one song, correctly
separated. And Gargoyle FULL SONG's two files split 3.8% / 12.1%, the 3.8% being the one
piucenter tagged `run_without_twists`.

## 5. Folder baselines (materialized)

Per (mix, type, level, badge): **p75 coverage cutoff**, **p90 coverage** (the drenched bar), and
**qualified prevalence** (count of folder charts clearing the qualifying bar). Plus per (mix,
type, level) geometry rows: mid-4/mid-6 note-share **p75** and mid-6 **p10**, and side-on
**p90**. Owned by Catalog (internal entity + repository, registered in
`CatalogModelContribution`); rebuilt at the end of every snapshot import / crawl — the same sweep
that triggers the Speed rebuild. Chips for any chart on any surface (SRP included, where a page
mixes folders) are computed against **that chart's own folder** baseline.

## 6. One engine, every surface

Catalog contract: `GetChartIdentityQuery(chartIds, mix)` →
`ChartIdentityRecord(ChartId, IReadOnlyList<IdentityChipRecord>)`,
`IdentityChipRecord(Kind, Tier, Badge, DisplayName, BadgeCategory? Family, decimal? Detail,
IReadOnlyList<IdentityChipBadge> Badges)` where `Tier ∈ {Identity, Feature}`, `Kind ∈ {Width,
Twist, Speed, Rare, Drenched, Spike, HardSection, Sustained, Plain}`, `Detail` carries coverage,
peakiness or seconds, and `Badges` is populated only for the merged hard-section chip. Consumers:

- **/TierLists** card + table chips (replaces `GetChartSkillChipsQuery`).
- **Chart SRP** cards.
- **ChartDetailsDialog + /Charts/{mix}/{song}/{difficulty}**: the two chip groups, an identity
  sentence assembled from per-claim fragments (localizable — never hand-written prose), and the
  drawer rows: hardest section, speed (with raw NPS), stance, pad use, bracket share.
- **Verdict engine**: the crux-driven facet keeps its own 0.7 gate this iteration; aligning it to
  0.5 is a separate call, recorded here so it is not changed by accident.

## 7. Groupings (locked)

- **One grouping ships: "Group By Speed."** Group By Skill was pulled by the owner 2026-08-26
  ("for now"); its five family-name localization keys stay in the resx against its return. The
  Speed × Skill cross grouping remains shelved.
- Group By Speed: sections = Speed bands (§2), slow → fast, header carries the NPS range;
  pass-rate heat as today. Heat is honest, not monotone — slow sections can be red; that is
  signal, not a bug.
- **The Folder Stats drawer ships Variant B — per-badge rows** (owner, 2026-08-25): one ability
  row per badge with qualified presence in the folder (≥2 charts), worst-first by ability ×
  completion, weights = qualified coverage (whole-chart picks weigh 1.0), label column widened
  92px → 140px. Rows tint by the badge's family.

## 8. Golden examples (acceptance)

Computed against real Phoenix 2 metrics and real folder baselines. The build must reproduce these.

| Chart | Identity | Features |
|---|---|---|
| DUEL SC D23 | Half-Double · Twist-heavy · ✦Over-90 Twists · Close Twists | 90° Twists |
| Jupin SC D20 | Half-Double · Twistless · Drills | Bursty · Hardest 15s: Runs, Anchor Runs |
| Mr. Larpus S22 | Twist-heavy · ✦5-Stairs · ✦90° Twists · Over-90 Twists | — |
| Doppelganger S22 | Twistless · Difficulty Spike +0.7 · Hardest 22s: Runs, Bracket Runs | Jumps · Drills · Doublesteps · Bracket Jumps |
| Hymn of Golden Glory SC D20 | Very Slow · Twist-heavy · ✦Far Twists · Over-90 Twists · Close Twists | Cross-pad Transitions · Mid-6 Doubles |
| Windmill D23 | ✦Staggered Brackets · ✦Bracket Runs · 90° Twists | Anchor Runs |
| Nakakapagpabagabag D20 | Very Slow · Sustained · Doublesteps · Difficulty Spike +1.2 · Hardest 6s: Jumps, Brackets | 90° Twists |
| STAGER D20 | Very Slow · ✦Jacks · Doublesteps | Jumps |
| Gargoyle FULL SONG S21 | Sustained | — |
| Burn Out D23 | Difficulty Spike +0.8 · Hardest 15s: Runs, Close Twists | Doublesteps |
| Monolith D23 | ✦Over-90 Twists · ✦Bracket Twists · Close Twists · Difficulty Spike +1.4 · Hardest 21s: Yog Walks, Runs | Sustained |
| A Site De La Rue D20 | Runs · Difficulty Spike +1.2 · Hardest 6s: Drills, 90° Twists | Anchor Runs |
| BSPower Explosion D20 | Hardest 10s: Drills, 90° Twists | — (bracket jumps vetoed) |
| Heliosphere D20 | Difficulty Spike +1.0 · Hardest 14s: Runs, Drills | Bursty (bracket jumps vetoed) |
| **That Kitty D22** | **none** | Jacks · Mid-6 Doubles |
| **New Rose D23** | **none** | Mid-6 Doubles · Doublesteps · Hardest 23s: Runs, Anchor Runs |
| **4NT D20** | **none** | Mid-4 Doubles · Doublesteps · Hardest 24s: Close Twists, Over-90 Twists (brackets vetoed) |

The three "none" rows are as load-bearing as the rest: they are the owner's *"it's fine for it to
be nothing"*, and a build that invents a claim for them is wrong.

## 9. Runbook (one-time, post-deploy)

1. Deploy.
2. /Admin/PiuCenter → upload `piucenter-snapshot-050726.zip`. The re-import banks the `crux_*`
   **and geometry** metrics and rebuilds folder baselines. Chips are empty until it runs.
3. Press "Rebuild Speed tier lists" once per mix that has metrics.
4. No other presses. Verdict/meta caches roll daily at 13:00 UTC; blend caches within 6h.

## 10. Out of scope, recorded so nobody re-litigates

- Tempo/gimmick axis (§1) — dead without .ssc data.
- The Speed × Skill cross grouping, and Group By Skill itself — both owner-shelved.
- Dominant-skills line on band headers — rejected ("bloat").
- Fast/Slow/EndRun derived tags — deleted with the mapper, intentionally.
- Softening the Very Fast / Very Slow bands to catch near-misses (A Site at z = 1.46) — rejected
  2026-08-26: those bands must mean what they say.
- An NPS chip on the comfortable card — deleted 2026-08-26.
- `practice_rank` as an alternative route to a Drenched claim — the owner chose margin-only, and
  the p90 fix removed the case that motivated it.
- api/v2 — untouched. `ChartSkillProfile` already speaks raw badges; the Speed list stays
  unpublished like Popularity/Chabala.
