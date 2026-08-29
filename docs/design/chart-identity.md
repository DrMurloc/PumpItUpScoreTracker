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

- **Identity** — what the chart *is*. The only thing a card shows.
- **Features** — what the chart *also has*. Chart page and dialog only, under a printed label.

**The detail surfaces are chips and nothing else** (owner, 2026-08-26). On the chart page the
chips live in the **hero verdict block**, not in a section further down — that block is what a
reader looks at to find out what they are about to play, and it was answering with one sentence
built from a different rule than the chips use. The separate Skill fingerprint section is gone.
The old section and the chart dialog both carried percentage bars — badge coverage, grind/spikes,
pad and stance shares — and every one of them went. A percentage only reads against the folder the
reader cannot see from either surface, which is the comparison the chips have already done; the
owner's verdict on the two that survived longest was *"idk what grind and side-on even are"*. Two
sentences went with the bars: the crux line is the "Hardest Ns" chip in longer words, and
"Dominated by…" picked its two badges by a different rule than the chips do, so it could contradict
the row directly beneath it. Both surfaces also print the chart's **speed band** — all five, not
just the outer two, since they have room for a measurement where a card does not; it replaces the
engine's own Speed chip rather than joining it, and files under Features unless it is an outer band.

**A surface that already says how fast a chart is drops the speed chip.** On the tier list grouped
by Speed, the section heading over the card and the chip inside it are the same word, and a claim
that repeats its own heading spends the card's loudest slot on the one thing the reader cannot fail
to know (owner, 2026-08-26).

**Cards show identity and nothing else** (owner, 2026-08-26): spending a card's room on what a
chart merely also has buys nothing a player deciding what to play can use. A chart with no identity
therefore shows no chips at all.

