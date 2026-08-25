# Chart identity — Speed, chips, crux

Owner-approved 2026-08-25. This doc is the buildable spec for the chart-identity system that
replaces the deleted Chabala skill rollup ([nuke-old-skill-categories.md](nuke-old-skill-categories.md),
whose §7 addendum defers to this doc for everything additive). It was designed against real data
— the full Phoenix D24/D23 folders and the complete piucenter snapshot — and the golden examples
in §8 are the acceptance bar: an implementation that does not reproduce them is wrong, not
differently-tuned.

Everything here derives from data already on disk. **No crawl, no new upstream dependency**:
piucenter is winding down (050726 = Phoenix final) and the snapshot zip
(`Downloads\piucenter-snapshot-050726.zip`, all 4,414 charts) is the corpus. The one-time
runbook is §9.

## 1. The model

A chart's identity = **Speed band × chips**, both computed **relative to its folder**
(mix, type, level). Absolute percentages are banned from the default display — "bracket_run:
55%" means nothing without the folder around it (owner). Raw coverage stays available behind
the existing "Show Skill Metric" switch.

Three axes were investigated and two survive:

| Axis | Verdict |
|---|---|
| Composition (badge coverages) | ✅ banked already (`badge_fraction:*`, `top3:*`) |
| Crux (the chart at its hardest) | ✅ in the snapshot's per-segment data, banked by this build (§4) |
| Tempo / gimmicks | ❌ **dead — do not build.** piucenter's per-section "bpm" is back-computed from note density through a guessed note resolution; classifier wobble prints phantom tempo changes (CHAOS AGAIN D26 "shows" 30 tempos). Real gimmick data needs .ssc tempo maps (the Resistance pack); separate effort, owner-gated |

## 2. The Speed tier list

A stored tier list named **`Speed`** in the tier-list family — the Popularity precedent: not a
difficulty judgement, same table, same machinery (`TierListSaga` computes, `ITierListRepository`
stores, per tier-list-family ownership rules).

- Per (mix, type, level): collect each chart's banked `nps` metric; μ and σ over the folder.
- Bands at z: `< −1.5` Very Slow · `< −0.5` Slow · `≤ +0.5` Moderate · `≤ +1.5` Fast · else Very Fast.
- **Tail merge**: a band holding < 4 charts merges into its inward neighbor (Very Fast → Fast,
  Very Slow → Slow). D24 merges Very Fast (2 charts); D23 merges nothing.
- A chart with no `nps` metric → `Unrecorded` (renders as an "Unmeasured" section).
- `Order` = round(nps × 100) so the raw value survives; `Category` carries the band via
  `TierListCategory` ordinals mapped in presentation (VeryEasy=Very Slow … VeryHard=Very Fast —
  same trick every non-difficulty list in the family uses).
- Recompute: after a snapshot import / crawl completes, plus an /Admin rebuild button. Not
  nightly — the input changes only when piucenter data does.
- Band labels are **computed folder-relative labels with the NPS range printed beside them**
  ("Slow · 10–11.3 NPS"), never a resurrected enum. NOT published via api/v2 (Popularity/Chabala
  precedent).

## 3. Chips v2 — four folder-relative kinds

Selection rules, in order. Constants are owner-tunable and live in one place; current values
were validated by eye on D24/D23/S7 (owner: "Those are great").

| # | Kind | Rule | Cap | Render |
|---|---|---|---|---|
| 1 | **✦ Unique** | badge has *qualified presence* on the chart AND ≤ **12%** of the folder's charts have it at all; rarest first | 2 | family tint + dashed border + ✦ |
| 2 | **Core** | coverage ≥ **0.15** floor AND ≥ **75th percentile** of the folder's coverages for that badge; candidates = qualified badges ∪ dominance picks meeting the floor; dominance picks rank first, then by percentile | 3 | family tint (today's chip) |
| 3 | **▲ Spike** | crux peakiness ≥ **+0.7** (crux segment level − printed METER) | 1 | amber, "Spike +1.3" |
| 4 | **crux:** | the crux segment's badges, only when a Spike fired and the badge isn't already shown | 2 | family tint, outlined, "crux:" prefix |
| 5 | fallback | when 1–4 all empty: piucenter's plain top-3, muted neutral | 3 | today's neutral chip |

