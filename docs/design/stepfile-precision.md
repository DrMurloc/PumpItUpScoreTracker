# Step-file precision — what the simfile corpus can and cannot say

Measured 2026-08-29 against the game's own judged note counts (Phoenix 1, prod-synced data).
This is the reference for any feature that reads the banked step analysis: which quantities are
trustworthy, at what precision, with which guards. The identity-chip system that consumes this
data is specified in [chart-identity.md](chart-identity.md); the hold-tick decomposition in
[phoenix-score-calculator.md](phoenix-score-calculator.md).

## 1. The corpus, and why "out of date" is the wrong model

The step files are the community's: the **Rayden repo** (`rayden-61/PIU-Simfiles`, the
Resistance's supply — checked out locally at `repos/PIU-Simfiles`), processed through the
piu-annotate pipeline (`repos/piu-annotate`) into per-chart arrow lists, hold-tick lists and
segment analyses, imported via /Admin/PiuCenter into `scores.ChartSkillMetric`.

Audited once and for all (owner ask, 2026-08-29):

- **The files are current.** The local checkout is GitHub HEAD (2026-08-12), and the 2026-08-26
  regeneration provably read it — every chartstruct records its `ssc_file` source path.
- **The Resistance ships every game patch (2.02→2.12 observed) but almost never edits existing
  charts.** Between piucenter's frozen 2024 corpus and the 2026 files, the implied note total
  changed on **10 of 4,228** same-key charts (Big Daddy ×8, Jogging D22, MEGAHEARTZ D25) — none
  to exact agreement.
- Therefore the disagreement measured below is **structural, not staleness**: pre-Phoenix packs
  are faithful to their own era's charts, the game re-tuned holds in Phoenix, and pulling
  updates will never close the gap.

Hold ticks in the files are **authored**, not modelled — they come from the .ssc's own
`#TICKCOUNTS` field (`piu_annotate/formats/ssc_to_chartstruct.py:104`). A total mismatch
therefore means the file genuinely differs from the shipped chart (in steps or in tick
authoring), never that a converter guessed wrong.

## 2. Headline precision

Test: per chart, the file's implied judged total (**tap rows + authored tick sum**) against the
game's `ChartMix.NoteCount` (learned only from passing plays; verified exact on effectively all
of Phoenix 1). n = 4,229 aliased, note-counted P1 charts.

| Population | Exact | Within 1% | Off > 5% |
|---|---|---|---|
| All charts | 49.6% | 74.2% | 18.9% |
| PHOENIX-pack files | 50.6% | **89.2%** | 2.6% |
| Pre-Phoenix-pack files | 48.2% | 68.2% | **25.4%** |

Distribution is **bimodal**: median error **0.08%**, p90 **20.1%**, p95 **32.3%**. Half the
corpus is byte-exact; the rest divides into a benign within-1% shoulder and a fat structural
tail. Exact is rare even on Phoenix-pack files (~50%) because the game's totals are designed
round numbers (Conflict S22 = 1,400; ERRORCODE: 0 S25 = 2,222) that authored tick counts
approximate — **within-1% is the honest bar**.

The tail does **not** hide at junk levels. It concentrates where players live, because that is
where holds are heaviest and where old boss charts were re-tuned:

| Level band | n | Off > 5% | Off > 25% |
|---|---|---|---|
| 1–9 | 911 | 5.0% | 2.9% |
| 10–14 | 812 | 10.7% | 4.3% |
| 15–19 | 1,476 | 23.2% | 10.3% |
| 20–23 | 968 | 30.2% | 10.5% |
| 24+ | 238 | 27.3% | 7.1% |

## 3. Anatomy of the tail (832 charts off > 5%)

Three signatures, with different consequences:

