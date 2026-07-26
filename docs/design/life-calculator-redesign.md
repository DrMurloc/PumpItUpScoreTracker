# Life Calculator redesign

The `/LifeCalculator` rebuild: the lifebar becomes an instrument you can play instead of
a wall of prose. Workshopped 2026-07-26 over three mock rounds; the decisions below are
owner-locked. Route is unchanged.

## Why it exists

The old page opened with a yellow disclaimer, then three ApexCharts, then twenty-two
consecutive `<MudText>` lines. The two numeric inputs that drove the charts sat *between*
chart one and chart two, so they never read as controls. The question players actually
bring to the page — *how many misses can I afford?* — was answered somewhere around
paragraph seventeen.

Worse, the prose was making claims nothing tested. "17–21 combo per Bad" and "40–50 combo
per Miss" read as if the numbers vary by level. They don't: the break-even combos are
**identical at every level 1 through 29**. Those ranges were the perfect-vs-great spread at
a fixed level, written up as if they were a level range. The page had no test that would
have caught it because the math lived in the `.razor` code-behind.

## Decisions

1. **The playable lifebar is the hero.** The bar renders to scale, judgment keys drive it,
   and a telemetry rail exposes the two variables the cabinet hides: the gain multiplier
   and what a miss costs *right now*. Pressing MISS at a full bar and watching perfects pay
   `+0` afterwards teaches the multiplier in one gesture. Considered and rejected: opening
   on the survival-budget tiles with the bar demoted beneath them.
2. **The bar is drawn in two zones.** The **visible bar** (0–1000, the rainbow) and the
   **overflow** above it, at true proportion. At level 23 that's 1000 visible and 1587 you
   can't see — the single most useful fact on the page, previously three overlapping bar
   series in an ApexChart.
3. **"Overflow", never "reserve" or "overfill".** Owner's call, and it matches the term the
   old page already used in prose ("up to about a 2350 overflow at level 28"). One root
   across the zone label, the `Overflow full` state chip, the ladder key and the
   `--life-overflow` token — two near-identical words would be a translator trap.
4. **Phoenix 2's electric effect is recorded on the bar.** P2 runs an electric effect once
   the overflow is completely full; the note under the bar says so and lights up when you
   actually reach that state. It is the only moment the game admits the overflow exists, so
   it belongs on the instrument rather than in the prose.
5. **A step toggle, not canned runs.** `×1 / ×20 / ×50` applies to every judgment key
   (mouse and keyboard). Each key's delta previews the *whole* press, which is what makes
   the multiplier legible: ×50 perfects from a dead multiplier reads `+303`, the same fifty
   from a capped one reads `+450`.
6. **Multi-note presses sweep.** The bar animates on a CSS transition whose duration rides
   a `--sweep` custom property set inline — free on a server circuit. The digits count up
   through one `InvokeVoidAsync` per press. **Never animate this in C#**: on
   `InteractiveServerRenderMode` that is one round-trip per frame, fifty for a ×50 press.
   The readout is data, so the count-up carries a timer-based guarantee that the true value
   lands even when animation frames never run (background tab).
7. **Two views on one chart.** *Where life settles* (new) answers "what does 1 miss per 30
   notes actually leave me at" and exposes the cliffs; *How long you survive* is the old
   notes-to-death curve, kept and retitled. Both stay ApexCharts on
   `ApexChartTheming.BaseOptions` — nothing in this app hand-rolls a canvas.
8. **The Life Threshold field is gone.** A raw 0–3000 numeric input was never the question.
   The three thresholds anyone wants — alive, half bar, rainbow — are the survival-budget
   tiles and the chart's reference lines.
9. **The level slider stays.** Owner's call, even though the break-even combos don't move
   with level; it makes the bar concrete at a level you actually play. A chart picker was
   considered and rejected, which also keeps the page dispatch-free — no `IMediator`, no
   catalog read, no DB.
10. **The lifebar stays page-local markup, not a shared component.** One-off until the
    judgment-distribution work finds it a second home. Promotion later is mechanical: the
    markup already reads every color from tokens.
11. **Derived math moves to the SharedKernel.** `LifebarAnalysis` holds the settle point,
    break-even combo, straight-death count and fill time as pure static functions, with
    `DomainTests` pinning the numbers the page states as fact. Three of these loops
    previously hid in the `.razor` where nothing tested them.