Definitions:
- *Qualified presence* = `coverage ≥ ThresholdFor(badge)` (0.30 default; jack .40, jump .50,
  run .40, twist_90 .40 — the existing `PiuCenterSkillMapper.ThresholdFor`, which survives the
  mapper's deletion), **or** the badge is a whole-chart quality (`bursty`, `sustained` — they
  never carry coverage) present in the top-3.
- The **Achluoias rule** (owner-caught): a dominance-only top-3 pick with sub-threshold coverage
  **renders** (fallback/ordering) but never establishes presence — piucenter listed
  `bracket_drill` #3 on Achluoias D24 at 12.5% measured brackets; that must not make it a
  bracket chart.
- Percentiles/prevalence come from the **folder baseline** (§5); absent coverage counts as 0 in
  the folder distribution.
- Chip order on a card: unique → core → spike → crux → fallback.

## 4. Crux metrics (new, banked from the snapshot)

Source: each chart JSON's `Segments` (`[start_s, end_s, start_note, end_note]`) and
`Segment metadata` (`{eNPS, level, 'Skill badges', 'rare skills'}`) — present for **all 4,414
charts**. Crux = the **first segment achieving the maximum `level`**. New rows in
`scores.ChartSkillMetric` (Source=`PiuCenter`), written by `PiuCenterDataParser` +
`BuildMetrics`:

| Metric | Value |
|---|---|
| `crux_level` | the crux segment's `level` |
| `crux_peakiness` | `crux_level − METER` (spike ≥ +0.7; negative = endurance grind — also identity) |
| `crux_position` | (crux start − chart start) ÷ (chart end − chart start), 0–1 |
| `crux_duration` | crux segment seconds |
| `crux_enps` | the crux segment's eNPS |
| `crux_badge:{badge}` | rank 1..3 for the crux segment's top badges |

Caveats measured, not guessed: per-segment `level` is piucenter's model against the printed
METER — good for banding, not arithmetic. Their `nps_summary` is already peak-ish (crux_enps ÷
chart nps reads ≤ 1.0 nearly everywhere) — do **not** build a "crux is faster" ratio on it.
Catalog-wide facts for sanity checks: median 7 segments/chart; crux position median 0.76 (2,312
of 4,414 in the last quarter); peakiness p50 +0.23, p90 +0.89; 319 charts ≥ +1.0; crux badges
introduce a skill absent from the chart's own top-3 in 95% of charts.

## 5. Folder baselines (new, materialized)

Per (mix, type, level, badge): **p75 coverage cutoff** and **qualified prevalence** (count of
folder charts with qualified presence). Owned by Catalog (internal entity + repository,
registered in `CatalogModelContribution`); rebuilt at the end of every snapshot import / crawl
— the same sweep that triggers the Speed rebuild. This is what makes identity one normalized
read instead of five surfaces re-deriving folder context. Chips for any chart on any surface
(SRP included, where a page mixes folders) are computed against **that chart's own folder**
baseline.

## 6. One engine, every surface

New Catalog contract: `GetChartIdentityQuery(chartIds, mix)` →
`ChartIdentityRecord(ChartId, IReadOnlyList<IdentityChipRecord>)`,
`IdentityChipRecord(Kind, Badge, DisplayName, BadgeCategory? Family, decimal? Detail)` where
Kind ∈ {Unique, Core, Spike, Crux, Fallback} and Detail carries coverage (core/unique) or
peakiness (spike). Consumers:

- **/TierLists** card + table chips (replaces `GetChartSkillChipsQuery`).
- **Chart SRP** cards (replaces `GetChartBadgeChipsQuery` chips; that query's coverage-bar use
  on the chart page/dialog stays for the detail bars).
- **ChartDetailsDialog + /Charts/{mix}/{song}/{difficulty}**: identity chips + one identity
  line in the step-analysis block: `Speed band · ▲ spike · crux badges` (Speed read via the
  ChartIntelligence tier-list contract — composed in Web; Catalog never references
  ChartIntelligence).
