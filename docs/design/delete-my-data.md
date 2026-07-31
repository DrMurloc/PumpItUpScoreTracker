# Delete my data

Status: **owner-workshopped 2026-07-30, not yet built.** Replaces the one-button, two-checkbox
Danger Zone with three distinct operations — undo an import, delete a chosen scope, delete the
account — and closes the gaps in the account purge that self-serve deletion would otherwise
expose.

Companion specs: [score-truth-model.md](score-truth-model.md) (the journal model this builds on —
in flight on `claude/score-journal-storage-8183f9`), [login-overhaul.md](login-overhaul.md)
(the merge purge chain this reuses).

## 1. Context

Today the whole feature is one button and two checkboxes in the `/Account` profile panel: *Delete
All Scores*, an acknowledgement, and *"(Optional) Also delete historical data."* It deletes every
mix at once and no player can tell what the second checkbox means.

The requests behind it are not one operation:

- **"I imported the wrong card."** The most common one by far. The player does not want their
  data deleted — they want the last twenty minutes undone.
- **"I did a few imports over a few weeks that I regret."** Same shape, wider window.
- **"Delete my Phoenix 2 scores, keep Phoenix."** Scope by mix.
- **"Delete everything and my account."** Currently impossible without asking the owner.

One control cannot serve those, and the current one serves the first — the common one — worst of
all: it is strictly worse than doing nothing.

## 2. The model

- **D1 — three operations, not one.** *Undo* removes one session. *Delete* removes a chosen scope
  permanently. *Delete account* removes everything and the `User` row. They are separate routes
  with separate confirmations.

- **D2 — undo is the default suggestion.** The delete page opens with an undo banner and its
  destructive form inert. Arming the form is an explicit act (§5).

- **D3 — a session is a row, not a GUID.** `SessionId` exists today only as an in-memory grouping
  key. It becomes a real table (§4), which is what gives undo a wall-clock import time — the
  journal's `OccurredAt` is the *site's* play date and cannot answer "what did I import on
  Tuesday."

- **D4 — the undo floor is `2026-08-01T05:00:00Z`.** Nothing before it can be undone and nothing
  is backfilled. Sessions that predate the table have no true import time, and inventing one from
  play dates would reintroduce the exact confusion D3 removes.

  **The copy must not print that date.** The floor is a guard; the real cutoff is whenever phase 2
  deploys, which is later. "Sessions from before 1 August 2026 can't be undone" would be true and
  still mislead — it implies sessions exist from 1 August, and none do until the table ships. The
  empty state and the list footer say *before we started recording sessions* instead, or name the
  earliest session actually on file.

- **D5 — undo is surgical, not a rewind.** It removes *that one session* and nothing else. Every
  other session — including newer ones — is untouched, and sessions can be undone in any order.
  It is not "restore to this point in time," and the copy must never let anyone read it that way
  (§6).

- **D6 — undo replays, it does not delete-and-hope.** Dropping a session's journal rows leaves the
  wrong scores standing as the record. Undo drops the rows, then recomputes each affected chart's
  best from the surviving journal rows through `BestAttemptPolicy` — the same policy the live
  import uses — and removes the record entirely where nothing survives. Independence falls out of
  this for free: a chart a later session also improved keeps the later score, because that row is
  still there to be replayed.

  **Replay reads every surviving play, not just the flagged ones.** Since PR #209 the journal also
  carries plays that never became a record, so the winner is whatever `Beats` selects across the
  lot — filtering on `IsBest` first would drop the very row that is about to become the new best.

  **And the survivors' `IsBest` is raised to match.** Removing rows can promote a surviving play,
  and `SessionFeedHandler` renders that flag — leave it stale and the page marks the wrong play as
  the record. Only *raising* is ever needed: undo removes rows, so a survivor can gain best-ness
  and never lose it. `IScoreJournalRepository`'s own contract already sanctions exactly this —
  *"rows are never updated except to raise IsBest."*

- **D7 — everything a session produced travels with it.** Journal rows, `ScoreHighlight`,
  `PlayerMilestone`. A milestone is not recomputed from scores, so leaving it behind would strand
  a "you hit Expert" card for a title the player no longer holds.

- **D8 — the journal is deletable by the player.** [score-truth-model.md §3](score-truth-model.md)
  says rows "are never updated or deleted by the application." That is about the *write* path —
  the importer must not rewrite history. A player asking for their data to be gone is the
  sanctioned exception, alongside that doc's own §6 repair. The sentence gets amended in whichever
  PR lands second.

- **D9 — derived state is never a checkbox.** Pumbility, titles, folder lamps and player stats are
  recomputed from scores. They cannot outlive their inputs, so they are consequence copy, never a
  choice the player is offered.

- **D10 — the grace period is account-only.** A scoped delete is immediate. Account deletion is
  soft for 7 days, reusing the merge grace mechanism.

- **D11 — a delete is not a promise until the purge is complete.** Self-serve account deletion
  cannot ship until §9's gaps are closed. Eleven user-owned tables and two whole verticals are
  currently missed by the purge, including the encrypted piugame credential key.

## 3. The three operations

| | Undo | Delete scope | Delete account |
|---|---|---|---|
| Route | `/Account/Data/Undo` | `/Account/Data/Delete` | dialog off the delete page |
| Scope | one session | mix × selected items | everything |
| Reversible | n/a (it *is* the undo) | no | 7-day grace |
| Confirm | preview + button | type username | type username |
| Floor | 2026-08-01 | none | none |

Both routes live under one **Your Data** frame carrying a two-item switcher, the pattern
`OfficialSectionFrame` already establishes. The `/Account` Danger Zone links to **Delete** — that
is what a player goes looking for — and the banner does the redirecting.

## 4. `ScoreSession`

New table, owned by ScoreLedger.

