# Qualifiers Overhaul

Status: **planned**. No code written; this doc is the plan.

Collapse the two qualifier pages into **one player page**, delete the **photo-verification
ecosystem** outright, and give tournament organizers a **dedicated admin screen** where photos
live and duplicate entries get cleared. The name-matching "claim your entry" flow goes with it.

Interactive mock (real Phoenix palette, live CEO 2026 board data):
<https://claude.ai/code/artifact/b5529046-42cc-46e6-8793-7985e67b565b> — four viewer states
(Competitor / Signed out / Organizer / After cutoff) × three widths, plus a 4-chart and a
13-chart pool.

Today's pages: [`MatchTournamentQualifiers.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/MatchTournamentQualifiers.razor)
(leaderboard) and [`MatchTournamentQualifiersSubmit.razor`](../../ScoreTracker/ScoreTracker/Pages/Competition/MatchTournamentQualifiersSubmit.razor)
(submission). Both are KEEP-and-makeover in the pages-modernization audit.

---

## 1. Scope

### In

- **One route for players.** `/Tournament/{id}/Qualifiers` answers all four questions a
  competitor has — what do I play, when is it due, how am I scored, where do I stand — in that
  order. `/Qualifiers/Submit` retires and 301s to it; submitting becomes a dialog.
- **Verification deleted.** No approve step, no `IsApproved` on any player-facing surface, no
  "Pending" column. A typed score needs a photo; a score imported from the official site does
  not. Photos are organizer reference only and **players never see each other's**.
- **No photo lifecycle** (owner, explicit). Nothing marks a photo reviewed; nothing expires it.
- **Organizer admin screen** at `/Tournament/{id}/Qualifiers/Admin`: tallies, duplicate
  detection, entry list with per-submission photo review, entry + submission delete, seeding
  export, setup summary.
- **Claim-a-username deleted.** An entry belongs to the account that created it. Duplicates are
  an organizer cleanup action, not a player puzzle.
- **Leaderboard rebuilt to the house standard** — `.olb-rank-card` compact rows, the top N
  carried as chart chips (jacket + grade ring + rating), no side-scroll at 390px.

### Out (this pass)

- Scoring formulas. `UserQualifiers.Rating`/`CalculateScore` are untouched.
- Co-op teams (`SaveTeam`, `GetCoOpTeams`, `CoOpPlayer`) — same repository, different feature.
- The bracket/seeding subsystem beyond a CSV/start.gg export button.
- Tournament creation and role management (`SaveTournamentCommand`, the invite commands).

### Fixed in passing

Three defects found while auditing, cheap to fix inside this work:

1. **`/Tournament/{id}/Admin` is a dead link.** `MatchTournamentQualifiers.razor:160` renders an
   "Admin" button for `HeadTournamentOrganizer` pointing at a route **no page declares**. It
   becomes the new qualifiers admin route.
2. **`SaveQualifiersHandler` can throw on save.** It filters the leaderboard to
   `CalculateScore() > .001`, then calls `orderedNewLeaderboard.First(kv => kv.q.UserName == user)`
   — unguarded. A save that leaves the entry at zero (or a scoring type that returns 0) throws
   out of the handler.
3. **Every save reads the whole leaderboard twice** (before + after) to compute a Discord
   placement message. Fold to one read plus an in-memory apply.

---

## 2. Domain

`UserQualifiers` (`ScoreTracker.Domain/Models/`) is the only domain type that moves.

### 2.1 Approval comes out

- Delete `IsApproved` and `Approve()`. Nothing replaces them.
- `UserQualifiers.Submission` gains two fields:

  ```csharp
  public sealed class Submission
  {
      public Guid ChartId { get; set; }
      public PhoenixScore Score { get; set; }
      public Uri? PhotoUrl { get; set; }
      public SubmissionSource Source { get; set; }   // new
      public DateTimeOffset SubmittedAt { get; set; } // new
  }
  ```

- New `SubmissionSource` enum in `ScoreTracker.SharedKernel.Enums`: `Manual`, `OfficialImport`.

**Why explicit rather than inferring from `PhotoUrl == null`.** That inference works today —
`QualifiersSaga` calls `AddPhoenixScore(chartId, score, null)` for imports and the submit page's
`Validated` requires a photo — but it is a coincidence of two call sites, not a rule, and the
admin screen now renders a decision from it ("From official" vs a photo button). Make it a field.

### 2.2 The photo rule becomes an invariant

