# Phoenix Calculator redesign

**Status: built.** Folds `/RatingCalculator` into `/PhoenixCalculator` and rebuilds the result
as one chained tool. Mock (owner-driven, drove five rounds and doubles as the spec):
<https://claude.ai/code/artifact/baba987d-80be-43fe-9c7f-fb91bd77312e>

The maths lives in [`ScoreAnalysis`](../../ScoreTracker/ScoreTracker.SharedKernel/Models/ScoreAnalysis.cs)
under `ScoreAnalysisTests`; the page's wiring is pinned by `PhoenixCalculatorPageTests`.

Design system: [UX-GUIDELINES.md](../UX-GUIDELINES.md). Domain terms: [DOMAIN.md](../DOMAIN.md).

---

## 1. Why

Two pages were two halves of one question. Judgments make a score, a score makes a grade, a
grade and a folder make PUMBILITY — one pipeline, split across two routes that didn't know
about each other.

Each half had its own problem:

- **`/PhoenixCalculator`** spent an enormous amount of copy on a small idea: seven inputs,
  then five `Score Loss: N` lines, a random-walk sentence for the next grade, a banner image,
  a screenshot of the formula, a sixteen-line list of grade cutoffs and an eight-line list of
  plate rules. Owner: *"uses up way too many words and visual space for what it is."*
- **`/RatingCalculator`** had the better idea — a colour-coded level × grade grid — carried by
  a hand-rolled rgb lerp, with its most useful feature (click a value, highlight everything
  worth about the same) undocumented and untuned for Phoenix 2.

They also disagreed about the mix: the score page silently followed the selected mix while
the rating page carried its own toggle.

## 2. The shape

One page, one mix control, **two ways in** (owner, 2026-07-28). Step 3 is shared, which is
the entire point of combining.

| | step 1 | step 2 | step 3 |
|---|---|---|---|
| **From a score** (default) | score + plate | — | what it's worth |
| **From a results screen** | judgments + max combo | what it scores | what it's worth |

Most people know their score and want the PUMBILITY value; only someone standing at a
results screen wants the judgment breakdown. Switching routes carries the number across.

**Level selection is per-mix, because the formulas are:**

- **Phoenix** prices on level alone — a slider (10–29), plus a CO-OP chip for the flat
  base-2000 folder. Naming a chart type here would be a lie, so no labels do.
- **Phoenix 2** prices singles one level up the base curve, so type is part of the folder.
  It uses the shipped [`FolderPicker`](../../ScoreTracker/ScoreTracker/Components/FolderPicker.razor)
  — ◄ ► steppers around a button opening the type-tab + level grid popover. Singles cap at
  26 (`FolderLevels.MaxSingleLevel`), no Co-Op tab (P2 excludes co-op outright), sub-10
  folders greyed as missing since they price at zero.

**Plate** is a picker on the score route and derived from the judgments on the other. It
only appears for Phoenix 2 — Phoenix PUMBILITY ignores plate, and the page says so rather
than showing a dead control.

## 3. What this pass adds

### 3.1 Step 2 shows both halves of the million

Owner, 2026-07-28: *"I'd expect two different views — one is where your score came from and one
is what score did you lose."* Right, and the current page only has the second (five
`Score Loss: N` lines). They are complements — earned + lost = 1,000,000 — and each answers a
question the other cannot.

**Where your score came from** decomposes what you banked, on the full 0–1,000,000 axis.
Perfects pay full, greats 60%, goods 20%, bads 10%, misses nothing, plus the max-combo
component:

```
contribution_j = 0.995 × weight_j × count_j / total × 1,000,000
combo          = 0.005 × maxCombo / total × 1,000,000
```

On the sample play that is 831,833 from perfects, 76,785 from greats, 4,266 from goods, 533
from bads and 3,751 from combo — summing to exactly the 917,168 score.

**Where your score came from** is the bar that carries the zoom (§3.2), because it is the one
perfects would otherwise swallow. Segments stack biggest-first and clip at the baseline. No
caption is needed explaining what fell off: the baseline is never above what perfects earned,
so the cut always lands *inside* the perfects segment and no later contribution is ever
clipped.

**What you lost** is a **distribution, not a scale** (owner, 2026-07-28). The full width is the
points you lost, split by what took them, with each share printed. **Perfects never appear —
a perfect loses nothing.** On the sample play: greats 62%, goods 21%, misses 10%, bads 6%,
combo 2%.

