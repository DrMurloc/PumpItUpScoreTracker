# Weekly Charts — the challenges hub

**The page**: `/WeeklyCharts` is the challenges hub — Weekly Charts and Daily Step
([daily-step.md](daily-step.md)) on one statically rendered page, so crawlers see the concept,
with islands only where a circuit earns its keep.

**Section numbers are stable.** Code comments cite this file by section (`§3.3`, `§4.5`, `§12.2`,
and the `M*`/`O*` marks), so sections are rewritten in place and retired ones leave a gap rather
than renumbering everything downstream. §2, §9 and §11 are gaps by that rule.

---

## 1. Owner calls

Scope calls:

| # | Call |
|---|---|
| O1 | **Static SSR + islands here**, independent of the chart-details pilot. |
| O2 | Route and name stay **`/WeeklyCharts`**. Daily Step integrates into the page. |
| O3 | Monthly leaderboard **stays mechanically the same** (best-N-per-window). The **BITE relic drops**. Aggregation lives in the vertical, not the page. |
| O4 | **Scoring: the game's own PUMBILITY replaces the homebrew PUMBILITY+** — per mix, through `ScoringConfiguration.PumbilityScoring(mix, includeCoOp)`. Consequences in §6. |
| O5 | **Per-user Daily Step history ships** on this page. |

Presentation calls:

