# Stage breaks and max combo

Status: **owner-workshopped 2026-08-17, three rounds, settled (§11); built the same day on
`claude/stage-broken-scoring-9d7b44` in the commit order of §12 — the repair script (§8) is
still to run after deploy.** Splits
"broken" into a stage that was *failed* and a stage that was *interrupted*, keeps the second out
of every personal best no matter what the import opt-in says, journals it, and puts a stored max
combo beside the five judgement counts on the record and the journal.

Companion spec: [score-truth-model.md](score-truth-model.md) — this extends its model, so the
decisions here continue its numbering (D10 onward) and its D1/D7 are refined in §2. Measurements
were taken against the prod-synced local database on 2026-08-17; the card-shape evidence in §3
came from a read-only walk of the owner's own my_page on both sites the same day
(`ExplorationTests/LiveSite/StageBreakCardShapeReconTests`), and the date semantics in §6's D18
from a second such walk on 2026-08-18 (`BestCardDateSemanticsReconTests`).

## 1. Context

Two different things have been living under one flag:

- **Broken** — the player did not pass. The life bar reached zero at some point, the song played
  to its last note, the game graded the run (the grade art carries an `x_` prefix), awarded no
  plate, and every note was judged. The score is a real chart score.
- **Stage broken** — the song was interrupted: the stage broke, the play ended before the last
  note. The game assigns no grade. The recently-played pages print `STAGE BREAK` where the score
  would be; the Phoenix 2 best list prints a *running* score — the score at the moment the stage
  broke, normalised against the notes judged so far, not the chart's total.

That running score is the whole problem. On the owner's own Phoenix 2 best list, `404 (New Era)`
D20 shows **655,723** with no plate. The recently-played card for the same play (same timestamp)
reads `STAGE BREAK`, judgements `134/2/0/0/70`: 206 notes judged of 1,163, life gone, song over
(and 655,723 is exactly what the score formula gives over 206 notes, not over 1,163 — over the
whole chart those judgements are worth ~135,000).
`Human Extinction` S21 shows **929,722** the same way. Both look like near-passes; neither is a
score on the chart at all. With "Record broken scores as your best" on — the Phoenix 2 default,
because the official site keeps a best for a failed stage — the import saves them as personal
bests. They then sit above any real failed run on the same chart, because the precedence policy
compares broken-to-broken on score, and a running score is usually higher than a finished fail's.

Phoenix has the same three shapes on its recently-played page but its classic best list carries
no breaks at all, so on Phoenix the leak has no route in (the `STAGE BREAK` skip has been in the
parser since the broken-score import was added, `a0a54e72`, 2024-05).

