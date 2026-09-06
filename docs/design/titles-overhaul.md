# Titles page overhaul

Rebuilds `/Titles` from a grouped data grid into ladder rails. Mocked and iterated with the
owner over three rounds before any code; the mock's final state is this doc.

## Why

The old page was one `MudDataGrid`, grouped by category, in a 500px scroll box holding all 213
Phoenix titles (272 in Phoenix 2). The grouping was the visible bulk, but the deeper problem was
that it flattened **ladders** into undifferentiated rows: 31 difficulty titles read as 31 rows
rather than four folder tiers, and 32 plate titles as 32 rows rather than eight families.

About 90% of every mix's title list is a ladder:

| Section | Phoenix | Phoenix 2 |
|---|---|---|
| Progression | 31 (4 rails) | 70 (9 rails: 2 pools × 3 bands + capstone, plus the merged gems) |
| Skill | 67 (6 rails) | 100 (9 rails) |
| CO-OP | 15 (2 rails) | 16 (2 rails) |
| Plates | 32 (8 rails) | — |
| Boss breakers | 32 (20 rails) | 34 (21 rails) |
| Step artists | 12 (6 rails) | 36 (12 rails) |
| Play count | 5 (1 rail) | 6 (1 rail) |
| One-offs | 19 | 11 |
| **Total** | **213 titles, 47 rails** | **272 titles, 54 rails** |

Ratcheted by `TitleRailTests` and `TitleRailsTests`; if those numbers move, the tests say so.

## The two groupings are not the same grouping

`Title.Ladder` is the **display** rail. `TitleHelpers.LinkLadder` groups by what **scoring**
shares. They legitimately differ, and Advanced is the worked example: three scoring ladders (the
20s, the 21s, the 22s, each floored on its own level) that the player reads as one rail of ten.

`Rung` cannot be derived from `CompletionRequired` either:

- Advanced Lv.3 asks 39,000 and Lv.4 asks 15,000, so sorting a rail by requirement scrambles it.
- Expert Lv.1 (23s) and Lv.6 (25s) are both exactly 40,000, so requirement cannot break the tie.
- Every skill and boss-breaker title requires 1.

Most rails fall out of constructor arguments that were already being passed and discarded — a
skill's name and level, a boss chart's mix. Basic titles have no requirement to sort by, so they
declare their rail as data. **Nothing keys off a title's name**: Phoenix 2's 272 names are
verbatim scrape output (typos preserved — the 20,000 PUMBILITY capstone `ABYSS ABSOLUTE` even
carries a trailing space on the site), so a name is external data and a rename would silently
collapse a rail.

## The page

- **Standing bar** — the title you wear and how much of the list you hold, above the fold. The
  worn title is the **furthest** progression rung, which is not the dearest one: Expert Lv.2 asks
  80,000 on the 23s where Lv.5 asks 20,000 on the 25s, so Phoenix ranks by folder level then
  rating, matching what `TitleSaga` already writes as your highest difficulty title. On Phoenix 2
  it is the merged-pool `[P.B]` rung; the `[S]`/`[D]` ladders only stand in until you have one.
  (Field test round 4: shipped ordering by requirement alone, which read Expert Lv.2 for a player
  on Lv.5.)
- **Rails** — one row per ladder. Only an in-progress rung carries a fill, and the fill is that
  rung's climb from the rung below it (`CompletionFloor`), not from zero.
- **Boss breakers** tile as cells, with the mix name **above** its buttons rather than beside
  them. Side by side, a long mix name and a 150px button fought over a 190px cell and the name
  lost; stacked, every cell also gets an identical button width, which is what stops the two
  near-identical labels ("Single Boss breaker" / "Double Boss breaker" — chart data, so the same
  in every language) from looking misaligned.
- **One-offs** are a badge sheet. Name colour is rarity.
- **Who has it lists who is standing on the rung**, not everyone who has ever held it, and counts
  the rest as "+N others have climbed higher". On Intermediate Lv.1 the raw holder list is very
  nearly the whole site and says nothing. One indexed read over the whole rail answers it, rather
  than one per rung.
- **Detail drawer** — requirement, your climb, rarity, and holders. Holders load on open, never
  with the page. **The selected rung is the only open state**: a second boolean let a scrim
  dismissal reopen itself on the page's next render (field test round 4).
- **Filters are furniture** above the list, never sticky. A non-matching rung **fades in place**
  rather than disappearing: a ladder with holes punched through it stops reading as a ladder.

## Officially-awarded titles

A title with `CompletionRequired == 0` — every `PhoenixBasicTitle`, which is 77 of Phoenix's 213
and 53 of Phoenix 2's 272 — has no formula behind it. Play counts and step-artist plays are things
piuscores never sees at all, and the plate titles do not count what they appear to (see below).
Phoenix 2's `[CO-OP]` rating ladder used to sit here too, on the reasoning that the site surfaces
the CO-OP Rating on no leaderboard for a computed value to be checked against; since 2026-08-17 it
computes like Phoenix's (`Phoenix2CoOpTitle`, 80 × (grade + plate) per co-op chart, every chart
summed — [phoenix2-implementation.md](phoenix2-implementation.md) has the evidence), so both mixes'
CO-OP rails now draw real progress.

These **never show partial progress** — the old page drew them as 0% of a requirement that does
not exist. A dashed edge is the only mark they carry on the page itself; the drawer says the rest
in words. (Field test round 5: they also wore an "official" tag, which was noise for something
the drawer already explains.)

The marking follows the model, not the name: Phoenix's `[X] EXPERT` capstones are basic titles
(official-only), while Phoenix 2 made the same titles `Phoenix2TitleSetTitle` and computes them.