| Column | Notes |
|---|---|
| `Id` | the existing `SessionId` GUID |
| `UserId`, `MixId` | |
| `Source` | `manual` \| `officialImport` \| `csv` |
| `AccountTag`, `CardId` | official imports only — **which card this came from** |
| `StartedAt`, `LastActivityAt` | **wall clock**, not the site's play date |
| `ScoreCount`, `NewCount`, `UpscoreCount` | denormalized for the list |

`AccountTag` and `CardId` are the answer to "wrong card" — the phrase is literal, and the tag is
the only thing on the row that tells a player *whose* scores those are. Both already ride
`StartOfficialImportCommand` → `RunOfficialImportCommand` into the background job, so recording
them at session creation costs nothing. The tag displays; the card id is a tiebreak for players
running several cards on one account, and rides the `title`.

`ScoreEventJournal.SessionId` stays a loose key rather than a foreign key — see §15 for why the
constraint cannot be added without breaking `IsBest` updates on historical rows. `ScoreHighlight`
and `PlayerMilestone` keep theirs loose too: they live in PlayerProgress, and a cross-vertical FK
would couple two verticals' entity models for no gain.

**Write path.** [`GetOrExtendSession`](../../ScoreTracker/ScoreTracker.ScoreLedger/Infrastructure/PlayerScoreBatchAccumulator.cs)
is called on *every* submission including no-ops, so a row write per call would put thousands of
writes on the import path. Insert on session creation; update `LastActivityAt` and the counts at
batch drain, which is already a checkpoint every two minutes. The accumulator stays the source of
session identity — the table records what it decided.

**Reads.** The undo list queries this table directly. Today
[`GetSessionGroups`](../../ScoreTracker/ScoreTracker.ScoreLedger/Infrastructure/EFScoreJournalRepository.cs)
pulls every journal row a user has and groups in memory — a cost that gets materially worse once
the journal starts carrying non-best plays. The Sessions page moves onto the table in the same
pass.

## 5. The delete page

**The gate.** The page renders the full delete form immediately, dimmed and disabled, under a
banner:

> **Had a bad import?** Undoing puts your scores back the way they were before it — nothing is
> lost. → **Undo an import instead**

Below it, one checkbox: *"No — I want to permanently delete data."* Checking it arms the form;
unchecking re-disables it.

The form is visible-but-inert rather than hidden behind an expander on purpose. A hidden form
makes people hunt, and hunting makes them determined; a visible one lets them read what it offers
and conclude for themselves that undo is what they wanted. The guard is one deliberate act, and it
is reversible.

**Scope.** A mix selector — All / Phoenix / Phoenix 2 / XX, listing only mixes with data — above
two buckets. Each bucket header is a tri-state master; open it to cherry-pick.

**Scores** (mix-scoped)

| Item | Removes |
|---|---|
| Best scores | `PhoenixRecord`, `BestAttempt` (XX), `PhoenixRecordStats` |
| Play history | `ScoreEventJournal` |
| Rating history | `PlayerHistory` |
| Session roundups & highlights | `ScoreHighlight` |
| Milestones | `PlayerMilestone` |
| *automatic* | `PlayerStats`, `UserHighestTitle`, `PlayerFolderLevel`, and a `PlayerScoresUpdatedEvent` per affected mix |

**Contributions** (all mixes — the mix selector greys out)

| Item | Removes |
|---|---|
| Tier list votes | `UserTierListEntry` |
| Chart difficulty ratings | `UserChartDifficultyRating` |
| Chart preference ratings | `UserPreferenceRating` |
| Co-op ratings | `UserCoOpRating` |
| Weekly & Daily Step entries | `WeeklyUserEntry`, `UserWeeklyPlacing`, `DailyStepEntry`, `UserDailyStepPlacing` |
| Tournament results | `UserTournamentSession`, `UserTournamentRegistration`, `UserQualifier`, `PhotoVerification`, `TournamentRole` |
| Community memberships | `CommunityMembership`, `CommunityHighlight` |

**The button names the blast radius** — `Delete 3,198 Phoenix scores`, not `Confirm`. The count is
the guard; it is worth more than another acknowledgement checkbox, and it fixes the current page's
worst property, which is that nobody can tell what they are about to lose.

Counts come from ScoreLedger for the score bucket. The contributions bucket shows category names
without counts — five new cross-vertical contract queries to print five numbers is not worth it.

**Export first.** `Download Scores` already exists in the profile panel. It appears in this flow
too, beside the banner.

## 6. The undo page