| # | Call |
|---|---|
| M1 | **The fold answers "what's up for competition now."** Daily Step is a slim card, not a hero; the concept paragraph lives in the **meta description** only. |
| M2 | The record dialog speaks **Quick Record's vocabulary** (§8): live derived grade, plate dropdown, prefill from your current entry, in-place "Recorded" flash. |
| M3 | **Photos are optional.** Disclaimers: a photo is your proof if a score's legitimacy is disputed; suspected cheaters will be *required* to attach photos for future competition entries (the enforcement mechanism is future scope — the words set the policy now). |
| M4 | **No broken concept in the record dialogs.** Manual competitive entries are **score + plate, period** — always a pass. Plated brokens matter for personal recording (Quick Record → ledger), not competition; real brokens ride the official import. |
| M5 | **Trust ladder on board rows**: ✔ officially imported > 📷 photo attached (opens the proof) > **blank** for bare self-reports (no "unverified" text). Footer legend: "✔ imported · 📷 photo proof". |
| M6 | **Boards show every entry.** No `MaxPlaces` cap in the shared `LeaderboardDialog`, so the home-page widgets inherit full boards; the dialog scrolls. Pagination only if a board outgrows a scroll (not expected under ~50). |
| M7 | Unplayed cards show a **dim "—"** (hover title "Not played yet"), never words. **Suggested-only is the default** for calibrated players, `?suggested=all` the escape. |
| M8 | **Grade glues to the score** — one right-aligned pair per row, never orphaned next to the name. |
| M9 | **Chart identity opens the shared `ChartDetailsDialog`** (video included) from every density and the Daily card. Anchors keep real `/Chart/{id}` hrefs for the crawl mesh; the island upgrades the click. |
| M10 | **Density switcher is on-page UI** — the Tier Lists treatment (three small icon buttons, active = primary), right-aligned above the weekly grid. |
| M11 | **No "Suggested" chips on cards** — the gold border is the only on-card signal; the filter note above the grid does the explaining. |
| M12 | **Compact keeps one action**: a bottom-right **entry-count chip** opens the leaderboard. Record has no Compact affordance by design — Compact is a scanning mode. |
| M13 | **"Static really should mean static on load, not requiring a page load to change anything on the screen."** The whole page ships in the first response; after that, every view swap is an instant client-side toggle. URL state survives for sharing/crawlers via `history.replaceState`, never as the mechanism. |
| M14 | **The rail layout**: Daily Step (top 5 + pinned you) and the Monthly Leaderboard (top 20) as homepage-widget-chrome cards in a 330px left rail; the week's grid takes the rest. Trophy on each rail card opens the full board dialog. |
| M15 | **Real icons, ubiquitous**: Record = `AddCircleOutline`, boards = `EmojiEvents` — the homepage Daily Step widget's pair — on weekly cards **and** the Daily card. |
| M16 | **Monthly rows carry the player's avatar and competitive level**, in the rail and the dialog. |
| M17 | **Top-3 in Comfortable** (your row folds in when you rank ≤3); Table stays top-1 + you; Compact unchanged. |
| M18 | **Type switching is the tier-list segment row** (FolderGrid's `MudButtonGroup` vocabulary: joined buttons, active = filled primary) — not pills, not a select. |
| M19 | **Table density renders jacket + bubble side by side** (the tier-table pattern), never the overlay. |
| M20 | **"Relevant players"**: a quiet persisted switch hiding out-of-band players on **weekly per-chart boards only** — monthly is exempt ("it highlights people pushing their highest, not dominating low boards"), daily is exempt. Ranks renumber after the filter. Default off. Page-level beside density, mirrored in the weekly board dialog. |
| M21 | **The monthly counted-scores expansion goes compact**: the official-leaderboards pattern (jacket + bubble + grade/score + points), **no song names**; names live in the tooltip. |
| M22 | **The collapse ladder**: ≥~1080px rail-left; ~640–1080px the rail cards go 2-up above the grid; <~640px single column **Daily → This Week → Monthly**. Two media queries, no JS. |

Amend calls (§9):

| # | Call |
|---|---|
| A1 | **A manual submission is editable in place** — including downward. The dialog was always shaped like an edit; the write now matches. |
| A2 | **A lower score warns before it lands**, showing what it costs: the score it replaces and the place you fall to. |
| A3 | **No withdrawals.** There is no path that removes your entry from a board. Correcting a score is in scope; disappearing from the competition is not. |
| A4 | **Imported entries are not hand-editable.** The ✔ tier means the official site said so. |
| A5 | **A weekly score event fires only on an improvement or a first recording** — never on a correction downward. |

## 3. The render split

### 3.2 The static core

Everything you can *read* renders statically, anonymous and signed-in alike: the Daily card and
its standing, the weekly grid (all densities are server-rendered variants of the same rows),
your per-chart lines, the monthly boards, the daily history, the scoring legend, every empty
state, and the not-yet-featured pool behind `?pool=1`. Static display vocabulary per
static-shell.md: `SongImage`, `DifficultyBubble`, `LetterGradeIcon`, `ScoreBreakdown`,
`UserLabel`; `--mix-*` tokens only; no Mud popover dependencies; numbers always printed.

`WeeklyCharts.razor` carries no `@rendermode` and is the first entry in
`RenderModeDeclarationTests.StaticPages` — the ratchet that makes static-by-omission a
deliberate act rather than an accident.

### 3.3 One island: the dialog host

`ChallengeDialogHost` — one `@rendermode Interactive` root hosting **four dialogs plus the admin
action**: **Record** (§8, §9), the shared **`LeaderboardDialog`** (§5), `MonthlyBoardDialog`
(§12.4), the shared **`ChartDetailsDialog`** (M9), and the admin rotate confirm (publishes
`RotateWeeklyChartsCommand`).

Static elements carry `data-challenge-action` + `data-chart-id`;
`wwwroot/js/challenge-board.js` registers one delegated click listener and forwards to the
host's `DotNetObjectReference`. Chart-identity anchors keep their real `/Chart/{id}` hrefs —
crawlers follow them (the internal-link mesh), the listener `preventDefault`s and opens the
dialog for humans. The host self-loads dialog data on demand, keyed by primitive ids (the
chart-details island grammar). Mud popovers work because `MudProviders` mounts ahead of every
island.

### 3.4 Layout, head, navigation

- **Layout: MainLayout**, which renders statically around every page. The dock renders as plain
  markup from the page; `challenge-board.js` calls `shell.setDockState(true, false)` on load.
- Mix resolution: request-side via `IUiSettingsAccessor`; **any mix without a weekly board falls
  back to Phoenix** (`mix is not (Phoenix or Phoenix2) → Phoenix`).
- Head: served by **`StaticHeadResolver`'s `/WeeklyCharts` branch** — a static page's
  `PageTitle`/`HeadContent` would render into a HeadOutlet island it never reaches. Title, meta
  description **carrying the concept copy the fold no longer holds** (M1), OG tags (the daily
  jacket, falling back to the week's first), and canonical `/WeeklyCharts` via
  `StaticHeadModel.Canonical` (filter/week variants fold into clean). The JSON-LD `ItemList`
  renders in the **body** — valid placement, and the body is what a static page owns. Sitemap
  entry ships in `SitemapController`.
- Navigation: links to the page full-load; links out are plain anchors. Enhanced nav stays off
  app-wide.

### 3.5 Explicitly not here

Output caching / CDN; other pages' render modes; the cheater photo-enforcement mechanism (M3 —
needs a per-user flag and an admin surface; the disclaimer ships, the lever when first needed).

## 4. Anatomy

Sections carry anchors (`#daily`, `#weekly`, `#monthly`, `#history`); the mobile dock is static
markup with jump links + the next-reset countdown.

1. **Header row** — h1, the monthly-place chip, and the **Week** picker (a disclosure of
   past-week links, `?week=…`). No prose.
2. **The rail and the grid** — see §12.1/§12.2. The rail holds `DailyStepRailCard` and
   `MonthlyRailCard`; `WeeklyBoardGrid` takes the rest.
3. **Weekly grid** (`#weekly`) — grid bar: heading + count/rotation sub + suggested filter note
   ("showing suggested — show all N") + the **density switcher** right-aligned (M10). Card
   (Comfortable): jacket + bubble (chart-details on click), name, **top-3** and **your line**
   (dim "—" when unplayed), footer count + Record + Board (M15/M17). Compact: jacket sticker +
   count-chip → board (M12). Table: jacket + bubble side by side (M19), top-1, your line, count,
   actions. Suggested = gold border only (M11). Empty state: "This week's charts post Monday at
   midnight ET."

   **Board order is the query's, not the draw's**: `GetWeeklyBoardQuery` returns the week in the
   canonical Phoenix 1 order — level descending, **singles before doubles within a level**,
   CO-OPs last with the 2-player duet last of all. The order lives in one shared key
   (`WeeklyBoardOrder.SortKey`) that both the query and the homepage Weekly widget sort by, so
   the grid, the widget, the page's JSON-LD, and any future consumer can't drift.
4. **Monthly board** (`#monthly`) — in the rail (§12.2), full depth in `MonthlyBoardDialog`
   (§12.4). Empty state: "Scores land here as boards close."
5. **Your Daily Step history** (`#history`, signed-in) — last 14 days: date, chart chip
   (chart-details on click), Limbo tag, place/total, grade + score pair. Empty state names the
   action. Anonymous visitors get a sign-in CTA card instead.
6. **Scoring legend** — rendered from the active mix's `ScoringConfiguration` (never
   hand-copied): grade multipliers, Phoenix 2's additive plate bonuses, and the rules sentences
   (§6). Phoenix only — Phoenix 2 uses the game's own formula, nothing to footnote.