**Each bar labels itself** with per-judgment points and share of that bar's own total (owner,
2026-07-28) — so no summary table is needed and there isn't one:

```
Where your score came from   917,168      zoomed to 825,000 – 1,000,000
  Perfect 831,833 90.7% · Great 76,785 8.4% · Good 4,266 0.5% · Bad 533 0.1% · Combo 3,751 0.4%

What you lost                −82,830
  Greats 51,189 62% · Goods 17,063 21% · Misses 8,531 10% · Bads 4,799 6% · Combo 1,248 2%
```

Reading the two against each other is the payoff: **a good banks 4,266 (0.5%) and loses 17,063
(21%)**; a bad banks 533 and loses 4,799. Four and nine times what they earn — which is why
cleaning goods off a chart is worth more than it looks, and why a good is barely better than
a miss.

The earned legend is also what rescues the clipping: it prints perfects at 90.7% of the score
even when the window can only show a sliver of them. **The legend is the truth; the bar is the
view.**

Shares are of each bar's own total, so both read 100%. Where a share falls under 1% every entry
in that legend gains a decimal place — otherwise a 99.5% segment rounds to "100%" beside a
visible second one and reads as broken.

### 3.2 The earned bar's baseline keeps a perfect window open

On a full 0–1,000,000 axis every contribution but perfects is an invisible sliver, so the
earned bar spans **from a baseline up to a perfect million**. Owner, 2026-07-28 — the baseline
is the **lower of two candidates**:

```
threshold ladder                     perfects floor
  score > 990,000  →  975,000          what perfects alone earned,
  score > 975,000  →  950,000          floored to 25,000
  score > 950,000  →  925,000
  score > 925,000  →  900,000
  score > 900,000  →  850,000
  otherwise        →  floor to the previous 100,000
```

The ladder tightens as the grades do (AA 900k, AA+ 925k, AAA 950k, S+ 975k, SSS 990k — six of
the sixteen grades live inside the last 3%), each score sitting a rung above its floor. The
perfects floor is what keeps the goal honest: **taking the lower guarantees the cut lands
inside the perfects segment**, so every play shows some perfect window.

That second candidate is not optional. A plain 900,000 floor on a 917,168 hides perfects
*entirely*, because perfects only reach 831,833 there — the bar opened on greats and the
biggest contribution to the score was invisible. Measured across the range:

| play | baseline | perfect window |
|---|---|---|
| 856,378 | 725,000 | 7.8% |
| 917,168 | 825,000 | 3.9% |
| 979,988 | 925,000 | 32.2% |
| 996,572 | 975,000 | 45.9% |
| 1,000,000 | 975,000 | 80.0% |

A play with no perfects at all floors to 0 and simply has no perfect segment, which is correct.

**Known thin case:** when the perfects total lands just above a 25,000 line the window is
technically nonzero but visually hairline — a 598,822 play whose perfects earn 426,581 floors
to 425,000 and shows 0.3%. Track segments carry `min-width: 2px` so nothing worth real points
rounds away to nothing, and every value is printed in the ledger regardless. If that is not
enough, the fix is one line: step the perfects floor down another 25,000 whenever the visible
share would fall under ~5%.

### 3.3 Step 3 lists neighbours, not numeric twins

Owner, 2026-07-28. The chips beside the value answer *"what else could I have played instead"*,
so they walk **three folders either side and name the closest grade at each**, in ladder order.

The alternative — hunting the whole grid for values within a tolerance — produces
real-but-useless pairs in Phoenix, because that mix spans 75×: locking an S on level 21 offers
a *C on 25* and a *D on 26* as equivalents. The arithmetic is right and the advice is absurd.
Three folders is the range a player would actually consider, so those matches are unreachable
by construction rather than filtered out afterwards.

Where the closest grade at a folder still misses badly the chip says so instead of presenting
a ceiling as a match — from AA on 21 (760), level 18 tops out at *SSS+ = 690*, and the chip
reads `even SSS+ −70`. Ranges clamp at both ends (level 10 offers only 11–13; level 29 only
26–28), and co-op — which has no level of its own — anchors on the level worth the same as its
flat base, where `AA on 29 = 2,000` matches it exactly.

### 3.4 The next-grade diff stays a simulation

**Points to the next grade** is exact and always was: `nextGradeFloor − score`. A 917,168 in
Phoenix is 925,000 − 917,168 = **7,832** from AA+. That figure replaces the current page's
prose lead.