**It is called Undo, not Revert** (owner, 2026-07-30 — "it's confusing to me what clicking Revert
does"). *Revert* reads as a rewind to a point in time, which invites three wrong questions at
once: does this remove the session, restore *to* the session, or roll back everything since?
*Undo* carries one meaning — remove the effect of this one thing — and it is the meaning D5
actually implements.

The page states the semantics once, above the list, rather than leaving them to be inferred:

> Undoing a session removes only the scores it added and puts back what you had before it. Other
> sessions aren't affected, including newer ones, and you can undo them in any order.

**Session kinds** are named for what a player recognizes, not for the `Source` enum:

| `Source` | Label | Row shows |
|---|---|---|
| `officialImport` | **PIUGAME** | the game tag imported from, card id on the `title` |
| `csv` | **CSV upload** | the row count |
| `manual` | **Manual** | the row count |

"Played" was the first draft and it is wrong — nobody plays on this site. `manual` is scores typed
into one of the four record forms, and it reads as *Manual*.

**One wrinkle worth knowing**: `manual` also covers the public API's `RecordScore`, so a partner
tool posting on your behalf shows up as Manual too. Splitting it into `manual` and `api` at the
session level is possible — the session knows which route wrote it — but it is a `Source` change
that ripples past this feature, so it is deliberately out of scope here.

## 7. Responsive

Widths are the owner's field-test matrix — **desktop 1280, tablet 768, phone 390**
([static-shell.md](static-shell.md)). The shell switches to bottom nav at `960` (`Md`), so
**tablet sits on the mobile side of every rule below**, not the desktop side.

- **The destructive button moves into the page dock below 960** (rule 10). It is the primary action
  on an action-heavy page, and on a phone the form is long enough that an inline button lands below
  the fold behind a scroll. The dock renders it **disabled rather than absent** while the gate is
  unchecked — a control that pops into existence shifts the page under the thumb already reaching
  for it.

- **Above the fold at 390×844** (rule 1) is the banner, the gate, and the first row of mix chips.
  The page's job is *choose undo or delete*; that choice is the answer, so it comes before the
  scope controls.

- **Session rows reflow to two lines below 960.** Kind chip, game tag and mix on the first; time and
  counts on the second; Undo right-aligned beneath. Wrapping, never side-scrolling (rule 5). At
  1280 they stay single-line.

- **Every dialog here uses `MaxWidth.False`.** `charts.scss` line 1 carries
  `.mud-dialog-width-sm { max-width: none !important; }` **site-wide**, so a `MaxWidth.Small`
  dialog has its cap stripped and renders near-viewport-width — `!important` beats a component's
  own rule, so adding one and assuming it works is the trap. The cap lives in each dialog's own
  stylesheet. Below 960 they go full-bleed with actions pinned to the bottom.

- **The confirm input turns off the keyboard's help**:
  `autocapitalize="off" autocorrect="off" autocomplete="off" spellcheck="false"`. Otherwise iOS
  capitalizes the first letter of the username, the match fails, and it reads as the site refusing
  to delete the account.

- **+40% text** (rule 7): item labels wrap, counts never do (`flex: none`, tabular numerals). The
  blast-radius button wraps to two lines rather than truncating — the number *is* the guard, so it
  must never be the part that gets cut. Worst case is pt-BR and fr-FR on "Session roundups &
  highlights."

- **No density toggle.** Rule 5 governs collection pages; this is a form. Three modes here would be
  three ways to render seven checkboxes.

- **Tablet-specific**: the buckets stay one column. Two columns at 768 would put the Contributions
  bucket beside Scores and imply they are alternatives rather than independent scopes.

## 8. Account deletion

**Confirm.** A dialog requiring the player to type their username — not a literal `DELETE`, which
needs translating into nine locales and is awkward to type in ko/ja. Case-insensitive, with the
exact string shown to copy.

**On confirm.** `IsPublic = false`, game tag cleared, claims invalidated, signed out, a durable
row records `PurgeAfter = now + 7 days` and the pre-deletion snapshot. This is the merge grace
mechanism (`MergeRequest` + `RetiredUserSnapshot`) with no survivor — the login overhaul already
called this out: *"merge is 'purge + move logins,' delete is just 'purge.'"* The existing daily
`process-account-purges` job drives it, including the week of idempotent re-fires that makes an
in-memory-bus crash mid-purge self-healing.

**During the window** the account works normally — it is invisible and scheduled, not locked. A
half-locked state buys nothing and doubles the surface.

**The doomsday dialog.** Signing in during the window raises a modal: the countdown, the date, and
a button to the account page. It does **not** auto-cancel — signing in is not consent to keep an
account, and a silent cancel would let a stray login quietly undo a deliberate decision. It fires
on every sign-in during the window. `/Account` carries a persistent banner with the same countdown
and the actual **Cancel deletion** control.

**Cancel** restores the snapshot: `IsPublic`, game tag, and the purge row marked undone.

### 8.1 Owning a community blocks deletion

Owner call, 2026-07-30. A community is other people's, so the creator hands it over **themselves**
rather than having the system pick an heir. Two guards, and they close a loop:

- **You cannot request deletion while you own a community.** `RequestAccountDeletionCommand`
  returns a refusal listing what you own. It is not a disabled button — the dialog explains, and
  routes to the two exits that already exist: `TransferCommunityOwnershipCommand` and
  `DeleteCommunityCommand`. A sole owner of an empty community deletes it; nobody is dead-ended.

  **"Choose a new creator" is a link, not a picker.** The transfer UX already ships as a
  **Make Creator** button on each member's row in
  [`/Community/Members`](../../ScoreTracker/ScoreTracker/Pages/Communities/CommunityMembers.razor),
  gated on `_myRole == Creator` and the target being `Member` or `Admin`. That is the right shape
  and gets reused rather than rebuilt (rule 3): you can only transfer to someone already in the
  community, so the control is a bounded list, not a search over site users — and the members page
  is where you can see who is an admin and what they hold before choosing. The blocker links to
  `/Community/Members?communityName=<name>`.

  The button only renders when another Member or Admin exists, which lines up with the blocker
  offering **Delete community** instead for a solo-owned one.

  ⚠ **Make Creator has no confirmation today** — one click hands away the only creator seat, and
  `Transfer()` dispatches straight through. That was survivable while the only route in was a
  deliberate visit to the members page; account deletion now sends people there on purpose. Add a
  confirm in the same pass.
- **A flagged account cannot acquire one.** While a deletion is pending, `CreateCommunityCommand`
  refuses, and so does any path that would make that user creator — `TransferCommunityOwnershipCommand`
  checks the **recipient**, not just the sender. Without the second guard the first is decorative:
  request deletion while owning nothing, then get handed a community on day three of the window and
  it evaporates on day seven.

Both guards lift automatically on cancel, because they read the pending row rather than a copied
flag.

**Where each check lives.** `Communities → Identity` is a direct reference, so the two Communities-side
guards query Identity's `GetPendingAccountDeletionQuery` directly and pass the answer into the
aggregate as a fact — community authorization lives in the aggregate, and the aggregate does no I/O.

The Identity-side check cannot work that way: Identity must not reference Communities, or the
assemblies cycle. It goes through the **published port** `ICommunityReader`
(`Domain/SecondaryPorts/`), which is exactly the escape hatch `IDiscordFeedReader` already
established for OfficialMirror → Communities. One new method, `GetOwnedCommunities(userId)`.

**System communities are excluded.** World and the per-country communities are auto-joined and
nobody can transfer them; `CommunityOverviewRecord` already carries the regional flag that
distinguishes them. Counting them would block every account permanently.

## 9. Purge gaps

The purge fan-out was built for merge, where the retired account's data was already merged away and
leftovers were cosmetic. Exposing it as self-serve deletion makes every gap a broken promise. All
of these are missed today:

| Vertical | Missed |
|---|---|
| PlayerProgress | `ScoreHighlight`, `PlayerMilestone`, `PlayerFolderLevel`, `PlayerSeasonRecap` |
| WeeklyChallenge | `DailyStepEntry`, `UserDailyStepPlacing` |
| EventCompetition | `UserQualifier` |
| Communities | `CommunityHighlight` |
| Identity | `UserImportCredentialKey` — the encrypted piugame credential |
| Randomizer | `UserRandomSettings`, `RandomizerDraw` — **no purge consumer at all** |
| HomePage | `HomePage` and its widget instances — **no purge consumer at all** |

`PlayerStats` and `UserHighestTitle` are deleted per-mix for Phoenix and Phoenix 2 only, so rows
for any other mix survive a purge.

Two rows in that table are **not** deletions:

- **`OfficialPlayer.UserId`** mirrors a public piugame leaderboard entry that exists whether we do
  or not. Deleting it would corrupt the mirror; the link is nulled and the row stays.
- **`Community.OwningUserId`** is a *twelfth* missed table, and the one that is a product question
  rather than a coverage bug — it is also the reason the ratchet must match `*UserId`, not `UserId`
  (§15). Deleting a community because its creator left would take everyone else's community with
  it. **Owning a community blocks account deletion outright** (§8.1, owner 2026-07-30): the player
  hands each one over or deletes it first. So the purge never encounters an owned community, and
  the row needs no purge behavior at all.

Randomizer and HomePage need `AccountPurgeConsumer` + `IAccountPurgeRepository` pairs following
the existing six. A ratchet test asserting every entity with a `UserId` column is named by some
vertical's purge — with an explicit exemption list carrying `OfficialPlayer` and its reason —
stops the next vertical from reopening the hole.

## 10. The re-import trap

score-truth-model's new journal key `(UserId, MixId, ChartId, OccurredAt)` means a re-imported play
collapses onto the same row. So undoing a wrong-card import and then importing again brings all of
it straight back, and the player will read that as the undo having failed.

The undo confirmation names it, and — when the account has a stored credential — offers to forget
it in the same step. A fix that lasts until the next click of Import is not a fix.

## 11. Copy

Nine locales, keys inserted in alphabetical position
([resx rules](../../CLAUDE.md)). Three things the copy must not do:

- **Never print a raw exception.** `DiagnosticExposureTests` already forbids it outside
  `Pages/Admin/`. A failed undo says what the player can do next.
- **Never say "permanently" about something reversible, or omit it from something that is not.**
  Undo is not deletion; a scoped delete has no undo; account deletion has seven days. Each screen
  says which one it is.
- **Never imply undo is a rewind** (§6). No "restore to", no "roll back to", no "as of" — those
  all say the thing D5 does not do.

Empty states name the action, per UX rule 9 — a player with nothing to undo sees why (nothing
imported since August 1), not a blank list. The mock arrives populated; the build must not seed
anything.

## 12. Test plan

- **Unit (`DomainTests/`)** — the undo replay: a session's rows removed and the survivors replayed
  through `BestAttemptPolicy` yields the pre-session best; a chart whose only rows were in the
  session yields no record; a chart a *later* session also improved keeps the later score; two
  sessions undone in either order reach the same state.
- **Component (`ApplicationTests/`)** — the scoped delete handler, one case per bucket item and one
  for mix scoping; the account-delete command writes the purge row and publishes nothing else; the
  cancel command restores the snapshot.
- **Architecture (`ArchitectureTests/`)** — the purge-coverage ratchet from §9.
- **Integration (`Tests.Integration/`)** — session rows written on create and updated at drain,
  carrying `AccountTag` for an official import; undo against a real database across two
  overlapping sessions.
- **Components (`Tests.Components/`)** — the delete form stays disabled until the gate checkbox is
  checked, and re-disables when it is unchecked; the button label carries the count.
- **E2E** — not warranted. No new whole-workflow path; the ladder above covers it.

## 13. Sequencing

Built on `claude/score-journal-storage-8183f9` (`BestAttemptPolicy` is what D6 replays through).
That branch currently has **no commits** — its work is uncommitted in its worktree — so this waits
for its first pass to land before any migration is scaffolded. Two migrations touching
`ScoreEventJournal` in parallel would fight over `ChartAttemptDbContextModelSnapshot`.

Order within this work:

1. Purge gaps (§9) + the ratchet — independently shippable, and a bug fix on its own.
2. `ScoreSession` + the Sessions page moving onto it.
3. The Your Data frame, the delete page, the scoped delete command.
4. Undo.
5. Account deletion, the grace row, the doomsday dialog.

## 14. Class-level manifest

### The constraint that decides everything

The vertical reference graph, read off the `.csproj` files:

```
Catalog · WeeklyChallenge · EventCompetition · Identity · HomePage   (leaves)
ChartIntelligence → Catalog          Randomizer → EventCompetition
PlayerProgress    → Catalog, ChartIntelligence, Identity
Communities       → Catalog, ChartIntelligence, Identity, PlayerProgress, Randomizer, WeeklyChallenge
ScoreLedger       → Communities
OfficialMirror    → Identity, ScoreLedger
```

ScoreLedger sits near the top, so it *could* reach most verticals transitively — and leaning on
that is the trap. Transitive project references are not a sanctioned boundary, and it would make
ScoreLedger the owner of deleting tier-list votes, which it has no business owning. **Anything
crossing a vertical goes over the bus or through a Domain port**, exactly as the existing purge
does. Two edges below are used deliberately because they are declared and direct:
`OfficialMirror → ScoreLedger` and `ScoreLedger → Communities`.

Legend: **＋** new · **~** changed · **✕** deleted. Everything under a vertical is `internal`
except `Contracts/` and `Wiring/`.

### Phase 1 — purge gaps (no dependency on the journal branch)

| | File | What |
|---|---|---|
|~| `PlayerProgress/Infrastructure/EFAccountPurgeRepository.cs` | + `ScoreHighlightEntity`, `PlayerMilestoneEntity`, `PlayerFolderLevelEntity`, `PlayerSeasonRecapEntity`, and its **own** `PlayerStatsEntity` / `UserHighestTitleEntity` for **every** mix |
|~| `WeeklyChallenge/Infrastructure/EFAccountPurgeRepository.cs` | + `DailyStepEntryEntity`, `UserDailyStepPlacingEntity` |
|~| `EventCompetition/Infrastructure/EFAccountPurgeRepository.cs` | + `UserQualifierEntity` |
|~| `Communities/Infrastructure/EFAccountPurgeRepository.cs` | + `CommunityHighlightEntity` |
|~| `Identity/Infrastructure/EFAccountPurgeRepository.cs` | `DeleteIdentityData` + `UserImportCredentialKeyEntity` |
|＋| `Randomizer/Domain/IAccountPurgeRepository.cs` | |
|＋| `Randomizer/Infrastructure/EFAccountPurgeRepository.cs` | `UserRandomSettingsEntity`, `RandomizerDrawEntity` |
|＋| `Randomizer/Application/AccountPurgeConsumer.cs` | `IConsumer<AccountPurgeStartedEvent>` |
|~| `Randomizer/Wiring/RandomizerRegistrationExtensions.cs` | bind the repo; **new `AddRandomizerConsumers`** — its class doc currently states "the vertical has no bus consumers," which stops being true |
|＋| `HomePage/Domain/IAccountPurgeRepository.cs` | |
|＋| `HomePage/Infrastructure/EFAccountPurgeRepository.cs` | `HomePageEntity` + widget instances |
|＋| `HomePage/Application/AccountPurgeConsumer.cs` | |
|~| `HomePage/Wiring/HomePageRegistrationExtensions.cs` | bind + **new `AddHomePageConsumers`** |
|＋| `OfficialMirror/Domain/IAccountPurgeRepository.cs` | `UnlinkUser`, not `DeleteAllForUser` — the name should say it |
|＋| `OfficialMirror/Infrastructure/EFAccountPurgeRepository.cs` | **nulls** `OfficialPlayerEntity.UserId`; deletes nothing |
|＋| `OfficialMirror/Application/AccountPurgeConsumer.cs` | |
|~| `OfficialMirror/Wiring/OfficialMirrorRegistrationExtensions.cs` | bind + `AddConsumer<AccountPurgeConsumer>()` in the existing hook |
|~| `Web/Program.cs` | `AddRandomizerConsumers()`, `AddHomePageConsumers()` beside the existing nine |
|＋| `Tests/ArchitectureTests/AccountPurgeCoverageTests.cs` | the ratchet |
|~| `Tests/ArchitectureTests/…` MassTransit tripwires | two new — Randomizer and HomePage consumer discovery |
|~| `Tests/ApplicationTests/AccountPurgeSagaTests.cs` | the new consumers |

MassTransit's assembly scan skips internal types, so a consumer whose vertical has no hook — or
whose hook is never called from `Program.cs` — registers nowhere and fails **silently**. That is
what the tripwire tests exist for.

Moving `PlayerStats` / `UserHighestTitle` into PlayerProgress's own purge is a boundary fix as much
as a coverage one: today ScoreLedger's `WipeUserScoresHandler` deletes them through Domain ports for
Phoenix and Phoenix 2, which is right for a *score wipe* and wrong as the only purge path.

**The ratchet**: reflect over every vertical assembly for entity types carrying a `UserId` property,
then scan the purge repositories' source for both access styles in use — `Set<TEntity>()` and
`database.<DbSet>`. Source scanning follows the `UiColorTokenTests` precedent. `OfficialPlayer` sits
on the exemption list with its reason written down.

### Phase 2 — `ScoreSession`

| | File | What |
|---|---|---|
|＋| `ScoreLedger/Infrastructure/Entities/ScoreSessionEntity.cs` | §4's columns |
|＋| `ScoreLedger/Domain/IScoreSessionRepository.cs` | `Open`, `Touch`, `Get`, `ListFor`, `Delete` |
|＋| `ScoreLedger/Infrastructure/EFScoreSessionRepository.cs` | |
|＋| `ScoreLedger/Contracts/Commands/BeginScoreSessionCommand.cs` | `(MixEnum, string Source, string? AccountTag, string? CardId) : IRequest<Guid>` |
|＋| `ScoreLedger/Application/BeginScoreSessionHandler.cs` | |
|＋| `ScoreLedger/Contracts/ScoreSessionRecord.cs` | the DTO the list renders |
|~| `ScoreLedger/Wiring/ScoreLedgerModelContribution.cs` | `ToTable("ScoreSession")`, FK `ScoreEventJournalEntity.SessionId`, index `(UserId, StartedAt desc)` |
|~| `ScoreLedger/Wiring/ScoreLedgerRegistrationExtensions.cs` | bind `IScoreSessionRepository` |
|~| `ScoreLedger/Application/UpdatePhoenixRecordHandler.cs` | insert on a **new** session; `Touch` counts + `LastActivityAt` at batch drain |
|~| `ScoreLedger/Infrastructure/PlayerScoreBatchAccumulator.cs` | `GetOrExtendSession` returns `(Guid Id, bool IsNew)` |
|~| **`Domain/SecondaryPorts/IPlayerScoreBatchAccumulator.cs`** | same signature — **this port lives in Domain, not the vertical** |
|~| `ScoreLedger/Contracts/Queries/GetRecentSessionsQuery.cs` + `Application/SessionFeedHandler.cs` | read the table instead of grouping the journal |
|~| `ScoreLedger/Infrastructure/EFScoreJournalRepository.cs` | `GetSessionGroups` retired |
|~| `OfficialMirror/Application/…ImportSaga` | `BeginScoreSessionCommand` before the scrape; pass the id as the explicit `SessionId` on every submission — a parameter `UpdatePhoenixBestAttemptCommand` **already accepts** |
|＋| `Data/Migrations/<stamp>_ScoreSession.cs` (+ `.Designer.cs`) | |
|~| `Data/Migrations/ChartAttemptDbContextModelSnapshot.cs` | |
|~| `Web/Pages/Progress/PlayerSessions.razor` | onto the new record |
|~| `docs/DATABASE-SCHEMA.md` | the `ScoreSession` row |

The accumulator stays a pure in-memory concurrency primitive — it never touches the database. It
only reports *that* a session is new; the handler does the insert.

### Phase 3 — the pages

| | File | What |
|---|---|---|
|＋| `Web/Pages/Account/DataDelete.razor` | `@rendermode RenderModes.Interactive` (ratcheted by `RenderModeDeclarationTests`) |
|＋| `Web/Pages/Account/DataUndo.razor` | same |
|＋| `Web/Components/Account/YourDataFrame.razor` | the switcher chrome |
|＋| `Web/Components/Account/DeleteScopeForm.razor` | gate + mix chips + buckets; owns the blast-radius label |
|~| `Web/Components/Account/ProfilePanel.razor` | Danger Zone becomes a link; **✕** the wipe dialog and its five state fields |
|~| `Web/Resources/App.*.resx` ×9 | new keys, alphabetical position |
|＋| `Tests.Components/DeleteScopeFormTests.cs` | gate arms/disarms the form; the label carries the count |

`<PageDock>` (existing — `Components/PageDock.razor` → `PageDockService` → `PageDockHost`) carries
the destructive button below 960.

### Phase 4 — the scoped delete

| | File | What |
|---|---|---|
|~| `ScoreLedger/Contracts/Commands/WipeUserScoresCommand.cs` | `(Guid UserId, MixEnum? Mix, ScoreDeletionItems Items)` — **breaking**; `AccountPurgeConsumer` is the other caller |
|＋| `ScoreLedger/Contracts/ScoreDeletionItems.cs` | `[Flags]` — BestScores · PlayHistory · RatingHistory · Highlights · Milestones |
|~| `ScoreLedger/Application/WipeUserScoresHandler.cs` | honor mix + items; journal deletion (D8) lands here |
|~| `ScoreLedger/Application/AccountPurgeConsumer.cs` | passes "every mix, every item" |
|＋| **`Domain/Events/ContributionsDeletionRequestedEvent.cs`** | sits beside `AccountPurgeStartedEvent` — no vertical owns a fan-out event |
|＋| `ChartIntelligence/Application/ContributionsDeletionConsumer.cs` (+ repo method) | votes, three rating kinds |
|＋| `WeeklyChallenge/Application/ContributionsDeletionConsumer.cs` (+ repo method) | weekly + Daily Step |
|＋| `EventCompetition/Application/ContributionsDeletionConsumer.cs` (+ repo method) | sessions, registrations, qualifiers, photos, roles |
|＋| `Communities/Application/ContributionsDeletionConsumer.cs` (+ repo method) | memberships, highlights |
|~| four `Wiring/…RegistrationExtensions.cs` | register each consumer |
|＋| `Tests/ApplicationTests/WipeUserScoresScopeTests.cs` | one case per item + mix scoping |

The Scores bucket is synchronous because everything it touches is ScoreLedger's own or behind an
existing Domain port (`IPlayerStatsRepository`, `ITitleRepository`, `IPlayerHistoryRepository`) —
which is how that handler already works. Contributions goes over the bus because reaching four
verticals synchronously would cost either cross-vertical references or orchestration in
Presentation. Highlights and milestones are PlayerProgress's, so they ride phase 5's session event.

### Phase 5 — undo

| | File | What |
|---|---|---|
|＋| `ScoreLedger/Domain/SessionUndoReplay.cs` | **pure** — surviving journal entries for one chart → the restored best, composed with `BestAttemptPolicy` |
|＋| `ScoreLedger/Contracts/Commands/UndoScoreSessionCommand.cs` | |
|＋| `ScoreLedger/Contracts/Queries/GetScoreSessionUndoPreviewQuery.cs` | the confirm dialog's three counts |
|＋| `ScoreLedger/Application/UndoScoreSessionHandler.cs` | delete rows → replay → write bests → republish `PlayerScoresUpdatedEvent` |
|＋| `ScoreLedger/Application/GetScoreSessionUndoPreviewHandler.cs` | |
|＋| `ScoreLedger/Contracts/Events/ScoreSessionUndoneEvent.cs` | |
|＋| `PlayerProgress/Application/ScoreSessionUndoneConsumer.cs` | drops that session's `ScoreHighlight` / `PlayerMilestone` |
|~| `PlayerProgress/Wiring/PlayerProgressRegistrationExtensions.cs` | register it |
|＋| `Tests/DomainTests/SessionUndoReplayTests.cs` | pre-session best · no-previous-score · later session wins · order independence |
|＋| `Tests.Integration/ScoreSessionUndoTests.cs` | two overlapping sessions against a real database |

Web sends `ForgetImportCredentialCommand` (Identity, already exists) when the dialog's checkbox is
ticked.

### Phase 6 — account deletion

| | File | What |
|---|---|---|
|＋| `Identity/Infrastructure/Entities/AccountDeletionRequestEntity.cs` | **a sibling table, not `MergeRequest`** — that record demands a `SurvivorUserId` and carries `MovedLogins`, both meaningless here; a nullable survivor would force every merge query to defend against a row that is not a merge |
|＋| `Identity/Domain/IAccountDeletionRepository.cs` + `Infrastructure/EF…` | |
|＋| `Identity/Contracts/Commands/RequestAccountDeletionCommand.cs` | returns `AccountDeletionResult(Outcome, IReadOnlyList<OwnedCommunityRecord>)` — the `ImportStartResult` / `ImportStartOutcome` pattern, so a refusal is data rather than an exception (`DiagnosticExposureTests`) |
|＋| `Identity/Contracts/AccountDeletionResult.cs` | + `AccountDeletionOutcome` |
|＋| `Identity/Contracts/Commands/CancelAccountDeletionCommand.cs` | |
|＋| `Identity/Contracts/Queries/GetPendingAccountDeletionQuery.cs` | read by both Communities guards |
|＋| `Identity/Application/AccountDeletionHandlers.cs` | blocks on owned communities via `ICommunityReader`; hide + snapshot on request; restore on cancel |
|~| **`Domain/SecondaryPorts/ICommunityReader.cs`** | + `GetOwnedCommunities(Guid userId)` — the published port that lets Identity ask without referencing Communities (`IDiscordFeedReader` precedent) |
|~| `Communities/Infrastructure/EFCommunityRepository.cs` (the `ICommunityReader` impl) | implement it; **exclude system communities** — World and the per-country ones can't be transferred |
|~| `Communities/Application/…CreateCommunityHandler` | refuse while a deletion is pending |
|~| `Communities/Application/…TransferCommunityOwnershipHandler` | refuse when the **recipient** has a deletion pending |
|~| `Communities/Domain/Community.cs` | the two refusals as aggregate rules, taking the pending-deletion state as a passed-in fact |
|＋| `Web/Components/Account/OwnedCommunitiesBlocker.razor` | the "hand these over first" panel; **links** to `/Community/Members?communityName=…` rather than hosting its own transfer control |
|~| `Web/Pages/Communities/CommunityMembers.razor` | a confirm on **Make Creator** — it currently dispatches on one click and hands away the only creator seat |
|~| `Identity/Application/AccountPurgeSaga.cs` | second source of purgeable users beside `GetPurgeable` |
|~| `Identity/Wiring/IdentityModelContribution.cs` + registration | table + bindings |
|＋| `Data/Migrations/<stamp>_AccountDeletionRequest.cs` | |
|＋| `Web/Components/DeletionNoticeHost.razor` | the doomsday dialog — a **render-nothing-until-relevant island** in `MainLayout`, modelled on `RecapPointer`. It cannot be a plain dialog in the layout; the layout renders statically now |
|~| `Web/Components/MainLayout.razor` | the third island |
|＋| `Web/Components/Account/DeleteAccountDialog.razor` | type-to-confirm, `MaxWidth.False` |
|~| `Web/Components/Account/ProfilePanel.razor` | the pending banner + Cancel deletion |
|＋| `Tests/ApplicationTests/AccountDeletionHandlerTests.cs` | |

Everything downstream — `AccountPurgeStartedEvent`, the ten consumers, the week of re-fires, the
daily `process-account-purges` job — is reused untouched.

## 15. Ratchets

The purge is the kind of ecosystem that rots quietly: nothing breaks when a new vertical forgets
it, and the symptom — data surviving a deletion the player was promised — is invisible until
someone looks. Three new ratchets, one of which is a constraint rather than a test.

### R1 — purge coverage (essential)

`Tests/ArchitectureTests/AccountPurgeCoverageTests`. **Declaration-driven, not source-scanned.**
Each vertical's purge repository states what it owns, and the same array is what executes:

```csharp
internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    // The vertical's user-owned tables. R1 asserts this covers every *UserId-bearing
    // entity in the assembly; UserDataPurge is what runs it.
    internal static readonly Type[] UserOwned =
    {
        typeof(WeeklyUserEntry), typeof(UserWeeklyPlacingEntity),
        typeof(DailyStepEntryEntity), typeof(UserDailyStepPlacingEntity)
    };

    public Task DeleteAllForUser(Guid userId, CancellationToken ct) =>
        UserDataPurge.DeleteAll(_factory, UserOwned, userId, ct);
}
```

`UserDataPurge` lives in `ScoreTracker.Data` (every vertical already references it), resolves each
type's schema and table **from the EF model** so the name cannot drift from `ToTable`, and issues
parameterized deletes in array order — which is also how FK ordering between a vertical's own tables
gets expressed.

The test reflects over each vertical assembly for EF entity types with a `Guid` property whose name
**ends in `UserId`**, and asserts set equality against that assembly's `UserOwned` plus an exemption
list. `Assembly.GetTypes()` and `GetField(…, NonPublic | Static)` see internal types, so no
`InternalsVisibleTo` is needed.

Why declaration-driven beats the source scan I originally proposed: the declaration *is* the
implementation, so the two cannot drift; the failure message names the exact type
(`DailyStepEntryEntity is user-owned but not purged`); and there is no regex straddling the two
access styles in use today (`Set<TEntity>()` and `database.<DbSet>`). It is also less code than the
eight hand-written repositories it replaces.

Two exemptions, each carrying its reason in the list: `OfficialPlayer` (unlinked, not deleted — an
UPDATE) and `Community` (an account that owns one cannot be deleted at all, §8.1, so the purge never
meets one).

**The blind spot to know about**: matching on the `*UserId` suffix catches `OwningUserId` and would
catch `CreatedByUserId`, but an entity that references a user through some other name is invisible
to R1 and has to be added by hand. That is a documented limit, not a solved problem.

### R2 — every vertical consumer is registered (valuable)

The existing tripwires in `VerticalBoundaryTests` are hand-written per vertical **and name specific
consumer types** — `MassTransitDiscoversTheCatalogsInternalConsumers` asserts `PiuCenterCrawlSaga`
and nothing else. So two things slip through today:

1. a **new vertical** with consumers gets no test until someone remembers to write one, and
2. a **new consumer inside an already-covered vertical** is not caught at all.

Phase 1 does exactly (2) — it adds `AccountPurgeConsumer` to OfficialMirror, whose tripwire names
`OfficialDigestFeedSaga`. Forgetting the `AddConsumer<>()` line would pass every test and the purge
would simply never run for that vertical.

Generalize it: for each vertical assembly, find every `IConsumer<>` implementation by reflection,
run the host's real `AddMassTransit` block, and assert each one resolved. One test, no per-vertical
maintenance, and it fails loudly for a vertical that has consumers but no hook.

### R3 — every model contribution is registered (a gap found on the way)

`VerticalModelContributions.All()` has **no test anywhere**. CLAUDE.md documents the failure mode —
omitting one silently drops that vertical's tables from scaffolded migrations — and nothing enforces
it. Phase 2 and phase 6 each add a table, so this is live risk now.

Reflect over the vertical assemblies for `IDbModelContribution` implementations and assert
`All()` contains one of each.

### The journal → session foreign key, and why there isn't one

An FK from `ScoreEventJournal.SessionId` to `ScoreSession.Id` would be the strongest guarantee
available that a score-writing path opened a session — the database would refuse the row
otherwise. **It cannot be added.** Two facts collide:

- Existing journal rows carry `SessionId` values minted before the table existed, and §4 rules out
  backfilling stubs for them.
- Since PR #209 the journal is **updated in place** — an observed play already journaled is raised
  to `IsBest` rather than duplicated.

`WITH NOCHECK` handles the first (it skips validating existing rows) but not the second: the
constraint still fires on every INSERT *and UPDATE*. So the first time an old row with an orphan
`SessionId` was raised to `IsBest`, the update would fail — turning a silent gap into a live import
error. Nulling the orphans instead would erase the grouping the Sessions page renders today.

So the link stays a convention, guarded where it is actually written: the session row is opened
before the id is handed out, in one place. Delete ordering still matters — undo removes journal
rows before the session row — but by discipline rather than by constraint.

### Already guarded, no work needed

`UiColorTokenTests` (no color literals) · `LocalizationKeyTests` (locale parity, case collisions,
alphabetical order, the Murloc alphabet) · `RenderModeDeclarationTests` (a page declares its
circuit) · `DiagnosticExposureTests` (no raw exception text outside `Pages/Admin/`) ·
`MessageTaxonomyTests` (the new commands, queries and events land in the right folders with the
right names) · `LayerDependencyTests` and `VerticalBoundaryTests` (the reference graph, and that
only `Contracts/` and `Wiring/` are public) · `Tests.Api` goldens (no public wire shape moves).

### Deliberately not ratcheted

- **Scoped-delete coverage.** A new contributions table that nobody wires into
  `ContributionsDeletionRequestedEvent` survives a partial delete — annoying, not a broken promise,
  because R1 still guarantees the account purge takes it. One ratchet on the backstop is worth more
  than two half-ratchets, and "is this a contribution?" is not machine-decidable.
- **Copy rules** (§11's no-rewind-language ban). A reviewer's job; a regex over nine locales for
  "restore to" would fail on false positives faster than it would catch anything.

## 16. Docs to update in the same PR

| Doc | Change |
|---|---|
| [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md) | rows for `ScoreSession` and `AccountDeletionRequest`. No archival section changes — nothing is dropped |
| [ARCHITECTURE.md](../ARCHITECTURE.md) | the Pages table's `/Account` entry gains `/Account/Data/Undo` and `/Account/Data/Delete`; the published-ports paragraph gains `ICommunityReader`'s second cycle-breaking use (Identity asking who owns a community) |
| [SCHEDULED-JOBS.md](../SCHEDULED-JOBS.md) | `process-account-purges` currently reads "retired accounts whose 30-day merge grace window ended" — it now also drives self-serve deletions on a 7-day window |
| [UX-GUIDELINES.md](../UX-GUIDELINES.md) | two additions to §2: **the armed-form gate** (a destructive form renders visible-but-inert until an explicit opt-in, because hiding it makes people hunt) and **the blast-radius button** (a destructive confirm names its count, which outperforms another acknowledgement checkbox) |
| [score-truth-model.md](score-truth-model.md) | §3's "never updated or deleted by the application" is amended — the write path never rewrites history, a player deleting their own data is the sanctioned exception (D8). **Whichever PR lands second makes this edit** |
| [login-overhaul.md](login-overhaul.md) | §"Merge execution" line 139 calls self-serve delete a future feature; it points here instead |
| [communities-overhaul.md](communities-overhaul.md) | owning a community blocks account deletion, and a pending deletion blocks creating one or receiving the creator seat (§8.1) |
| [CLAUDE.md](../../CLAUDE.md) | the three new ratchets in the architecture-test list; note that Randomizer and HomePage now have bus consumers and therefore `AddXxxConsumers` hooks |

**No change needed**: [API.md](../API.md) (no public endpoint deletes scores — the wipe is MediatR-only,
so no wire shape moves and the `Tests.Api` goldens are untouched), HOW-TO-TEST, HOW-TO-RUN,
TECHNOLOGIES, CONTRIBUTING, DOMAIN.

## 17. Settled by the owner, 2026-07-30

- **Buckets with individually selectable items** — "full customizable, but bucketed for simplicity
  of deciding."
- **No backfill; the floor is 2026-08-01 00:00 EST.** Read literally as UTC-5 →
  `2026-08-01T05:00:00Z`. August is EDT, so if the intent was Eastern *wall clock* it is
  `04:00:00Z`; the difference is an hour on a floor date, and this doc takes the literal reading.
- **The undo banner lives on the delete page**, with a checkbox to proceed to deletion — not on
  the public Sessions page.
- **Milestones and highlights travel with an undo.**
- **Grace period on the account only.** Scoped deletes are immediate.
- **Type the username** to confirm.
- **The sign-in dialog does not auto-cancel** — it points at the account page.
- **The journal append-only sentence is overstated** and does not bind here (D8).
- **"Revert" → "Undo"**, and the session kinds are named PIUGAME / CSV upload / Manual (§6).
- **Official imports record the game tag** they pulled from (§4).
- **Owning a community blocks account deletion** (§8.1) — hand it over or delete it first; the
  system never picks an heir. A flagged account also cannot create a community or be handed one.