7. **The pool** (`?pool=1`) — the not-yet-featured chart list, server-rendered on its own URL.
8. **Admin** — the rotate trigger via the dialog host's confirm; a quiet card at the bottom,
   admin-only.

## 5. The shared LeaderboardDialog (changes land once, all consumers inherit)

[LeaderboardDialog](../../ScoreTracker/ScoreTracker/Components/LeaderboardDialog.razor) serves
the Daily/Weekly widgets and the challenges hub alike:

- **No `MaxPlaces` cap** (M6): every entry renders; the content area scrolls; your row glows in
  place.
- **Trust ladder** (M5): ✔ imported · 📷 photo (click opens the proof photo) · blank. Daily has
  no photo intake, so its ladder is ✔/blank.
- Row layout keeps grade+score+plate as the glued right group (M8 — `ScoreBreakdown` already
  does this).
- The relevant-players filter (M20) applies on weekly per-chart boards only.

## 6. Scoring — PUMBILITY per mix (O4)

`ScoringConfiguration.PumbilityScoring(mix, includeCoOp: false)` prices per-chart points and
monthly totals. Consequences, all the game formulas' own rules:

- **Phoenix board** → Phoenix PUMBILITY; **Phoenix 2 board** → Phoenix 2 PUMBILITY
  (`GradePlusPlate`, additive plate bonus, verified grade table).
- **Broken plays score 0** toward totals (`StageBreakModifier = 0`). They still appear on
  per-chart boards, ranked by the existing policies.
