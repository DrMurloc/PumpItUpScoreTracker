# Rename matching

How a player who renames on piugame stays one player here. Supersedes decision 3 of
[official-leaderboards-overhaul.md](official-leaderboards-overhaul.md), which said renames
never merge automatically.

## 1. The problem

A rename splits one person into two mirror rows: the old tag stops appearing on the boards
and a new one starts, carrying the same history under a name nothing connects to the old
one. Everything keyed on the tag — rival edges, the account link, a player's board history —
points at a row that has stopped existing.

**There is no account identifier to key on.** The site renders a tag as `NAME#1234`, and the
discriminator is regenerated along with the name: all 15 rename pairs on record changed both
halves. Nothing else in the scraped markup identifies an account — no player-page href, no
`data-*` id, nothing across 25 captured fixtures.

What survives a rename is the scores.

## 2. The rule

Once per sealed non-baseline snapshot, against the **official reading only** (§6).

**Vanished tag** — held ≥ 5 chart-board placements last snapshot, appears on nothing at all
now, chart and rating boards alike. About 24 a week across both mixes.

**Candidate** — on a chart board now, on nothing at all last snapshot.

Three tests, in order of authority:

| | Test | Why it is the authority it is |
|---|---|---|
| 1 | **Nobody goes backwards.** A candidate on one of the old tag's boards with a *lower* score is disqualified. | Mirrored bests only ever improve. One violation is enough; there is no threshold to argue about. It never once fired on a real rename, and it eliminates 40–70% of candidates. |
| 2 | **Scores do not evaporate.** A score the old tag held that would still rank comfortably inside its board, with nobody standing there, routes to an admin. | Not a rename. Usually a ban. |
| 3 | **Exact non-perfect matches identify the person.** Count the boards where the scores are *identical* and not 1,000,000. | Sharing a perfect game means nothing. Sharing five identical imperfect scores does not happen to strangers. |

Verdicts:

- **Merge** — passes 1 and 2, ≥ 5 exact non-PG matches, and either the only qualifying
  candidate or ≥ 5× the runner-up. Merged inside the sweep, nobody asked.
- **Ambiguous** — two candidates comparably good. Never guessed between.
- **Suspicious** — test 2 fired. The ban case.
- **Propose** — fits, nothing contradicts, but thin: ≥ 3 boards present with ≥ 1 exact match.
- **DroppedOff** — nothing fits and every score has fallen below its board's cut. The
  ordinary way a player leaves the boards: they got passed.

**The avatar is evidence, never a gate.** Players change name and picture in the same
sitting; of the renames this finds, well under half kept the avatar. The old rule required
avatar equality and therefore could not see them at all.

## 3. Why the tail margin exists

Test 2 needs to know where a board's cut actually is, and the lowest captured score is not
it. Boards are paged until the site stops serving rows, and the last row or two moves
between runs. So "comfortably inside" means ranking at least `TailMargin` (20) places above
the board's captured depth.

This is not a theoretical concern. Without the margin the rule fires on **20 boards across
6 of 8 known renames** — and every one of those 20 would have ranked 301st–305th on a board
of 301–314 rows. Not one was mid-board. With the margin: zero false flags, and a genuine
mid-board disappearance still routes for review.

The margin also removes any need to hardcode a per-mix depth — Phoenix's ~100-deep boards
and Phoenix 2's ~300-deep ones both fall out of the captured row count.

## 4. Where the thresholds come from

Measured against the two most recent snapshot pairs of both mixes (2026-07-26 → 2026-08-02),
all 34 vanished tags:

| Verdict | Tags |
|---|---|
| Merge | 22 |
| Propose | 2 |
| DroppedOff | 10 |
| Suspicious | 0 |
| Ambiguous | 0 |

Separation on the eight tags that had human-reviewed proposals: the true match scored 11–71
exact non-PG matches; the best *other* candidate that passed test 1 scored **0 in seven cases
and 1 in the eighth**. The ≥ 5 threshold sits an order of magnitude clear of the noise. Across
both mixes the widest true match was 201 of 227 boards (`99RANCH#3971` → `ALBERTSONS#9337`).

`5×` for dominance is a judgment call, not a measurement — the branch never fired in the data.
It exists so 5-vs-200 resolves itself and 5-vs-8 does not.

Re-run the measurement with `Downloads/rename-detection-dryrun.sql`, which is read-only and
takes the snapshot pair as a CTE.

## 5. What this deliberately does not solve

A player with fewer than 5 board placements, or none at all, never produces a finding. Most
site users are not on any chart board's top 50, so **the detector will never see their
rename**. That is not a gap this closes: `User.GameTag` carries the account's current tag
from its most recent import, and the supplemented roll-up derives its cohort from that
rather than from the mirror links ([supplemented-leaderboards.md](supplemented-leaderboards.md)).
An account owning two mirror rows in one mix is accepted and collapsed by the consumer,
which is why the import path was left alone here.

Known false negative in the data: `DETONADOR#3383` → `D370N470R#3846` is obviously the same
person and lands in the DroppedOff pile, because they held 5 boards and matched on 1. The
desk shows the best candidate beside every unresolved tag so it can be approved in one click.

## 6. The supplemented reading is invisible to all of this

Supplemented placements are rolled up from linked public players' own ledgers on a press of an
admin button. If the analyzer counted them, a week where the roll-up ran against a week where
it did not would read as thousands of players vanishing and thousands appearing at once. It
reads `PlacementScope.OfficialOnly` on both snapshots, always.

The merge has a related trap, fixed alongside: where the old tag holds a **published** row and
the new tag a **supplemented** one on the same board and snapshot, the published row wins.
Dropping it would delete a placement the crawl genuinely recorded, and delete it from the
official reading, where the player would simply be missing from a board they charted on.

## 7. The desk

`/Admin/OfficialLeaderboards` lists **every** vanished tag grouped by verdict, worst first,
including the ones that merged themselves. That is deliberate: a rule that has quietly stopped
detecting renames is indistinguishable from a quiet week if the only thing on the desk is what
it could not decide.

A merge deletes the old dimension row and re-points its placements, and afterwards there is no
record of which placements came from which tag — **it cannot be undone.** `AutoAccepted` is
therefore its own status, so "did a human look at this one?" always has an answer.
