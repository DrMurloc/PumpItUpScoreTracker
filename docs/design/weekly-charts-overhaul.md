# Weekly Charts overhaul — the challenges hub, and a static page

**Status**: designed 2026-07-15, owner calls locked in-session; executing per §9. Builds on the
Stage-2 hosting flip (this branch descends from `claude/render-modes-scope`). The chart-details
overhaul (branch `claude/chart-details-overhaul`) pilots the same static-SSR-with-islands shape
on `/Chart/{id}`; **the two pages are independent** (owner call) and share exactly one commit's
worth of mechanism (§3.1) — whichever merges second rebases that commit away.

**The page**: `/WeeklyCharts` becomes the challenges hub — Weekly Charts and Daily Step
([daily-step.md](daily-step.md)) on one page, statically rendered so crawlers finally see the
concept, with islands only where a circuit earns its keep. daily-step.md L6 reserved the
per-user history view "for a future view on the Weekly-charts rebuild"; this is that rebuild.

---

## 1. Owner calls (2026-07-15)

| # | Call |
|---|---|
| O1 | **Static SSR + islands here too**, independent of the chart-details pilot — neither blocks the other. |
| O2 | Route and name stay **`/WeeklyCharts`**. Daily Step integrates into the page. |
| O3 | Monthly leaderboard **stays mechanically the same** (best-N-per-window, weekly windows into a monthly board). The **BITE relic drops** (hardcoded July 2024 event, dead since August 2024). Aggregation moves out of the page into the vertical. |
| O4 | **Scoring: the game's own PUMBILITY replaces the homebrew PUMBILITY+** — per mix, through the existing `ScoringConfiguration.PumbilityScoring(mix, includeCoOp)` seam (Phoenix formula on the Phoenix board, Phoenix 2's additive grade+plate formula on the Phoenix 2 board). Consequences spelled in §6 — they are the formulas' own rules, not new design. |
| O5 | **Per-user Daily Step history ships** on this page (new read over `UserDailyStepPlacing`, which rotation has been writing since Daily Step landed). |

## 2. Sins this pays down

The 2026-07 audit of [WeeklyCharts.razor](../../ScoreTracker/ScoreTracker/Pages/Competition/WeeklyCharts.razor)
against [UX-GUIDELINES.md](../UX-GUIDELINES.md), kept here as the acceptance list:

| Rule | Today | After |
|---|---|---|
| 1 — answer above the fold | Filter furniture renders before the first chart; no "your week" anywhere | Daily hero + weekly grid lead; controls become furniture (§4) |
| 3 — one concept, one component | Hand-rolled per-chart leaderboard dialog beside the shared `LeaderboardDialog`; hand-rolled grade `MudImage`s off a hardcoded CDN URL; avatar `MudImage`s beside `UserLabel` | Shared `LeaderboardDialog`, `LetterGradeIcon`, `UserLabel`, `ScoreBreakdown` everywhere |
| 5 — density | None — one fixed card grid | `Density__WeeklyCharts`: Comfortable / Compact / Table on the weekly grid (§8) |
| 6 — filters are furniture | Selects strewn above content; "Leaderboard Type" floats mid-page with ambiguous scope; week selection is circuit state | Week + type are **URL state** (crawlable links); communities a compact disclosure form above the board they filter (§5) |
| 7 — +40% text | ~10 raw-English strings (submit dialog warning, dialog headers, snackbars, `Singles/Doubles/CoOp`, admin button); a dead localized key with 2024 dates baked in | Every string through `L[…]`, all nine locales, in the same commit each key lands |
| 9 — loading looks like the layout | One monolithic sequential `OnInitializedAsync` (N+1 per community and per week-of-month); blank page until the last query; no empty states | Static core arrives *with the document* (no skeleton can beat that); islands have fixed footprints; every section has a named empty state (§4) |
| 10 — thumbs first | No dock; primary actions are per-card icon slivers; admin button parked after four `<br/>`s | Page dock with section jumps + reset countdown; record stays per-card but thumb-sized in Comfortable/Compact (§4) |

