# Pass command detection

Status: **owner-workshopped 2026-08-28/29, settled (§8); building.** Splits stage breaks into the
ones the life bar caused and the ones a **Stage Pass command** caused, names the command where the
evidence allows, and shows it on the session page.

Companion spec: [stage-breaks-and-max-combo.md](stage-breaks-and-max-combo.md) — that one taught the
importer to tell a *failed* stage from an *interrupted* one and gave us `IsStageBroken`. This one
asks the next question: **what interrupted it.** Decisions continue that chain's numbering (D29
onward). Every measurement below was taken against the prod-synced local database on 2026-08-28/29,
over the 2,593 judged stage breaks a fresh production import had produced by then.

## 1. What a Stage Pass command is

Phoenix 2's command window carries a **Stage Pass** family, Premium Mode only. It ends the stage the
instant a chosen target stops being reachable. The official list is on
`piugame.com/game_info/premium_mode.php` — **not** `full_mode.php`, which documents every other
command family and is where you would look first.

Twenty targets, in two groups:

- **PASS(PLATE)** — `G FG TG MG SG EG UG PG`
- **PASS(GRADE)** — `A A+ AA AA+ AAA AAA+ S S+ SS SS+ SSS SSS+`

The site's own copy is the specification: *"If PG becomes unattainable, you will skip to the next
stage"*, and for `Pass_Plate_G`, *"When the life gauge reaches 0, move on to the next stage."* The
plate order the page lists them in is `G < FG < TG < MG < SG < EG < UG < PG`.

Two consequences worth stating plainly:

- **`Pass G` is indistinguishable by construction.** It *is* the life bar break.
- **Premium Mode continues play at 0 gauge** (`full_mode.php`, benefits list). `Pass G` is the option
  that restores death-on-empty. So for a Premium player every stage break is a Stage Pass event of
  some kind — we cannot read mode from an import, so this changes nothing mechanical, but it is why
  the UI must never call an unclassified break "normal".

The command art is piugame's own, mirrored at `piuimages.arroweclip.se/commands/Pass_Plate_*.png`
and `Pass_Grade_*.png` (112 command-window icons uploaded 2026-08-29; the `+` in a filename serves
correctly both raw and percent-encoded).

## 2. The evidence we have, and the evidence we do not

The recently-played card carries **no modifier data at all** — song, chart-type ball, level, score
slot, judgement table, date. Verified against the snapshot fixture. So every attribution here is
inference from the judgement counts, the chart's note count and level, and the mix's grade floors.

What we get per stage break: `Perfects / Greats / Goods / Bads / Misses` at the moment the run
ended, `NoteCount`, `Level`, `MixId`. What we never get: max combo, the order the judgements fell
in, which side of the cabinet the player was on, or the command itself.

## 3. The model

### D29 — the life bar gate comes first, and it is a gate

Two screens, cheapest first, starting from 500 life on the level's bar
(`MaxLife = 1000 + 3·level²`, 3700 above level 30). The bar drains only on bads and misses — goods
do nothing, which is the same mechanism that makes a passing F possible at all
([`LifebarSimulator.ApplyJudgment`](../../ScoreTracker/ScoreTracker.SharedKernel/Models/LifebarSimulator.cs));
greats and perfects heal.

1. **The friendliest ordering**: heal through every perfect and great, then land the damage in its
   most lethal interleaving. If even this dies, the row is refuted outright.
2. **The cruellest ordering**: a Pareto search for the least life ANY ordering of the judgements
   can end on. This is what makes the flag a proof — the friendliest ordering alone is not, because
   a miss subtracts 0.7 from the heal multiplier, so an ordering that spaces the damage through the
   heal stream keeps the multiplier crushed and a perfect heals one point instead of ten. That is
   what a struggling run actually looks like, and review measured 24 of an earlier gate's 348 flags
   carrying a valid ordering that dies. Two deliberate conservatisms keep the searched minimum a
   lower bound: every heal is applied as a great (the weaker heal on the slower ramp; every
   transition is monotone in life and multiplier, so the substituted trajectory sits under the real
   one for any ordering), and orderings that empty the bar mid-run clamp and continue — invalid as
   evidence, so counting them only errs toward refusing to claim.

