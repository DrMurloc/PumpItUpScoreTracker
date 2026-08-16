# The PUMBILITY Calculator — `/PumbilityCalculator/{mix}`

The rebuild of `/RatingCalculator` (2026-08-15/16). "Rating" is what this site called the number
before Andamiro named it PUMBILITY — the page predates the name — and it was the last page still
wearing the old shape: a `MudTable` of level × grade inside a circuit no crawler ever ran, a colour
ramp from a rank in the table, a ±10 % click-highlight that grabbed forty percent of the Phoenix 2
grid, and a "Max Rating" column left over from before there were PUMBILITY leaderboards at all.

The page's job now: **the site's canonical explanation of how PUMBILITY is calculated** — the
formula in words, every constant, what any grade on any level is worth, and honest answers to the
three questions players actually ask (*should I push levels or scores*, *how much do plates
matter*, *does scoring matter more than it did on Phoenix*). It is written for a search engine and
an LLM as much as for a player: real HTML, one URL per mix, the numbers stated in prose.

Mock (the spec for layout and copy): https://claude.ai/code/artifact/d0fdf81a-c5e4-4c11-8c5a-c0a2a55ddb46
— the Phoenix 2 page with the Phoenix variant behind the mix pill.

---

## 1. Decisions

**D1 — One URL per mix, and the mix is in the path.** `/PumbilityCalculator/phoenix2` and
`/PumbilityCalculator/phoenix` are separate, self-canonical pages (the `/MixChanges/{from}/{to}`
precedent). Bare `/PumbilityCalculator` serves the viewer's mix and canonicalises to it. The old
`/RatingCalculator` 301s (a real MVC 301, `ChartPermalinkController` style, so bookmarks and search
signals consolidate). Both mix URLs are in the sitemap.

**D2 — Static SSR, zero circuits.** The page declares no `@rendermode` (listed in
`RenderModeDeclarationTests.StaticPages`, the `ChartDetails` pattern). Everything a crawler needs is
real HTML generated at render: the formula, the constants, the value tables, the ruler, the
comparison. **Both chart types render into the HTML** on Phoenix 2. The three interactions — the
Singles/Doubles button group, the table's contour click, and the quick calculator — are one vanilla
JS module (`wwwroot/js/pumbility-calculator.js`, served through `@Assets`) working on markup and on
a JSON block of constants the server emits from `ScoringConfiguration`. The script holds no table
of its own, so it cannot drift from the formula; without JS the page still shows both types and
every value, and only the calculator is inert. Rejected: an island for the calculator (the type
toggle would then have to be an island too, and the ruler and table with it — the crawlable content
would move into a circuit), and the type in the URL (`…/phoenix2/doubles` — two near-duplicate URLs
per mix for a page whose reason to exist is search).

**D3 — Every constant on the page comes from `ScoringConfiguration`.** Base per level, grade
multipliers per type, plate bonuses per type, the singles-priced-one-level-up rule, the zero rules
and the score floors are read from `PumbilityScoring(mix, …)` and `GetMinimumScoreFor(mix)` at
render, never typed into markup. A component test asserts every cell of the value table equals
`GetScore` for that level, type and grade floor.

**D4 — The ruler's baseline is a score, 900,000 — not a grade name** (owner correction,
2026-08-15). 900,000 is exactly the AA floor on Phoenix and exactly the A+ floor on Phoenix 2, so it
is a real rung on both mixes and *the same play* on both; anchoring on "AA" would have compared two
different scores. `PumbilityLevelEquivalence.AnchorGrade(mix)` resolves the grade whose floor is
900,000 from the floors table — it is never hardcoded.