The *diff* underneath it is the interesting part, and the existing implementation is doing
something deliberate that is easy to mistake for a shortcut. `IterateWithWeightedRandom` draws
a note **weighted by your current distribution** and upgrades it, repeatedly, until the grade
flips — so the answer looks like a realistic next play: mostly greats if you drop greats,
mostly misses if you miss. Owner, 2026-07-28: *"unless you have some formula with weights to
provide a deterministic result for that, you're not doing that same simulation."* Correct — a
"clean 19 greats" figure answers a different question (fix **only** greats), and is not an
improvement on this one.

Keep the model, drop only the sampling. Because the walk draws proportionally to what you
still have wrong, allocating **fractionally** instead of drawing gives its expected diff in
closed form. Per upgraded note the gains are fixed by the score formula:

```
great→perfect  = 0.995 × 0.4 / total × 1,000,000
good→perfect   = 0.995 × 0.8 / total × 1,000,000  + 0.005 / total × 1,000,000
bad→perfect    = 0.995 × 0.9 / total × 1,000,000  + 0.005 / total × 1,000,000
miss→perfect   = 0.995 × 1.0 / total × 1,000,000  + 0.005 / total × 1,000,000
```

(the trailing term is the point of combo a good, bad or miss also cost you). Each step then
gains the **share-weighted average** of those, with shares `count_j / (greats+goods+bads+misses)`,
and the counts deplete fractionally as the walk proceeds. Iterate until the gap closes and the
consumed fractions are the diff.

On the 933-note sample play that is ~15 notes → **−12 greats, −2 goods, −1 miss**. A miss-heavy
play of the same score instead reports −8 misses and −5 greats. Same shape of answer as today,
without a seed.

Two things this also fixes. The current walk is **not reproducible** — `ScoreScreen` holds a
`private static readonly Random Random = new(1949)` shared across the whole process, so the
answer depends on how many times anything else called it first, and `Random` is not
thread-safe under the concurrent web and Discord callers. And identical inputs can print
different sentences on two refreshes.

### 3.5 The grid's lock band is derived, not a constant

The grid's click-to-lock highlight keeps a tolerance — it answers a different question from
§3.3's chips ("show me everything worth about the same as *this cell*", a landscape query
rather than a shortlist of alternatives). But the tolerance was a hardcoded ±10%, tuned for
Phoenix and never revisited, and Phoenix 2 broke it — the owner spotted the band was far too
wide there.

The cause is scale compression. Phoenix values span **75×** (base 100 → 2000, grade
multipliers 0.40–1.50). Phoenix 2 spans barely **2×**: its base curve is nearly flat
(185 → 280 across the whole playable range) and its grade multipliers are squeezed into
1.08–1.50. The same percentage cannot mean the same thing in both.

| | value spread | flat ±10% caught | derived band | now catches |
|---|---|---|---|---|
| Phoenix, levels 10–29 | 75× | 7% of the grid | **8.5%** | 5% |
| Phoenix 2 singles, 10–26 | 2.1× | **44% of the grid** | **1.2%** | 5% |
| Phoenix 2 doubles, 10–29 | 2.3× | 39% of the grid | **1.2%** | 4% |

The band is now **half of one folder step**, taken as the median adjacent-level ratio off
whichever mix's base curve is active: *two folders are worth about the same when the gap is
smaller than half of what climbing a folder would earn you*. It lands on ~5% of the grid in
every mix, it re-tunes itself if a base curve is ever re-derived (Phoenix 2's still has
unverified rungs below B), and the resulting figure is printed in the UI rather than hidden.

Note this validates the old constant for Phoenix — that mix's half-step is 8.5%, so ±10% was
about right there, which is why nobody caught it until Phoenix 2 existed.

**Generalizable rule** (worth carrying into UX-GUIDELINES): a similarity band must be
derived from the spread of the scale it measures. A constant tolerance silently becomes
either noise or nothing when the same control is pointed at a second scale.

The corollary is §3.3's: where a shortlist is what the user wants, bound it by something the
domain understands — folders away — rather than by any tolerance at all.

## 4. Everything else that changes

**Replaced with something rendered rather than recited:**

- Five `Score Loss: N` lines → two self-labelling bars in the `--judg-*` colours (§3.1).
- The random-walk sentence → an exact points-to-next-grade figure plus the same walk's
  expected diff, computed rather than sampled (§3.4).