Max combo is the second half: the game's Phoenix score is `floor((.995(P + .6G + .2Go + .1B) +
.005C) / N × 1e6)`, so once the five counts and the note count are known, `C` is the one unknown
and inverts. `PhoenixComboSolver` already does this for the CSV export; nothing stores it and
nothing else shows it.

## 2. The model

- **D10 — a stage break is never a personal best.** No opt-in reaches it, no source may seat one
  (manual sources included: a human cannot type a stage break, because the game gives it no chart
  score). D1's "a broken attempt lives in `PhoenixRecord` only when…" now reads *broken* in the
  narrow sense: failed and finished.

- **D11 — a stage break is a play, and the journal keeps it**: `IsBest = false`,
  `IsStageBroken = true`, `Score = NULL` (the site prints no chart score for it and the running
  score is not one), no plate, judgements as the card shows them. D7 stands and is restated in
  its terms: a stage break where **nothing was hit** — `P + G + Go + B = 0`, only misses — is a
  walk-off and is never stored anywhere. Everything else stage-broken is journaled.

  Consequences, all intended: *attempts before this clear* counts them (today a stage break is
  invisible, so the count is low); the session's *All plays* lists them; a chart's journey shows
  them; per-chart play counts include them; the session hero and every board still ignore them
  (a stage break is broken, and breaks were already excluded there).

- **D12 — the site is the classifier; the data is a tripwire.**
  1. **The site says so.** The recently-played card prints `STAGE BREAK` in the score slot and an
     empty grade image; the Phoenix 2 best-list card prints an empty grade image (`<img src="">`)
     beside a plate-less score, where a failed-but-finished run prints an `x_` grade
     (`x_a.png`, `x_d.png`). §3 has the evidence. This is the game's own classification, it needs
     neither judgements nor a note count, and **it is the only thing that sets `IsStageBroken`**.
  2. **The data disagreeing is logged, never acted on.** A judged play whose sum differs from the
     chart's stored note count — a pass (which judges every note), or a fail the site graded as
     finished — writes one warning naming the chart, the mix, the stored count, the judged sum
     and the outcome, so a stale catalog or the game's own edge (§6: two graded fails that stopped
     2 and 9 notes short) can be found in App Insights in one query. It refuses nothing,
     reclassifies nothing and rewrites nothing (owner, 2026-08-17: "log a warning but don't block
     anything — we SHOULD be fine, but that'll help me query quick if we run into an issue").
     Max combo is simply null on such a play, which the solver already does.
  3. **Blind — there is no third way.** A broken play with no site signal and no judgements is
     only ever a manual/CSV/API entry, and D9 (manual is sacred) governs those: recorded as
     broken. The "no note count → assume stage broken" fallback that was on the table is
     **dropped** (owner, 2026-08-17: "if we have better ways to detect it already then don't do
     that"). It also means an `x_` broken card on the Phoenix 2 best list with no judgements
     and no note count is what the site says it is — a finished fail — and records under the
     opt-in.

  Where it lives: the parser reads the slot and the label and hands `IsStageBroken` down;
  `BestAttemptPolicy` publishes "a stage break is never a best" beside `Beats` and `IsWalkOff`
  (the Ledger owns what counts as a best, score-truth-model §2), and the Ledger's write handler
  refuses one before the policy runs. The tripwire in (2) fires from the two Ledger write handlers,
  which is where a play, its judgements and the catalog's count meet.

- **D13 — the catalog stays write-once; the tripwire watches it.** No pass overwrites a stored
  count and no fail fills a blank from code (owner, 2026-08-17). What was on the table — passes
  overwrite on disagreement, finished fails fill blanks — is what the one-off repair script does
  by hand (§8), and it can be re-run whenever D12(2)'s warnings say the catalog has drifted. The
  numbers that motivated it stand as context: 2,519 of 4,616 Phoenix 2 charts have no note count;
  120 of them hold unanimous finished-fail samples; across both mixes only 2 charts have a
  finished-fail sum that disagrees with their passes.

- **D14 — max combo is stored**, `MaxCombo int NULL` beside the five judgement columns on both
  `PhoenixRecord` and `ScoreEventJournal`, solved by `PhoenixComboSolver` at write time on both
  paths, null when it cannot be (no judgements, no note count, sum ≠ note count, stage broken).
  It rides `JudgementCounts` as an optional sixth member so every consumer that already threads
  the five carries it: API v2 `JudgmentsDto` gains `MaxCombo`, the CSV export reads the stored
  value instead of re-solving. Stored rather than derived-on-read because the owner wants a
  backfill and because a stored value can be sorted, filtered and exported without a catalog
  join at every read; the backfill button (D15) is what keeps it honest after a note-count fix.
  Precision caveat from the solver stands: exact to one combo under ~2,500 notes, one combo out
  above.

- **D15 — an admin button backfills max combo**, user by user: `BackfillMaxCombosCommand` on the
  bus, a ScoreLedger consumer that walks every user holding judged rows (306 on Phoenix, 126 on
  Phoenix 2), re-solves every row with judgements — not only the null ones, so a corrected note
  count re-derives — and writes in batches. Idempotent, safe to re-press. 19,127 record rows and
  26,993 journal rows are solvable today (§6). It touches nothing but `MaxCombo`.

- **D16 — repair is SQL in Downloads, not a button** (score-truth-model §6 precedent): one script
  for the catalog note counts (delivered, runs any time) and one, run once after deploy, that
  withdraws the Phoenix 2 broken personal bests the import's own fingerprint marks as stage
  breaks (§6, §8), followed by the two existing admin presses — *Re-price Phoenix2 ratings*,
  *Clear Cache*. Records only; the journal keeps every play, exactly as the Your Data cleanup
  does (delete-my-data.md D2b).

- **D17 — a finished fail from the recent window may replace a broken best-list card.** The
  Phoenix 2 best list freezes the first non-pass attempt (§6), so the card is not the player's
  best fail; the recent window often holds a better one. When the card is broken, the window's
  best finished fail competes with it through the ordinary policy — broken against broken, higher
  score wins — and the winner is what gets saved, judgements and all. A pass on the card is never
  touched by anything from the window (owner, 2026-08-17: "broken scores are allowed to overwrite
  broken scores but not passing scores; stage break scores obviously overwrite nothing and are
  just event history"). This narrows D3, which was written to stop plate-only improvements
  dragging scores down; the policy has closed that door since, so the window is safe to let in on
  this one axis.

### The precedence policy, amended

```
a stage break never enters:                       refused before the policy runs (D10)
incoming beats stored when:
  1. stored is broken and incoming is not          → incoming wins
  2. incoming is broken and stored is not          → stored wins
  3. otherwise, higher score wins
  4. at equal score, better plate wins
  5. otherwise, no change