Non-UX repairs riding along: `@inject IUserRepository` leaves the page (queries return
display-ready rows, §7); dead compute deleted (`_userCharts`/`_userTopFour*`/`_userTotalPlace`
computed and never rendered, `_countryFlags` never read); the 10 MB limit that reports "20MB";
five raw `DateTimeOffset.Now` calls (the reset-drift bug class daily-step.md §6 diagnosed);
the stray `@if` inside a C# method; zero test coverage.

**Untouched**: `api/weeklyCharts` (contract-pinned by `Tests.Api`), both home widgets, the
Discord card path (`GetUserWeeklyPlacementsQuery`), the rotation sagas and their cron slots.

## 3. The render split

### 3.1 Mechanism — the framework's own per-page opt-out

Verified present in the installed net10.0.9 shared framework:
`ExcludeFromInteractiveRoutingAttribute` (`Microsoft.AspNetCore.Components.dll`) and
`HttpContext.AcceptsInteractiveRouting()` (`Microsoft.AspNetCore.Components.Endpoints.dll`).

- `WeeklyCharts.razor` declares `@attribute [ExcludeFromInteractiveRouting]`. The interactive
  router then treats every navigation to it as a full document load, and the endpoint renders
  it **statically** — real HTML, live `HttpContext`, no circuit, no prerender double-render
  (static rendering runs once; the prerender ban is about rendering *twice*).
- `App.razor` makes the render mode conditional — the standard shape:
  `IComponentRenderMode? PageRenderMode => HttpContext.AcceptsInteractiveRouting() ? Interactive : null;`
  applied to **both** `<Routes>` and `<HeadOutlet>`. On every existing page that resolves to
  today's exact behavior; on an excluded page both render statically, so the page's
  `<PageTitle>`/`<HeadContent>` land in the raw HTML — the SEO payoff. The hardcoded fallback
  `<title>` emits only when interactive routing is accepted (an excluded page owns its title;
  two `<title>`s is a crawler ambiguity).
- **This is the one commit the chart-details pilot also needs.** Kept mechanism-only and
  page-free so either branch can carry it and the other rebases it away.

### 3.2 The static core