- The sixteen-line grade cutoff list → the grade band prints beside the derived grade.
- The eight-line plate rules → the plate chips carry their own bonuses.
- The rgb lerp → the rarity ramp, with values always printed (rule 8).

**Dropped:** the Phoenix banner image, the `PhoenixFormula.jpg` screenshot, the Max Rating
column (owner: the page predates PUMBILITY being anything but title progression within a
folder, and `count × SSS+` is misleading now that only your top 50 count), the chart-count
column, and XX (its scoring keyed off arbitrary combo rather than judgments, so a score was
never computable from a results screen — owner, 2026-07-28).

**Kept:** calories → arrows pressed (owner uses it), the value grid as a bottom-of-page
collapsible, and **both community credits in the footer** — MR_WEQ for the formula, daryen for
the grade ranges (owner, 2026-07-28: if one stays, both stay). Both reuse their existing resx
values verbatim, so neither needs retranslating.

**Deferred:** the score-distribution chart (your judgment spread against the average spread for
that score) is **out of scope for this PR** — owner, 2026-07-28: the journal judgment data wants
analysing before anything is built on top of it. `ScoreDistributionDto` and its
`Show Score Distribution` resx key stay where they are; nothing consumes them until that
analysis happens. Do not delete them as orphans.

## 5. Technical scope

### Domain

New `ScoreTracker.SharedKernel/Models/ScoreAnalysis.cs` — the page must not carry this math,
and `ScoreScreen` lives in `Domain/Records/` which is `[ExcludeFromCodeCoverage]`. Precedent
is `RecapPlayerTypeCalculator` / `LifebarAnalysis`: a testable analysis type beside the model.

- `EarnedBreakdown(screen)` — what each judgment banked, plus the combo term (§3.1).
- `EarnedBaseline(score, perfectEarned)` — the lower of the grade-ladder floor and the
  perfects total floored to 25k, so a perfect window always shows (§3.2).
- `PointsToNextGrade(score, mix)` — subtraction against the cutoff table (§3.4).
- `ExpectedDiff(screen, need)` — the weighted walk's expected diff in closed form, replacing
  the seeded sampling but **not** the model (§3.4).
- `Neighbours(mix, type, level, grade, plate, reach)` — closest grade at each folder within
  reach, clamped to the type's level range, flagging ceiling/floor misses (§3.3).
- `EquivalenceBand(mix, chartType)` — half the median adjacent-level base ratio (§3.5).

⚠ **`ScoreScreen.NextLetterGrade` is also consumed by the Discord bot**
([BotCommandSaga.cs:668](../../ScoreTracker/ScoreTracker.Communities/Application/BotCommandSaga.cs)),
along with `GreatLoss`/`GoodLoss`/`BadLoss`/`MissLoss`/`ComboLoss`/`EstimatedSteps`/`PlateText`.
The `/piu` score command renders the same breakdown. **Do not delete it in this PR** — add the
deterministic methods alongside and leave the bot on the existing call. Migrating the bot
changes user-visible Discord copy and belongs in its own change, behind the canary.

### Web

- `Pages/Tools/PhoenixCalculator.razor` — rebuilt. Stays `@rendermode RenderModes.Interactive`
  (`RenderModeDeclarationTests` ratchets the declaration).
- `Pages/Tools/RatingCalculator.razor` — deleted.
- `Controllers/ChartPermalinkController.cs` — `/RatingCalculator` → `RedirectPermanent("/PhoenixCalculator")`,
  matching the existing `/ChartCompare` → `/MixChanges` 301.
- `Components/Shell/ShellNav.razor` + `Components/Shell/ShellMoreSheet.razor` — the two
  Tools entries collapse to one, and that one is **gated to Phoenix-family mixes**
  (owner, 2026-07-28: *"no one is expecting a XX score converter"*). The predicate already
  exists and states its own reason: `!Model.CurrentMix.UsesLegacyScoring()` — the page computes
  Phoenix-formula scores, so it appears for the mixes that use them. Note `LegacyMixGate.IsGatedMix`
  is *not* the right condition here: XX is deliberately un-gated, so today it still shows these
  links. `ShellMoreSheetTests.GatedMixKeepsChartsAndDiscordAndDropsTheGatedGroups` already
  asserts absence under a gated mix and keeps passing; add the XX case beside it.
- Route as URL state (owner, 2026-07-28): `?from=score` / `?from=judgments`, so a route is
  shareable — what-data-you-see belongs in the URL, presentation does not.