**Only a run whose worst ordering still ends above the margin is flagged.** Empirically the bar
does not die early on real passes — 2,791 finished-and-*passed* runs in the journal carry exactly
3 misses; 1,989 carry 6 — which is why the margin (D30) rather than zero is the boundary.

### D30 — a 5% life margin, because the calculation is generous

The gate heals first and takes all damage second, which is the most survival-friendly ordering
possible. Without a margin, 105 rows "survive" on 1–5% of the bar — one is `174/2/0/0/12` on **15
life** — and those are life bar deaths. Requiring more than 5% of the bar to remain removes them.
Past 5% the unexplained share sits flat, so 5% is the knob's whole value.

### D31 — plate and grade are two fields, not one label

A row can satisfy both tests (41 do). Storing them separately makes that a fact rather than a
precedence argument, and lets a reader see both.

### D32 — a plate is named when it was broken by exactly one judgement

`PG` by the first non-perfect, `UG` by the first good/bad/miss, `EG` by the first bad/miss, `SG` by
the first miss, then `MG` at the 6th miss, `TG` at the 11th, `FG` at the 21st. Where more than one
fits, take the **highest** — `EG` over `SG` when a miss ended a run carrying no bads, since both
would have fired and `EG` is the higher target.

### D33 — a grade is named when the reachable ceiling died within one note of its floor

Best score still reachable = every remaining note perfect, best possible max combo. If that ceiling
sits below a grade floor by less than one note's worth of score, that grade just became unattainable
and is a candidate.

Combo is bounded, not known — why it is bounded rather than estimated is §5 — and the bound
respects that **only bads and misses break a combo; a good holds it without advancing it**. A run
whose only blemishes are goods can therefore still combo every other note
(`perfects + greats + remaining`); a run carrying a bad or miss is bounded by its longest
breaker-free stretch, `max(perfects + greats, notes remaining)`, with goods transparent inside it.

### D34 — an unattributed break still says what it is

The gate's answer is *"the life bar did not do this"*, which is worth saying on its own. The row
reads `Stage break · likely a Pass command on the other pad` on Singles and Single Performance —
one pad either way, the other free for the player whose command ends your run — and
`Stage break · couldn't determine what caused stage break` on Doubles and CO-OP. The split is real:
on Doubles and CO-OP one player holds both pads, so there is no other side to blame.

**The other-pad mechanism is real and confirmed in the field.** The Pass command applies to both
sides of a cabinet and cannot be set per-side, so a player on the other pad triggering it ends
*your* run too. Owner-replicated 2026-08-29, and visible in the data: RexBmxTwo's Pavane S17 break
at `7/0/0/0/0` on 2026-08-23 05:14:52 sits between two rows where he and mattmiller played the same
charts seconds apart (59 plays within 90 seconds of each other across the session); mattmiller has
no Pavane row at that timestamp because he was off-pad, and both replayed it 2.5 minutes later.

**We do not try to detect co-play in code.** A future target, not this one.

### D36 — the walk-off wall sits at 51 misses (2026-08-30, follow-up to this doc)

Premium continues play at 0 gauge (§1), so a bar-side stage break is either Pass G ending the
song or the **AFK guard** ending it on the 51st consecutive miss — and the journal shows the
wall exactly: one bar-side break each at 49 and 50 misses, then 19 at 51 and 26 at 52, with a
valley of 8 rows across 40–49 between 1,310 genuine deaths below and 382 walk-offs above (22%
of Phoenix 2's bar-side breaks; Phoenix 1 carries the same second hump at 23%, so the guard
predates Phoenix 2). Corroboration by shape: walk-offs average fewer bads (4.7 vs 6.1) and
goods (7.2 vs 10.5) than genuine deaths despite tenfold the misses — nobody grazes notes from
off the pad.

`StageBreakCauseSolver` therefore answers a fourth way: `misses ≥ 51` ⇒ **walked off**
(`IsWalkOff`, its own journal column), checked before everything else because it needs only
the miss count — it classifies even rows whose level or note count is unknown. It is
deliberately NOT a non-lifebar claim: the bar did empty during the walk; the point is that
the death was a formality. Surfaces quote the give-up point (judged minus the 51-miss tail).
Co-op stays unclassified under D35. The backfill button re-derives the column wholesale.