12. **The disclaimer stops being the headline.** The NX2/Prime data-mine provenance is a
    quiet chip in the header that expands, restated in full beside the source link. The
    caveat is still unmissable; it is no longer the first thing anyone reads.

## The numbers, and where they come from

Every figure on the page is computed at render time from `LifebarSimulator` via
`LifebarAnalysis` — none of it is hardcoded prose. The facts the copy leans on:

| Fact | Value | Note |
|---|---|---|
| Break-even combo, perfect filler, miss break | 18 / 38 / 47 | alive / half bar / rainbow — **same at every level** |
| Break-even combo, great filler, miss break | 22 / 47 / 56 | the spread the old copy mistook for a level range |
| Break-even combo, bad break | 18 (perfect), 22 (great) | a bad is flat −50, so clearing the cliff recovers fully |
| Straight misses from song start | 7 | level-independent; you always start at 500 |
| Straight bads from song start | 10 | 500 ÷ 50 |
| Straight misses from a full bar, level 23 | 15 | level-dependent, unlike the combo figures |
| Miss cost at ≥1000 life | −270 | `trunc(min(life,1000) ÷ 4 + 20)` |
| Life below which a miss is cheaper than a bad | 120 | the old page's "12% or lower visual life" |
| Multiplier rebuild after a miss | 40 perfects / 50 greats | 0 → the 0.80 cap |
| Overflow | `3 × level²` | 300 at level 10, 2523 at level 29 |

**The cliff is a cliff, not a slope.** At 17 notes between misses you die at any level; at
18 you survive. Nothing gradual sits between them — below the line the multiplier never
rebuilds, so gain truncates to nothing. The old chart hid this by plotting notes-to-death,
which simply goes vertical.

**"Hold the rainbow" is unreachable below level 10** (level 5 for bads): the overflow is
thinner than a single break, so no amount of combo keeps the visible bar full. The fourth
budget tile says so rather than printing a number that doesn't exist.

## Two new semantic token groups

Both are **shared across mixes**, exactly like the alert colors — a MISS has to read as a
miss in every theme. Emitted by `MixThemes.CssVariablesFor`, reached through
`ThemeScales`, documented in [UX-GUIDELINES](../UX-GUIDELINES.md) §1.

- **`--judg-*`** — the game's own judgment vocabulary: perfect ice-blue, great green, good
  amber, bad violet, miss red. The first consumer is this page; anything that renders a
  judgment should reuse them rather than re-deriving.
- **`--life-*`** — the lifebar's own zones: the seven rainbow stops (`--life-r1`…`r7`)
  composed into `--life-rainbow`, plus `--life-overflow` and `--life-danger`.

The four chart series are **two hues × two line styles** (miss red / bad violet, solid for
perfect-filled and dashed for great-filled) rather than four hues — the same trick the
widget charts use for era. Chart *reference* lines are neutral ink on purpose: they are
furniture, not data, and a violet threshold line beside a violet series is a misread
waiting to happen.

## Localization

The page goes from 46 keys to roughly 90. Two rules the copy is built around, both learned
from round-2 review:

- **No judgment name spliced into a shared sentence.** The "impossible at this level" line
  is two complete sentences, one per judgment, with only the level as `{0}`. Interpolating
  the word *miss* or *bad* into one template breaks the moment a language inflects it.
- **No inline markup inside a resource string.** Emphasis is carried by a tile's border
  color and its number, never by `<b>` baked into the copy.

Tightest spots at +40% text are the tile unit labels ("notes per miss") and the uppercase
telemetry labels; both wrap rather than truncate. The Zelllooo quote is a proper noun and
is not translated.

## What this does not touch

`LifebarSimulator` is referenced by exactly two things: this page and its own tests. The
page dispatches no `IMediator` calls and injects no repository, so the rebuild reaches no
Application handler, no vertical, no database, no migration, no bus message, no API, and no
scheduled job. The E2E suite needs no changes either — `LayoutContractTests` and
`TierListTests` both visit `/LifeCalculator` but assert nothing about its content (one
checks the legacy-mix gate redirects away from it, the other uses it as a back-navigation
origin).

## Source

Formulas data-mined from NX2 and Prime by
[Team Infinitesimal](https://github.com/Team-Infinitesimal/Infinitesimal/blob/lts/Modules/PIU/Gameplay.Life.lua).
Unconfirmed against Phoenix and Phoenix 2 — the page says so, twice.
