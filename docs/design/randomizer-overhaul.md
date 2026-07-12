# Chart Randomizer Overhaul

Design record for the /ChartRandomizer rebuild: draw-first UX, the page-dock shell primitive,
micro-tournaments on EventCompetition, live draw spectating, and the extraction of the
Randomizer vertical. Decisions locked with the owner 2026-07-12. Mock board (six annotated
screens, Phoenix palette): https://claude.ai/code/artifact/7736a4cc-65fb-40a8-b9bb-d24b4e8f8650

## Why

The page predated the design system and violated most of it (UX rules 1, 4, 6, 7, 9, 10; see
[UX-GUIDELINES.md](../UX-GUIDELINES.md)), and its tournament integration rode the deprecated
Match subsystem via `IMatchRepository` injected straight into the page. Owner pain points:

1. **Draft scroll pain** — competitors process protects/vetoes across a scrolling grid that
   *re-sorts on every click*; tournament staff run drafts on a shared tablet players tap.
2. **Sharing** — no way to share settings TO-to-TO, no way to spectate a card draw live.
3. **Setup friction** — configuring levels means ~50 individual switches.
4. **Mix** — no mix selector; settings must never "disappear" because the site is on the
   wrong mix.

## Locked decisions

| # | Decision |
|---|---|
| 1 | **Micro-tournaments are real EventCompetition Tournaments**, created unlisted from the randomizer page. No new `TournamentType`; micro-ness is an `IsUnlisted` flag. Graduation (qualifiers posting, listing) is a flag flip on the same entity — nothing migrates. |
| 2 | Any logged-in user creates tournaments, no cap. Owner keeps curating what's highlighted/listed. |
| 3 | Roles reuse `TournamentRole` (HeadTO / TO / Assistant). Head TO invites via **role-carrying links** (optional expiry, built for "drop in a volunteer Discord channel") or manual add from public users. |
| 4 | **New `ScoreTracker.Randomizer` vertical** — settings, draws, spectator slugs. No membership model of its own; roles always via EventCompetition contracts. |
| 5 | **Staff-only protect/veto in v1** (players call, staff taps — often on a staff tablet in player hands). Current draw only, no history. |
| 6 | **Range slider is the primary level control**; per-level toggles/weights demoted behind Advanced. Gap-y level sets are meme-tourney territory, still supported there. |
| 7 | Settings sharing is decoupled from tournaments: a **share token on any saved settings** that anyone can import into their own library. |
| 8 | Spectator link is **stable per tournament** (draws swap underneath); personal draws can mint one too — tournaments are only needed when multiple operators need buttons. |
| 9 | Vetoed cards **stay in place** (dimmed + struck, red border); "Clear vetoed" is the explicit compaction and renumbers. Protected = mix-primary ring + HELD chip — deliberately *not* the owner-locked solid-green "passed" border. |
| 10 | Draw-order **number badges** on every card — drafts are verbal ("veto four"). |
| 11 | UI copy says **"Tournament"** (people say "join this tourney"), even for micro ones. |
| 12 | **Clean break on Match-tournament randomizer settings** — no data migration; the Match dropdown, the page's `IMatchRepository` use, and the match-scoped settings path are deleted. MatchSaga itself remains until its separate owner-gated deletion. |

## The shell primitive: page dock + focus mode

The bottom nav owned the bottom third unconditionally, which is why action-heavy pages
(tier lists before, randomizer now) kept fighting Rule 10. One fix, shell-level:

- **Page dock**: MainLayout renders one slot above the bottom nav at phone widths. Pages
  register a dock RenderFragment through a scoped `PageDockService` (cascading; set on init,
  cleared on dispose). No registration → today's shell, pixel for pixel.
- **Scroll-away nav**: scroll down hides the nav, scroll up (or page top) restores it. Both
  bars coexist only at rest; steady state mid-task is the dock alone. Nav items never move
  or reflow. Implemented with a small JS interop scroll listener.
- **Focus mode**: a page may request the nav dropped entirely; the shell requires an explicit
  exit affordance in return. Draft mode uses it for space *and* kiosk safety — a
  staff-authenticated tablet in player hands has no site to wander into.
- Tier Lists (and later WhatShouldIPlay quick-record) can adopt the dock; the Rule 6
  content-bar chips convention is unchanged.

## Architecture

### Randomizer vertical (mostly a move, not a build)

The randomizer already lives in Catalog: `RandomizerSaga`, `EFRandomizerRepository`,
`UserRandomSettingsEntity`, and the settings contracts. These move to
`ScoreTracker.Randomizer` (UCS template). The draw engine does not change —
`RandomizerSagaTests` move byte-for-byte.