**D5 — The ruler axis is level-equivalent: "which level's 900,000 is worth the same".** Each row is
a level; the bar starts at a 900,000 on that level (the anchor tick sits at its own level) and climbs
through the grades; the number at the end is *levels bought*. `EquivalentLevel` inverts the
anchor-grade value curve for the chart type by piecewise-linear interpolation — no closed forms, so
it survives any base table (Phoenix's quadratic, Phoenix 2's kink at 24 and singles priced a level
up all fall out of the interpolation). Bands are coloured by grade metal (copper AA, silver AAA,
gold S, ice SSS — `MixThemes.GradeVar`), and **the legend names a band by the grades it covers**:
the first solid band starts at the pass line, so on Phoenix 2 it reads A+ · AA · AA+ and on Phoenix
AA · AA+; the two grades below the line draw as one faded copper tail.

**D6 — "How much does scoring matter" is the ratio, not the grade span** (owner correction,
2026-08-15). The grade span alone (+50 % on Phoenix, +11 % on Phoenix 2) says the opposite of the
truth. A grade is only worth something next to what a level is worth, and the level shrank harder:
the comparison section states three things in order — ① 900,000 → SSS+ on one chart (+50 % vs
+11 %), ② one level higher on one chart (+17 % vs +2.2 % at 20), ③ so scoring one chart is worth
**2.7 levels on Phoenix and ~4.6 on Phoenix 2** — with ③ as the hero. The concrete inversion that
reads best: passing one level higher took a 900,000 → S (970,000) to match on Phoenix; on Phoenix 2
it takes AA (920,000) singles / AA+ (940,000) doubles. Above 24 the base steps 10 a level and the gap
closes — which is where scoring stops being the cheap lever.

**D7 — The table's click is a contour, not a band.** Clicking a value lights the *closest* cell on
every other row and dims rows that cannot reach it; the caption names the equivalents. One cell per
row by construction on both mixes — the ±10 % band was fine on Phoenix (values span 75×) and useless
on Phoenix 2 (2.5×). Cell colour is the level-equivalent (`pc-ramp-0…9`, `color-mix` on
`--mix-primary`), so the diagonal bands *are* the equal-value lines and the same colour means the
same thing on both mixes.

**D8 — Score floors ride the column headers** (`AA · 920k`); Phoenix 2 moved A/A+/AA/AA+ to
800/900/920/940k and the shift gets one line in the comparison (a 905,000 was an AA ×1.00 on Phoenix
and is an A+ ×1.33 here). Light touch — `/PhoenixCalculator` is still the score page.

**D9 — Population by rung, one average bar, ≥5 players.** "Should I push levels or push scores?"
draws the pool split **once**, as the average over every full pool on the mix (Level | Score | Plate,
true to scale, measured from ×1.00 like `/Pumbility/Pool` — D16 of the pumbility overhaul, so the two
pages agree), with the rung-to-rung movement in one sentence. The per-rung bar table was drawn and
cut: on Phoenix 2 every rung's split is 68/31/0.4 and the bars were identical. Bands: Phoenix 2 by
total-PUMBILITY title rung (BRONZE 10k … ABYSS 20k, `Phoenix2PumbilityLevel`), Phoenix by eight uneven
totals (<20k, 20–30k … 70–80k, 80k+ — the shift the data shows is at 58–64k). Only full 50-chart
pools count (`PumbilityCohortKeys.PoolSize`, the tier-list gate); a band under five players prints
"not enough players yet"; the source is named on the page ("players tracked on PIU Scores").

**D10 — Merged pools for the population, own-type pools for the tier lists.** The title rungs and the
number a player sees are the merged Singles+Doubles top-50, so the composition sweeps the merged pool;
the tier-list sweep keeps its own-type pools. Both are built from one read of the scores.

**D11 — The quick calculator is level · grade · plate → value.** No signed-in "adds to your pool"
half: *that is what the PUMBILITY page is* (owner). D18 of the pumbility overhaul handed the what-if
here; it lands as this, without the pool read.