- **Co-op is never PUMBILITY-priced on this page**: Combined excludes co-op (Phoenix 2's own
  rule), and the **Co-Op view ranks raw score sum** — the only currency co-op charts share.
- **Ties** break by total raw score (stepped grades tie more than a continuous scale).
- The legend renders from the config so the page can never drift from the engine. UI says
  **PUMBILITY** everywhere.

## 7. Contracts and data

Read model: `GetWeeklyBoardQuery`, `GetWeeklyChartBoardQuery`, `GetMonthlyLeaderboardQuery`,
`GetDailyStepBoardQuery`, `GetUserDailyStepHistoryQuery` — display-enriched via `IUserReader`
(the page injects no repository), priced per §6, one batched history read. Handlers sit on the
existing sagas.

**Trust source**: the `Source` column on `WeeklyUserEntry` is `Official` | `Manual`, existing
rows defaulted to `Official` (historically imports dominated and photos were mandatory for the
manual path). The import consumer stamps Official; a Record submission stamps Manual. The
source describes **the ranked score's** provenance — it moves only when the score does.

**⚠ The API golden caution**: `api/weeklyCharts` serializes weekly entries and is pinned by
`Tests.Api`. The shared `WeeklyTournamentEntry` record does **not** grow properties — per-entry
extras (`Source`, `WasWithinRange`, `InRangePlace`) surface on `WeeklyBoardRow` instead. Run the
API goldens in any commit that touches Contracts to prove the wire shape never moved.

The 📷 tier derives from the entry's existing nullable `PhotoUrl` — no third flag.

## 8. The record dialog (Quick Record vocabulary, M2–M4)

One dialog, two modes, hosted by the island:

- **Fields**: score (live derived grade beside it, display-only — mix-aware, since Phoenix 2
  moved the A+/AA/AA+ floors) + plate dropdown (shorthands; empty = the run broke). **No broken
  control of any kind.**
- **Prefill from your current entry** — an edit, not a blank. A fresh photo is never prefilled:
  it proves *this* score.
- **Weekly adds the optional photo block**: add → uploading (progress) → done (thumb +
  Remove), captioned "Optional — a photo is your proof if a score's legitimacy is ever
  disputed," footnoted "Suspected cheaters will be required to attach photos for future
  competition entries."
- **Daily**: score + plate only (no photo — `RecordDailyStepScoreCommand`).
- **Submit** enables on score alone → in-place **"Recorded"** flash, then the static page
  reloads so the board and your standing reflect the write.

Presentation preferences (density, relevant-players) persist through `POST /Preferences/Set`
with an allowlisted key set — the static page reads them at render.

## 9. Amending a submission

### 9.1 The defect this fixes

`RegisterWeeklyChartScoreCommand`'s handler was a per-field monotonic merge: max score, max
plate, un-break only, never clear the photo. That is exactly right for the importer, which
re-registers your official best on every run and must be idempotent. It was wrong for the Record
dialog, which prefills your current entry (§8) and therefore *looks* like an edit — so a player
correcting a fat-fingered `974,220` down to `947,220` got the green "Recorded" flash, a page
reload, and a board still showing `974,220`. A silent no-op behind a success confirmation, and
no way to fix a typo before Monday's rotation.

### 9.2 Intent, not a flag on the score

The two callers want different things from the same command, so the command says which:

| Intent | Caller | Rule |
|---|---|---|
| `BestWins` | the import consumer, and any submission that raises a score | Per-field monotonic merge. Idempotent — replaying an import can never move a board. |
| `Replace` | the Record dialog, when the typed score is **below** the stored one | The submitted entry becomes the entry. |

`WeeklyEntryIntent` lives in `Domain/Records/` beside `ChallengeEntrySource`, and
`RegisterWeeklyChartScoreCommand` defaults to `BestWins` — every existing call site keeps its
behavior without naming the parameter.

An enum rather than a `bool ReplaceExisting` because the call site reads as the rule it wants,
and because the third case (a correction that raises the score) is genuinely `BestWins` rather
than a false `Replace`.

### 9.3 The merge is a domain policy

The ~20 lines of merge `if`s move out of the saga into
[`WeeklyEntryMergePolicy`](../../ScoreTracker/ScoreTracker.Domain/Services/WeeklyEntryMergePolicy.cs),
beside `WeeklyChartSuggestionPolicy` — the other weekly rule that must never fork between its
consumers. It is a pure function with no ports and no clock:

```
Merge(existing, incoming, existingSource, incomingSource, intent, competitiveLevel)
  → (Entry, Source, IsImprovement, IsRefused)
```

`IsRefused` is the A4 gate (§9.4). `IsImprovement` is the A5 gate (§9.5). Both are computed
where the merge happens, because both are facts *about* the merge — a caller that recomputed
them from the returned entry would be re-deriving the rule.

Under `Replace` the entry is taken wholesale, with two carry-forwards that are not the player's
to lose by omission: the **photo** (a photo-less resubmit never wipes attached proof — M3) and
the freshly computed **competitive level** (the band verdict must reflect now, not whenever the
row was first written).

### 9.4 A manual amend cannot touch an imported entry (A4)

`Replace` applies only when the stored entry's source is `Manual`. Against an `Official` row the
policy refuses and the handler no-ops.

This is arithmetic before it is policy. The import consumer re-registers your official best
under `BestWins` on every run, so a hand-lowered official score returns to its old value the
next time you import — allowing the edit would ship a number that silently reverts, which is
worse than declining it. Declining also keeps the ✔ tier meaning what the M5 legend says.

The dialog surfaces the refusal as a read-only state (§9.6) rather than letting a player type
into a field whose submit will be swallowed. The handler-side check is the real gate; the UI
state is the courtesy.

### 9.5 The event only fires on progress (A5)

A downward correction changes your place, and the old publish condition was "place changed" —
so a corrected typo would have published `UserWeeklyChartsProgressedEvent`, and
`HighlightCaptureSaga` would have written a gold `WeeklyPlacement` milestone onto the player's
Sessions page celebrating them falling from #2 to #6.

The publish is now gated on `IsImprovement` — a new entry, or a ranked score that went up —
and the event is renamed to state that guarantee in its own name:

**`UserWeeklyChartsProgressedEvent` → `UserWeeklyChartScoreImprovedEvent`.**

The rename also fixes a plural that was never true: the event has always described one score on
one chart, not a set of charts. Consumers: `HighlightCaptureSaga` only.

### 9.6 The four dialog states

The dialog already loads your current entry to prefill it. It now keeps that entry — score,
source and place — and compares live as you type.

| Condition | What renders |
|---|---|
| **No entry on this chart** | §8 unchanged: blank score, default plate, "Submit". |
| **Typed score ≥ your board score** | The standing line ("Your board score: 947,220 · #6 of 41") and a live place preview. Ordinary primary "Submit". No friction — this is the path nearly everyone takes. |
| **Typed score < your board score** | A warning panel: the score it replaces, the arrow, and **the place you fall to** (`#2 → #6 of 41`). The action button turns warning-colored and reads **"Replace with lower score"**. Sends `Replace`. |
| **Your entry came from the importer** | Score and plate render read-only; a panel explains that imported scores can't be changed by hand and that a higher score can be submitted any time. The submit action is gone; only "Close" remains. |

Two deliberate choices in that table:

- **The warning appears while typing, not on submit.** A confirmation you meet after committing
  is a speed bump; one you meet before is information.
- **The button relabels rather than adding a checkbox.** The competition happens on an arcade
  floor with a phone in one hand — a second confirmation tap is friction that gets tapped
  through without reading, which buys nothing over a button that already says what it does.

The place preview reuses `WeeklyChartSuggestionPolicy.ProcessIntoPlaces` against the rows the
board query already returned — the same ranking the saga will apply, not a second
implementation of it.

### 9.7 What this deliberately is not

- **No withdrawal** (A3). Nothing removes an entry from a board.
- **Daily Step is untouched.** `DailyStepSaga.UpsertEntry` has the same shape — a keep-the-extreme
  merge that silently discards a correction (with the Limbo inversion on top). The same treatment
  would fit; it is out of scope until asked for. See §10.
- **The board's meaning splits, knowingly.** Imported entries remain *your best score this week*;
  manual entries become *the score you last declared*. That is the honest reading of a
  self-report, and the monthly leaderboard (§6) prices whatever the row holds.

## 10. Open (deliberately parked)