**Transitional pin relocates**: `GetRandomChartsQuery` stays in `Application/Queries`
(MatchSaga and `ChartsController` send it), so the `→ Application` project reference moves
from Catalog to Randomizer with the same unpin condition (Match subsystem deletion). Catalog
comes fully clean. CLAUDE.md Known Divergences updates accordingly.

**New contracts** (`Randomizer.Contracts`): `CreateDrawCommand` (the page dispatches
`GetRandomChartsQuery` itself — Web may reference Application — and hands chart IDs in),
`SetDrawCardStateCommand`, `ClearVetoedCardsCommand`, `AddChartToDrawCommand`,
`GetActiveDrawQuery`, `GetDrawBySlugQuery`, `SaveTournamentRandomSettingsCommand`,
`GetTournamentRandomSettingsQuery`, share-token commands, and `DrawUpdatedEvent`
(`INotification`, in-process). Tournament-context writes authorize via EventCompetition's
`GetTournamentRolesQuery`.

**Per-pull identity**: every pulled card is a row with its own `PullId`; protect/veto is
state on the row. This deletes the index-arithmetic block in `RemoveVetoedCharts` and makes
`AllowRepeats` safe by construction. Clear-vetoed deletes rows and renumbers.

**Live updates** reuse the ImportStatus pattern (MainLayout `INotificationHandler` bridging
to a static event): mutating handlers publish `DrawUpdatedEvent`, subscribed circuits
(staff devices, spectators) re-query and re-render. State persists in SQL; restarts safe.

### EventCompetition additions

- `IsUnlisted` on the **entity only** — `TournamentRecord` is untouched, which is what keeps
  the tournaments API wire shape and `Tests.Api` frozen. `GetAllTournamentsQuery` filters
  unlisted at the repository (every existing consumer wants listed-only).
- `CreateUnlistedTournamentCommand(Name)` — creator becomes HeadTournamentOrganizer.
- `GetMyTournamentsQuery` — role-based, includes unlisted; replaces the page's
  `Type == Match` dropdown filter and its N+1 role loop.
- `TournamentRoleInvite` entity + create/redeem commands + a redeem landing route.

### API safety invariants (do not break)

- `api/charts` random endpoint builds `RandomSettings` from its own query params and returns
  `ChartDto` — decoupled from every change here. **`RandomSettings` gets no shape changes**;
  mix rides beside it (already a `GetRandomChartsQuery` parameter), and saved-settings mix is
  a column on the storage row, not a field in the JSON.
- `TournamentRecord` unchanged; unlisted rows invisible to existing queries.
- If `ScoreTracker.Tests.Api` fails during this work, something went wrong — zero expected
  changes there.

## Database (all additive)

| Table | Change |
|---|---|
| `RandomizerDraw` (new) | Id, UserId?, TournamentId?, Slug (unique), Mix, timestamps; filtered unique indexes: one active draw per user / per tournament |
| `RandomizerDrawCard` (new) | PullId PK, DrawId FK cascade, ChartId, Order, State; unique (DrawId, Order) |
| `TournamentRandomSettings` (new) | TournamentId, Name, SettingsJson, Mix |
| `UserRandomSettings` | + Mix (default Phoenix), + ShareToken (nullable, unique) |
| `Tournament` | + IsUnlisted (bit, default 0) |
| `TournamentRoleInvite` (new) | Token PK, TournamentId FK, Role, ExpiresAt?, CreatedBy |

Randomizer tables register via the vertical's `IDbModelContribution`, listed in
`VerticalModelContributions.All()`. Rows added to DATABASE-SCHEMA.md.

`ChartRandomizer__LastResults` UiSettings rows go orphaned (draw state now lives in
`RandomizerDraw`; ephemeral, not migrated). `ChartRandomizer__LastConfig` stays but
serializes `RandomSettings` verbatim — this is the fix for the Count/Ordering/CoOp-bounds
round-trip bugs (the hand-copied `SavedConfiguration` DTO dropped them).

## UX specifics carried from the mock board

- Draw-first layout: chips bar (active constraints, removable) + live "N charts match"
  (`GetIncludedRandomChartsQuery`, counted client-side, 300ms debounce) + grid + dock
  (Randomize primary, settings, copy). Density via `Density__ChartRandomizer`
  (Comfortable / Compact / Table).
- Armed-action selector (Protect / Veto / Details): in compact, tap applies the armed
  action, tap again undoes it. Loud styling — on the shared tablet the mode must be visible
  at arm's length.
- Draft focus mode: staff actions (Clear vetoed / Redraw / Add chart) live on a staff
  rail/dock with confirm-on-second-tap; exit chip explicit.
- Spectator page `/Randomizer/Live/{slug}`: anonymous, read-only, same tiles/numbers/states.
- Mix: property of the settings, chosen in the drawer header, defaults to site mix; saved
  settings list all mixes with a chip. Phoenix-only panels (letter grades, pass state)
  disable with a one-line reason on XX.
