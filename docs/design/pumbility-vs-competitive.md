# PUMBILITY vs Competitive Level — where the two rulers agree

**Status (2026-07-31).** Analysis, not a product proposal. Asked by the owner: what do players with
similar PUMBILITY have in common versus players with similar Competitive Level, across the 18–23
band? The interactive companion is
[pumbility-vs-competitive.html](pumbility-vs-competitive.html) — open it in a browser; it is
self-contained, needs no server, and recomputes against a chart-type / plate / reference-chart
control row. Everything below is what falls out of it.

The two formulas as implemented:

| | Phoenix 2 PUMBILITY | Competitive Level |
|---|---|---|
| per chart | `Base(L) × (gradeMult + plateBonus)`, `Base = 130 + 5L + 5·max(0, L−24)`, singles price at `Base(L+1)` | `L + (score − 965,000) / 17,500`, singles ≥ L20 `×= 1.008^(L−19)` |
| reads | letter grade + plate | raw score |
| shape | step function, 12 steps A→SSS+ | linear, continuous, unbounded below |
| aggregation | **sum** of top 50 (and top-50 pools per type) | **average** of top 100 (top 50 per type) |
| source | `ScoringConfiguration.Phoenix2PumbilityScoring` | `ScoringConfiguration.CalculateFungScore`, aggregated in `PlayerRatingSaga.RecalculateCore` |

The comparison needs a common unit. Both are expressed as **the doubles level whose clean SSS+ /
Marvelous play is worth the same** — an anchor that is identical on both sides, so the two agree
exactly on a doubles SSS+ MG play at any level and every other cell is a real difference.

---

## 1. On doubles above A+, they are very nearly the same ruler

Over the whole D18–D23 × A+→SSS+ grid the two never disagree by more than **0.46 of a level**, and
usually by under 0.2.

This is arithmetic coincidence rather than shared design. PUMBILITY's base grows 5 points per level
on a base of 220–245, so one level is worth ~2.1% of a chart's value; the grade ladder happens to
spend about that much per 17,000–20,000 points of score in the S range. Competitive Level's constant
is 17,500. They land on the same exchange rate without being related.

Practical consequence: for a **doubles player scoring A+ or better**, the two metrics rank charts
almost identically. Anywhere that matters, the disagreement people feel is not coming from the
per-chart formulas.

## 2. The fork is the A band, and it is structural

PUMBILITY reads the grade, so the A band — **100,000 points wide** — prices identically at 800,000
and at 899,999. Competitive Level moves **5.71 levels** across that same span. That one band
produces the whole disagreement: PUMBILITY rates an A-grade play up to **+4.7 levels** higher than
Competitive Level does.

It is why the two rank grinding and pushing so differently. To PUMBILITY a barely-passed D23 is
worth 95% of a polished D18; to Competitive Level it is worth six levels less.

The same staircase structure means score movement inside a band is worth **exactly zero** PUMBILITY:

| band | width | competitive levels across it | PUMBILITY gain |
|---|---|---|---|
| A | 100,000 | 5.71 | 0 |
| A+ / AA | 20,000 | 1.14 | 0 |
| AA+ / AAA / AAA+ | 10,000 | 0.57 | 0 |
| S and above | 5,000 | 0.29 | 0 |

## 3. Singles is where the per-chart formulas actually disagree

Both pay a singles bonus, in different shapes. PUMBILITY prices a single one level up its base
curve — a flat **+2.1%** at every level in this band. Competitive Level compounds: **nothing below
level 20**, then +0.17 of a level at 20, +0.36 at 21, +0.57 at 22, +0.79 at 23.

Switch the companion page to Singles and the worst above-A+ disagreement widens from 0.46 to
**1.3 levels** — roughly three times the doubles figure, all of it from the bonus rather than the
core curve.

## 4. Sum versus average is the bigger divergence

The per-chart formulas mostly agree; the *aggregations* do not, and that is what makes "similar
PUMBILITY" and "similar Competitive Level" different populations.

Four synthetic 50-chart doubles records in the band (Marvelous plates):

| archetype | charts | PUMBILITY | Competitive | profile |
|---|---|---|---|---|
| Grinder | 50 | 16,643 | 20.04 | D18–19 at SSS |
| Balanced | 50 | 16,926 | 20.90 | D20–21 in the S range |
| Pusher | 50 | 15,544 | 14.21 | D22–23 barely passing |
| Sniper | 15 | 5,255 | 22.44 | fifteen charts, all polished |

Grinder, Balanced and Pusher sit within **9% of each other on PUMBILITY** while spanning **6.7
competitive levels**. Sniper has the **best** competitive level of the four and **a third** of
anyone's PUMBILITY, purely because 15 charts cannot fill a 50-chart sum but do fill a 100-chart
average.

Sweeping the same parameter space more broadly (panel 04 of the companion page): every synthetic
player within **3% of Balanced's PUMBILITY** spans about **4 competitive levels**, and every player
within **0.35 of a level** of Balanced's competitive level spans a **~330% range** of PUMBILITY.

So the honest answer to the original question:

- **Similar PUMBILITY** ⇒ similar *volume of decent scores in the band*. It says very little about
  how clean those scores are, and nothing at all about score movement inside a grade.
- **Similar Competitive Level** ⇒ similar *accuracy on their best charts*. It says nothing about how
  many charts they have played.

## 5. Two implementation notes

- **Singles Competitive Level selects and scores by different numbers.** In
  `PlayerRatingSaga.RecalculateCore`, the per-type pools order by the type-aware Fung score (which
  applies the `1.008^(L−19)` singles multiplier) and then average the type-*blind* one. The selected
  50 is therefore not the top 50 by the value being averaged — at high level the multiplier reorders
  enough to swap charts near the cutoff. Doubles is unaffected (the multiplier is singles-only).
  Whether the type-blind average is intentional is an owner call; the select/score mismatch is not
  obviously so.
- **The sub-A grade multipliers remain extrapolated.** B and below are extended at the last observed
  −0.05 step and carry `TODO(P2-pumbility)` in `ScoringConfiguration`. Nothing in §1–§4 depends on
  them (they are all at A or above), but any number left of the A floor inherits that uncertainty.

---

## Method and caveats

- The companion page's model is a JS port of both formulas, checked against all 30 golden per-chart
  rows in `ScoreTracker.Tests/DomainTests/Phoenix2PumbilityScoringTests` — every one reproduces to
  the cent, plus the base-curve, perfect-game and sub-level-10 cases.
- **The players are synthetic.** No prod data was available; panel 04 is a parameter sweep over
  plausible D18–D23 records (centre level, mean score, record size), not a sample of real accounts.
  The per-chart maths in §1–§3 is exact and needs no data.
- Plate is held constant across a record, CO-OP and performance charts are excluded (neither counts
  toward PUMBILITY), and broken plays are excluded from both.