Everything you can *read* renders statically, anonymous and signed-in alike: the Daily Step
hero and its board, the weekly grid (jackets, bubbles, top-3s, entry counts), your per-chart
entries and standing (static SSR reads the auth cookie — personalization is per-request
server-side, **not** an island), the monthly table with expandable per-player best-lists
(native `<details>` in the row — the shell's mix-picker precedent), the daily history, the
scoring legend, and every empty state. Display vocabulary in static regions is already
sanctioned by static-shell.md: `SongImage`, `DifficultyBubble`, `LetterGradeIcon`,
`ScoreBreakdown`, `UserLabel` — with the rule that **no static row may rely on a Mud popover**
(no `MudTooltip` modes; the number is always printed, per UX rule 8).

### 3.3 Exactly one island

`ChallengeDialogHost` — one `@rendermode Interactive` root hosting the three dialogs: **Record**
(score/plate/broken/photo, the photo requirement unchanged), the shared **`LeaderboardDialog`**
(full per-chart board, `MaxPlaces` 10 weekly / 50 daily, `Ascending` on Limbo), and the admin
**rotate** confirm. Static buttons carry `data-challenge-action="record|board|rotate"` +
`data-chart-id`; a small `wwwroot/js/challenge-board.js` registers one delegated click listener
and forwards to the host's `DotNetObjectReference`. One island, zero per-card circuit weight,
buttons paint with the document. The host self-loads dialog data on demand (the chart-details
island grammar: self-loading, keyed by primitive ids — parameters cross a serialization
boundary, so nothing rich crosses it). Mud popovers work because `MudProviders` mounts ahead of
every island in App.razor — the Stage-1 cross-root proof, unchanged.

Buttons are live once the circuit connects (sub-second); until then they are honest inert
HTML — same progressive posture as the shell's static nav.

### 3.4 Layout, head, navigation

- **`Shared/StaticPageLayout.razor`** (new, ~10 lines): `MudContainer` + `@Body`. MainLayout is
  circuit-shaped (recap `MudDialog` could static-render stuck open, `LocationChanged` wiring,
  JS-interop lifecycle, `PageDockService`) — the static page does not fight it, it opts out via
  `@layout`. The dock renders as plain markup from the page itself (§4); `page-dock.js` already
  operates on the class, not the component.
- Mix resolution: the page reads the request itself (that is static SSR's whole advantage) —
  selected mix via `IUiSettingsAccessor`, then **any mix without a weekly board falls back to
  Phoenix** (`mix is not (Phoenix or Phoenix2) → Phoenix`, superseding the XX-only check). No
  legacy-mix gate needed.
- Head: `<PageTitle>` "Weekly Charts — PIU Scores" (localized), `<HeadContent>` meta
  description naming the concept and the week's chart count, canonical `/WeeklyCharts` (filter
  variants canonicalize to the clean URL), OG title/description/image (the current daily
  jacket), and a JSON-LD `ItemList` of this week's charts. `/WeeklyCharts` joins the sitemap
  (it is absent today).
- Navigation: links *to* the page from circuit pages full-load (the attribute's contract);
  links *out* are plain anchors. Enhanced nav stays off app-wide, so nothing new to test there.

### 3.5 Explicitly not here

Output caching / CDN work — the production `no-store` header and ARR-affinity cookies
(static-shell.md D18) are a platform fight the chart-details P3 owns. Crawlability does not
need caching; caching is a later perf bonus. Also not here: any change to other pages' render
modes.

## 4. Anatomy

Section order = answer → evidence → history → methodology. Sections carry anchors
(`#daily`, `#weekly`, `#monthly`, `#history`); the page dock (mobile) is static markup with
jump links + the next-reset countdown (server-computed text; `challenge-board.js` ticks it).

1. **Header strip** — h1, one localized sentence of concept copy (the crawlable pitch: new
   charts Monday, a fresh chart daily at midnight ET, Limbo Thursdays-or-whenever), the week
   picker (disclosure of past-week **links**), and for signed-in players the "your week" line:
   charts played / suggested remaining / current monthly place.
2. **Daily Step hero** (`#daily`) — jacket-led card: chart identity (links to `/Chart/{id}`),
   Limbo banner on Limbo days ("lowest passing score wins"), top-3 rows + your row, entry
   count, "resets in …", Record + full-board buttons. Empty state: "Today's Step posts at
   midnight." (existing key).
3. **Weekly grid** (`#weekly`) — the density-managed grid (§8). Card = jacket header +
   `DifficultyBubble` + top-3 (`ScoreBreakdown` + `UserLabel`) + your entry line when you sit
   past third + entry-count button (full board) + Record. Suggested charts wear a chip;
   the suggested-only toggle is presentation (§8). Jacket links to `/Chart/{id}` — the
   YouTube button drops (the chart page owns video). Empty state: "This week's charts post
   Monday at midnight ET."
4. **Monthly board** (`#monthly`) — type links (Combined / Singles / Doubles / Co-Op → `?type=`),
   the window subtitle (week N of the month, dates), then the table: rarity-ramp place, player,
   top-4 chart chips, count (a `<details>` disclosure expanding the player's full best-list),
   PUMBILITY total. Empty state: "Scores land here as boards close."
5. **Your Daily Step history** (`#history`, signed-in) — last 14 days: date, chart chip, Limbo
   tag, place/total that day, grade + score. Empty state names the action ("Play today's Step
   to start your streak").
6. **Scoring legend** — rendered from the active mix's `ScoringConfiguration` dictionaries
   (never hardcoded values): grade multipliers (`LetterGradeIcon`), plate bonuses (plate art),
   and the three sentences: best 4 per week window count; broken plays score 0; co-op counts on
   Phoenix, never on Phoenix 2, and the Co-Op view ranks raw score (§6).
7. **The pool** (`?pool=1`) — the not-yet-rotated chart list (today's "Show Remaining Charts"
   dialog), server-rendered only when asked, grouped by level. A real URL, so it is also
   crawlable inventory.
8. **Admin** — the rotate trigger, admin-only, restyled as a quiet card at the true bottom
   (via the dialog host's confirm — it publishes the same `RotateWeeklyChartsCommand`).

## 5. URL grammar

| URL | Meaning |
|---|---|
| `/WeeklyCharts` | Current week, Combined monthly view, persisted community filter — **canonical** |
| `/WeeklyCharts?week=2026-07-06` | A finished week (its boards + that month's window) — every past week is a shareable, crawlable page; the picker is links, not a select |
| `/WeeklyCharts?type=Single` (`Double`, `CoOp`) | Monthly view filter — canonicalizes to clean |
| `/WeeklyCharts?suggested=all` | Escape hatch for the suggested-default (server-rendered default comes from your stats, as today) |
| `/WeeklyCharts?pool=1` | Renders §4.7 |
| Community filter | GET form (checkbox disclosure) → `?communities=a,b`; persisted to `WeeklyCharts__SelectedCommunities` on render when present, read back when absent. Signed-in only; never emitted as crawlable links |

## 6. Scoring — PUMBILITY per mix (O4)

`ScoringConfiguration.PumbilityScoring(mix, includeCoOp: true)` prices every entry
(per-chart points chips and monthly totals). Spelled-out consequences, all of them the game
formulas' own rules:

- **Phoenix board** → Phoenix PUMBILITY (the same config `PlayerRatingSaga` uses): co-op
  included at its in-game 2000 base, brokens at 0.
- **Phoenix 2 board** → Phoenix 2 PUMBILITY (`GradePlusPlate`, additive plate bonus, the
  verified grade table): co-op **never** counts (the flag deliberately doesn't apply), brokens
  at 0.
- **Broken plays score 0** toward totals on both mixes (`StageBreakModifier = 0`). They still
  appear on per-chart boards, ranked below passes as today. (Under PUMBILITY+ a broken AAA
  earned full points — that quirk dies with it.)
- **The Co-Op monthly view ranks by raw score sum** (top-4 raw scores) on both mixes — P2
  prices all co-op at zero, and Phoenix's 2000-base would swamp the table anyway; raw score is
  the only comparable co-op currency. Combined view keeps excluding co-op (now *consistent*
  with P2's own rule instead of merely conventional).
- **Ties**: stepped grade multipliers tie more often than PUMBILITY+'s continuous scale;
  tiebreak = higher raw-score sum.
- The legend (§4.6) renders from the config so the page can never drift from the engine. UI
  says **PUMBILITY** — the game's word — everywhere PUMBILITY+ appeared.

## 7. Contracts (WeeklyChallenge `Contracts/Queries/`, display-enriched via `IUserReader`)

All rows carry player display fields (name, avatar, country) so the page never touches
`IUserRepository`. Chart pricing inside handlers uses the chart port the sagas already read
(type + level per chart id suffice — `AdjustToTime` is off in both PUMBILITY configs).

| Query | Returns | Notes |
|---|---|---|
| `GetWeeklyBoardQuery(Mix, WeekStart?, UserId?, CommunityFilter?)` | Per chart: top-3 rows, entry count, caller's entry + place, suggested flag, board expiration | Replaces the page's `GetWeeklyCharts` + `GetWeeklyChartEntries` + user-lookup cascade (those stay for widgets/API). Past weeks served by the same query via `WeekStart`. Suggestion via `WeeklyChartSuggestionPolicy` + `IPlayerStatsReader` when `UserId` present |
| `GetMonthlyLeaderboardQuery(Mix, AnchorWeek?, Type?, UserId?, CommunityFilter?)` | Window meta (weeks, dates, week-N) + ordered rows: place, player, top-4 entries with points, total, full best-list | One repo read over the window's dates — the per-week N+1 dies. Prices per §6; `Type = CoOp` ranks raw score |
| `GetDailyStepBoardQuery(Mix, UserId?)` | Board meta (chart id, IsLimbo, expiration) + ordered rows + caller's row + source/community sets for the dialog | The page-side sibling of the widget's raw reads; Limbo ordering ascending, brokens excluded on Limbo (existing policy) |
| `GetUserDailyStepHistoryQuery(UserId, Mix, Take)` | Per day: ForDate, chart id, IsLimbo, Place, TotalThatDay, Score, Plate, IsBroken | Reads `UserDailyStepPlacing` (+ per-date entry counts). First consumer of the L6 history table |

Handlers live on the existing sagas (`WeeklyTournamentSaga` / `DailyStepSaga`) as
`IRequestHandler`s with component tests; no schema changes, no new tables.

## 8. Presentation preferences

- **Density** (`Density__WeeklyCharts`, default Comfortable): Comfortable cards / Compact
  sticker-sheet / Table rows, all three server-rendered from the same rows. Toggle =
  `challenge-board.js` swaps a `data-density` attribute (instant, no reload, no circuit) and
  POSTs the persisted value.
- **Suggested-only** toggle: same JS pattern over `data-suggested` card attributes;
  server-rendered default from stats (as today); not persisted (as today).
- **`POST /Preferences/Set`** (new, tiny MVC controller in the `/Mix/Set` / `/Culture/Set`
  lineage): authenticated, allowlisted key prefixes (`Density__`), writes through
  `IUiSettingsAccessor`. Anonymous visitors get the in-page toggle without persistence.

## 9. Commit plan

l10n keys land ×9 locales **within each commit** (house rule), never as a sweep commit.

| C | Content |
|---|---|
| C1 | **Vertical read model**: the four §7 queries + repo methods + saga handlers + builders + component tests. Nothing visual changes |
| C2 | **Static-page mechanism** (the shared commit): App.razor conditional `PageRenderMode` + fallback-title guard; `StaticPageLayout`; nothing uses it yet |
| C3 | **The static core**: WeeklyCharts rebuilt on C1+C2 — anatomy §4, URL grammar §5, scoring display §6, empty states, dock, dead-code deletion. Old dialogs/toggles temporarily absent (page is read-complete, act-light for one commit) |
| C4 | **The island**: `ChallengeDialogHost` + `challenge-board.js` bridge + Record/board/rotate wired through the shared `LeaderboardDialog` |
| C5 | **Presentation prefs**: density variants + suggested toggle + `POST /Preferences/Set` |
| C6 | **Head/SEO**: PageTitle/HeadContent/OG/JSON-LD, sitemap entry, canonical rules |
| C7 | **Tests as facts**: bUnit over the §4 components (static markup, empty states, density variants); E2E — anonymous GET of `/WeeklyCharts` contains this week's chart names in raw HTML (the payoff, pinned), plus one signed-in record round-trip |
| C8 | **Doc sync**: UX-GUIDELINES (static-page grammar note if it graduates a pattern), daily-step.md L6 flip, DATABASE-SCHEMA untouched (no new tables), this doc's status stamp |

## 10. Verification

- `dotnet test` fast suites green per commit; Integration/E2E at C7.
- The SEO fact: `curl -s https://localhost/WeeklyCharts` (anon) contains chart names, the
  concept sentence, `<title>Weekly Charts`, and the JSON-LD block — asserted in E2E, eyeballed
  via Aspire before merge.
- Localization: every new key present in all nine resx files (`grep -c` parity).
- `UiColorTokenTests` allowlist only shrinks.

## 11. Open

- The `<details>`-heavy static grammar (monthly row expansion, week picker, community form) has
  no field test yet at phone widths — first UX round after C3 decides if any of it wants
  promotion into the island.
- `page-dock.js` interplay with a dock that exists at first paint (no circuit re-sync): verify
  during C3; the script was written for circuit-born docks.
- Whether the suggested-chip treatment reads at Compact density (may collapse to a border tick).
- Chart-details convergence: if that branch merges first with its own App.razor conditional,
  C2 here rebases to nothing — coordinate at merge time, not before.