- Full `L[…]` pass — the page is currently half-localized; new keys land in all eight
  locales. Shipped typos die: "Player Baseed Settings", "Open Embeded Video", meta
  "Randommize" ×2.
- `UiColorTokenTests` allowlist entry for the page (2) burns to zero; `chart-card` retired
  for the `tier-chart-card` system.

## Testing strategy (owner directive 2026-07-12)

Lowest-granularity-possible ladder: domain/handler logic in the existing fast suites;
**component behavior in bUnit with mocked data** (new — bUnit approved for the stack in this
PR); Playwright reserved for critical end-to-end user journeys only. This PR introduces
`ScoreTracker.Tests.Components` (bUnit + xUnit + Moq, no Docker, runs with the fast suites)
in C6, covering `DrawCardTile` (state/density rendering), `ArmedActionSelector` (toggle
semantics), `LevelRangeSlider`, and drawer gating logic. HOW-TO-TEST.md and the CLAUDE.md
test tables gain the project row in the same commit. The draft-flow Playwright fact remains
a post-stabilization follow-up.

## Out of scope (fences)

No brackets, match tracking, seeding, registration, or scores on micro-tournaments
(LinkOverride already points at start.gg/Challonge). No draw history. No player-held veto
rights. No cross-mix draws. No Match-subsystem deletion in this PR. E2E for the draft flow
is a post-stabilization follow-up.

## Commit plan (single PR)

| # | Commit | Contents |
|---|---|---|
| C1 | Design doc | This file. |
| C2 | Page dock shell primitive | `PageDockService`, MainLayout dock slot + focus mode, nav scroll-hide interop. Inert (no consumers). UX-GUIDELINES.md dock/focus addendum under Rule 10. |
| C3 | Randomizer vertical extraction | New project; move RandomizerSaga, EFRandomizerRepository, UserRandomSettingsEntity, randomizer contracts out of Catalog; Catalog drops its Application ref; wiring hooks + `VerticalModelContributions.All()`; arch-test assembly lists; RandomizerSagaTests move. No behavior change. CLAUDE.md + ARCHITECTURE.md updates. |
| C4 | EventCompetition: micro-tournaments | `IsUnlisted` column + repo filter, `CreateUnlistedTournamentCommand`, `GetMyTournamentsQuery`, `TournamentRoleInvite` + create/redeem commands. Migration + handler tests + DATABASE-SCHEMA.md. |
| C5 | Randomizer: draws + settings storage | `RandomizerDraw`/`RandomizerDrawCard`/`TournamentRandomSettings` entities + migration; `UserRandomSettings` Mix/ShareToken columns; draw commands/queries/handlers; `DrawUpdatedEvent`; role-authorized tournament writes. Handler tests + DATABASE-SCHEMA.md. |
| C6 | Draw components + bUnit | `DrawCardTile` (3 densities, tier-card CSS, order badge, state treatments), `LevelRangeSlider`, `ArmedActionSelector`. Token-only styling. Introduces `ScoreTracker.Tests.Components` (bUnit) with facts for all three; HOW-TO-TEST.md + CLAUDE.md test-table rows. |
| C7 | Settings drawer | `RandomizerSettingsDrawer` absorbing RandomizerSettingsConfiguration: sliders primary, Advanced disclosure, mix select, player-panel gating, count presets, live match count w/ debounce. |
| C8 | Page rebuild | ChartRandomizer.razor draw-first: chips bar, grid on draw storage (stable order, tap actions), dock registration, density setting, busy states, delete confirm, verbatim LastConfig persistence, `+` add tile, copy export. Deletes IMatchRepository use, tournament dropdown, dead code (`ShowIncludedCharts` wiring replaced by the count, unused injections/helpers). Burns UiColorTokenTests entries; fixes typos incl. meta. |
| C9 | Tournament flows + draft mode | Create sheet, invite link management, redeem landing, tournament context switcher, TournamentRandomSettings UI, draft focus mode with staff rail + confirm taps. |
| C10 | Spectating + sharing | `/Randomizer/Live/{slug}` anonymous page, live update bridge, personal-draw share, settings share links (mint/preview/import). |
| C11 | Localization | All new keys × 8 locales, per-locale glossaries followed. |
| C12 | Docs + sweep | ARCHITECTURE.md pages table row, README/doc index if needed, API.md verification note (no surface change), final suite run. |

Each commit builds green and passes the fast suites; C4/C5 also run integration locally
(migrations). Field-test checkpoints for the owner: after C8 (personal flow), after C9
(tournament flow on a real tablet), after C10 (spectator on a second device).
