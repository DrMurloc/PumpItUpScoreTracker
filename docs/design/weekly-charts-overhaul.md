# Weekly Charts overhaul — the challenges hub, and a static page

**Status**: **rail rewrite in progress** — the 2026-07-18 field test rejected round 9's
interaction model and layout, and rounds 10–12 (same artifact, labels `round-10-rail` …
`round-12-locked`) locked a redesign: leaderboards move to a left rail, every view swap is
instant, and per-chart boards gain a persisted relevant-players filter. §12 is the rewrite —
its calls, its data, and its commit plan. Sections 1–11 remain the round-9 as-built record;
where §12 supersedes one, it says so. The round-9 build (all ten commits, §9) shipped to the
branch 2026-07-15 and was reconciled to the static router 2026-07-16 after the app-wide flip
(seo-friendly-site.md §7 PR-3) merged — §3.1 records what replaced the branch's own mechanism.
The E2E suite now runs locally (the Docker blocker was environmental) and is green as of the
third main-merge (`f5c8e91e`).

**The page**: `/WeeklyCharts` becomes the challenges hub — Weekly Charts and Daily Step
([daily-step.md](daily-step.md)) on one page, statically rendered so crawlers finally see the
concept, with islands only where a circuit earns its keep. daily-step.md L6 reserved the
per-user history view "for a future view on the Weekly-charts rebuild"; this is that rebuild.

---

## 1. Owner calls

Scope calls (2026-07-15, pre-mock):

| # | Call |
|---|---|
| O1 | **Static SSR + islands here too**, independent of the chart-details pilot — neither blocks the other. |
| O2 | Route and name stay **`/WeeklyCharts`**. Daily Step integrates into the page. |
| O3 | Monthly leaderboard **stays mechanically the same** (best-N-per-window). The **BITE relic drops**. Aggregation moves out of the page into the vertical. |
| O4 | **Scoring: the game's own PUMBILITY replaces the homebrew PUMBILITY+** — per mix, through `ScoringConfiguration.PumbilityScoring(mix, includeCoOp)`. Consequences in §6. |
| O5 | **Per-user Daily Step history ships** on this page. |

Mock-round calls (R1–R9, all locked):

| # | Round | Call |
|---|---|---|
| M1 | R2 | **The fold answers "what's up for competition now."** Daily Step is a slim **strip**, not a hero; the concept paragraph is deleted from the page — it lives in the **meta description** only. Weekly cards compress until the whole week fits ~3 rows at desktop. |
| M2 | R4 | The record dialog speaks **Quick Record's vocabulary** (§8): live derived grade, plate dropdown, prefill from your current entry, in-place "Recorded" flash. |
| M3 | R5 | **Photos are optional.** Disclaimers: a photo is your proof if a score's legitimacy is disputed; suspected cheaters will be *required* to attach photos for future competition entries (the enforcement mechanism itself is future scope — the words set the policy now). |
| M4 | R5 | **No broken concept in the record dialogs.** Manual competitive entries are **score + plate, period** — always a pass. Plated brokens matter for personal recording (Quick Record → ledger), not competition; real brokens ride the official import. |
| M5 | R6 | **Trust ladder on board rows**: ✔ officially imported > 📷 photo attached (opens the proof) > **blank** for bare self-reports (no "unverified" text — R8). Footer legend: "✔ imported · 📷 photo proof". |
| M6 | R7 | **Boards show every entry.** The `MaxPlaces` cap dies in the shared `LeaderboardDialog`, so the home-page widgets inherit full boards in the same commit; the dialog scrolls; pagination only if a board ever outgrows a scroll (not expected under ~50). |
| M7 | R7 | Unplayed cards show a **dim "—"** (hover title "Not played yet"), never words. **Suggested-only stays the default** for calibrated players, `?suggested=all` the escape. |
| M8 | R8 | **Grade glues to the score** — one right-aligned pair per row, never orphaned next to the name. |
| M9 | R8 | **Chart identity opens the shared `ChartDetailsDialog`** (video included) from every density and the Daily strip. Anchors keep real `/Chart/{id}` hrefs for the crawl mesh; the island upgrades the click. |
| M10 | R8 | **Density switcher is on-page UI** — the Tier Lists treatment (three small icon buttons, active = primary), right-aligned above the weekly grid. |
| M11 | R9 | **No "Suggested" chips on cards** — the gold border is the only on-card signal; the filter note above the grid does the explaining. |
| M12 | R9 | **Compact keeps one action**: a bottom-right **entry-count chip** (tier-list corner-chip vocabulary) opens the leaderboard. Record has no Compact affordance by design — Compact is a scanning mode. |