> **The Phoenix plate titles are not a follow-up.** They look computable — every imported record
> carries its plate, so "count the charts at UG or better" is right there — but that is not what
> the game counts. The in-game rule is considerably more convoluted than a plate threshold, and
> the owner's call (2026-07-26) is that we cannot reproduce it accurately. They stay
> official-import-only. Do not offer to compute them.

## Rarity

A title's rarity is the **percentile of players who do not hold it**, so eight holders in 1,562
reads as being above 99.5% of players. That reuses the one shipped ramp
(`ThemeScales.BandFor`) rather than inventing a second, inverted set of cutoffs — consumers never
re-implement band cutoffs (UX-GUIDELINES §1). The percentage always prints beside the colour.

## Suggested level (Phoenix 2 only)

The tier-list page's PUMBILITY track (`FolderTitleTrack`, [pumbility-title-track.md](pumbility-title-track.md))
answered *"what does this folder do for me"* and needed your pool, floor and median to do it —
since 2026-09-05 only its gate survives, behind a pointer at the PUMBILITY page. The
drawer asks the opposite question — *"which folder is this title"* — which is a property of the
title, so `SuggestedTitleLevel` is deliberately **impersonal**:

```
S13   at SSS+
S14   at SS
S16   at AAA
Fifty charts, TG plate.
```

The lowest folder whose fifty charts reach the threshold, answered at **three fixed reference
grades** on a Talented Game plate, every multiplier read from the shipped `ScoringConfiguration`.
Singles price one level up the base curve; nothing below level 10 counts (Phoenix 2 prices those at
zero). A merged-pool rung names both types, since either side can fill it.

One number was the wrong shape for this answer: how well you play moves it by **three levels**
([S] ADVANCED LV.4 is S15 at SSS+ and S18 at AAA), so a single folder was right only for the player
already performing at the reference. Three rungs bracket it instead, and AAA — what the drawer
printed alone before — is now the floor of that bracket rather than its middle.

**The floor is set by what the field averages, not by how wide a bracket it makes** (owner,
2026-09-06). The block read SSS+ / AAA / A until then, which spanned eight levels — and the bottom
row was the reason the whole thing looked wrong: an A on Phoenix 2 is 800,000, so *fifty S28s at an
A* told a player they could earn `[S] EXPERT LV.8` on a folder they would be scraping through. A
grade nobody averages names a folder nobody plays. Raising the floor to AAA costs the bracket most
of its width — SSS+ / SS / AAA is 1.50 / 1.47 / 1.41, a 6.4% span against the old 17%, so the three
rows now land within three levels of each other and 28 of 70 rungs merge at least two of them. That
is the deliberate trade: three narrow rows that are all askable beat three wide ones whose bottom is
fiction. Do not widen it again by reaching down the grade ladder — the way to widen it is a grade
players actually average.

Grades run best-first, so the levels ascend down the block and the column reads in the same order as
every other grade list on the site. One shape falls out of the curve's ends and is rendered rather
than hidden:

| Shape | Rungs | What the drawer does |
|---|---|---|
| Grades landing on the same folder | 28 of 70 | The run collapses to one row reading *at {lowest} or better* — the level-10 floor produces identical rows for every easy title, the narrow grade span produces them again wherever a level costs more than 6.4%, and identical rows read as a rendering fault. 19 of those collapse all the way to a single row. |

A rung no folder reaches keeps its place naming the ceiling that falls short (*D29 still isn't
enough at A*) and never merges, since its folders are not an answer. **No title is in that shape at
the current reference grades** — at AAA the top folder reaches every threshold, and ABYSS ABSOLUTE,
the last one that fell short, does so only at a bare A. The branch stays as a guard against a
threshold moving out from under the curve.

Owner call, field-test round 3: a personalised version was built first and rejected as too wordy —
*"don't mix in personalization here."* Three impersonal rungs are the answer to the same problem
that one was too blunt for. The reference grades are the single knob if a column reads wrong
against real data.

## What was tried and dropped

- **A "Closest to earning" panel**, ranking unearned titles by work left. Dropped in round 3:
  what counts as "close" invites endless min/maxing and would not be good enough. Removing it also
  fixed the phone fold. Do not re-propose.
- **A per-section segmented meter** in the standing bar. Eight abutting fills in a 7px strip read
  as mush and the smallest segment was 15px wide; the per-section counts already live in each
  section header. Replaced with one overall bar.
- **Paragon anywhere on the page** — the grade letter inside an earned rung, the paragon ladder in
  the drawer, the paragon tag on a holder row. Dropped in round 4: paragon levels are being
  retired in favour of folder completion, which is not title-related. Nothing on this page
  references `ParagonLevel` any more, so the retirement will not collide with it.

## Tests

| Suite | Covers |
|---|---|
| `TitleRailTests` (unit) | The rail inventory: counts, contiguous rungs, capstones, the EXTRA double-only trap, rung order ≠ requirement order, a PUMBILITY band per rail |
| `TitleRailsTests` (component) | Section assembly, official marking across both mixes, rarity banding, the worn title |
| `SuggestedTitleLevelTests` (component) | Impersonality, singles-one-level-up, monotonicity, the level-10 floor; grade order, the merge, and that no rung falls short above one that doesn't |
| `TitleDetailDrawerTests` (bUnit) | What a player reads: the grade on the same line as its folder, both types on a merged row, the merge and the no-folder row rendered rather than dropped |
| `TitlesPageTests` (bUnit) | Rails render, filters dim, search matches a title's chart, drawer states, signed-out |