- **Verdict engine**: `StyleFingerprintVerdict` is replaced by a crux-driven facet — top
  identity chips + spike/position — rendering "Its hardest 19 seconds are anchor runs at the
  very end." Contract re-key: `SkillCoverageRecord(Skill, …)` → badge string + display name.
  Lands in SEO meta descriptions automatically.

## 7. Groupings and filing (locked)

- **Two groupings ship: "Group By Speed" and "Group By Skill"** (owner: "for NOW"). The
  Speed × Skill cross grouping is **shelved** — the design and validated numbers live in this
  session's mock; do not build it unprompted.
- Group By Speed: sections = Speed bands (§2), slow → fast, header carries the NPS range;
  pass-rate heat as today. Heat is honest, not monotone — slow sections can be red (D24: Slow
  33% vs Moderate 56%); that is signal, not a bug.
- Group By Skill: **the 5 `BadgeCategory` families**, weakest-first. **Filing is chip-driven**
  (owner-approved 2026-08-25): a chart files under the families of its chips — unique ∪ core ∪
  crux-when-spiked ∪ fallback. Spike files nothing (shape, not skill). Fallback filing means a
  chart is "Not Tagged" only when piucenter has nothing at all. The chip that filed a chart is
  visible on its card — grouping and chips are one system.
- The five family display names (Brackets, Twists, Stamina & Runs, Tech, Doubles Tech) and five
  band names become localization keys ×9 locales, alphabetical insertion, Murloc alphabet rules
  apply (`docs/LOCALIZATION-en-ZW.md`).
- **Open (owner)**: the Folder Stats drawer ("Your Skills in Folder") — 5 family rows vs 30
  badge rows vs expandable-family-rows. Mock shows both at true height. Default if forced:
  family rows, same ability math, weights = qualified coverage (whole-chart picks weigh 1.0).

## 8. Golden examples (acceptance)

From the prototype against real data — the build must reproduce these (constants as in §3):

| Chart | Expected identity |
|---|---|
| Scorpion King D23 | core Brackets, Doublesteps, Bursty · **▲+1.5 · crux: Bracket Jumps** |
| Achluoias D24 | ✦Drills ✦Anchor Runs (≈10% folder prevalence each); **never files Brackets** |
| Hymn of Golden Glory SC D24 | ✦Bracket Runs ✦Staggered Brackets · ▲+1.3 · crux: Close Twists |
| WI-EX-DOC-VA D24 | ✦Jacks ✦Drills · core incl. Splits |
| Gargoyle D23 | ✦Sustained · Runs, Drills → files Stamina & Runs **only** |
| Cygnus D23 | fallback only (Bracket Drills, Bracket Twists, Twists) — still files via fallback |
| Uranium D24 | fallback (10-Stairs, Jacks, Twists) — thin coverage everywhere is honest |
| 8 6 FS D23 | Very Slow (6.5 NPS, folder slowest) · ✦Sustained · **no spike** (peakiness −1.0: an 82-second grind) |
| Beethoven Virus S7 | ✦far/over-90 twists · spike ≈ +1.4 · crux Twist 90 (the folder's famous liar, found unaided) |

D24 noise check: median chips/card drops 5 → 3; accents scarce (D24: 3 spikes; D23: 12 spikes,
1 fallback-only card across 125 — all alias-matched).

## 9. Runbook (one-time, post-deploy)

1. Deploy.
2. /Admin/PiuCenter → upload `piucenter-snapshot-050726.zip` (re-import banks the `crux_*`
   metrics and rebuilds folder baselines; the skill-tag regeneration step no longer exists).
3. Press "Rebuild Speed tier lists" once per mix that has metrics.
4. No other presses. Verdict/meta caches roll daily at 13:00 UTC; blend caches within 6h.

## 10. Out of scope, recorded so nobody re-litigates

- Tempo/gimmick axis (§1) — dead without .ssc data.
- The Speed × Skill cross grouping — shelved by owner.
- Dominant-skills line on band headers — rejected ("bloat").
- Fast/Slow/EndRun derived tags — deleted with the mapper, intentionally (Speed grouping
  carries the axis now).
- api/v2 — untouched. `ChartSkillProfile` already speaks raw badges; the Speed list stays
  unpublished like Popularity/Chabala.