- **Daily Step amend**: the same silent-discard shape as §9.1, plus the Limbo inversion (where
  "lower" is an improvement, so `IsImprovement` inverts with the board). Cheap to add once the
  weekly path has field time.
- **Daily photos**: the daily record has no photo intake, so daily boards cap at ✔/blank.
- **Record from Compact**: none by design (M12); revisit only if players ask.
- **Photo-required enforcement for suspected cheaters** (M3): per-user flag + admin surface; the
  dialog's disclaimer already states the policy.
- **Board pagination**: only if a board outgrows a scrollable dialog (~50+ sustained).
- **Dock countdown is static** (server value at load), not ticking — a live one would duplicate
  the localized reset string in JS. Revisit if wanted, with a format the client can localize.
- **Amend audit trail**: a replaced score leaves no record of what it replaced. If disputes ever
  need it, the row would need history rather than an in-place update.

---

## 12. The rail

### 12.1 Layout

`WeeklyCharts.razor` renders header row → a CSS grid `330px 1fr`: the **rail** (Daily Step card,
Monthly card) and the **week's grid**. The collapse ladder (M22) is two media queries. The static
core ships **everything**: all cards (non-suggested present but hidden), all four monthly boards
(top 20 each, inactive hidden), daily top-5 + pinned you. Below-fold sections (history, legend,
pool, admin) follow.

### 12.2 The rail cards

- **`DailyStepRailCard`**: widget chrome (`dash-widget` vocabulary), kicker + record/board icon
  buttons in the head, jacket + bubble + name + "resets in", top-5 rows (place colored by the
  rarity ramp, avatar, name, grade+score), pinned "your standing" when you sit past 5, Limbo =
  secondary edge + chip on the card.
- **`MonthlyRailCard`**: segment row (M18), window subline, top-20 rows — place · avatar · name ·
  CL chip · PUMBILITY total — you-glow, pinned-below-20, "Full board" trophy. All four type
  boards render; the segment row swaps them client-side.

### 12.3 Relevant players (M20)

The band is the suggestion band: `floor(competitive level) ∈ [level−1, level+2]`, co-op always
in — `WeeklyChartSuggestionPolicy.IsWithinRange`, so the two consumers (suggestions, this filter)
can never fork.

- **Boards derive in-range at read time** from the entry's stored `CompetitiveLevel` via the
  policy — correct for all history.
- **`SaveEntry` also stamps `WasWithinRange`** so rotation snapshots and the recap
  (`WeeklyRecapCalculator`) carry the verdict the entry was judged under.

`WeeklyTournamentEntry` is pinned by the `api/weeklyCharts` goldens and does not change (§7). The
flag surfaces on `WeeklyBoardRow` as `WasWithinRange` + `InRangePlace`; summaries carry
`InRangeTopPlaces` + `InRangeEntryCount` beside the overall head, so the static page ships both
states and the switch swaps which renders — ranks renumber because both ladders are
server-computed. Persistence: `WeeklyCharts__RelevantPlayers` through `POST /Preferences/Set`.

**Place is the overall place on every row, including in-range rows.** The grid merges the two
heads and orders by place; an in-range row carrying its own renumbered place would sort into the
overall ladder by the wrong number — a 947k in-range #2 landing above an 989k overall #3.

### 12.4 The monthly dialog

`MonthlyBoardDialog` is island-hosted and separate from `LeaderboardDialog` — the monthly table
(players × top-4 × counted × total) is not the per-chart board shape, so it does not contort the
shared one. Segment row inside; rows carry avatar + CL; `TopFour` renders as compact stickers;
the counted expansion is the official-leaderboards pattern (M21): jacket + bubble + grade/score
strip + points badge, no song names. `MonthlyLeaderboardRow` carries `CompetitiveLevel`, derived
from the counted entries' stored CL — no cross-vertical read.

### 12.5 Instant behaviors

`challenge-board.js` carries delegated handlers beside density: the monthly segment swap,
show-all/show-suggested, and the relevant-players toggle — each flips pre-rendered DOM and calls
`history.replaceState` so the URL grammar (`?type=`, `?suggested=all`) still names the state for
sharing and crawlers. Past weeks stay real links (different data, honest navigation). The circuit
remains exactly one island.