**D12 — What the page does not say.** No "all data validated" prose (defensive writing; the site
scrapes thousands of accounts against the official page). One footnote only: no pool on PIU Scores
has held a Phoenix 2 level 28 or 29 yet, so Base(28)/Base(29) follow the curve rather than a reading.
No "we did it first" line either (owner: "we're not putting that on the page lol"). The site never
says "we".

**D13 — The Phoenix table keeps its CO-OP row** (flat 2,000 base) with the note that CO-OP never
counted in the total; the official Phoenix formula priced it, and Phoenix had the page that showed
it. "Max Rating" (chart count × SSS+) is gone — a leaderboard-era leftover meaningless against a
fifty-chart pool. Chart counts per level stay.

**D14 — The section headings are the questions.** "Should I push levels or push scores?", "How much do
plates matter?", "Does scoring matter more than it did on Phoenix?" — literal H2s with the measured
answers under them. No FAQ schema (the mix-diff precedent); `TechArticle` + `BreadcrumbList` JSON-LD,
title and a stat-loaded description from `StaticHeadResolver`.

## 2. Page anatomy (top to bottom)

1. **Hero** — eyebrow with the other mix's link · H1 · two-sentence definition (fifty highest-valued
   charts; Phoenix 2 keeps three pools) · the Singles/Doubles button group (Phoenix 2 only; governs
   the whole page).