**The card prints every identity chip — no cap, no "+2"** (owner, 2026-08-26: *"we should just show
all chips at this point… they're MOSTLY all meaningful now"*). The three-chip cap was there when
features shared the row and the chips were not all earned; once features came off and every
remaining chip is a claim measured against the chart's own folder, a count told the player there
was something else without telling them what. The row wraps.

**Identity is uncapped** (owner: *"Core identity should not be limited… if you got a deep twisty
half double with sustained bracket runs and anchor shuffles, I want that shit called out"*), and
**a chart may earn none** (*"if a chart is like… just a chart, it's fine for it to be nothing"*).
Claims stack; they are not a ladder where the first match wins.

**A claim wears the family it belongs to** (owner, 2026-08-26). Every measured claim started on
one shared "geometry" hue, which described where the number came from rather than what it says:

| Claim | Family | Why |
|---|---|---|
| Longest run | Stamina & Runs | A chart's longest run *is* a stamina claim. |
| Quarter Double · Half-Double · Wide | Doubles Tech | Width only ever fires on a doubles chart. |
| Twist-heavy | Twists | It is a twists claim. |
| Twistless | Stamina & Runs | A chart that never turns you is a running chart — the absence of twists is not a fact about twists, it is what is there instead. |
| Hold-heavy | Tech | A chart built on holds plays as technique — reading, weight, timing under tension — not as footspeed (owner, 2026-08-29). |
| Few Holds | Stamina & Runs | Every judgement is a step your feet make; the absence of holds is a stamina fact, by the same reasoning as Twistless. |
| Very Slow · Mid Tempo · Very Fast | the Speed ramp (§2) | So a chip and the folder section it came from cannot disagree. |

Nothing wears the geometry hue any more; the token survives only as the Core Skills label's tint.

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

**Its own ramp, cool to hot** — `--speed-1` … `--speed-5`, read through `ThemeScales.SpeedColor`.
Deliberately not the difficulty ramp: a slow chart at a high level is not an easy one, and the
folder's pass rates routinely say the opposite, which is exactly what a green-to-red reading would
assert. The Speed grouping's section headers wear it instead of the pass-rate heat every other
grouping wears — heat made the whole list one shade of red, because a hard folder is hard at every
tempo, so the colour said nothing about speed while looking exactly like it did (owner, 2026-08-26).

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

**The union with piucenter's own picks** (owner, 2026-08-26). Their `chart_skill_summary` is the
same idea as ours computed on better inputs — a percentile of each technique against charts of the
same type a level either side (`piu-annotate`, `get_top_chart_skills`) — so where they name
something we did not, that is a second opinion worth carrying rather than a contradiction.
Measured on Phoenix 2 D23: **58% of our claims are exactly theirs**, another 19% are the same claim
at finer granularity (we say `twist_close`, they say `twists`), and 23% are genuinely new. The
union adds under half a badge per chart.

Their picks are still subject to every veto, because a veto exists to overrule a measurement we do
not trust: the bracket-row-share gate (§3.4), the geography rule (§3.5), and the sustain gate —
their `sustained` is the variance of the eNPS timeline, which says a chart is EVEN rather than
long, so a pick over ten seconds of tension is still refused.

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
| 4 | **Hold extreme** | hold share ≥ folder p90 **and** the file passes the hold trust check → `Hold-heavy`; hold share ≤ folder p10, where p10 > 0 → `Few Holds` (§3.9) | Tech / Stamina & Runs |
| 5 | **✦ Rare** | present, **coverage ≥ 1.25 × the qualifying bar**, and ≤ **12%** of the folder qualifies for that badge. Rarest first | family tint + ✦ |
| 6 | **Drenched** | present, **coverage ≥ the folder's p90 for that badge**, and ≥ 1.25 × the qualifying bar | family tint |
| 7 | **Difficulty Spike** | crux peakiness ≥ **+0.7** | amber, `Difficulty Spike +1.2` |
| 8 | **Hardest _X_s** | crux peakiness ≥ **+0.5** — the crux badges, as **one merged chip** | neutral body, per-badge family text |
| 9 | **Sustained** | the `sustained` dominance pick **and** `time_under_tension` ≥ folder p90 | family tint |

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

### 3.3b Phantom skills from limb prediction

**`footswitch`, `hold_footswitch` and `hold_footslide` require the chart's measured
repeated-panel share to reach 5%.** Piucenter defines a footswitch as *a repeated single panel
where the **predicted** limbs differ* (`piu_annotate/segment/skills.py`), so a footswitch and a
jack are the **same note pattern** and only an ML guess separates them. `hold_footswitch` is a limb
annotation code outright; `hold_footslide` is built entirely from which foot reads which arrow.

Their own pipeline knows this failure mode and guards only one badge with it: `models.py` excludes
`doublestep` from rare skills on predicted charts because it is *"a common error for predicted limb
annotations, especially on chart sections with holds and taps"*. These three never got the guard.

The precondition is measurable from the arrows and owes the limb model nothing, and it separates
cleanly with nothing in between:

| chart | repeated-panel rows | share | verdict |
|---|---|---|---|
| Hi-Bi D21 | 123 / 732 | **16.80%** | real (and hand-annotated) |
| Headless Chicken S21 | 93 / 587 | **15.84%** | real |
| Cleaner S21 | 15 / 688 | 2.18% | phantom |
| Gothique Resonance S21 | 12 / 703 | 1.71% | phantom |
| Baroque Virus FULL S21 | 3 / 2,327 | 0.13% | phantom |

Their `Manual limb annotation` flag is **not** the guard: only 698 of 4,414 charts carry it, and
Headless Chicken S21 is not one of them.

### 3.8 Longest run

**`sustain_time` is the chart's longest single run**, not a total — their pipeline computes it as
`max(range_len(r) for r in eNPS ranges of interest)`, where `time_under_tension` is the `sum`. It
renders as **`Longest run: 22s`**, in seconds, because seconds are what a player feels.

The claim is **absolute, not folder-relative** (owner, 2026-08-26): a fifty-second run is a
fifty-second run whoever it stands next to. The bar is **22% of the chart's played span**, the
corpus 75th percentile — the median longest run covers 13.5%. Cleaner SHORT CUT S21 (46.5%),
Gothique Resonance S21 (22.5%) and Baroque Virus FULL S21 (20.8%) are run charts; Gargoyle FULL S21
(14.3%) and Headless Chicken S21 (11.2%) are not.

### 3.9 Hold share

Owner-approved 2026-08-29. What fraction of a chart's judgements are held rather than stepped —
the axis that separates Jupin D23 (927 steps in 1,142 notes) from Solve My Hurt D23 (429 steps
in 1,378) at the same level. The full precision audit behind these rules is
[stepfile-precision.md](stepfile-precision.md).

**The measure**: `hold share = (NoteCount − tap_rows) ÷ NoteCount`, where `NoteCount` is the
game's own judged total and `tap_rows` the banked step count. It is computed **where the chart
meets a mix's catalog** — never banked, because `NoteCount` is per-mix and Phoenix 2's keeps
refilling from play. Phoenix 2 nulls fall back to Phoenix 1's count (the calculator's rule); a
chart whose taps exceed its judged total (3 on Phoenix 1, one more on Phoenix 2) yields
nothing. The derivation never
reads the file's own hold data, which is era-authored and unreliable.

**The claims** — both Identity tier, judged against the folder's `hold_share` baseline row:

- **`Hold-heavy`** — hold share ≥ folder **p90**, and the file passes the trust check below.
  Wears **Tech** (owner: a hold chart plays as technique, not footspeed).
- **`Few Holds`** — hold share ≤ folder **p10**, only where the folder's p10 is **above zero**.
  Wears **Stamina & Runs**. The p10 > 0 floor is load-bearing: below S10 most of a folder has no
  holds at all, its p10 is zero, and an unfloored low claim fires on a third of S01–S03.

**No numbers, anywhere** (owner, 2026-08-29): the chips carry the word only, on every surface —
"not until I have HIGH confidence in those numbers". No steps-of-notes line, no percentage.

**The trust check** (high claim only): the derivation borrows the file's step count, and a file
that is not the shipped chart always errs by *inflating* the inferred holds — missing steps read
as holds. The check asks the file to confirm its own story: if the inferred hold ticks exceed
**1.5×** the tick total the file itself contains, the two numbers cannot both be true, the file
is not this chart, and `Hold-heavy` stays silent. Destination SHORT CUT D20 infers 525 holds
from a file that contains 123 — vetoed; Iolite Sky D20 infers 844 against a file carrying 848 —
keeps. Flags 5.1% of the corpus, over half of it SHORT CUTs, and removes 76 of 457 would-be
high claims. `Few Holds` needs no check — this failure mode cannot point that direction. A
vetoed chart heals with no code change when its file is fixed and re-annotated: the check reads
the banked metrics, so the next snapshot upload un-gates it.

**Why it is a new axis**: NPS is hold-blind by construction (p95 of effective downpresses —
taps and hold starts only, ticks nowhere in it), so speed and hold share are structurally
independent; of 457 high-hold charts only 73 are also Very Slow. And the badge vocabulary has
no hold word at all — the only two candidates are limb-prediction phantoms already vetoed.

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

## 4c. Key matching, and why an upload can look like a no-op

A metric only exists for a chart we bound its piucenter key to. Keys match on a composite of
`Normalize(song) | Normalize(artist) | singles|doubles | level | ARCADE|REMIX|SHORTCUT|FULLSONG`,
where `Normalize` folds diacritics and keeps only letters and digits. Four rules govern it, three
of them learned from failures that all presented the same way — a re-upload that changed nothing:

- **A parked alias is retried every run.** Whether a key matches depends on the matcher *and* on
  the catalog, and both move. Nothing retried, so a key that failed once failed forever: the
  Phoenix 2 catalog flip should have rebound 176 aliases and rebound none, because the rows
  already existed and only brand-new keys were ever matched. **Manual** rows are exempt — an
  admin's binding, including a deliberate non-binding, is not the auto-matcher's to overwrite.
- **A rejected key neither binds nor reserves.** One chart takes one key, so refusing to ingest a
  key while it still held the chart's slot left the surviving key of the pair unable to bind at
  all — Gargoyle FULL SONG S21 kept metrics from before its v2 key was rejected, with no route to
  replace them.
- **The `_v1` / `_v2` variant marker is stripped from the song half.** Piucenter appends it when
  two stepcharts share a song, type and level; left alone it normalizes *into* the song name and
  matches nothing. Four keys in the corpus carry one, and no catalog song ends in a `vN` token.
- **A bare artist is a fallback key, never a rewrite.** We store the localized name beside the
  Latin one and piucenter carries Latin only, and normalization cannot bridge it — Hangul and CJK
  characters *are* letters, so they survive the fold and `IVE (아이브)` never meets `IVE`. The
  stripped form is merged into the match index only where nothing exact claims that key, so it
  can rescue a dead lookup and can never repoint a live one.

What normalization **cannot** fix is a romanized artist: `打打だいず Vs. Tanchiky Vs. からめる` and
`D-D-Dice Vs. Tanchiky Vs. Karameru` share no characters. Those need the generator to emit the
native `ARTIST` string, or a per-song alias.

## 5. Folder baselines (materialized)

**A folder's percentiles are read from levels L−1, L and L+1** of the same mix and type — the
cutoffs still belong to L, only the population widens (owner, 2026-08-26). Charts do not respect
level boundaries: a technique that is ordinary at 21 is ordinary at 20 and 22, and one level is a
thin sample to read a percentile off — thinner still at either end of the scale. Piucenter reached
the same shape independently (`piu-annotate`, `get_top_chart_skills`) with a two-level window.

Per (mix, type, level, badge): **p75 coverage cutoff**, **p90 coverage** (the drenched bar), the
**presence bar** (§3.1), and **prevalence** (count of peer charts carrying the badge at all). Plus per (mix,
type, level) geometry rows: mid-4/mid-6 note-share **p75** and mid-6 **p10**, side-on
**p90**, and the **`hold_share` row** (p10 low / p90 high — §3.9), whose input is the one
per-mix number in the sweep: the baseline read carries each chart's judged note count per mix,
Phoenix 2 falling back to Phoenix 1. Owned by Catalog (internal entity + repository, registered in
`CatalogModelContribution`); rebuilt at the end of every snapshot import / crawl — the same sweep
that triggers the Speed rebuild. Chips for any chart on any surface (SRP included, where a page
mixes folders) are computed against **that chart's own folder** baseline.

## 6. One engine, every surface

Catalog contract: `GetChartIdentityQuery(chartIds, mix)` →
`ChartIdentityRecord(ChartId, IReadOnlyList<IdentityChipRecord>)`,
`IdentityChipRecord(Kind, Tier, Badge, DisplayName, BadgeCategory? Family, decimal? Detail,
IReadOnlyList<IdentityChipBadge> Badges)` where `Tier ∈ {Identity, Feature}`, `Kind ∈ {Width,
Twist, Speed, Holds, Rare, Drenched, Spike, HardSection, Sustained, Plain}`, `Detail` carries coverage,
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
| DUEL SC D23 | Half-Double · Twist-heavy · Hold-heavy · ✦Over-90 Twists · Close Twists | 90° Twists |
| Jupin SC D20 | Half-Double · Twistless · Drills | Bursty · Hardest 15s: Runs, Anchor Runs |
| Mr. Larpus S22 | Twist-heavy · ✦5-Stairs · ✦90° Twists · Over-90 Twists | — |
| Doppelganger S22 | Twistless · Difficulty Spike +0.7 · Hardest 22s: Runs, Bracket Runs | Jumps · Drills · Doublesteps · Bracket Jumps |
| Hymn of Golden Glory SC D20 | Very Slow · Twist-heavy · Hold-heavy · ✦Far Twists · Over-90 Twists · Close Twists | Cross-pad Transitions · Mid-6 Doubles |
| Windmill D23 | ✦Staggered Brackets · ✦Bracket Runs · 90° Twists | Anchor Runs |
| Nakakapagpabagabag D20 | Very Slow · Hold-heavy · Sustained · Doublesteps · Difficulty Spike +1.2 · Hardest 6s: Jumps, Brackets | 90° Twists |
| STAGER D20 | Very Slow · ✦Jacks · Doublesteps | Jumps |
| Gargoyle FULL SONG S21 | Few Holds · Sustained | — |
| Burn Out D23 | Difficulty Spike +0.8 · Hardest 15s: Runs, Close Twists | Doublesteps |
| Monolith D23 | Hold-heavy · ✦Over-90 Twists · ✦Bracket Twists · Close Twists · Difficulty Spike +1.4 · Hardest 21s: Yog Walks, Runs | Sustained |
| A Site De La Rue D20 | Runs · Difficulty Spike +1.2 · Hardest 6s: Drills, 90° Twists | Anchor Runs |
| BSPower Explosion D20 | Hardest 10s: Drills, 90° Twists | — (bracket jumps vetoed) |
| Heliosphere D20 | Difficulty Spike +1.0 · Hardest 14s: Runs, Drills | Bursty (bracket jumps vetoed) |
| That Kitty D22 | Hold-heavy | Jacks · Mid-6 Doubles |
| **New Rose D23** | **none** | Mid-6 Doubles · Doublesteps · Hardest 23s: Runs, Anchor Runs |
| **4NT D20** | **none** | Mid-4 Doubles · Doublesteps · Hardest 24s: Close Twists, Over-90 Twists (brackets vetoed) |

The two "none" rows are as load-bearing as the rest: they are the owner's *"it's fine for it to
be nothing"*, and a build that invents a claim for them is wrong. That Kitty D22 was the third —
it pinned the over-claiming rules its jacks exposed — and gained `Hold-heavy` when hold share
arrived (owner, 2026-08-29: 354 steps inside 1,087 notes, 0.674 against a folder p90 of 0.610):
a new true measurement is not those bugs returning. Its rules stay pinned by the other two rows.

The 2026-08-29 hold chips in this table were verified against the real banked Phoenix 2 data
with the engine's own L±1 folder windows and the trust check applied: six rows gained a chip
(DUEL SC, Hymn SC, Monolith, Nakakapagpabagabag and That Kitty wear `Hold-heavy`; Gargoyle FULL
SONG wears `Few Holds` at 0.250 against an S21 p10 of 0.328), 4NT (0.556) and New Rose (0.446)
sit mid-folder and correctly stay quiet, and Burn Out misses the D23 bar by 0.007 — measured,
not rounded into a chip. Mr. Larpus S22 is the table's one vetoed file (its implied total
disagrees past 1.5×), which changes nothing here: its share sits mid-folder anyway.

## 9. Runbook (one-time, post-deploy)

1. Deploy. **Order matters**: the import binds aliases with whatever matcher is running, and a
   key it fails to bind is now retried next run rather than parked forever (§4c) — but only from
   a build that has the retry.
2. /Admin/PiuCenter → upload the snapshot. The import banks the `crux_*` **and geometry** metrics
   — including `nps` and `chart_span`, which Speed and Longest run read — rebinds every parked
   alias, and rebuilds folder baselines. Chips are empty until it runs.
3. Press "Rebuild Speed tier lists" once per mix that has metrics.
4. No other presses. Verdict/meta caches roll daily at 13:00 UTC; blend caches within 6h.

The hold-share claims (§3.9, added 2026-08-29) piggyback on the same lever: their `hold_share`
baseline rows exist only after a baseline rebuild, so a deploy that adds them needs **one
snapshot re-upload** (or the next crawl completion) before hold chips appear.

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
- Hold numbers on any surface — rejected 2026-08-29 ("not until I have HIGH confidence in those
  numbers"); the hold chips are word-only everywhere.
- A hold-share drawer row or Show-Skill-Metric read-out — same ruling, same date.