### D35 — co-op is never classified

A co-op chart's `Level` column is the **player count**, so the life bar the solver would size from
it is fabricated — level 2 makes a 1,012 bar — and nothing is known about how a co-op stage's life
actually works. All 48 judged co-op stage breaks in production happened to fall the safe side of
that fabricated bar; the skip makes the mistake impossible instead of lucky. Never guess.

## 4. What it finds

Over the 2,593 judged stage breaks, at the 5% margin:

| | rows |
|---|---|
| Life bar could have emptied under some ordering — untouched | 2,269 |
| **Non-Lifebar break** | **324** |
| — Pass Plate named | 152 |
| — Pass Grade named | 123 |
| — both named | 37 |
| — neither | 86 |

Named plates: `PG 82 · SG 20 · UG 16 · MG 16 · EG 15 · TG 3`. Named grades: `SSS 62 · SSS+ 54 ·
SS+ 3 · SS 2 · S+ 1 · S 1`. 33 distinct players. (An earlier revision of this table paired the
5% margin with histograms measured at 0% — these are all one measurement of the shipped pipeline.)

**Every Non-Lifebar break is Phoenix 2. None of the 569 judged Phoenix 1 breaks qualify.** Noise
would have spread across both mixes in proportion; a Phoenix-2-only feature producing a
Phoenix-2-only signal is the strongest corroboration in this document.

A further 1,616 stage breaks carry no judgement counts at all and can never be classified. They
render exactly as they do today.

## 5. What was tried and rejected

Recorded because each looked right and cost real time.

- **"All damage first from 500 life" as the gate.** Too conservative by 95 rows, and wrong in kind:
  the ordering it tests would have ended the run at note 7, contradicting the card. The
  survive-to-the-last-note constraint is what makes the gate sound.
- **A loose combo ceiling** (`notes − goods − bads − misses`, i.e. every combo breaker adjacent).
  Overstates the reachable score by up to 5,000 points — wider than a top-end grade band — and hid
  the grade signal entirely. It reported four of five known Pass SSS+ runs as *not* grade breaks.
- **A combo point-estimate** (`combo_lost = (judged − goods) / (bads + misses)`). Breaks 3 of the 5
  known Pass SSS+ runs, and fails in *both* directions: too low on a late break, above the floor on
  early ones where `remaining` dominates. Population cost: grade named 179 → 90. **Max combo is
  0.5% of the score = 5,000 points = exactly one top-end grade band, so no point estimate survives
  at that resolution.** The bound stays.
- **Simultaneous misses.** Proposed to explain runs ending 2–5 misses past a plate threshold.
  Disproved twice: 82 rows end on exactly one non-perfect against 2 on exactly two, and the owner
  tested a Pass PG on a chart whose first note is a fast hold tick — it broke on the first miss.
- **Session repetition as evidence** (treating a repeated near-floor break in one session as
  confirmation). Only ever existed to rescue rows the broken gate could not prove; the corrected
  gate proves them outright. Do not build it.
- **The friendliest ordering as the whole gate** (heal everything first, then damage). Flagged 24
  rows whose judgements admit a valid dying ordering — 15 of them badged — because spacing damage
  through the heal stream suppresses nearly all the healing. Caught in review; the adversarial
  minimum in D29 is the repair, and every owner-confirmed ground-truth row survives it.
- **Goods as combo breakers** in the reachable ceiling. A good holds the combo, so on a goods-only
  break the old bound understated the ceiling by up to a grade band and could name a grade that was
  still reachable. Caught in review; on today's data the repair adds two legitimate names and
  removes none.
- **Closest-plate-by-miss-count** as a fallback label. On the owner's own five Pass SSS+ runs it
  produces `MG, MG, MG, SG, SG` — confidently wrong five times, and inconsistent within one session
  on one chart. Grade must resolve first, and the plate test stays exact.

## 6. The 86 we cannot explain

Non-Lifebar, no plate broken by one, no grade within a note. (92 before the adversarial gate
reclaimed six; the analysis below was measured on those 92 and its conclusions are unchanged.)
Ruled out as causes:

- **Bad note counts** — 0 mismatches across all 88 testable charts (the catalog equals what finished
  plays sum to; Gargoyle - FULL SONG - S21 = 3,333 confirmed by 108 plays).
- **Stale or inferred rows** — 90% are live-observed stage breaks, the same share as the explained
  set.
- **One unusual player** — 92 rows over 21 players and 66 charts.
- **Combo slack hiding a grade** — explains ~17 (82 windows contain a floor against 65 expected by
  chance; a control of certain life bar deaths sits at chance, 111 against 123).

A known share are **bail-outs** — the run ended for a non-scoring reason. Rex's Pavane is the proven
case. The gate is right that the bar did not empty; the inference "therefore a Pass command" is what
does not hold, which is why D34's copy hedges.

**95 breaks are ungradeable purely for want of a catalog note count, across 69 Phoenix 2 charts.**
That is the cheapest remaining improvement and it is a data task, not a code one.

## 7. Where it lives

| Layer | What |
|---|---|
| `SharedKernel` | `StageBreakCauseSolver` + `StageBreakCause`; the plate miss-tolerance table moves onto `PhoenixPlate` so this and `ScoreScreen.PlateText` read one source |
| `Domain` | `ScoreScreen.PlateText` reads the extracted table. Nothing else |
| `Application` | nothing |
| `ScoreLedger` | three journal columns; `NoteCountWatch` widened to carry Level; both write paths classify; `SessionRow` carries it out; backfill command + consumer |
| `Data` | the migration only |
| `Web` | `PassCommandBadge`, the session row, the admin backfill button, the strings |

The solver sits in `SharedKernel` rather than `Domain` because it is the same class of thing as
`LifebarSimulator` and `ScoreScreen` — pure game model over value types and enums, no ports — which
also makes it testable with no doubles at all.

## 8. Settled by the owner

2026-08-28/29, across the workshop above:

- The gate is the whole story for rule 1: **if the bar could feasibly have emptied, leave the row
  alone and run none of the rest.** False negatives for someone running Pass A+ are accepted.
- Flag Non-Lifebar regardless of whether anything downstream names a command.
- Store Pass Plate and Pass Grade separately.
- Highest matching plate — **EG over SG**.
- No SS/SS+ restriction, no MG/TG/FG restriction: any plate and any grade may be named.
- 5% life margin.
- Session page only, this pass.
- The badge **replaces** the phrase — the command art is the sentence.
- Backfill is an **admin button**, not a SQL script: the algorithm has to exist in C# for the live
  path regardless, so a SQL port would be a second implementation of a formula that has already
  moved twice.

## 9. Build order

Docs first, i18n last, one PR.

1. this document, the schema row, the pointer from `stage-breaks-and-max-combo.md`
2. `refactor(kernel)` — the plate miss-tolerance table gets one home
3. `feat(kernel)` — `StageBreakCauseSolver` + its unit tests
4. `feat(ledger)` — three journal columns and the migration, together
5. `feat(ledger)` — classify on the write path
6. `feat(ledger)` — the cause travels on `SessionRow`
7. `feat(ledger)` — backfill command, consumer, wiring
8. `feat(web)` — `PassCommandBadge`
9. `feat(web)` — the session row renders it
10. `feat(admin)` — the backfill button
11. `i18n` — six keys across nine locales

Between 9 and 11 the new strings render as their English key text; that is the cost of i18n-last,
not a regression.

A review pass (2026-08-29, same PR) followed: the adversarial gate, the goods-transparent combo,
the same-kind fill guard, the co-op skip, the breaks-only backfill read, the Single Performance
copy, and a resx key whose apostrophe didn't match its call site.

## 10. Not in this pass

- **api/v2 stays unchanged.** `PlayerScoreDto` exposes `IsStageBroken` today; adding cause fields is
  a wire-shape change and a contract-test rewrite with no consumer waiting for it.
- **Co-play detection.** The other-pad mechanism is understood and confirmed; detecting it from two
  players' journals is its own feature.
- **The 69 missing note counts.** A catalog data task, tracked separately.