2. **The formula** — `Base(level) × (grade + plate)` / `Base(level) × grade` large, the three terms
   as coloured cells (level primary, grade secondary, plate accent — the pool page's split colours),
   the zero rules, four worked examples.
3. **What scoring buys, in levels** — the ruler (D4/D5) with its legend and a footnote carrying the
   numbers.
4. **Every level, every grade** — the value table (D7/D8), chart counts, the contour caption, the
   ramp key, the 28/29 footnote (P2) / CO-OP note (P1).
5. **Quick calculator** (D11) — level · grade · plate → exact value, the arithmetic, "worth about the
   same as" on the levels around it.
6. **The constants** — the grade ladder (Double row + Single overrides + floors) and the plate
   bonuses (Double + Single overrides).
7. **Q · Should I push levels or push scores?** (D9) — the average split card, the rung sentence,
   the answer.
8. **Q · How much do plates matter?** — the magnified plate rail (RG on all → PG on all) and the
   answer; on Phoenix, "Nothing." in a sentence.
9. **Q · Does scoring matter more than it did on Phoenix?** (D6) — paired bars per level, the three
   fact cards with ③ as the hero, the paragraph.

Mobile: the table scrolls inside its own container; the ruler is fluid (percent-positioned divs);
everything else stacks.

## 3. Code

| Piece | Where |
|---|---|
| `PumbilityLevelEquivalence` — `AnchorGrade(mix)`, `EquivalentLevel(config, type, value)`, `LevelsBought(config, type, level, grade)` | `ScoreTracker.SharedKernel/Models/` beside `ScoringConfiguration` (pure) |
| `PumbilityPoolBands` (band policy per mix, the ≥5 gate), `PumbilityPoolComposition` (model) | `ScoreTracker.ChartIntelligence/Domain/` |
| The sweep side output — merged pools, `Decompose` per pooled chart, 16-grade histogram, aggregated per band | `TierListSaga.Consume(ProcessPumbilityTierListCommand)` |
| `PumbilityPoolCompositionEntity` → `scores.PumbilityPoolComposition`; `IPumbilityPoolCompositionRepository` + `EFPumbilityPoolCompositionRepository` | `ScoreTracker.ChartIntelligence/Infrastructure/` (+ the model contribution) |
| `GetPumbilityPoolCompositionQuery(mix)` → `PumbilityPoolCompositionRecord?` (1 h cache, 1 min when empty) | `ScoreTracker.ChartIntelligence/Contracts/Queries/` + `Application/` |
| `Pages/Tools/PumbilityCalculator.razor` (static) + `Components/PumbilityCalculator/*` (static section components) | `ScoreTracker.Web` |
| `wwwroot/js/pumbility-calculator.js` (toggle · contour · calculator) | `ScoreTracker.Web` |
| `StaticHeadResolver` entry, `SitemapController` URLs, the `/RatingCalculator` 301 | `ScoreTracker.Web` |

Nothing lands in `ScoreTracker.Application` or PlayerProgress; Catalog's `GetChartsQuery` supplies
the chart counts unchanged.

## 4. Tests

- **DomainTests** — `PumbilityLevelEquivalenceTests`: the anchor resolves to AA on Phoenix and A+ on
  Phoenix 2 from the floors; `EquivalentLevel` returns the level itself for the anchor grade at every
  level and type on both mixes; monotone in value; a Single is priced a level up and still equals its
  own level at the anchor; goldens for the headline numbers (Phoenix 20 → +2.7 · Phoenix 2 D20 →
  +4.6 · S20 → +4.5) and the one-level inversion (S on Phoenix, AA/AA+ on Phoenix 2).
- **ApplicationTests** — `PumbilityPoolBandsTests`; `TierListSagaPumbilityTests` gains the
  composition: merged pool, gate, per-band sums equal the `Decompose` totals, histogram; the query
  handler's cache behaviour.
- **Tests.Components** — the page renders both mixes as static markup; every value cell equals
  `GetScore`; the CO-OP row only on Phoenix, the 28/29 footnote only on Phoenix 2; both type blocks
  present; the emitted constants JSON equals the configuration; the ruler's end labels and legend
  band names; the comparison labels; the population card and its empty state; `StaticHeadResolver`
  entries; the nav label.
- **Tests.Integration** — the composition repository round-trips; the sweep writes bands from
  seeded pools against a real database.
- **Tests.E2E** — `/PumbilityCalculator/phoenix2` serves the formula line, both value tables and
  `ld+json` in the raw HTML (the static renderer once dropped an expression-bodied `<script>`
  silently — seo-friendly-site.md; a regression here is invisible in a browser and fatal to the
  page's purpose).

## 5. The numbers (local prod-synced database, full pools only, 2026-08-15)

Formula facts, 900,000 anchor: levels an SSS+ buys — Phoenix 2.1 @16 · 2.7 @20 · 3.5 @24 (grows,
quadratic base); Phoenix 2 Doubles 4.7 @16 · 4.6 @20 · 2.8 @24, Singles 5.5 @16 · 4.5 @20 · 3.3 @24
(narrows above 24 where the base steps 10 not 5). Both curves are non-monotonic at their ends and it
is real: Phoenix 2 wobbles at 22–24 (the kink, plus singles priced a level up), Phoenix turns back up
below level 13 (the quadratic flattens near 10). Plates: PG-on-all over RG-on-all ≈ 1.4 % of a
Phoenix 2 pool ≈ a third of a level at 24; on Phoenix exactly nothing.

Population: **Phoenix** (1,369 full pools) — the grade multiplier sits flat at 1.13–1.16 (score
11–13 % of the number from ×1.00 = AA) from under 20,000 through the 50,000s while the pool's average
level climbs 14.4 → 22.5; above ~60,000 the level stalls at 23–25 and the score share climbs to 27 %
(S-or-better 16 → 47 of 50). Levels until your pool averages about 24, then scores. **Phoenix 2**
(67 full pools; GOLD 8 · PLATINUM 19 · DIAMOND 26 · RED BERYL 13) — grade multiplier 1.46–1.47 at
every rung (S-or-better 36–44 of 50), average level 15.9 → 23.0, split ~68/31/0.4 everywhere; pools
scored near the ceiling from the first rung, the climb is level. The two halves reconcile: scoring is
the cheap lever on Phoenix 2 (§D6), everyone has already taken it, so what is left to climb is level.

## 6. Post-deploy

One press of "Rebuild {mix} PUMBILITY tier lists" per mix on `/Admin` — the same button as before;
the sweep now also writes the composition rows the population section reads.