| Signature | n | What it is | Taps trustworthy? |
|---|---|---|---|
| **Over-ticked** (implied > game) | 482 | The file's authored `#TICKCOUNTS` exceed the game's tuned tick behaviour — pre-Phoenix hold authoring, occasionally absurd (Conflict D26's file implies **24,675** against a judged 1,600) | Yes — the error is entirely tick-side |
| **Under-ticked** (implied < game, file has ticks) | 293 | The file ticks less than the game does (Come to Me S17: file ticks 21, game needs ~536) | Usually — but not separable per chart |
| **No-hold file vs holdy game** | 57 | The file has zero holds while the game's chart is hold-heavy (Final Audition 2 SHORT CUT D19: 313 taps, 0 ticks, judged 901) — hard evidence the file is **not the shipped chart** | **No** |

Direction overall: 70% of all mismatches imply *more* than the game — Phoenix trimmed holds.
SHORT CUT files are the worst class: **51.6%** disagree past 1.5×, against 2.6% of arcade
charts — the community's cut is frequently not the official cut.

## 4. Per-quantity reliability

What each derived quantity actually depends on, and its measured trust level:

| Quantity | Reads from the file | Verdict |
|---|---|---|
| **Tap rows** (step count) | taps only | Strong. Provably exact on the 49.6% exact-total charts; hard-impossible (taps > judged total) on only **3** charts corpus-wide. Untrustworthy only on the ~57 no-hold re-steps and an unseparable slice of the under-ticked 293. |
| **Derived hold ticks / hold share** (`NoteCount − tap_rows`) | taps only | Strong, and **immune to tick-side errors** — on Conflict D26, the worst file in the corpus, it derives 402 ticks (25.1% hold share), perfectly plausible. Exposed only where taps are wrong; guard below. |
| **File tick sums / per-chart hold profiles** | authored ticks | **Do not use as per-chart truth.** Up to ~15× off; 18.9% of the corpus is > 5% wrong. Aggregate statistics only (the calculator's ruling stands). |
| **NPS / eNPS timeline** (p95 of per-second effective downpresses) | tap + hold-start timing | Strong. Hold-tick-blind by construction (`piu_annotate/formats/nps.py` — downpress = `1`/`2` only, dedup guards on hold spam). Wrong only where the steps themselves differ. |
| **Badges / coverage, segments, crux** | arrow patterns | As good as the steps; same exposure. Limb-model phantoms are a separate axis, already vetoed in the engine (footswitch family needs ≥ 5% measured repeated-panel share; bracket family needs ≥ 3% bracket-row share — chart-identity.md §3.3b/§3.4). |
| **Geometry** (pad shares, stance angles, bracket rows, repeated panels) | arrows + limb reads | Validated against known canon (chart-identity.md §4b); generation-time validation on the 2026 run: 94.8% badge / 99.7% limb agreement on the hand-checked subset. |

**The trust guard for tap-dependent per-chart claims**: when the derived tick count exceeds
**1.5×** the file's own tick sum, no version of that file supports the holds we infer — the file
is suspect end-to-end. Flags 5.1% of the corpus, over half of it SHORT CUTs. This is the
identity system's high-hold-claim veto (chart-identity.md, Hold-heavy).

## 5. Rules of thumb for future features

1. **Folder-relative claims are robust.** A percentile against the chart's own folder tolerates
   this noise easily — the folder spread is an order of magnitude wider than the typical error.
   This is why the chip system is safe on this corpus.
2. **Per-chart counts shown to users must be NoteCount-anchored** (derived, never file ticks),
   and even then the owner's standing rule applies: no numbers on user surfaces until they earn
   high confidence (2026-08-29).
3. **When per-chart precision matters, gate on agreement.** The population with implied total
   within 1% of the judged count — **74.2% of P1** (89.2% restricted to Phoenix-pack files) —
   is the set where the file is demonstrably the shipped chart. Treat it as the high-confidence
   corpus for timing-sensitive or count-sensitive work; treat everything outside it as
   era-approximate.
4. **SHORT CUT files are guilty until proven agreeing.** Half of them describe a different cut.
5. **These numbers will not improve by pulling updates** (10 changed charts in two years).
   Improving them means per-chart re-authoring or data from the game itself — a separate,
   owner-gated effort.

## 6. Reproduction

The audit joins `scores.ChartSkillMetric` (`tap_rows`, `hold_ticks`, `pack_is_phoenix`,
Source=`PiuCenter`) to `scores.ChartMix.NoteCount` on the Phoenix mix, plus a zip-vs-zip
same-key comparison of `Downloads\piucenter-snapshot-050726.zip` and
`piucenter-snapshot-082626.zip` (parse: tap rows = distinct timestamps in part 0; tick sum from
the metadata `Hold ticks` list). Caveats: 242 snapshot files (2.7%) skipped over Windows
filename quirks; 193 note-counted charts had no snapshot file; the local DB banks a mix of both
snapshot vintages (`data_version` 82626 on 3,153 charts, 50726 on 1,361) — immaterial, the
vintages produce near-identical totals.

## 7. Appendix — the blatantly wrong charts (Phoenix 1, audited 2026-08-29)

**121 charts** whose file is not credibly the chart the game ships, by three criteria. The
criteria are the durable part — the list regenerates from §6's join as note counts refill:

- **Tier A — arithmetically impossible** (3): the file's tap rows alone exceed the game's judged
  total, or its derived ticks fall below its own hold-row count. All three overshoot by only
  1–2 notes — trivial in magnitude, but no version of the shipped chart can produce them.
- **Tier B — hold-less file, holdy game** (57): the file contains **zero** holds while the
  judged total demands more than 5% of the chart in ticks. Almost all are 1ST–EXCEED-era
  steppings of arcade classics (Bee S17, Beethoven Virus D21, Slam D22, Love is a Danger Zone
  S17…) that Phoenix re-stepped with holds. Taps untrustworthy.
- **Tier C — wildly disagreeing totals** (61): implied total off by **more than 50%** and not
  already A/B. Over-ticked monsters (Conflict D26/S22, Sarabande S20) and under-stepped
  community cuts (half are SHORT CUTs).

Every one of the 121 comes from a **pre-Phoenix pack** — not a single Phoenix-pack file
qualifies. 92 of 121 sit at level 15+. Composition: 88 arcade · 32 SHORT CUT · 1 FULL SONG.

Use as an **exclusion set** for precision-sensitive work. The identity engine's existing guards
already cover the chip surface (the 1.5× hold trust check, the bracket/footswitch vetoes); this
list is for future analyses that would otherwise trust these files' steps.

| Tier | Chart | File taps + ticks | Judged | Err |
|---|---|---|---|---|
| A | God Mode feat. skizzo S4 | 210 + 0 | 208 | 1% |
| A | Set me up S10 | 276 + 0 | 274 | 1% |
| A | Slam S5 | 193 + 0 | 192 | 1% |
| B | Final Audition 2 - SHORT CUT - D19 | 313 + 0 | 901 | 65% |
| B | Beethoven Virus D21 | 495 + 0 | 1000 | 51% |
| B | Slam D22 | 506 + 0 | 1000 | 49% |
| B | Bee S17 | 442 + 0 | 830 | 47% |
| B | Gun Rock D24 | 528 + 0 | 1000 | 47% |
| B | Love is a Danger Zone S17 | 432 + 0 | 790 | 45% |
| B | Vook D21 | 574 + 0 | 1032 | 44% |
| B | Another Truth D18 | 381 + 0 | 653 | 42% |
| B | She Likes Pizza D18 | 384 + 0 | 653 | 41% |
| B | Slam S18 | 421 + 0 | 702 | 40% |
| B | We will meet again D11 | 182 + 0 | 300 | 39% |
| B | Slam S20 | 505 + 0 | 800 | 37% |
| B | Winter D21 | 505 + 0 | 800 | 37% |
| B | Beethoven Virus D13 | 255 + 0 | 400 | 36% |
| B | Will-O-The-Wisp D20 | 510 + 0 | 801 | 36% |
| B | Bee D15 | 339 + 0 | 500 | 32% |
| B | Extravaganza D15 | 340 + 0 | 502 | 32% |
| B | Final Audition Ep. 1 D15 | 340 + 0 | 500 | 32% |
| B | Close Your Eye S6 | 141 + 0 | 201 | 30% |
| B | Final Audition D19 | 488 + 0 | 700 | 30% |
| B | Will-O-The-Wisp D16 | 384 + 0 | 550 | 30% |
| B | Dr. M D18 | 474 + 0 | 666 | 29% |
| B | My Way D16 | 395 + 0 | 550 | 28% |
| B | We will meet again S13 | 366 + 0 | 510 | 28% |
| B | Final Audition Ep. 1 S17 | 444 + 0 | 606 | 27% |
| B | My Way S15 | 369 + 0 | 507 | 27% |
| B | First Love S6 | 151 + 0 | 203 | 26% |
| B | Mr. Larpus D18 | 492 + 0 | 660 | 25% |
| B | Mr. Larpus D16 | 426 + 0 | 550 | 23% |
| B | Dr. M D14 | 310 + 0 | 394 | 21% |
| B | Final Audition S7 | 234 + 0 | 296 | 21% |
| B | First Love D15 | 357 + 0 | 450 | 21% |
| B | Vook D15 | 354 + 0 | 450 | 21% |
| B | Winter D17 | 472 + 0 | 600 | 21% |
| B | An Interesting View S13 | 325 + 0 | 400 | 19% |
| B | Beat of The War S16 | 406 + 0 | 500 | 19% |
| B | Pump me Amadeus D15 | 363 + 0 | 449 | 19% |
| B | She Likes Pizza D11 | 244 + 0 | 300 | 19% |
| B | All I Want For X-mas S5 | 167 + 0 | 200 | 17% |
| B | Love is a Danger Zone pt.2 [Another] S18 | 637 + 0 | 764 | 17% |
| B | Caprice of DJ Otada S21 | 760 + 0 | 904 | 16% |
| B | Caprice of DJ Otada D22 | 842 + 0 | 1000 | 16% |
| B | Point Break S6 | 168 + 0 | 200 | 16% |
| B | A nightmare S6 | 171 + 0 | 200 | 15% |
| B | 2006. LOVE SONG D14 | 344 + 0 | 401 | 14% |
| B | Final Audition S18 | 517 + 0 | 601 | 14% |
| B | Mission Possible S7 | 184 + 0 | 212 | 13% |
| B | Winter S16 | 472 + 0 | 540 | 13% |
| B | Oh! Rosa D11 | 270 + 0 | 300 | 10% |
| B | Will-O-The-Wisp S16 | 449 + 0 | 500 | 10% |
| B | Dr. M S9 | 276 + 0 | 303 | 9% |
| B | An Interesting View S6 | 185 + 0 | 200 | 8% |
| B | Extravaganza D18 | 600 + 0 | 654 | 8% |
| B | Mr. Larpus S15 | 458 + 0 | 500 | 8% |
| B | She Likes Pizza S10 | 279 + 0 | 303 | 8% |
| B | Csikos Post D16 | 517 + 0 | 550 | 6% |
| B | Turkey March D13 | 287 + 0 | 304 | 6% |
| C | Conflict D26 | 1198 + 23477 | 1600 | 1442% |
| C | Another Truth D19 | 167 + 1872 | 715 | 185% |
| C | Blaze Emotion S2 | 104 + 129 | 117 | 99% |
| C | Imagination S18 | 386 + 1047 | 721 | 99% |
| C | Conflict S22 | 1196 + 1573 | 1400 | 98% |
| C | Like Me S14 | 245 + 675 | 481 | 91% |
| C | Sarabande S20 | 521 + 1596 | 1111 | 91% |
| C | Tales of Pumpnia D21 | 295 + 1671 | 1125 | 75% |
| C | Final Audition Ep. 2-2 D23 | 520 + 1591 | 1213 | 74% |
| C | Mental Rider D22 | 506 + 1495 | 1200 | 67% |
| C | Come to Me S17 | 250 + 21 | 786 | 66% |
| C | Wedding Crashers - SHORT CUT - S4 | 78 + 22 | 284 | 65% |
| C | Pop The Track - SHORT CUT - D16 | 282 + 46 | 882 | 63% |
| C | Destination - SHORT CUT - D21 | 449 + 1474 | 1186 | 62% |
| C | Extravaganza - SHORT CUT - D16 | 285 + 23 | 820 | 62% |
| C | Hyperion - SHORT CUT - S20 | 372 + 20 | 1032 | 62% |
| C | Moonlight - SHORT CUT - S19 | 286 + 20 | 800 | 62% |
| C | Tales of Pumpnia D18 | 119 + 1581 | 1052 | 62% |
| C | K.O.A : Alice in Wonderworld - SHORT CUT - D18 | 327 + 66 | 973 | 60% |
| C | Love is a Danger Zone pt. 2 - SHORT CUT - D23 | 366 + 134 | 1244 | 60% |
| C | Poseidon - SHORT CUT - S14 | 195 + 47 | 610 | 60% |
| C | Tales of Pumpnia S20 | 279 + 1433 | 1068 | 60% |
| C | Tales of Pumpnia S17 | 118 + 1521 | 1023 | 60% |
| C | Break it Down D21 | 383 + 28 | 1000 | 59% |
| C | Trotpris - SHORT CUT - D15 | 229 + 42 | 668 | 59% |
| C | Ignis Fatuus(DM Ashura Mix) - SHORT CUT - D21 | 270 + 151 | 1000 | 58% |
| C | Can-can ~Orpheus in The Party Mix~ - SHORT CUT - D23 | 254 + 188 | 1030 | 57% |
| C | Leather D22 | 830 + 1444 | 1450 | 57% |
| C | Naissance S20 | 363 + 21 | 900 | 57% |
| C | Poseidon - SHORT CUT - D21 | 388 + 44 | 1000 | 57% |
| C | Pumptris 8Bit ver. - SHORT CUT - D22 | 346 + 106 | 1043 | 57% |
| C | Chimera S18 | 508 + 1047 | 1000 | 56% |
| C | Emperor S16 | 417 + 696 | 713 | 56% |
| C | Final Audition Ep. 2-2 S21 | 561 + 1253 | 1163 | 56% |
| C | Poseidon - SHORT CUT - S21 | 389 + 48 | 1000 | 56% |
| C | Exceed2 Opening - SHORT CUT - S16 | 139 + 119 | 578 | 55% |
| C | Switronic - SHORT CUT - S9 | 171 + 23 | 434 | 55% |
| C | Xenesis S18 | 413 + 942 | 873 | 55% |
| C | Baroque Virus - FULL SONG - S15 | 675 + 1327 | 1300 | 54% |
| C | Chase Me S20 | 608 + 1047 | 1077 | 54% |
| C | Death Moon - SHORT CUT - D23 | 430 + 173 | 1300 | 54% |
| C | Emperor D17 | 414 + 674 | 708 | 54% |
| C | Final Audition 2 - SHORT CUT - S17 | 277 + 149 | 931 | 54% |
| C | Super Fantasy - SHORT CUT - S19 | 252 + 127 | 829 | 54% |
| C | Super Fantasy - SHORT CUT - D17 | 252 + 129 | 831 | 54% |
| C | Can-can ~Orpheus in The Party Mix~ - SHORT CUT - D21 | 260 + 206 | 1000 | 53% |
| C | Can-can ~Orpheus in The Party Mix~ - SHORT CUT - D17 | 197 + 121 | 678 | 53% |
| C | Slam D24 | 468 + 4 | 1004 | 53% |
| C | XX OPENING - SHORT CUT - S6 | 88 + 66 | 325 | 53% |
| C | Final Audition 3 - SHORT CUT - D18 | 361 + 108 | 981 | 52% |
| C | Final Audition 3 - SHORT CUT - S18 | 364 + 107 | 983 | 52% |
| C | Gun Rock S20 | 424 + 6 | 900 | 52% |
| C | Bad Apple!! feat. Nomico D20 | 408 + 1171 | 1043 | 51% |
| C | Desaparecer D25 | 926 + 1486 | 1600 | 51% |
| C | Hyperion - SHORT CUT - S16 | 372 + 20 | 793 | 51% |
| C | Kasou Shinja - SHORT CUT - S20 | 360 + 146 | 1023 | 51% |
| C | Poseidon - SHORT CUT - S11 | 198 + 30 | 468 | 51% |
| C | Another Truth D21 | 348 + 161 | 1021 | 50% |
| C | Destination - SHORT CUT - D20 | 278 + 123 | 803 | 50% |
| C | Kasou Shinja - SHORT CUT - D21 | 327 + 175 | 1014 | 50% |
| C | Witch Doctor S19 | 372 + 1102 | 981 | 50% |