```

`SessionUndoReplay.BestOf` — the undo's rebuild — walks surviving journal rows and can seat the
first survivor unconditionally; it must skip stage-broken rows, or an undo could crown a stage
break the write path refused.

## 3. What the site shows

The read-only probe walked all 11 pages of the owner's Phoenix 2 best list (127 cards) and both
recently-played pages on 2026-08-17. Every plate-less card, by grade slot:

| Surface | Grade slot | Score slot | Judgements | Reading |
|---|---|---|---|---|
| P2 best list — 13 cards | `EMPTY` | `0` ×5, `655,723`, `683,059`, `794,532`, `828,872`, `842,042`, `890,117`, `901,800`, `929,722` | none (the list carries none) | stage break; the number is the running score |
| P2 best list — 2 cards | `x_d`, `x_a` | `590,032`, `890,256` | none | failed, finished |
| P2 recently played | `EMPTY` | `STAGE BREAK` | e.g. `134/2/0/0/70`, `244/5/2/1/110`, `334/7/0/0/60`, `0/0/0/0/51` | stage break; the same plays as the best-list rows above, same timestamps |
| P2 recently played | `x_f`, `x_d` | `331,137`, `361,685`, `341,212`, `590,032`, `573,510` | full — `4/61/124/11/0` = 200 on a chart a pass judged at 200 | failed, finished |
| P1 recently played | `EMPTY` | `STAGE BREAK` | e.g. `82/5/4/4/63`, `561/3/5/4/59`, `1,182/19/11/3/56` | stage break |
| P1 recently played | `x_aa_p`, `x_a_p`, `x_d` | `938,662`, `858,903`, `879,557`, `549,927` | full | failed, finished |

Two things follow. The empty grade slot is the discriminator on the one surface that has no
judgements and no label (the Phoenix 2 best list), and the parser reads that slot today only to
strip an `x_` prefix — it never asks whether the slot is empty. And the recurring `…/51` on
walk-off cards is a full life bar draining on consecutive misses, which is why D7's "nothing hit"
test is the right shape for a walk-off rather than "sum is zero" (their sum is 51).

## 4. Where each thing lives

| Store | Change |
|---|---|
| `scores.ScoreEventJournal` | `+ IsStageBroken bit NOT NULL DEFAULT 0`, `+ MaxCombo int NULL`. Every existing row is a finished play (stage breaks were skipped), so the default is right for all 1.11M |
| `scores.PhoenixRecord` | `+ MaxCombo int NULL`. No stage-broken flag — a record is never one (D10) |
| `scores.ChartMix.NoteCount` | unchanged, and unchanged in when it is written (D13) |

Domain: `JudgementCounts(P, G, Go, B, M, MaxCombo = null)`; `ScoreJournalEntry` gains
`IsStageBroken`; `RecordedPhoenixScore` needs nothing new beyond the combo inside its judgements.
Contracts: `RecordObservedPlaysCommand.ObservedPlay` gets `IsStageBroken` and a nullable score;
`UpdatePhoenixBestAttemptCommand` gets `IsStageBroken` (the site's word, carried down — the
handler refuses on it and journals the play instead). API v2: `JudgmentsDto.MaxCombo`, the
journal entry DTO's `IsStageBroken` — both additive, both move the `Tests.Api` goldens.

## 5. Import shape

**Parser (`PiuGameApi`).** `GetRecentScores` stops skipping `STAGE BREAK` cards and returns them
with `IsStageBroken = true`, `Score = null`, judgements and date intact. `ParseRedesignedBestScores`
sets `IsStageBroken` when the card is plate-less and its grade slot is empty. Both derive the flag
from the label *or* the empty grade slot, so either signal alone is enough. Approval fixtures
already hold the cards; the tests that assert "the STAGE BREAK card is skipped" flip to asserting
the flag.

**`OfficialSiteClient`.**
- `MapBestList` never lets a stage-broken best-list card near the record — but it does not drop
  it either. The list freezes the *first* attempt (§6), so an `EMPTY`-grade card on an unpassed
  chart is a stage break *at that card's date*: it is journaled as a stage-broken observation
  with no judgements and no score. That is the one recovery route for stage breaks older than
  the recent window, on every chart not passed since. When the window still holds the same play,
  the two rows share the play key and collapse; the upsert keeps the judged one.
  `IsNewOrImprovedBest`, which paces the dated walk, treats a stage-broken card as "not work" for
  the same reason a walk-off is — otherwise every stage break on the list would keep the walk
  going.
- `ResolveRecentPlays` keeps stage-broken plays except walk-offs (D11).
- `BestOf` never elects a stage-broken play; a chart whose recent window is all stage breaks
  contributes no best, no `ImportedScore` entry, no Daily Step observation.
- `EnrichBestsFromRecentPlays` (D17): the window's best finished fail competes with a broken
  best-list card through the policy and replaces it when it wins; the opt-in-only path that fills
  a chart the list never mentioned considers finished fails only; a passing card is never touched.
- `LearnNoteCounts` is unchanged (D13): a blank count is learned from a pass, once. Stage breaks
  teach nothing. The disagreement tripwire (D12-2) lives in the Ledger, not here.
- The observed-plays batch carries stage breaks with `Score = null` and the flag.
- The Phoenix 2 grade observation (`ScoringObservations`) skips them — there is no grade.

**Ledger.** `UpdatePhoenixRecordHandler` refuses a stage break before the session/policy logic
touches the record (after the session envelope is extended — activity is activity), and journals
it as an observation instead when it came with a date. `RecordObservedPlaysHandler` writes them
flagged. Both take `IChartRepository` (already a Ledger dependency elsewhere) for the note count
that D12(2) and D14 need.

## 6. What the numbers say

Prod-synced local database, 2026-08-17. `PhoenixRecord` 1,068,187 rows, 19,275 with judgements;
`ScoreEventJournal` 1,112,278 rows, 27,181 with judgements.

**Broken personal bests, by what can be known about them:**

| Mix | Class | Records | Users |
|---|---|---|---|
| Phoenix | no judgements | 30,045 | 988 |
| Phoenix | judged, sum = note count (finished) | 1,642 | 119 |
| Phoenix | judged, sum > note count (stale catalog) | 6 | 6 |
| Phoenix | judged, sum < note count | **0** | — |
| Phoenix 2 | no judgements | 971 (956 `officialImport`, 15 `manual`) | 89 |
| Phoenix 2 | no note count (judged) | 133 | 48 |
| Phoenix 2 | judged, sum = note count (finished) | 606 | 90 |
| Phoenix 2 | judged, sum < note count | **0** | — |

Every judged broken record is a finished run. Phoenix's 30,045 unjudged ones postdate the
`STAGE BREAK` skip and came off a best list that lists no breaks — they are finished fails or
manual entries, and stay. Phoenix 2's 956 unjudged `officialImport` records came off the
redesigned best list, which is where the running scores live; on the owner's account 13 of 15
plate-less best cards were stage breaks. **Those 956 are the leak.** The grade slot was never
captured, but the import's own behaviour leaves a fingerprint that splits most of them:

Every import fetches the recently-played window right after the best list, and
`EnrichBestsFromRecentPlays` copies the judgements of any window play whose score and broken-ness
match a best being saved. A finished fail in the window therefore always arrived judged. A
`STAGE BREAK` card was skipped, so it never could. So: **a broken best that arrived unjudged even
though its play date sits inside the window that same import journaled was in the window and
was not matched — a stage break.** Every one of the 956 has its writing journal row and import
session; classifying on that:

| Class | Records | Users | Reading |
|---|---|---|---|
| A — a judged broken observation at the same score exists (7 sum = note count, 4 chart uncounted) | 11 | 2 | finished fail (the site graded it) — keep, and copy the judgements onto the record |
| B — play date inside the import's journaled window, arrived unjudged | 765 | 83 | **stage break** — withdraw |
| C — play date older than the window's earliest journaled observation | 179 | 34 | unknown; the window may or may not have reached it |
| D — the import journaled no window at all | 1 | 1 | unknown |

Checked against the owner's own account: the five unjudged broken bests it holds
(`Becouse of You` D20 901,800, `Human Extinction` S21 929,722, `Odin` D23 828,872, `Nade Nade`
S20 890,117, `Gargoyle - FULL SONG -` S21 794,532) all classify B, and all five are `EMPTY`-grade
stage breaks on the live best list (§3). Class C is heavy on a few accounts (one holds 45).

**The Phoenix 2 best list freezes the first attempt on an unpassed chart.** Found while checking
class B: 66 broken records have a *later, higher, judged finished fail* on the same chart sitting
in the journal as a non-best observation. In 16 of those the import that journaled the higher
fail also wrote best-list rows dated *older* than the record's card — it walked past the card
and found nothing to raise, with the opt-in on ("follow the mix") for every account involved.
The airtight case: `Tropicanic` D13, card 426,227 at 23:15, a finished fail of 944,503 at 23:18,
the same import walked past the card and left 426,227 standing. Whatever the site's rule is, it
is not "highest score", and our records for unpassed charts mirror it: they hold the first
attempt. This predates and is separate from the stage-break question, but it interacts with it —
D17.

### D18 — the card's date is not a play time (measured live, 2026-08-18)

The stamp is set when the chart first reaches the best list and never moves; the score beside it
updates without it. Read directly from the owner's my_page
(`ExplorationTests/LiveSite/BestCardDateSemanticsReconTests`, read-only): of the 27 charts whose
recently-played window held a play at exactly the card's score, 19 agreed — every one of them a
single-attempt chart, where the first play *is* the best play and the two readings cannot be told
apart — and all 8 that could discriminate showed the card holding the best score against the
**first** play's date. `Switronic` D10's card reads 1,000,000 stamped 00:44:52, the time of a
573,510 three plays earlier; `Caprice of DJ Otada` S21 carries a 07-19 stamp against a score set
on 08-15. `Rush-More` D23 is the case that prompted the measurement: failed 08-12, passed 08-14,
and the card reads the pass against **08-12** to this day.

Two consequences, both fixed here:

- **The import prefers the producing play's own timestamp.** `EnrichBestsFromRecentPlays` used to
  keep the card's stamp and fall back to the play; the preference is now the other way round. The
  card's stamp survives only for a best the recent window no longer reaches, where nothing better
  exists.
- **`Append` refuses a row that disagrees with it.** Keying on a fabricated stamp meant a later
  pass landed on the earlier attempt's row and flipped its `IsBest`, leaving one play's row
  wearing another play's standing — 640 such rows on the prod-synced copy, 174 of them broken
  under a passing record, and after this branch the row landed on could be a scoreless stage
  break. Same key with a different score, broken or stage-broken state is now left exactly as it
  is; the record is written either way, and the play earns its own row as soon as a window dates
  it. The cost is deliberate: a pass imported long after the fact has no journal row until a
  window catches it, which is better than a row that misdates a play or overwrites a real one.

The rows already carrying this shape are a data question, not a code one — they are repaired
with the §8 script or left alone.

**Journal, broken rows:** 6 rows across 4 users have a judged sum below the note count, all
`IsBest = false`, all Phoenix, all August 2026. They fall in two groups: three charts where
*every* judged play — including a pass — lands on the same round number below the stored count
(`ERRORCODE: 0` D27: 2,400 judged, stored 2,581; `Paradoxx` D28: 2,500 vs 2,902; `CHAOS AGAIN`
D26: 1,500 vs 1,600 — a stale catalog, which is what the note-count script and the D12(2)
tripwire are for), and two runs the site graded as finished fails that stopped short (`Kokugen
Kairou Labyrinth` D22 1,076 of 1,078; `Altale` D6 259 of 268 — the game's own edge; they stay
"broken", as graded, and the tripwire would have logged them). Three more Phoenix charts hold
plays *above* their stored count (`Kugutsu` S20 993 → 1,117; `Over the Horizon` S20 980 → 1,000;
`Simon Says, EURODANCE!!` S20 938 → 1,005; the last two were already noted in score-truth-model
§4).

**Combo:** solvable today on 11,368 Phoenix + 7,759 Phoenix 2 records and 17,289 + 9,704 journal
rows; 306 + 126 users hold judged records.

**Note counts:** Phoenix 4,571 charts, 7 blank. Phoenix 2 4,616 charts, 2,519 blank; 120 fillable
from finished-fail samples already journaled, 0 disagreements among them.

## 7. Max combo on the page

Nothing player-facing renders the five judgement counts today — they exist in the record, the
journal, API v2 and the CSV export, and the site's own pages show score, grade and plate only.
"Beside the judgements" lands in the data model, the API and the export. **No page** (owner,
2026-08-17: "Absolutely not") — the value exists for downstream tools reading API v2, and the
export. A journaled stage break still needs its one-word `Stage break` label where a null score
would otherwise draw blank (session *All plays*, chart journey); that is not a judgement display.

Everything visual, mocked in the Phoenix 2 theme (2026-08-17,
[artifact](https://claude.ai/code/artifact/4eb3dd91-8d6b-434d-9272-05f35597ac9f)): the session
row (`Stage break` in muted ink on the score line, no grade, no plate, no number, chip stays
*Played*; never in *Scores that mattered*), the *Plays* tally and the *🎯 attempts* badge counting
them, the chart journey row (`Stage break` in the score column, no icon), one added sentence in
the opt-in helper, and the admin button. **The row says how far the run got** — `Stage break ·
31% in`, the judged sum over the chart's note count *for that row's mix* — and falls back to the
plain phrase when the count is unknown or the row's mix is not the page's (owner, 2026-08-17:
"X% in is cool"). The journey row does the same.

## 8. Data repair

Two SQL scripts in Downloads and two button presses.

**Note counts — `note-count-repair-2026-08-17.sql`, delivered, independent of the deploy.** A
census (every chart's stored count against every judged play), then one guarded `UPDATE`: a chart
whose passes are unanimous on a different count takes that count. Four charts today (`ERRORCODE:
0` D27 2,581 → 2,400; `Kugutsu` S20 993 → 1,117; `Over the Horizon` S20 980 → 1,000; `Simon Says,
EURODANCE!!` S20 938 → 1,005). Two single-fail-sample charts (`Paradoxx` D28 2,500 vs 2,902,
`CHAOS AGAIN` D26 1,500 vs 1,600) are listed and left alone; the 120 blank Phoenix 2 counts fill
from their unanimous finished-fail samples behind an off-by-default flag. Dry-run clean against
the prod-synced copy (rolled back). Press *Clear Cache* after it — the catalog is cached in
memory.

**Stage breaks — one script, run once after deploy** (owner, 2026-08-17: withdraw B and C/D,
"let import fix"; the re-seat rides in the same script).

1. **Withdraw classes B, C and D** (§6): 945 `PhoenixRecord` rows. Their `PhoenixRecordStats`
   rows go with them (the Your Data cleanup deletes the same pair). Class A's 11 stay; the 7 with a
   counted chart get their journal twin's judgements copied on, so the combo backfill can solve
   them.
2. **Turn the withdrawn records' journal rows into what they were.** Class B's rows are known
   stage breaks: `IsBest = 0`, `IsStageBroken = 1`, `Score = NULL`, date and chart intact — the
   play stays in the player's history, minus the judgements the skip threw away. Class C/D's rows
   are unknown: `IsBest = 0` only, score kept, so a re-import that finds them again as `x_` fails
   re-seats them and the history never claimed more than it knew.
3. **Re-seat from the journal**: 25 of the withdrawn charts hold a judged finished fail by the same
   player (a later, real run the site's frozen card hid). The best of those becomes the record —
   score, no plate, broken, judgements — exactly what D17 does going forward.
4. **Press** *Re-price Phoenix2 ratings* (breaks rate zero on Phoenix 2, so this is belt and
   braces) and *Clear Cache* (the per-user score cache would otherwise serve the withdrawn rows
   until it aged out).
5. **Press** *Backfill max combos* once (D15).

Left alone, and why: the 15 `manual` Phoenix 2 broken bests (D9); every judged broken record
(finished by measurement); every Phoenix broken record (no route for a stage break to have
entered). The next import re-walks the best list with the new parser regardless: finished fails
come back with an `x_` grade and re-seat under the opt-in — or better ones from the window replace
them (D17); stage breaks journal and seat nothing; a withdrawn record reads as new, so the
five-quiet-page walk keeps going while it keeps finding them.

## 9. What this touches elsewhere

| Doc | Change |
|---|---|
| [score-truth-model.md](score-truth-model.md) | D1 and D7 refined, D3 narrowed by D17; pointer to D10–D17 here |
| [DOMAIN.md](../DOMAIN.md) | *Broken* stops meaning "stage-broken"; *Stage broken* and *Max combo* get entries |
| [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md) | the three new columns |
| [API.md](../API.md) | v2 `JudgmentsDto.MaxCombo`, journal `IsStageBroken` |
| [import-scores-refresh.md](import-scores-refresh.md) | the opt-in helper copy says a stage break is never recorded |
| it-IT / fr-FR glossaries | both translate the Your Data cleanup explainer's "failed the stage" as *Stage Break*; that term now means the interrupted case |

UI strings: a `Stage break` label where a journaled stage break renders (session *All plays*, chart
journey — `ScoreBreakdown` draws nothing for a null score today), the admin button, and the
helper-copy sentence — all nine locales, alphabetical position.

## 10. Test plan

- **Unit (`DomainTests/`)** — `BestAttemptPolicy`: a stage break never beats anything and nothing
  needs to beat it; the amended `IsWalkOff` (nothing hit vs sum). `PhoenixComboSolver` round-trip
  already pinned; add the `JudgementCounts.MaxCombo` carry. `SessionUndoReplay` never seats a
  stage break.
- **Component (`ApplicationTests/`)** — `UpdatePhoenixRecordHandlerTests`: a stage break with a
  date is journaled and never recorded, with and without the opt-in; a finished fail still is; a
  judged play whose sum disagrees with the catalog count is written unchanged and logs one
  warning (mocked `ILogger`, `Times.Once`); combo lands on the record and the journal row.
  `RecordObservedPlaysHandler` writes the flag and a null score, drops the walk-off.
  `OfficialSiteClientTests`: a recent window of one stage break and one lower finished fail
  saves the fail; an all-stage-break window saves nothing and announces no Daily Step; an
  `EMPTY`-grade best-list card is journaled as a stage break at the card's date and saved
  nowhere; a broken best-list card plus a higher finished fail in the window saves the window
  play (D17); a passing card plus anything in the window saves the card; `LearnNoteCounts` still
  learns from a pass only. Backfill consumer: user-by-user, idempotent, re-derives after a count
  change.
- **Approval (`ApprovalTests/PiuGameApi/`)** — the existing fixtures' `STAGE BREAK` cards now
  parse flagged; a redesigned best-list card with an empty grade slot parses `IsStageBroken`, one
  with `x_a` does not.
- **Integration** — the two migrations apply; the journal upsert keeps the flag on re-import and
  keeps the judged row when a best-list stage break and its window twin collide.
- **API (`Tests.Api`)** — goldens for the two additive fields.
- **Components** — a session row with a null score and the flag renders the label, not blank.

## 11. Settled by the owner, 2026-08-17

- **The grade slot is the primary signal** (D12-1); the owner confirmed it on his own best list —
  a `Kugutsu` S25 706,197 broken best rendering a broken-B grade, and an unlabelled plate-less card
  two rows down with a score and no grade.
- **No blind fallback** (D12-3): "if we have better ways to detect it already then don't do that."
  Max combo stays null wherever judgements are absent; the backfill (D15) is still wanted for
  downstream tools.
- **Stage breaks are journaled** (D11): "we want that data — it helps with how many times did you
  play this chart before passing it."
- **No page for judgements or max combo** (§7): "Absolutely not."
- **The withdrawal is a SQL script, not a button** (D16) — the owner's own framing from the start
  ("a script in my downloads folder").
- **The note-count repair is its own artifact** — delivered as `note-count-repair-2026-08-17.sql`
  (§8).
- **The catalog stays write-once; disagreements log a warning and block nothing** (D12-2, D13):
  "we SHOULD be fine, but that'll help me query quick if we run into an issue."
- **Classes C and D are withdrawn with B — "let import fix"** (§8).
- **The 25 re-seat in the same script** (§8 step 3).
- **D17 is in**: broken may replace broken, never a pass; a stage break replaces nothing and is
  event history.

## 12. Build order

**One PR** (owner, 2026-08-17), one migration, commits in this order — docs first, localization
last, every commit between them building on the ones before it so the branch compiles and tests
green at each step (no ratchet reads code's `L["…"]` keys against the resx — the localization
tests compare locales to `en-US` — so between commits 11 and 13 the three new phrases simply
render their English). The withdrawal script (§8) is not in the PR: it
is written against the migrated local database while the PR is in review and handed over in
Downloads, to run once after deploy.

1. `docs(scores): stage breaks and max combo — D10–D17` — this doc's status → in flight; DOMAIN
   (*Broken* / *Stage broken* / *Max combo*); score-truth-model pointers (D1, D3, D7);
   DATABASE-SCHEMA (`IsStageBroken`, `MaxCombo` ×2); API.md (`isStageBroken`, `maxCombo`);
   import-scores-refresh (the helper sentence).
2. `feat(kernel): max combo rides JudgementCounts` — the optional sixth member; the round-trip
   test gains the carry.
3. `feat(domain): a journal entry knows it was a stage break` — `ScoreJournalEntry.IsStageBroken`.
4. `feat(ledger): a stage break is never a best — the published rule` — `BestAttemptPolicy`
   (`CanBeRecord`, `IsWalkOff` restated as "stage broken with nothing hit"); the two commands and
   `ScoreEventRecord` gain their fields; `BestAttemptPolicyTests`.
5. `feat(ledger): the journal and the record store the flag and the combo` — both entities, both
   EF repositories (mapping; the observation upsert keeps the judged row on a play-key collision),
   the one migration `AddStageBreakFlagAndMaxCombo` scaffolded from `Data` with the CompositionRoot
   startup project; integration tests for the migration and the collision.
6. `feat(ledger): the write path refuses a stage break, journals it, solves the combo, watches
   the catalog` — `UpdatePhoenixRecordHandler` and `RecordObservedPlaysHandler` (refusal →
   observation, walk-off drop, `PhoenixComboSolver` at write, the D12(2) `LogWarning`;
   `IChartRepository` + `ILogger<>` in); handler tests.
7. `feat(ledger): undo never re-crowns a stage break; the feed says how far it got` —
   `SessionUndoReplay` skips them; `SessionFeedHandler` threads `IsStageBroken` and the judged sum;
   tests for both.
8. `feat(ledger): Backfill max combos` — `Messages/BackfillMaxCombosCommand`, the consumer (user
   by user, every judged row, idempotent), the two repository backfill methods, the consumer added
   to `AddScoreLedgerConsumers`; consumer tests.
9. `feat(mirror): the parser reads STAGE BREAK and the empty grade slot` — `PiuGameApi` on both
   surfaces, the two DTOs (nullable score, `IsStageBroken`); approval tests flip from "skipped" to
   "flagged"; one labelled synthetic empty-grade non-zero card in the redesign fixture.
10. `feat(mirror): the import keeps stage breaks as history and lets a window fail beat a broken
    card` — `OfficialSiteClient` (`MapBestList` observation + `IsNewOrImprovedBest`,
    `ResolveRecentPlays`, `BestOf`, D17 in `EnrichBestsFromRecentPlays`, Daily Step, grade
    observation, the observed batch); `OfficialSiteClientTests`; any E2E count assertions the 20
    fixture cards move.
11. `feat(web): stage breaks on the session row and the chart journey` — `SessionScoreRow`,
    `SessionBreakdownBuilder`/models, `ScoreJourneyList` + its two hosts; the two admin-page
    lines for the backfill button; component tests.
12. `feat(api): isStageBroken and maxCombo on v2, the export reads the stored combo` —
    `PlayerDtos`, `ChartExport`, `DevApiReader` (optional carry); `Tests.Api` goldens.
13. `i18n: stage break in nine locales` — `Stage break`, `Stage break · {0}% in`, `Backfill max
    combos` inserted alphabetically in all nine; the three import-helper values gain their
    sentence in all nine (glossaries; Murloc alphabet for `en-ZW`); this doc's status → built.

The note-count script is already in Downloads and is independent of the PR.

## 13. Technical scope

Two verticals carry the logic — **OfficialMirror** (what the site said) and **ScoreLedger** (what
counts, what is kept) — with the kernel record, one Domain record, two migrations in `Data`, and
the Web touches from §7. No new vertical, no new port, no new table, no DI wiring beyond one
consumer added to an existing hook. Catalog, PlayerProgress, WeeklyChallenge, Communities and the
Discord renderer change nothing: stage breaks never reach a record, an event or a card, and the
attempts count PlayerProgress reads already comes through the Ledger's published port.

### Slice 1 — stop the loss and the leak

| Layer | Project | Change |
|---|---|---|
| Domain | `ScoreTracker.Domain` | `Records/ScoreJournalEntry` + `IsStageBroken` (default false). `Records/OfficialRecordedScore` unchanged (a stage break never becomes one). |
| Vertical — infrastructure | `ScoreTracker.OfficialMirror/Infrastructure/Apis` | `PiuGameApi.GetRecentScores`: stop skipping `STAGE BREAK`; `IsStageBroken` = label present *or* grade slot empty on a plate-less card; `Score` becomes nullable on `PiuGameGetRecentScoresResult`. `ParseRedesignedBestScores`: `ScoreDto.IsStageBroken` = plate-less *and* grade slot empty. `GradeFrom` unchanged. |
| Vertical — infrastructure | `ScoreTracker.OfficialMirror/Infrastructure` | `OfficialSiteClient`: `MapBestList` keeps a stage-broken card out of `results` and emits it as an observed play (chart, card date, no judgements, no score, flag); `IsNewOrImprovedBest` returns false for one; `ResolveRecentPlays` keeps stage breaks except walk-offs; `BestOf` ignores them; `EnrichBestsFromRecentPlays` gains D17 (window's best finished fail vs a broken card via `BestAttemptPolicy.Beats`; passing card untouched) and its opt-in path skips stage breaks; `AnnounceDailySteps` sees no stage break; `ScoringObservations.ObserveGrades` skips them; the observed-plays batch carries the flag and a nullable score. `LearnNoteCounts` untouched. |
| Vertical — contracts | `ScoreTracker.ScoreLedger/Contracts` | `Commands/RecordObservedPlaysCommand.ObservedPlay` + `IsStageBroken`, `Score` → `PhoenixScore?`. `Commands/UpdatePhoenixBestAttemptCommand` + `IsStageBroken` (default false). `BestAttemptPolicy` + `CanBeRecord(isStageBroken)` (or the rule folded into `Beats`'s callers — one published place either way) and `IsWalkOff` restated as "stage broken with nothing hit". `RecentSessionsPage.ScoreEventRecord` + `IsStageBroken`, + `JudgedNotes` (int?) so the row can say how far. |
| Vertical — application | `ScoreTracker.ScoreLedger/Application` | `UpdatePhoenixRecordHandler`: after the session envelope, a stage break → journal as observation (when dated) and return; the D12(2) tripwire (one `LogWarning` when a judged play's sum ≠ the catalog count — needs `IChartRepository`, already a Ledger dependency elsewhere, and `ILogger<>`, already used by two Ledger consumers). `RecordObservedPlaysHandler`: writes the flag and null score, drops walk-offs, same tripwire. `SessionFeedHandler`: `ScoreEventRecord` carries the flag and the judged sum; classification unchanged (`Played`). `SessionUndoReplay.BestOf` skips stage-broken rows. `SessionAttemptCountsHandler` unchanged (a stage break is broken, so it is already an attempt). |
| Vertical — infrastructure | `ScoreTracker.ScoreLedger/Infrastructure` | `Entities/ScoreEventJournalEntity` + `IsStageBroken bit NOT NULL DEFAULT 0`. `EFScoreJournalRepository`: map the column; the observation upsert keeps the judged row when a best-list stage break and its window twin collide on the play key. |
| Infrastructure | `ScoreTracker.Data/Migrations` | one migration, `AddStageBreakFlagToJournal`, scaffolded from `Data` with `--startup-project ../ScoreTracker.CompositionRoot`; metadata-only on SQL Server (constant default). |
| Presentation | `ScoreTracker` (Web) | `Components/Sessions/SessionScoreRow`: stage-broken row renders `Stage break · {0}% in` (judged ÷ `Chart.NoteCount`, same mix) or `Stage break`; no `ScoreBreakdown`. `Services/SessionBreakdownBuilder` / `SessionBreakdownModels`: thread the two new fields (PlayCount already counts every row). `Components/ScoreJourneyList`: the same phrase in the score column, no icon; takes the page mix's `NoteCount` from `ChartRecordPanel` / `ChartScoreHistoryTab` and prints the percentage only for rows in that mix. `Pages/UploadPhoenixScores`: helper copy — three existing keys' *values* change, no new key. `Dtos/ApiV2/PlayerDtos`: journal DTO + `IsStageBroken` (additive). Locales: two new keys (`Stage break`, `Stage break · {0}% in`) in all nine, alphabetical. |
| Tests | `ScoreTracker.Tests` | `ApprovalTests/PiuGameApi`: the fixtures' `STAGE BREAK` cards now parse flagged (five test bodies say "skipped" today); a redesigned best card with an empty grade slot and a non-zero score (one synthetic card added to `GetBestScores_Phoenix2Redesign.html`, labelled) parses `IsStageBroken`, the `x_` one does not. `DomainTests`: `BestAttemptPolicyTests`, `SessionUndoReplayTests`. `ApplicationTests`: `UpdatePhoenixRecordHandlerTests`, `RecordObservedPlaysHandlerTests`, `OfficialSiteClientTests` (D17 both ways, all-stage-break window, best-list stage break journaled), `SessionFeedHandlerTests`. `ArchitectureTests` need nothing new but bite: `MessageTaxonomyTests`, `VerticalBoundaryTests`, `ResxKeysAreStoredAlphabetically`, `UiColorTokenTests` (the phrase uses `--mix-ink-muted`). |
| Tests | `ScoreTracker.Tests.Components` | `SessionScoreRowTests` (flag + null score → the phrase, with and without a note count), `ScoreJourneyListTests`. |
| Tests | `ScoreTracker.Tests.Api` | journal golden gains `isStageBroken`. |
| Tests | `ScoreTracker.Tests.Integration` | migration applies; upsert collision keeps the judged row. |
| Tests | `ScoreTracker.Tests.E2E` | `RecentlyPlayed.html` carries 20 `STAGE BREAK` cards; any assertion on play or row counts moves — verify, don't assume. |
| Docs | `docs/` | DOMAIN (Broken vs Stage broken), score-truth-model pointers (D1, D3, D7), import-scores-refresh copy, this doc's status. |

### Slice 2 — max combo

| Layer | Project | Change |
|---|---|---|
| Kernel | `ScoreTracker.SharedKernel` | `Models/JudgementCounts` + `int? MaxCombo = null` as the sixth positional member; every existing five-argument construction still compiles. |
| Domain | `ScoreTracker.Domain` | `Services/PhoenixComboSolver` unchanged (already there, already pinned by a round-trip test). |
| Vertical — contracts | `ScoreTracker.ScoreLedger/Contracts` | `Messages/BackfillMaxCombosCommand` (bus trigger, plain record). |
| Vertical — application | `ScoreTracker.ScoreLedger/Application` | `UpdatePhoenixRecordHandler` and `RecordObservedPlaysHandler` solve the combo at write (`PhoenixComboSolver.MaxComboFor(judgements, score, chart.NoteCount)`, null when unsolvable) and store it inside the judgements. `BackfillMaxCombosConsumer` (`IConsumer<BackfillMaxCombosCommand>`): users with judged rows, one user at a time, re-solves every judged record and journal row, batch writes, idempotent. |
| Vertical — infrastructure | `ScoreTracker.ScoreLedger/Infrastructure` | `PhoenixRecordEntity` + `MaxCombo int NULL`, `ScoreEventJournalEntity` + `MaxCombo int NULL`; `EFPhoenixRecordsRepository` / `EFScoreJournalRepository` map it and gain the two backfill methods (`IPhoenixRecordRepository` / `IScoreJournalRepository`, vertical-internal ports). |
| Vertical — wiring | `ScoreTracker.ScoreLedger/Wiring` | `AddScoreLedgerConsumers` + the consumer (the registration tripwire test fails until it is there). |
| Infrastructure | `ScoreTracker.Data/Migrations` | one migration, `AddMaxCombo`. |
| Presentation | `ScoreTracker` (Web) | `Pages/Admin/Admin.razor` + *Backfill max combos* (publishes the command; one new key, nine locales). `Dtos/ApiV2/PlayerDtos.JudgmentsDto` + `MaxCombo` (additive). `Services/ChartExport` `MyMaxCombo` reads the stored value instead of re-solving. `Data/DevTooling/DevApiReader` may carry the field so a local populate keeps combos — optional. |
| Tests | `ScoreTracker.Tests` | `JudgementCounts` carry in `PhoenixComboSolverTests`; both handlers store the combo; `BackfillMaxCombosConsumerTests` (per-user, idempotent, re-derives after a count change). |
| Tests | `ScoreTracker.Tests.Api` | player-score and journal goldens gain `maxCombo`. |
| Tests | `ScoreTracker.Tests.Integration` | migration applies; the backfill writes real rows. |
| Docs | `docs/` | DATABASE-SCHEMA (three columns across the two slices), API.md, DOMAIN (*Max combo*), SCHEDULED-JOBS untouched (it is a button, not a job). |

### Slice 3 — repair

One SQL script in Downloads against the deployed schema (§8): withdraw B + C + D with their
`PhoenixRecordStats`; convert B's journal rows to stage breaks and clear `IsBest` on C/D's; copy
class A's judgements onto their records; seat the 25 from their journaled finished fails; then
*Re-price Phoenix2 ratings*, *Clear Cache*, *Backfill max combos*. Dry-run in a rolled-back
transaction against the prod-synced copy before it is handed over, like the note-count script.

### What the arch ratchets will say

`MessageTaxonomyTests` (a `*Command` plain record in `Contracts/Messages` — fine),
`VerticalBoundaryTests` (nothing new is public outside `Contracts/` and `Wiring/`), the consumer
registration tripwire (slice 2's consumer must be in the hook), `ResxKeysAreStoredAlphabetically`
+ `LocalizationKeyTests` (three new keys, nine locales, no case variants), `UiColorTokenTests` (no
literals — the phrase wears `--mix-ink-muted`), `DiagnosticExposureTests` (the tripwire is a log,
never a page), `RenderModeDeclarationTests` (no new page), `AccountPurgeCoverageTests` (no new
table, no new `*UserId`).
