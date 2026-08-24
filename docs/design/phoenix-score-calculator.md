# The Phoenix Score Calculator — `/PhoenixCalculator/{mix}`

The rebuild of `/PhoenixCalculator` (workshopped 2026-08-23/24). The old page was an interactive
circuit around six numeric fields: a formula that read one point high on two thirds of inputs
(`Math.Ceiling` where the game floors), a formula *image* hotlinked from arroweclip.se, a
"Score Distribution" comparison against a brute-forced table of judgement combinations rather
than real play, and none of it in HTML a crawler could read.

The page's job now: **the site's canonical explanation of the Phoenix score** — the formula in
real HTML, what each judgement costs, the letter cutoffs on both mixes, and what real scores
look like at every level, measured from the tracked population. Like the PUMBILITY calculator
([pumbility-calculator.md](pumbility-calculator.md)), it is written for a search engine and an
LLM as much as for a player: one URL per mix, the numbers stated in prose, zero circuits.

Mock (the spec for layout and copy): https://claude.ai/code/artifact/8b77a6d1-cc7a-4a91-836a-b9272705b74a

---

## 1. Decisions

**D1 — One URL per mix.** `/PhoenixCalculator/{mixSlug}` is self-canonical per mix, because the
pages genuinely differ: Phoenix 2 re-cut every letter below AAA (A 800k · A+ 900k · AA 920k ·
AA+ 940k against Phoenix's 750/825/900/925k), so the ladder, the judgement budgets and the
population are per-mix facts a searcher asks about by name. Bare `/PhoenixCalculator` — the
existing URL, which keeps its inbound signals — serves the viewer's mix and canonicalises to it;
a viewer on a mix without Phoenix scoring gets the newest mix that has it. Both mix URLs ride
the sitemap. `ScoreCalculatorMixes` is the one list the routes, the eyebrow links, the head
resolver and the sitemap agree on (the `PumbilityCalculatorMixes` pattern).

**D2 — Static SSR, zero circuits.** No `@rendermode` (listed in
`RenderModeDeclarationTests.StaticPages`). The formula, the cost cards, the budget table, the
ladder, the plates, the grade spreads and all three charts are server-rendered HTML/SVG. One
vanilla JS module (`wwwroot/js/phoenix-calculator.js`, served through `@Assets`) works on that
markup: the live calculator, the two attribution bars, the next-grade walk, the chart-size
chips, the Singles/Doubles toggle, chart tooltips, and the plays dialog. Every constant the
script needs (both mixes' grade floors, plate rules, the calorie table) is emitted server-side
from the enums as an `application/json` block inside a MarkupString — the static renderer drops
a `<script>` whose content is an expression — so the script can never drift from the engine.

**D3 — The game floors.** `ScoreScreen.CalculatePhoenixScore` used `Math.Ceiling`; the machine
floors — verified against 2,277 real judgement-carrying records (floor matched 2,268, ceiling
only the 736 already-integral). The fix lands with this page and the formula section says it
plainly: the game rounds down, and the formula is exact to ±1 point.

**D4 — The next-grade line keeps the weighted-random walk** (owner, 2026-08-23: "do NOT replace
my existing weighted random logic"). `IterateWithWeightedRandom` converts one non-perfect per
step with probability proportional to how many the player actually got, so the recipe
auto-scales to their own error mix — a goods-heavy play is told "fewer goods". The page frames
it as **"How close were you to the next letter?"** with the points distance and the walked
recipe. A deterministic cheapest-single-pool menu was proposed and rejected.

**D5 — Two attribution bars replace the loss list.** *Where your score came from*: the window
floors at the closest 100k below the score and caps at 1,000,000; segments stack Perfects →
Greats → Goods → Bads → Combo (the perfect mass clips at the window edge), and the space past
the score stays empty — the points not earned. *Where your loss came from*: 0 → the total lost,
always full-width, split Greats / Goods / Bads / Misses / Broken combo. Both live-update; both
carry per-segment tooltips with exact points.

**D6 — Calories stay.** The calories → arrows-pressed estimate is owner-verified and ships
unchanged (`ScoreScreen.EstimatedSteps` and its threshold table).

**D7 — Signed-in players load a real play from their journal.** A dialog with a table — jacket,
difficulty bubble, song name, the judgement string `P/GR/GD/BD/M · combo` with each count in its
judgement colour, and the score with the compare-page grade/plate stack — filterable, newest
first. Rows are the journal entries that *carry* judgements (recent-plays imports; manual and
best-list entries don't have them) with stage breaks excluded and finished fails included. The
data comes from a signed-in UI-support JSON endpoint (`/PhoenixCalculator/MyPlays`, the
`/Charts/Export.csv` pattern — not `api/*`), so the page keeps zero circuits; anonymous
visitors never see the button. The dialog is hand-rolled — a static page has no circuit for
MudBlazor, the `/Setup` precedent.

**D8 — "What a grade looks like" is measured, not enumerated.** The per-grade judgement table
(per 1,000 notes: perfects, greats, goods, bads, misses, combo) aggregates the ~28k
judgement-carrying bests, gated at 50 plays per grade. It replaces `ScoreDistributionDto`'s
brute-forced average over judgement *combinations*, which described no real player. The
calculator highlights the row for its current grade; the "typical X on Y notes" inline line was
tried and cut (adds nothing). The "no-miss plays" column was tried and cut (confusing).

**D9 — The population section shows what personal bests look like, not a sermon about 900k.**
Stacked shares per level, banded by the site's grade metals — below 900k (grey), the copper
900–950k band, silver 950–970k, gold 970–990k, ice 990k+ — with each mix's legend naming the
letters its bands cover. The measured story tells itself: at every competitive level 96–99% of
tracked bests sit at 900k or above and the median is an S. One disclaimer, owner-worded: the
distribution leans toward SSS+/PG at lower levels because players lamping folders push every
chart far past a first pass. Source line names the population ("personal bests of players
tracked on PIU Scores"); levels under 20 bests are hidden.

**D10 — The note-count section is the old `/NoteCounts` display reborn.** Per type and level:
the middle-80% band, the median line, and the extremes as dots (the min at D17 is Ugly Dee's
71 — every judgement a hold tick). Phoenix data is complete; **Phoenix 2 falls back to the
Phoenix count where its own is still null, with no disclaimer** (owner: totals changed on 11 of
2,042 charts judged on both mixes — too sparse to caveat). The old page's coverage/progress
graph stays dead.

**D11 — Hold ticks are the page's real finding, framed as tuning.** Judgements = tap rows (a
jump is one judgement) + hold ticks; hold ticks are perfects for as long as the hold is held.
From level 15 up the median chart is ~half hold ticks. Hold density is one of the levers a
chart's score difficulty is tuned with — **but some charts simply are hold songs** (Ugly Dee,
8 6, Pneumonoultramicroscopicsilicovolcanoconiosis), so the copy claims the lever without
claiming every hold is tuning. The section carries the tick-share band by level, the 37.6%
line above which a passing F is impossible (good-spam floor = 1e6 × (h + 0.199(1−h)) < 500k),
and the most/least tick-heavy lists.

**D12 — Hold data is aggregate-only, and says it is estimated.** No per-chart split anywhere —
not on the chart page, not as a Charts facet (owner: "I don't think we have accurate enough
data to promise a chart's hold split"). The section states the numbers are estimates: tap
counts come from community simfiles, totals from real plays, the difference is the ticks.

**D13 — piucenter's holds are never read; its taps are.** The simfiles piucenter builds on are
pre-Phoenix, and the XX → Phoenix re-balance moved hold counts broadly (their tick sums
reproduce the game on 89% of PHOENIX-pack charts but only 47–74% of older packs, usually
overcounting — Phoenix trimmed holds). Tap steps are ~98% stable across re-balances, so the
decomposition is **our judged total − their tap rows**, per mix, with sanity gates: implied
ticks below zero, or a no-hold simfile whose total exceeds its taps, mark a re-stepped chart
and drop it from the aggregate. The crawler banks `tap_rows`, `hold_rows` and `hold_ticks`
(their tick sum, kept for diagnostics, never displayed) as three more `ChartSkillMetric` rows
from the per-chart JSON it already fetches; the snapshot import banks them identically.

**D14 — The credits render on the Phoenix page only.** MR_WEQ (the formula) and daryen (the
grade ranges) earned the Phoenix line; Phoenix 2's cutoffs were measured by the site itself.

**D15 — No migration, no new job.** Every aggregate is a cached grouped query: the population
census and judgement spreads are single GROUP-BY-shaped reads over `PhoenixRecord` (cached 6h,
the `EFLedgerStatsRepository` precedent), the hold profile derives from banked metrics + the
catalog (cached 24h), and note counts compute at render from the already-cached chart reads.

## 2. Page anatomy (top to bottom)

1. **Hero** — eyebrow (Scoring / mix · other mix → · PUMBILITY Calculator →), H1, two-sentence
   definition. The score formula is mix-invariant; the letters are not — the lede says so.
2. **The calculator** — six judgement fields + optional calories; score, grade (with the other
   mix's letter when it differs), plate, the two bars (D5), the next-letter walk (D4), arrows
   pressed (D6). Signed in: Load one of your plays (D7).
3. **The formula** — real HTML with the five notes: a miss weighs zero; the machine rounds
   down (±1); combo counts perfects+greats in the longest unbroken run (a good holds it, a bad
   or miss breaks it); hold ticks are judgements; notes normalises chart length. Credits (D14).
4. **What each judgement costs** — per-judgement cost cards at a selectable chart size
   (500/1,000/1,500/2,000), the mid-combo miss caveat, and the greats-only budget table per
   grade — every number derived from the engine at render.
5. **Letter grades** — the mix's ladder with a vs-other-mix column; on Phoenix 2 the
   what-moved strip, on Phoenix a link to it; the plates (mix-invariant, misses not score).
6. **What a grade looks like** (D8).
7. **What personal bests look like** (D9).
8. **How many notes is a level?** (D10).
9. **Half the chart is holds** (D11/D12/D13).

## 3. Data

| Section | Source | Freshness |
|---|---|---|
| Calculator, formula, costs, ladder, plates | `PhoenixLetterGrade` / `PhoenixPlate` / `ScoreScreen` at render | live |
| Grade spreads | `GetJudgementSpreadsQuery` (ScoreLedger) — judged, non-broken bests | 6h cache |
| Population | `GetScorePopulationQuery` (ScoreLedger) — non-broken bests × chart level, Singles+Doubles | 6h cache |
| Note counts | `GetChartsQuery` both mixes (Catalog), P2 nulls coalesced from P1 | the chart cache |
| Hold ticks | `GetHoldTickProfileQuery` (Catalog) — `tap_rows` metrics × note counts | 24h cache |
| My plays | `GetJudgedPlaysQuery` (ScoreLedger) via the signed-in endpoint | live |

Measured facts behind the copy (prod DB, 2026-08-23; re-derivations live in the session record,
not here): P1 levels 10–23 hold 96–99% of bests at ≥900k with medians 960k–985k; the judgement
spreads per 1,000 notes run SSS+ 996.5/2.9/0.3/0.1/0.2 through AA 865/81/23/11/20 and the two
mixes agree within a note; hold-tick share medians ~0% at 1–2, 20–30% at 4–12, 46–51% from 15
up; `tap_rows + piucenter ticks` reproduced the game's count exactly on 2,152 of 4,404 charts
(within 1% on 74%) — and Feel My Happiness D21's staggered brackets land exactly (479 tap rows
+ 531 ticks = 1,010), because a rolling bracket is written one arrow per row and judged per row.

## 4. SEO surface

Per-mix `<title>`/description from `StaticHeadResolver` (`ScoreCalculatorHeadModel`), TechArticle
+ BreadcrumbList JSON-LD (Tools › the page), self-canonical mix URLs, both in the sitemap, the
bare URL canonicalising to the served mix. The E2E fact asserts the formula, the ladder and the
ld+json arrive as raw HTML on both URLs — the guard against the static renderer's script-drop.

## 5. Post-deploy

One action: re-upload `piucenter-snapshot-050726.zip` on /Admin/PiuCenter — the same parser now
banks the three new metrics, so the one upload backfills all aliased charts. Until then the
holds section renders its not-computed-yet state. (The weekly crawl would refill it over time
anyway; the upload is the fast path.) Phoenix 2's hold numbers also thicken on their own as P2
totals refill from play.