Rail-rewrite calls (R10–R12, 2026-07-18, all locked — the field-test verdict on round 9):

| # | Round | Call |
|---|---|---|
| M13 | R10 | **"Static really should mean static on load, not requiring a page load to change anything on the screen."** The whole page ships in the first response; after that, every view swap is an instant client-side toggle. URL state survives for sharing/crawlers via `history.replaceState`, never as the mechanism. |
| M14 | R10 | **The rail layout**: Daily Step (top 5 + pinned you) and the Monthly Leaderboard (top 20) as homepage-widget-chrome cards in a 330px left rail; the week's grid takes the rest. Trophy on each rail card opens the full board dialog. |
| M15 | R10 | **Real icons, ubiquitous**: Record = `AddCircleOutline`, boards = `EmojiEvents` — the homepage Daily Step widget's pair — on weekly cards **and** the Daily card. The `▲`/`☰` glyphs die. |
| M16 | R10 | **Monthly rows carry the player's avatar and competitive level** (N2), in the rail and the dialog. |
| M17 | R11 | **Top-3 in Comfortable** (your row folds in when you rank ≤3); Table stays top-1 + you; Compact unchanged. |
| M18 | R11 | **Type switching is the tier-list segment row** (FolderGrid's `MudButtonGroup` vocabulary: joined buttons, active = filled primary) — not pills, not a select. |
| M19 | R11 | **Table density renders jacket + bubble side by side** (the tier-table pattern), never the overlay. |
| M20 | R12 | **"Relevant players"**: a quiet persisted switch hiding out-of-band players on **weekly per-chart boards only** — monthly is exempt ("it highlights people pushing their highest, not dominating low boards"), daily is exempt. Ranks renumber after the filter. Default off. Page-level beside density, mirrored in the weekly board dialog. |
| M21 | R12 | **The monthly counted-scores expansion goes compact**: the official-leaderboards pattern (TierListChartCard Compact stickers — jacket + bubble + grade/score + points), **no song names**; names live in the tooltip. |
| M22 | R11 | **The collapse ladder**: ≥~1080px rail-left; ~640–1080px (tablet portrait, 1:1 windows) the rail cards go 2-up above the grid; <~640px single column **Daily → This Week → Monthly**. Two media queries, no JS. |

## 2. Sins this pays down

The 2026-07 audit of the old page against [UX-GUIDELINES.md](../UX-GUIDELINES.md), kept as the
acceptance list:

| Rule | Today | After |
|---|---|---|
| 1 — answer above the fold | Filter furniture renders before the first chart; no "your week" anywhere | One header row (title · your chips · pickers), the Daily strip, and the whole week's grid land in the first viewport (M1) |
| 3 — one concept, one component | Hand-rolled per-chart leaderboard dialog beside the shared `LeaderboardDialog`; hand-rolled grade `MudImage`s off a hardcoded CDN; avatar `MudImage`s beside `UserLabel` | Shared `LeaderboardDialog`, `ChartDetailsDialog`, `LetterGradeIcon`, `UserLabel`, `ScoreBreakdown` everywhere |
| 5 — density | None — one fixed card grid | `Density__WeeklyCharts`: Comfortable / Compact / Table, switcher above the grid (M10/M12) |
| 6 — filters are furniture | Selects strewn above content; "Leaderboard Type" floats mid-page; week selection is circuit state | Week + type are **URL state** (crawlable links); communities a compact disclosure form; type pills live in the monthly section header |
| 7 — +40% text | ~10 raw-English strings; a dead localized key with 2024 dates baked in | Every string through `L[…]`, ×9 locales, in the same commit each key lands |
| 9 — loading looks like the layout | Monolithic sequential `OnInitializedAsync` (N+1 per community and per week); blank until the last query; no empty states | Static core arrives *with the document*; islands have fixed footprints; every section has a named empty state |
| 10 — thumbs first | No dock; admin button parked after four `<br/>`s | Page dock (mobile) with section jumps + reset countdown; admin action moves into the dialog host |

Non-UX repairs riding along: `@inject IUserRepository` leaves the page (§7); dead compute
deleted (`_userCharts`/`_userTopFour*`/`_userTotalPlace` computed and never rendered,
`_countryFlags` never read); the 10 MB limit that reports "20MB"; five raw `DateTimeOffset.Now`
calls; the stray `@if` inside a C# method; zero test coverage.

**Untouched**: `api/weeklyCharts` (contract-pinned by `Tests.Api` — see the §7 caution), the
Discord card path (`GetUserWeeklyPlacementsQuery`), the rotation sagas and their cron slots.

## 3. The render split

### 3.1 Mechanism — superseded by the flip (SSR ladder PR-3)

This branch originally landed a per-page opt-out (`[ExcludeFromInteractiveRouting]` + a
conditional `PageRenderMode` in `App.razor`). The app-wide flip merged first
(`seo-friendly-site.md` §7 PR-3): the router renders statically and every interactive page
declares `@rendermode RenderModes.Interactive`, so a page converts to static SSR by *omitting*
the declaration. Reconciled at the 2026-07-16 main-merge:

- `WeeklyCharts.razor` carries no `@rendermode` and is the first entry in
  `RenderModeDeclarationTests.StaticPages` (the ratchet that makes static-by-omission a
  deliberate act, never an accident).
- The opt-out attribute, the `App.razor` conditional, and `StaticPageLayout` are gone —
  MainLayout renders statically around every page since the flip, so static pages use it
  like everyone else.

### 3.2 The static core

Everything you can *read* renders statically, anonymous and signed-in alike: the Daily strip
and its standing, the weekly grid (all densities are server-rendered variants of the same
rows), your per-chart lines, the monthly table with `<details>` row expansion, the daily
history, the scoring legend, every empty state, and the not-yet-featured pool behind
`?pool=1`. Static display vocabulary per static-shell.md: `SongImage`, `DifficultyBubble`,
`LetterGradeIcon`, `ScoreBreakdown`, `UserLabel`; `--mix-*` tokens only; no Mud popover
dependencies; numbers always printed.

### 3.3 One island: the dialog host

`ChallengeDialogHost` — one `@rendermode Interactive` root hosting **three dialogs plus the
admin action**: **Record** (§8), the shared **`LeaderboardDialog`** (§5), the shared
**`ChartDetailsDialog`** (M9), and the admin rotate confirm (publishes
`RotateWeeklyChartsCommand`). Static elements carry `data-challenge-action` +
`data-chart-id`; `wwwroot/js/challenge-board.js` registers one delegated click listener and
forwards to the host's `DotNetObjectReference`. Chart-identity anchors keep their real
`/Chart/{id}` hrefs — crawlers follow them (the internal-link mesh), the listener
`preventDefault`s and opens the dialog for humans. The host self-loads dialog data on demand,
keyed by primitive ids (the chart-details island grammar). Mud popovers work because
`MudProviders` mounts ahead of every island (the Stage-1 cross-root proof).

### 3.4 Layout, head, navigation

- **Layout: MainLayout** (post-flip it renders statically around every page; the branch's
  interim `StaticPageLayout` was deleted at the reconciliation). The dock renders as plain
  markup from the page; `challenge-board.js` calls `shell.setDockState(true, false)` on load.
- Mix resolution: request-side via `IUiSettingsAccessor`; **any mix without a weekly board
  falls back to Phoenix** (`mix is not (Phoenix or Phoenix2) → Phoenix`).
- Head: served by **`StaticHeadResolver`'s `/WeeklyCharts` branch** — a static page's
  `PageTitle`/`HeadContent` would render into a HeadOutlet island it never reaches. Title,
  meta description **carrying the concept copy the fold no longer holds** (M1), OG tags (the
  daily jacket, falling back to the week's first), and canonical `/WeeklyCharts` via
  `StaticHeadModel.Canonical` (filter/week variants fold into clean). The JSON-LD `ItemList`
  renders in the **body** (valid placement; the body is what a static page owns). Sitemap
  entry ships in `SitemapController`.
- Navigation: links to the page full-load (the attribute's contract); links out are plain
  anchors. Enhanced nav stays off app-wide.

### 3.5 Explicitly not here

Output caching / CDN (the D18 platform fights — chart-details P3 owns the groundwork); other
pages' render modes; the cheater photo-enforcement mechanism (M3 — needs a per-user flag and
an admin surface; the disclaimer ships now, the lever when first needed).

## 4. Anatomy (R9)

Sections carry anchors (`#daily`, `#weekly`, `#monthly`, `#history`); the mobile dock is static
markup with jump links + the next-reset countdown.

1. **Header row** — h1, the "your week" chips (`Played 4/18` · `3 suggested left` ·
   `Monthly #7`), and right-aligned pickers: **Week** (a disclosure of past-week **links** —
   `?week=…`) and **Communities** (checkbox disclosure, GET form, persisted). No prose.
2. **Daily Step strip** (`#daily`) — one slim bar: jacket thumb (chart-details on click),
   "Daily Step" kicker + chart name + bubble, the top-3 inline, your standing chip, count +
   "resets in …", Record + Board buttons. Limbo Day = same bar, secondary-color edge, a
   "Limbo — lowest pass wins" chip, ascending board. Empty state: "Today's Step posts at
   midnight." (existing key).
3. **Weekly grid** (`#weekly`) — grid bar: heading + count/rotation sub + suggested filter note
   ("showing suggested — show all N") + the **density switcher** right-aligned (M10).
   Card (Comfortable): jacket + bubble (chart-details on click), name, **top-1 line** and
   **your line** (dim "—" when unplayed), footer count + ▲ Record + ☰ Board. Compact: jacket
   sticker + count-chip → board (M12). Table: rows with name, top-1, your line, count, actions.
   Suggested = gold border only (M11). Empty state: "This week's charts post Monday at
   midnight ET."
4. **Monthly board** (`#monthly`) — type pills in the section header (`?type=` links), window
   subtitle (week N, best M count, PUMBILITY), the table: rarity place, player, top-4 chips,
   count (a `<details>` expansion with all counted scores), total. Empty state: "Scores land
   here as boards close."
5. **Your Daily Step history** (`#history`, signed-in) — last 14 days: date, chart chip
   (chart-details on click), Limbo tag, place/total, grade + score pair. Empty state names the
   action. Anonymous visitors get a sign-in CTA card instead.
6. **Scoring legend** — rendered from the active mix's `ScoringConfiguration` (never
   hand-copied): grade multipliers, Phoenix 2's additive plate bonuses, and the rules
   sentences (§6).
7. **The pool** (`?pool=1`) — the not-yet-featured chart list, server-rendered on its own URL.
8. **Admin** — the rotate trigger via the dialog host's confirm; a quiet card at the bottom,
   admin-only.

## 5. The shared LeaderboardDialog (changes land once, all consumers inherit)

[LeaderboardDialog](../../ScoreTracker/ScoreTracker/Components/LeaderboardDialog.razor) already
serves the Daily/Weekly widgets and UCS leaderboards. This overhaul changes it for everyone:

- **The `MaxPlaces` cap dies** (M6): every entry renders; the content area scrolls; your row
  glows in place. (Pagination is a future lever, not built now.)
- **Trust ladder** (M5): ✔ imported · 📷 photo (click opens the proof photo) · blank.
  Weekly rows can carry the tags once §7's `Source` column exists; daily rows already can —
  but daily has no photo intake, so its ladder is ✔/blank in v1 (the mock's daily 📷 rows were
  illustrative; giving Daily an optional photo is a trim-able follow-up, not in scope).
- Row layout keeps grade+score+plate as the glued right group (M8 — `ScoreBreakdown` already
  does this; the page's own compact rows follow the same rule).

## 6. Scoring — PUMBILITY per mix (O4)

`ScoringConfiguration.PumbilityScoring(mix, includeCoOp: false)` prices per-chart points and
monthly totals (the co-op flag is irrelevant here — see below). Consequences, all the game
formulas' own rules:

- **Phoenix board** → Phoenix PUMBILITY; **Phoenix 2 board** → Phoenix 2 PUMBILITY
  (`GradePlusPlate`, additive plate bonus, verified grade table).
- **Broken plays score 0** toward totals (`StageBreakModifier = 0`). They still appear on
  per-chart boards, ranked by the existing policies. (Under PUMBILITY+ a broken AAA earned
  full points — that quirk dies.)
- **Co-op is never PUMBILITY-priced on this page**: Combined excludes co-op (now Phoenix 2's
  own rule rather than convention), and the **Co-Op view ranks raw score sum** — the only
  currency co-op charts share.
- **Ties** break by total raw score (stepped grades tie more than the old continuous scale).
- The legend renders from the config so the page can never drift from the engine. UI says
  **PUMBILITY** everywhere PUMBILITY+ appeared.

## 7. Contracts and data ✅ queries landed, Source pending

Landed (C1): `GetWeeklyBoardQuery`, `GetMonthlyLeaderboardQuery`, `GetDailyStepBoardQuery`,
`GetUserDailyStepHistoryQuery` — display-enriched via `IUserReader` (the page drops
`IUserRepository`), priced per §6, one batched history read (the per-week N+1 died), handlers
on the existing sagas with component tests.

Still to land (the trust ladder's data):

- **`Source` column on the weekly entries table** (`WeeklyUserEntry` entity): `Official` |
  `Manual`, migration defaults existing rows to `Official` (historically imports dominate and
  photos were mandatory for the manual path). Write paths: the import consumer stamps
  Official; `RegisterWeeklyChartScoreCommand` stamps Manual.
- **⚠ The API golden caution**: `api/weeklyCharts` serializes weekly entries and is pinned by
  `Tests.Api`. The shared `WeeklyTournamentEntry` record does **not** grow a Source property —
  the entity column surfaces only through `WeeklyBoardRow` (which gains `Source` + the photo
  URL for the 📷 tier). Run the API goldens in the same commit as the migration to prove the
  wire shape never moved.
- The 📷 tier derives from the entry's existing nullable `PhotoUrl` — no third flag.
- `RegisterWeeklyChartScoreCommand` semantics per M3/M4: photo optional (already nullable),
  `IsBroken` always false from the dialog.

## 8. The record dialog (Quick Record vocabulary, M2–M4)

One dialog, two modes, hosted by the island:

- **Fields**: score (live derived grade beside it, display-only) + plate dropdown
  (shorthands; empty = no plate). **No broken control of any kind.**
- **Prefill from your current entry** — an edit, not a blank. A fresh photo is never
  prefilled: it proves *this* score.
- **Weekly adds the optional photo block**: add → uploading (progress) → done (thumb +
  Replace/Remove), captioned "Optional — a photo is your proof if a score's legitimacy is ever
  disputed," footnoted "Suspected cheaters will be required to attach photos for future
  competition entries."
- **Daily**: score + plate only (no photo, always a pass — `RecordDailyStepScoreCommand`
  unchanged).
- **Submit** enables on score alone → in-place **"Recorded"** flash (check, chart, grade,
  score), auto-dismiss ~1.4s, no snackbar.

## 9. Commit order

l10n keys land ×9 locales **within each commit** (house rule). ✅ = already in on this branch.

| # | SHA | Content |
|---|---|---|
| ✅ 0 | `53d1cb2d` | **Design doc v1** (this file's first cut) |
| ✅ 1 | `a6048ee8` | **Vertical read model**: the four §7 queries + repo methods + saga handlers + PUMBILITY pricing + 17 component tests. Fast suites 1309/60/102 |
| ✅ 2 | `0573d0e3` | **Static-page mechanism**: App.razor conditional `PageRenderMode` + fallback-title guard + `StaticPageLayout`. Mechanism-only; shared with the chart-details pilot |
| ✅ — | `ec21c8a8` | **Design doc v2**: mock rounds R1–R9 folded in; commit order re-cut |
| ✅ 3 | `9b20a78e` | **Trust source schema**: `ChallengeEntrySource` (Daily's enum promoted to `Domain.Records`) + `Source` on `WeeklyUserEntry` + migration (existing rows → Official) + DATABASE-SCHEMA row; import stamps Official, manual command defaults Manual; `WeeklyBoardRow` gains `Source`; `GetEntriesWithSources` port method; **API goldens ran in-commit** (§7 caution) — wire shape unmoved |
| ✅ 4 | `5eafcce4` | **Shared LeaderboardDialog** (§5): cap removed (full board, scrolling), trust tags ✔/📷/blank with photo-open; Daily widget drops `ManualUserIds`; 5 bUnit facts |
| ✅ 5 | `f2bb0a01` | **The static core**: WeeklyCharts rebuilt to the R9 anatomy (§4) on `[ExcludeFromInteractiveRouting]` + `StaticPageLayout` + six `Components/Challenges/` display components; URL grammar, PUMBILITY display, dead-code deletion, 45 l10n keys ×9. Read-complete, act-light |
| ✅ 6 | `a1ab2758` | **The island**: `ChallengeDialogHost` (Record §8 + LeaderboardDialog + ChartDetailsDialog + admin rotate) + `challenge-board.js` (delegated bridge, href-upgrade clicks, dock state) + `GetWeeklyChartBoardQuery`; 18 l10n keys ×9 |
| ✅ 7 | `9b6d7119` | **Presentation prefs**: density swap live + `PreferencesController` (`POST /Preferences/Set`, allowlisted keys, `[IgnoreAntiforgeryToken]`); suggested toggle is the server-rendered `?suggested=all` links |
| ✅ 8 | `99ba1cf4` | **Head/SEO**: PageTitle/HeadContent (meta description carries the concept copy + count), OG, JSON-LD ItemList (default encoder — no `<script>` breakout), canonical, sitemap entry |
| ✅ 9 | `8877bc87` | **Tests as facts**: 9 bUnit section facts; E2E `WeeklyChartsTests` (anon raw-HTML fact + signed-in record round-trip) + `SeedWeeklyChart/Entry` helpers. ⚠ E2E written but not locally executed (Docker pipe unreachable from the test host) |
| ✅ 10 | *(this)* | **Doc sync**: UX-GUIDELINES §5 + LeaderboardDialog note, daily-step.md L6 flipped to shipped, this as-built stamp |

Countdown-tick polish for the dock (design §3.3's "countdown tick") was dropped from #6 to avoid
duplicating the localized reset text in JS — the server-rendered "resets in …" is the value at
load. Noted in §11.

## 10. Verification

- Fast suites green per commit; Integration/E2E at #9.
- The SEO fact: anonymous GET of `/WeeklyCharts` contains chart names, `<title>`, the meta
  description, and the JSON-LD block — asserted in E2E, eyeballed via Aspire before merge.
- Localization: every new key present in all nine resx files.
- `UiColorTokenTests` allowlist only shrinks. API goldens prove `api/weeklyCharts` never moved.

## 11. Open (deliberately parked)

- **Daily photos**: the daily record has no photo intake, so daily boards cap at ✔/blank.
  Adding the same optional photo block to Daily is cheap if wanted later.
- **Record from Compact**: none by design (M12); revisit only if players ask.
- **Photo-required enforcement for suspected cheaters** (M3): per-user flag + admin surface,
  future scope; the dialog's disclaimer already states the policy.
- **Board pagination**: only if a board outgrows a scrollable dialog (~50+ sustained).
- The `<details>`-heavy static grammar (monthly expansion, pickers) gets its first phone-width
  field test after commit #5.
- Chart-details convergence: resolved by the flip (SSR ladder PR-3) — the app-wide static
  router superseded both branches' per-page mechanisms; this branch reconciled at the
  2026-07-16 main-merge (§3.1).
- **E2E not locally validated**: the suite couldn't reach Docker from the test host in the build
  environment, so the static page's runtime render (and the island round-trip) rest on CI's Linux
  Docker run + manual QA. First manual pass should confirm: anon `/WeeklyCharts` shows the week and
  the `<script type="application/ld+json">`; the Record button opens the island dialog and a submit
  reloads with your row; density buttons swap + persist; the mobile dock appears.
- **Dock countdown is static** (server value at load), not ticking — dropped to avoid duplicating
  the localized reset string in JS. Revisit if a live countdown is wanted (needs a format the
  client can localize).

---

## 12. The rail rewrite (rounds 10–12) — as-built 2026-07-18

All ten commits (C0–C9, §12.6) landed on the branch the day the plan was cut; suites green
throughout, the three weekly E2E facts — including the relevant-players loop end to end — run
locally. Two finds along the way: the `WasWithinRange` write path had been dead since the
vertical extraction (§12.3, fixed; owner-optional backfill in Downloads), and round 9's
Compact/Table density selectors never matched (ancestor form against the grid's own
attribute — fixed in the C5 rework).

The field test rejected two things about round 9: URL-state view switching (every toggle was a
document load — M13) and the layout (boards buried below the grid, squished cards, glyph
buttons). The round-12 deck is the acceptance spec; M13–M22 are the calls. What follows is the
technical delta.

### 12.1 Layout

`WeeklyCharts.razor` renders header row → a CSS grid `330px 1fr`: the **rail** (Daily Step
card, Monthly card) and the **week's grid**. The collapse ladder (M22) is two media queries.
The static core now ships **everything**: all 36 cards (non-suggested present but hidden), all
four monthly boards (top 20 each, inactive hidden), daily top-5 + pinned you. Below-fold
sections (history, legend, pool, admin) are unchanged.

### 12.2 The rail cards

- **`DailyStepRailCard`** (replaces `DailyStepStrip`): widget chrome (`dash-widget`
  vocabulary), kicker + ⊕/🏆 icon buttons in the head, jacket + bubble + name + "resets in",
  top-5 rows (place colored by the rarity ramp, avatar, name, grade+score), pinned "your
  standing" when you sit past 5, Limbo = secondary edge + chip on the card.
- **`MonthlyRailCard`** (replaces the on-page `MonthlyLeaderboard` section): segment row
  (M18), window subline, top-20 rows — place · avatar · name · CL chip · PUMBILITY total —
  you-glow, pinned-below-20, "Full board" trophy. All four type boards render; the segment
  row swaps them client-side.

### 12.3 Relevant players (M20) — the data

The band is the suggestion band: `floor(competitive level) ∈ [level−1, level+2]`, co-op always
in — extracted to `WeeklyChartSuggestionPolicy.IsWithinRange` so the two consumers
(suggestions, this filter) can never fork.

**Found during planning: the `WasWithinRange` write path has been dead since the vertical
extraction** — `EFWeeklyTourneyRepository.SaveEntry` never sets the column, so current-era
entries store `false`, which also degrades the recap's in-range pooling
(`WeeklyRecapCalculator`). Because of that:

- **Boards derive in-range at read time** from the entry's stored `CompetitiveLevel` via the
  policy — correct for all history, immune to the dead column.
- **The write path is fixed anyway** (`SaveEntry` gains the flag; both writers stamp it) so
  rotation snapshots and the recap heal going forward.
- An **owner-optional backfill SQL** (Downloads) repairs `WeeklyUserEntry` +
  `UserWeeklyPlacing` history from stored CL × chart level.

`WeeklyTournamentEntry` is pinned by the `api/weeklyCharts` goldens and does not change. The
flag surfaces on `WeeklyBoardRow` (the `Source` precedent) as `WasWithinRange` +
`InRangePlace`; summaries carry `InRangeTopPlaces` + `InRangeEntryCount` beside the overall
head, so the static page ships both states and the switch just swaps which renders — ranks
renumber because both ladders are server-computed. The dialog query gets the same shape.
Persistence: `WeeklyCharts__RelevantPlayers` through `POST /Preferences/Set` (allowlist gains
the `WeeklyCharts__` prefix); the page reads it at render, the dialogs read it live.

### 12.4 The monthly dialog

`MonthlyBoardDialog` is a **new** island-hosted component — the monthly table (players ×
top-4 × counted × total) is not the per-chart board shape, so it does not contort
`LeaderboardDialog`. Segment row inside; rows carry avatar + CL; `TopFour` renders as
compact stickers; the counted expansion is the official-leaderboards pattern (M21):
`tier-card-grid-compact`-sized stickers, jacket + bubble + grade/score strip + points badge,
no song names. `MonthlyLeaderboardRow` gains `CompetitiveLevel` (derived from the counted
entries' stored CL — no cross-vertical read).

### 12.5 Instant behaviors

`challenge-board.js` grows three delegated handlers beside density: the monthly segment swap,
show-all/show-suggested, and the relevant-players toggle — each flips pre-rendered DOM and
calls `history.replaceState` so the URL grammar (`?type=`, `?suggested=all`) still names the
state for sharing and crawlers. Past weeks stay real links (different data, honest
navigation). The circuit remains exactly one island: Record (weekly + daily), the per-chart
`LeaderboardDialog` (now honoring the relevant setting), `MonthlyBoardDialog`,
`ChartDetailsDialog`, admin rotate.

### 12.6 Commit order

l10n keys ×9 within each commit; fast suites green per commit; API goldens re-run in any
commit that touches Contracts.

| # | Content |
|---|---|
| C0 | Design doc v3 (this section; status + M13–M22) |
| C1 | `IsWithinRange` + `GetSuggestedCharts` refactor + domain facts |
| C2 | `SaveEntry` stamps the flag (port + EF + both writers) + handler facts; backfill SQL → Downloads |
| C3 | Read model: `WeeklyBoardRow` flag + dual places, summary in-range head/count, monthly CL, ×4-type dispatch + handler cache; **goldens in-commit** |
| C4 | `DailyStepRailCard` + `MonthlyRailCard` + bUnit facts |
| C5 | Page restructure: rail + ladder + all-36/all-4 rendering + card anatomy v2 (top-3, icons, Table side-by-side) + site.css rework |
| C6 | challenge-board.js instant handlers + `replaceState`; Preferences prefix + server-side read |
| C7 | `MonthlyBoardDialog` + sticker expansion + `OpenMonthly`; relevant filter in `LeaderboardDialog` |
| C8 | E2E adaptation + relevant-players round-trip; full suites locally |
| C9 | Sweep: delete superseded components, l10n audit, as-built stamp |