- `Controllers/SitemapController.cs` — **no change**; `/PhoenixCalculator` is already listed
  and `/RatingCalculator` never was.
- Localization: new keys in all nine locales, inserted alphabetically (`ResxKeysAreStoredAlphabetically`),
  no case-variant collisions (`LocalizationKeyTests`). Retire the `Rating Calculator` key;
  add `PUMBILITY Calculator`. Judgment names stay English literals, as with the Life
  Calculator. Follow `docs/LOCALIZATION-<locale>.md` glossaries.

### The colour ratchet has a hole

`UiColorTokenTests` scans for hex literals and `Colors.*` constants — **not `rgb()`/`rgba()`/
`hsl()`**. That is why `RatingCalculator.razor`'s hand-rolled lerp never had an allowlist
entry despite being the most literal-heavy colour code on the site. Closing it is cheap:
after this page goes, only three files carry such literals — `PhoenixRecap.razor` (7,
deliberately self-styled slide art, gets an allowance), `Privacy.cshtml` (2), and
`Components/Sessions/MilestoneStrip.razor` (1, worth just fixing).

Because RatingCalculator has no allowlist entry, deleting it needs **no allowlist edit** —
unusual for a page overhaul here, and worth stating so nobody hunts for one.

### Tests

- `ScoreTracker.Tests/DomainTests/ScoreAnalysisTests.cs` — baseline rounding incl. the
  perfect-score clamp; points-to-next-grade across both mixes' cutoff tables; the expected
  diff **tracking the input distribution** (a miss-heavy play reports mostly misses, a
  great-heavy one mostly greats) and being **stable across repeated calls**, which the seeded
  walk is not; neighbour clamping at both ends of the level range; the band per mix and chart
  type, pinned against the table in §3.5.
- `ScoreTracker.Tests.Components/PhoenixCalculatorPageTests.cs` — route switching keeps the
  number; the same score grades AA in Phoenix and A+ in Phoenix 2; slider for Phoenix vs
  folder picker for Phoenix 2; plate row absent on Phoenix; grid renders and locks.
- No E2E. The granularity ladder (owner, 2026-07-12) puts this at component level; a
  calculator is not a critical whole-workflow path.

## 6. Commit order

Each commit builds and leaves the suites green on its own.

| # | commit | why here |
|---|---|---|
| 1 | `docs: design doc for the phoenix calculator redesign` | the spec lands before the code implementing it |
| 2 | `feat(scoring): ScoreAnalysis — deterministic next-grade, fixes, equivalence band` + tests | pure domain, no UI; the band table in §3.5 becomes assertions |
| 3 | `feat(calculator): rebuild /PhoenixCalculator as two routes over one chain` | the core: steps 1–3, folder picker vs slider, plate picker, baselined bar, resx ×9 |
| 4 | `refactor(calculator): retire /RatingCalculator` | delete, 301, nav collapse, orphaned key removal |
| 5 | `test(arch): catch rgb()/rgba()/hsl() in the colour ratchet` | independent; cheapest after the worst offender is already gone |
| 6 | `docs: ARCHITECTURE + UX-GUIDELINES for the combined calculator` | the Tools row, and the derived-band rule from §3.5 |

The value grid landed inside commit 3 rather than as its own step: it shares the page's markup
and its heat reads the same plate and type the rest of the chain does, so splitting it would
have meant writing it twice.

Commits 3 and 4 are the review-heavy ones. Commit 3 carries the nine-locale resx pass — the
highest-risk file set in the PR ([resx editing hazards](../../CLAUDE.md), and every value
must be XML-validated after the splice).

## 7. Settled, and what is left

Resolved by the owner on 2026-07-28, all folded into the sections above: the step-3 chips stay
but are reformulated as neighbours (§3.3); the plate default stays **TG**; the absurd-pairing
problem is solved by §3.3 rather than by filtering; the route becomes URL state; and the page
is **hidden from nav outside the Phoenix family** rather than showing XX visitors a converter
that cannot work for them.

**Still open — one decision, and it is not blocking:** the Discord `/piu` score command prints
the same random-walk sentence from `ScoreScreen.NextLetterGrade`. Once `ScoreAnalysis` lands it
could print the exact figure and the expected diff instead (§3.4). That is a user-visible copy
change on a shipped surface with its own canary, so it stays out of this PR either way — the
question is only whether to schedule it afterwards or leave the bot as it is.