`AddPhoenixScore` currently accepts `Uri?` and always returns `true`. Split it:

- `AddManualScore(chartId, score, Uri photo, DateTimeOffset at)` — photo non-nullable, throws
  `QualifierPhotoRequiredException` (new, `Domain/Exceptions/`) if absent.
- `AddImportedScore(chartId, score, DateTimeOffset at)` — no photo, used only by the saga.

`AddXXScore` keeps its judgement signature and delegates to `AddManualScore`.

> The **XX judgement calculator already exists and ships today** (`MatchTournamentQualifiersSubmit.razor`
> Phoenix/XX radio → six counts → `ScoreScreen.CalculatePhoenixScore`). It is kept as an
> alternative entry mode, not rebuilt. `ScoreScreen.PlateText` also gives Perfect Game detection
> for free, which is what lights the PG halo on a chip.

### 2.3 Ratchets

`DomainTests/UserQualifiersTests.cs` exists and asserts approval behaviour — rewrite it for the
new surface (photo-required invariant, source assignment, `BestCharts` unchanged).

---

## 3. Application

### 3.1 The pages stop injecting the repository

Both pages inject `IQualifiersRepository` directly today. That violates the dispatch rule in
CLAUDE.md ("Razor pages… dispatch exclusively via `IMediator`. No `DbContext`, repository, or
`HttpClient` is injected into Web code") and it is what keeps the port pinned in
`Domain/SecondaryPorts`. Everything below routes through MediatR instead.

**Once Web no longer references it, `IQualifiersRepository` can move into the vertical** as an
internal port alongside `EFQualifiersRepository`. That is the architectural payoff of this
commit and should land with it, not later.

⚠ There is a **third Web consumer**: `MarchOfMurlocs.razor` injects the same port and uses it for
exactly one thing — `GetQualifiersConfiguration(id).PlayCount > 0`, to decide whether a listed
tournament shows a qualifiers link. The port cannot move until that page is converted too. It is
one call, so add a `TournamentHasQualifiersQuery(tournamentId)` (or fold the flag into the
existing tournament listing projection) and repoint it in the same commit. Budget for it; do not
discover it mid-refactor.

### 3.2 New contracts (`EventCompetition/Contracts/`)

Queries:

| Query | Returns |
|---|---|
| `GetQualifiersBoardQuery(tournamentId)` | The player board: config, ranked entries with their counting plays, entrants with no score, viewer's own standing + gap. **No photo URLs.** |
| `GetQualifiersAdminQuery(tournamentId)` | Everything the board has plus photo URLs, `Source`, `SubmittedAt`, account-vs-anonymous, and detected duplicate groups. Authorization-gated. |
| `GetQualifierChartPoolQuery(tournamentId)` | The pool alone, for the signed-out render. |

Commands:

| Command | Notes |
|---|---|
| `SubmitQualifierScoreCommand` | Manual submission; carries photo URL + source. Replaces the page's direct `SaveQualifiers` call. |
| `DeleteQualifierEntryCommand(tournamentId, entryId)` | Organizer only. |
| `DeleteQualifierSubmissionCommand(tournamentId, entryId, chartId)` | Organizer only; removes one chart from an entry. |
| `SetQualifierAutoSubmitCommand(tournamentId, userId, enabled)` | Wraps `RegisterUserToTournament`; adds the missing *off* direction. |

`SaveQualifiersCommand` stays (the saga uses it) but loses its player-page caller.

### 3.3 Authorization moves into handlers

Both pages currently compute `_myRole` in the page and branch the markup on it. The delete
handlers must re-check server-side via `GetTournamentRolesQuery` — `HeadTournamentOrganizer` or
`TournamentOrganizer`, plus `CurrentUser.IsLoggedInAsAdmin` — and throw otherwise. A hidden
button is not an authorization boundary.

### 3.4 Duplicate detection

Lives in the admin query handler, not the UI. Group an event's entries by normalized name
(case-fold, strip non-alphanumerics, collapse a trailing `_`/digits) and flag any group with
more than one entry where at least one has a `UserId` and at least one does not. Return the
group with each entry's `SubmittedAt` (first seen) so the screen can label which is stale.

`SubmittedAt` for existing rows comes from `UserQualifierHistoryEntity.RecordedDate` — the
history table already records every save — so **no column and no migration** is needed for it.

### 3.5 `SaveQualifiersHandler` repairs

Guard the `.First(...)` (item 2 in §1), and collapse the double `GetAllUserQualifiers` to one
read (item 3). Both are covered by `SaveQualifiersHandlerTests`, which exists.

---

## 4. Infrastructure

### 4.1 No migration

Worth stating plainly, because it shapes the commit order:

- `Submission.Source` / `SubmittedAt` serialize into `UserQualifierEntity.Entries`, which is a
  `string` column holding JSON. **Adding fields to `QualifierSubmissionDto` needs no schema
  change.** Old rows deserialize with defaults; backfill on read in `From(entity, config)` —
  `PhotoUrl != null ? Manual : OfficialImport`.
- Deleting an entry is a row delete. Deleting one submission rewrites the blob.
- `UserQualifierEntity.IsApproved` and `UserQualifierHistoryEntity.IsApproved` **stay in the
  schema, unread**. Per the owner's standard we do not drop; an unread column costs nothing and
  the data is a record of what happened.

So: no `dotnet ef migrations add`, no `docs/DATABASE-SCHEMA.md` row changes.

### 4.2 Repository additions

`IQualifiersRepository` gains, with `EFQualifiersRepository` implementations:

```csharp
Task DeleteQualifiers(Guid tournamentId, Guid entryId, CancellationToken ct = default);
Task<IEnumerable<UserQualifierSnapshot>> GetQualifierHistory(Guid tournamentId, CancellationToken ct = default);
Task UnregisterUserFromTournament(Guid tournamentId, Guid userId, CancellationToken ct = default);
```

`GetQualifierHistory` is what feeds `SubmittedAt`; it reads `UserQualifierHistoryEntity`, which
nothing currently reads back.

### 4.3 Standing debt (flag, don't fix here)

`EFQualifiersRepository` opens with a hardcoded `ChartIds` set of six GUIDs and a `Modifiers`
dictionary containing a single `106` note-count adjustment — leftovers pinned to one historical
tournament. They are not in scope but should be understood before anyone edits that file; a
follow-up should move them onto `QualifiersConfiguration.NoteCountAdjustments`, which is the
supported mechanism and is already populated per-chart.

---

## 5. Presentation

### 5.1 Routes

| Route | Now | After |
|---|---|---|
| `/Tournament/{id}/Qualifiers` | Leaderboard | **The** player page |
| `/Tournament/{id}/Qualifiers/Submit` | Submission page | 301 → the player page |
| `/Tournament/{id}/Qualifiers/Admin` | — | **New** organizer screen |
| `/Tournament/{id}/Admin` | Dead link | 301 → the admin screen |

### 5.2 Files

Rename while we are here — `MatchTournament*` misleads (these pages have nothing to do with the
deleted Match ecosystem):

- `MatchTournamentQualifiers.razor` → `Qualifiers.razor`
- `MatchTournamentQualifiersSubmit.razor` → deleted; its dialog content moves into components
- new `QualifiersAdmin.razor`

⚠ `UiColorTokenTests.Allowance` is **keyed by file path** (`Pages/Competition/MatchTournamentQualifiers.razor` = 1,
`…Submit.razor` = 4). Renaming breaks those keys — but this overhaul removes every literal
(`Colors.Green.Darken1`, `Colors.LightBlue.Darken1`, `Colors.Gray.Darken1`), so both entries get
**deleted** rather than re-keyed, and the ratchet tightens. Do it in the same commit.

### 5.3 Player page composition

Top to bottom, and this order is the point:

1. **Status strip** — event name, Open/Closed pill, live countdown coloured on the thresholds the
   code already branches on (>14d info, >7d warning, else error). Today `RemainingTime` is
   computed on the leaderboard and *never rendered*.
2. **Your standing** — place, total, exact gap to the rung above, primary action. Today
   `_currentPlace`/`_nextPlace` are computed on every save and never rendered.
3. **Auto-submit strip** — on/off. Copy: *"Auto-submit posts qualifier scores it finds in your
   imports, so you don't have to enter them twice."*
4. **Standings** — `.olb-rank-card` rows; place, name, chip strip, total. Expand for the
   breakdown.
5. **The charts** — the pool, rendered **unconditionally including signed-out**. Four states
   (counting / not counting / suggested / not played), each with a ring, a word, and a legend
   entry; the legend lists only states present on screen.
6. **How this is scored** — one plain sentence, plus the event links row (rules / start.gg /
   Discord) as **configuration**, replacing `@if (_tournamentName == "BITE 7")`.

### 5.4 New shared components (`Components/`)

- **`QualifierChip`** — one play: jacket, letter grade as the **ring** around it, rating beneath.
  Ring colour reads `MixThemes.GradeVar(grade)` — the `--grade-*` tokens already exist and
  already encode the game's metal ladder, so this adds no colour. Perfect Game adds the
  `--plate-pg` halo. ⚠ The ladder is **not injective**: SS+/SS, S+/S, AA+/AA and A+/A each share
  one hex (SSS+/SSS and AAA+/AAA are distinct). Owner decided against a printed key; the rating
  under each chip and the `title` carry the exact values.
  - Chip budget: **3** below 500px, **8** below 900px, **10** above. Past that the row renders an
    explicit `+N`, never a silent truncation like today's `PlayCount > 4 ? 4` cap.
- **`QualifierPoolGrid`** — auto-fill on a minimum track (`repeat(auto-fill, minmax(var(--card-min), 1fr))`);
  the tile floor drops from 172px to 108px once the pool passes six, so **13 charts** lay out
  without a second mode.
- **`QualifierSubmitDialog`** — photo → score → save, with the score/judgement mode switch. The
  submit button states its own blocker ("Add a photo to submit" → "Enter a score" → "Submit
  score").

Reuse, do not rebuild: `DifficultyBubble`, `ScoreBreakdown`, `ChartDetailsDialog`,
**`ChartVideoPlayer`**. The play badge is `.chart-jacket-play` from `site.css` — centred disc,
hairline ring, CSS triangle, `pointer-events: none` — and tapping a jacket opens the chart dialog
with the video **already autoplaying** (`?autoplay=1`, which `ChartVideoPlayer` already does).

### 5.5 Admin screen composition

Admin bar (with a jump to the player page) → tallies (entries / scored / photos / duplicates,
duplicates flagged when non-zero) → duplicates panel (the pair side by side, keep vs delete) →
entries list → seeding export (CSV + copy-for-start.gg) → setup summary.

Each entry row: name, Account/Anonymous chip, "N charts · M photos", total, delete. Expanding
shows each submission with chart, grade, claimed score, and **either** a photo button **or** a
"From official" badge. The photo dialog shows the shot, upload time and size, the claimed
score/grade/rating, an explicit *"Only you and other organisers can see this"*, and **Remove this
score**.

Delete confirms with contents — whose entry, which charts, what it was worth — and says plainly
that it cannot be undone.

### 5.6 Render mode

Both pages declare `@rendermode RenderModes.Interactive` today and keep it: the board updates
after a submit, and the dialogs are interactive. The player board is a reasonable future SSR
candidate (public, crawlable, links to chart pages) but converting it is not in this scope — see
[render-modes.md](render-modes.md) §7.1. `RenderModeDeclarationTests` ratchets the declaration
either way.

### 5.7 Localization

Every string goes through `L[…]`. The submit page currently ships **~30 hardcoded English
strings** — all six judgement labels, every snackbar, the auto-submit and rename blocks, the
photo warning, the import results. Most of those blocks are being deleted; whatever survives
gets keys.

New keys land in **all nine locales in the same pass, inserted alphabetically**
(`ResxKeysAreStoredAlphabetically`, `OrdinalIgnoreCase`), obey each `LOCALIZATION-<locale>.md`
glossary, and must not differ from an existing key only by case (`LocalizationKeyTests`). `en-ZW`
is at parity and gets real Murloc values from the `a b g l m o p r u` alphabet.

⚠ `"Play Num"` is currently `"Play at least {0}"` — a dangling sentence missing its noun, in every
locale. Fix the value, keep the key.

---

## 6. Deferred / open

- **Photo retention.** Nothing deletes qualifier photos from blob storage, before or after this
  change. Out of scope, but the delete paths make it more visible: deleting an entry drops the
  row and orphans its blobs.
- **Duplicate detection quality.** The normalization in §3.4 is a first cut. Without the name
  gate, duplicates get *easier* to create (submit anonymously Saturday, sign in Sunday), so this
  is doing real work. If it proves noisy, tighten to "flag only when one side has a `UserId`".
- **Soft delete.** Hard delete per the owner's call. If a mis-click ever needs recovering, the
  `archive` schema pattern is the house answer.
- **The organizer's own submission.** A TO who also competes sees the player page and the admin
  screen separately; no attempt is made to merge them.
- **Export shape.** CSV columns and the start.gg paste format are unspecified — needs one round
  with an actual TO before building.

---

## 7. Commit order

Each commit is independently mergeable, builds green, and lands its own tests + doc updates.

1. **Domain: drop approval, add submission source.** Delete `IsApproved`/`Approve()`; add
   `SubmissionSource`, `SubmittedAt`, `AddManualScore`/`AddImportedScore` +
   `QualifierPhotoRequiredException`. Rewrite `DomainTests/UserQualifiersTests.cs` for the photo
   invariant and source assignment. Pure — no infra, no UI. Nothing reads the new fields yet.
2. **Infra: DTO fields, delete, history read.** `QualifierSubmissionDto` gains the two fields
   with backfill-on-read in `From(...)`; `DeleteQualifiers`, `UnregisterUserFromTournament`,
   `GetQualifierHistory` on the port + `EFQualifiersRepository`. Integration test (real SQL) for
   blob round-trip of old-shape rows, entry delete, and submission delete. **No migration** —
   assert that in review.
3. **Application: contracts + handlers.** The §3.2 query/command set with organizer
   authorization in the handlers; repair `SaveQualifiersHandler` (guarded `.First`, single
   leaderboard read); duplicate detection. `ApplicationTests` with mocked ports, including a
   **non-organizer delete is rejected** test.
   *Port move is deliberately not here* — `IQualifiersRepository` still has three Web consumers
   until commits 4 and 5 land (§3.1). Do the move in commit 6, once
   `grep -rl IQualifiersRepository ScoreTracker/ScoreTracker/` comes back empty; that is the
   gate. `VerticalBoundaryTests` and `LayerDependencyTests` are what confirm it.
4. **Presentation: the player page.** Rename to `Qualifiers.razor`; fold the submit page in as
   `QualifierSubmitDialog`; build `QualifierChip` + `QualifierPoolGrid`; wire the status strip,
   standing panel, auto-submit strip, board, pool, scoring line, event-links config. Delete the
   BITE 7 hardcode and both `UiColorTokenTests` allowlist entries. 301 the old submit route.
   `Tests.Components` (bUnit): chip budget by width, pool state + legend agreement, signed-out
   renders the pool, no verification wording. Localization pass.
5. **Presentation: the admin screen.** `QualifiersAdmin.razor` at the new route; 301 the dead
   `/Tournament/{id}/Admin`; tallies, duplicates panel, entries + per-submission photo review,
   both deletes with confirms, export buttons, setup summary. `Tests.Components`: a
   non-organizer gets nothing, photo URLs never render on the player page, "From official"
   shows for imported submissions. Localization pass.
6. **Unpin the port, E2E, docs.** Convert `MarchOfMurlocs.razor`'s single
   `GetQualifiersConfiguration` call to a query, then move `IQualifiersRepository` out of
   `Domain/SecondaryPorts` into the vertical — no Web consumers remain by this point. One
   Playwright path (submit a score with a photo, see the board move), since qualifier submission
   is a critical whole-workflow journey; everything else stays at the component level per the
   granularity ladder. Update the docs in §8 and fill in §9.

Commits 1–3 ship no user-visible change; the page keeps working off the old surface until 4.

---

## 8. Docs to touch (per the same-PR rule)

- `docs/ARCHITECTURE.md` — the Competition row in the pages table (routes change); the
  `IQualifiersRepository` move out of `Domain/SecondaryPorts`.
- `docs/UX-GUIDELINES.md` — the grade-ring encoding is new vocabulary; note it under rule 8 with
  the non-injective ladder caveat, and burn the two allowlist entries in §3 Enforcement.
- `docs/API.md` — only if any of the new queries get an `api/*` endpoint. None planned.
- `CLAUDE.md` — no convention change expected; if the port move lands, the vertical table's
  "published ports" line may need a word.
- **Not** `docs/DATABASE-SCHEMA.md` — no schema change (§4.1).

---

## 9. As built — deviations from the plan

Commits 1–6 landed as planned. What differs from §1–8:

- **The port move slipped from commit 3 to commit 6**, and had a third Web consumer the plan
  found late: `MarchOfMurlocs.razor` used `GetQualifiersConfiguration(id).PlayCount > 0` to decide
  whether to show a qualifiers link. That became `TournamentHasQualifiersQuery`. Only after
  commits 4–5 removed the page injections did `grep -rl IQualifiersRepository ScoreTracker/ScoreTracker/`
  come back empty, which is the gate the move actually needed.
- **No `QualifierPoolGrid` component.** The pool grid is markup on the page; it has one caller and
  extracting it bought nothing. `QualifierChip` and `QualifierSubmitDialog` are components as
  planned.
- **The "suggested next chart" state is a restored regression, not a new feature.** The audit
  concluded there was "nothing to port, only a spec to invent" — that was wrong, and the owner
  caught it. `git log -L` on `CardStyle` shows the highlight shipped and worked: the line was

  ```csharp
  _suggestedCharts.Contains(chartId) ? $"border-color:{Colors.LightBlue.Darken1}" :
  ```

  with no guard, so suggested charts carried a light-blue border in ordinary pooled tournaments.
  **`d7173f2d` ("Added support for an AllCharts qualifier leaderboard", 2025-06-16)** added
  `_configuration.AllCharts &&` to that style *and* gated the legend on `!_configuration.AllCharts`
  — two edits in one commit, in opposite directions. From that day the highlight could only fire
  in AllCharts mode, where the grid renders `BestCharts()` (submitted charts only) while
  `EvaluateRecommended` strips submitted charts from the set, so it fired nowhere. The feature was
  dark for ~13 months and the caption stayed on screen the whole time.

  Restored in `QualifiersBoardSaga.SuggestFor`, faithful to the original algorithm: the chart one
  rung above anything already SSS'd, plus the pool charts the player's own scores predict they
  will rate highest (folder average standing in where they have never played, at a ten-play
  minimum), minus anything already submitted. One improvement: the grade floors read
  `config.Mix` rather than a hardcoded `MixEnum.Phoenix`, which matters now that P2's bands differ.
  `ASuggestedChartIsPaintedAndAppearsInTheLegend` is the assertion whose absence let it rot.
- **`AddPhoenixScore` was removed outright** rather than kept as a shim. Its four call sites moved
  to `AddManualScore`/`AddImportedScore` in the same commit, so nothing needed a transitional
  overload.
- **Deleting one submission needs no port method.** The handler loads the entry, drops the chart
  from the dictionary and saves — the blob rewrite covers it. `DeleteQualifiers` is only for
  whole entries. Saved through the repository rather than `SaveQualifiersCommand`, so removing a
  score does not announce a placement change to Discord.
- **A Perfect Game is derived, not stored.** Every step perfect lands on exactly 1,000,000 and no
  other judgement mix can (any great drops the weighted numerator below the note count), so the
  chip's plate halo reads off the score and needs no extra field.
- **Three of the 57 player-page keys collided by case** with keys already in the resx — `Max Combo`
  vs `Max combo`, `Not Played` vs `Not played`, `Phoenix Score` vs `Phoenix score`. The existing
  casing wins and the markup was repointed. This is the `resx-case-collision` class of bug: the
  compiler would have kept one and silently rendered English in all eight other locales.
- **E2E covers routing and render, not the photo round trip.** Five tests: both 301s, the
  anonymous board-plus-pool render against a real database, no photo URL in a player's response,
  and the admin route refusing a non-organiser. Driving a real file upload through Playwright was
  judged to add little over the component and handler coverage; the submit path is covered at
  those levels. The plan said "submit a score with a photo, see the board move" — that specific
  journey is **not** E2E-covered.
- **New E2E seed helpers**: `SeedTournamentAsync`, `SeedQualifiersConfigurationAsync`,
  `SeedQualifierEntryAsync`. ⚠ `TournamentEntity.Type` must be a real `TournamentType`
  (`Stamina`/`Match`/`CoOp`) — seeding `"Qualifiers"` throws inside `GetAllTournamentsQuery` and
  the page renders as an empty shell with no error anywhere on screen.

### Still open

- **Photo retention** — nothing deletes qualifier blobs, before or after this change. Deleting an
  entry drops the row and orphans its photos.
- **Duplicate detection quality** — the normalization is a first cut (case-fold, strip
  non-alphanumerics, trim trailing digits) and only flags a group when one side has an account and
  one does not. Without the name gate, duplicates get easier to create, so this is doing real work.
- **Export shape** — the seeding export in §5.5 of the plan is not built. CSV columns and the
  start.gg paste format need one round with an actual TO first, so the admin screen ships without
  it rather than guessing.
- One component-suite run failed once mid-build and passed on five consecutive re-runs; the test
  name was not captured. Watch for it in CI.
