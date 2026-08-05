# Rivals — design

> **Status: BUILT.** Workshopped and built 2026-08-02, one PR, 34 commits. Depends on the session-breakdown
> work (PR #211), which deliberately shipped with no Rivals hint at all
> ([session-breakdown.md](session-breakdown.md) D20: *"It is the next project and this branch
> blocks it."*).
>
> Mock (owner-approved): <https://claude.ai/code/artifact/44f10b77-25b7-4a4a-8e79-909980226ed9>

A **rival** is one player another player has picked out to measure themselves against. Unlike a
community — which is a room you join — a rivalry is a one-way arrow you draw, and the person on
the other end can see it and cut it.

The feature is three things: a place to manage the arrows, a feed of what the people you're
chasing have been doing, and a rival-shaped read of *"what did they get on this chart"* seeded
everywhere it fits.

---

## 1. Locked decisions

Owner calls from the 2026-08-02 workshop.

### Identity

| # | Decision |
|---|---|
| D1 | **A rival target is a site `UserId` OR an official board game tag** — never both, never a synthesized user. |
| D2 | **No ghost `User` rows.** `User` is the auth principal; minting empty accounts for board players would put phantoms into `GetUsers`, `UserLabel`, World membership, and every purge manifest. `OfficialPlayer` already *is* the reverse-engineered board identity. |
| D3 | **The ghost key is the tag string, not `OfficialPlayer.Id`.** The int is per-mix and a rename **deletes** the row (`MergePlayers` re-points placements then drops the dim), so an int-keyed edge dangles. A tag is cross-mix, which is what a person is. |
| D4 | **Never store a tag that already resolves to a site user.** Add-time resolution through `OfficialPlayer.UserId` normalizes to the strongest identity available, so `TargetTag` is *by definition* a ghost and the same human can't be added twice. |
| D5 | **Two events keep the tag honest.** `OfficialPlayerLinkedEvent` (fired from `LinkPlayer` on the import path) promotes every matching tag edge to a user edge — that's ghost-becomes-real, automatic. `OfficialPlayerRenamedEvent` rewrites the tag. It fires when a rename is accepted — **since 2026-08-05 that is usually the sweep itself rather than an admin** ([rename-matching.md](rename-matching.md)), which is why D6's orphaned-tag rendering matters less than it did. |
| D6 | **An orphaned tag renders, it doesn't vanish.** An undetected rename leaves an edge pointing nowhere; the row says *"no longer on the boards"* so the user can fix it. |
| D7 | **Rivals never normalizes a tag itself.** `OfficialPlayerTag.Normalize` is OfficialMirror-internal; Rivals stores whatever the resolve contract hands back, or the two normalizers drift the way the two `ImageRegex`es did. |
| D8 | **A linked player's site `ProfileImage` wins** over `OfficialPlayer.AvatarUrl` — that's the one they control. |

### Consent, privacy, blocking

| # | Decision |
|---|---|
| D9 | **Add-time gate, four bases:** the target is (a) a ghost with no linked site user, (b) a public site user, (c) in a user-created community with you, or (d) redeemed your rival invite code. |
| D10 | **A ghost that resolves to a *private* site user cannot be added.** That is the rule-3 exception, and it falls out of D4 for free. |
| D11 | **The edge is the consent.** Once formed it does not lapse. Going private does **not** sever your rivals, and neither does leaving a shared community — no dormancy state, no reconciliation job. *Owner: not over-engineering a path people don't take.* |
| D12 | **Going private warns you**, with a link to the reverse list, when you have rivals. That is the entire private-path handling. |
| D13 | **Rivalry is not secret.** Every edge is visible to its target, including a private player's edge onto a public one. |
| D14 | **The reverse list is the only revocation.** Remove drops their edge (they can re-add); Block drops it and prevents re-adding. |
| D15 | **Block is symmetric.** Neither party can rival the other. |
| D16 | **Ghosts are never blocked** — a board-only player has no account and can rival nobody. Block is always user↔user. |

### The roster and adding

| # | Decision |
|---|---|
| D17 | **Unbounded rivals.** A few hundred is accepted overhead, handled at each surface rather than by a cap. |
| D18 | **Default sort is recent activity**, with a sort control. A 300-row alphabetical list is a phone book. |
| D19 | **Two search fields, not one merged autocomplete.** The site index and the board index are different populations with different rules; merging them hides which one failed you. |
| D20 | **The site picker is "public users ∪ your community members."** A private clubmate is addable; a private stranger is not findable at all. |
| D21 | **Departed tags are filtered out of the board picker.** `GetOfficialPlayerNamesQuery` returns every tag ever seen — right for the Players page, wrong here, because the edge would be permanently empty. The picker asks `SearchOfficialBoardTagsQuery` instead: latest sealed snapshot, term and cap pushed into SQL. It is a separate query rather than a flag because the picker runs **per keystroke** — reading a snapshot's whole tag population to keep ten strings moves every board placement over the wire, and a search still running when the next character arrives gets cancelled, which SqlClient reports as a severe command error rather than a cancellation (§6). |
| D22 | **Adding happens on `/Rivals` only in v1.** No ⊕ affordance on leaderboard, community or official rows yet. |
| D23 | **The invite code is minted for private accounts only.** A public account has nothing to hand out, and a code nobody needs reads as a step they missed. |
| D24 | **Recycling invalidates the code, not the edges already made with it.** Revoking a person is the reverse list's job. |
| D25 | **One code per user**, no multi-code management. Landing page at `/Rivals/Invite/{code}`, matching the `/Communities/Invite/{code}` precedent. |

### Ghosts as rivals

| # | Decision |
|---|---|
| D26 | **A ghost rival is scores AND standings**, not standings alone — bounded by what the mirror actually holds (§2.3). |
| D27 | **Ghosts rank inline with live rows, marked by a single asterisk**, with one footnote per board carrying the "as of" date. No per-row disclaimer. |
| D28 | **Ghosts appear only on rivals-scoped surfaces.** World, Region, Community and Competitive Peers stay pure live-score boards — injecting snapshot data would break the percentile the score row already printed. |
| D29 | **Head-to-head takes a capability set, not a user id.** A ghost renders the reduced version; sections it can't fill are **absent with a reason**, never empty. That line is also the promotion promise. |
| D30 | **Ghosts never appear in any feed.** Wins come from imports; a board-only player has none. |

### Highlights and the feed

| # | Decision |
|---|---|
| D31 | **There is one tier-2 "significant win" computation, and it is already community-agnostic.** `CommunityHighlightPolicy.Classify` takes a site-wide rarity snapshot plus the player's own stats — zero community input. The community is purely the *audience*. |
| D32 | **The payload moves to a user-keyed store in PlayerProgress.** One policy, one payload, two audiences (§2.4). |
| D33 | **`CommunityHighlight` survives as the audience index.** Its fan-out is what keeps a World-scoped feed a seek instead of a join over every member. Stop writing `Payload`; don't drop the column. |
| D34 | **Relative wins are read-time, never stored.** *"NAME passed you on X"* is viewer-dependent and cannot ride an absolute payload — compute it from the scores already in hand. |
| D35 | **Page feeds are single-audience; the widget is where you mix.** On the Rivals feed every row is a rival, so red marks everything and therefore nothing — only green *"also a clubmate"* is meaningful. The community feed does the mirror image. |
| D36 | **The segmented state only appears where the population is genuinely mixed**: leaderboards, and the home widget with both boxes checked. |
| D37 | **Dedupe by `EventId`**, which falls out of the user-keyed payload — one row per event, wearing whichever markers apply. |
| D38 | **The widget renames its label, never its type key.** "Community Highlights" → "Highlights feed"; changing the persisted key orphans every dashboard that has one. The new config field defaults **off** so shipped dashboards don't silently change. |

### Surfaces

| # | Decision |
|---|---|
| D39 | **`--daily-rival: #FF3B5C`**, joining `--daily-you` and `--daily-community` in the mix-invariant row-token group. Not `MixPalette.Error` (#C72020) — too dark at 13% fill, and it means "something went wrong". Clear of the Phoenix secondary orange so a rival never reads as a brand accent. |
| D40 | **Precedence: you → both → rival → community.** |
| D41 | **Segmented = split fill plus a left and a right inset bar.** A two-tone `border-image` ring would kill `border-radius`. **Generalized 2026-08-04 (field test):** every state uses that geometry now — tint plus a bar down each edge, colour the only variable — so the segmented row is the same shape as its neighbours rather than a special case. Rings are gone from the whole group: at row height a 1.5px ring reads as a box drawn around the row and fights the board's grid. Ratcheted by `HighlightVocabularyTests`. The challenge boards moved onto the mix-invariant `--daily-*` tokens in the same pass, because the ladder emits `is-*` onto those rows and per-mix `--rarity-emerald`/`--mix-primary` beside a fixed `--daily-community` meant two greens for one meaning on one board. |
| D42 | **The sessions toggle is the `MudButtonGroup`** (the Tier Lists By Level / By Skill pair), not the "Ranked by" `MudSelect`. Renders only when you have rivals; persists per page like density. **Revised 2026-08-04 (field test):** Rivals leads and is the default whenever you have any — you chose those people, where community membership is mostly incidental — and the group is hidden entirely when there are no community peers, because a two-button switch with one dead half is worse than no switch. A saved preference still wins; only "no preference yet" changed. The selected half is Filled *and* Primary: Outlined-vs-Filled alone did not read at this size. |
| D43 | **The rivals peer board is uncapped but paged** — top rows plus your row pinned, then *Show all*, borrowing `ChartLeaderboardSection`'s vocabulary. A "N more rivals haven't played this" line carries the tail. |
| D44 | **Rivals is a fifth scope on `ChartLeaderboardDialog`**: World · Region · Community · Rivals · Competitive Peers. Lit only when you have rivals. |
| D45 | **Seeding, ranked:** sessions toggle, chart-leaderboard scope, head-to-head, home widget. Daily Step and the weekly board get **the red outline only** — no extra words. The `/Charts` "a rival beat me" facet is **out of scope**. |
| D46 | **`/Rivals` lives under Compete** in the nav, desktop menu and mobile More sheet. |
| D47 | **No notifications.** The reverse list is the discovery mechanism; a notification ecosystem is a separate future project and is not invented here. |
| D48 | **The recap's internal `RivalMatcher` is renamed; its public contract is not.** The Season Recap already uses "rival" for an *auto-picked* similar player — a different concept that now collides with a user-chosen one. `RivalMatcher` → `RecapPeerMatcher` is internal and free. `RecapRivals` / `RecapRival` / `PlayerRecap.Rivals` **stay**: `PlayerRecap` is serialized whole into `PlayerSeasonRecap.Payload` and read back behind a `SchemaVersion` equality gate, so renaming the property invalidates every stored recap until an admin rebuild — a real cost for a cosmetic win. The user-facing collision is fixed in the resx instead: the recap deck's section label stops saying "Rivals". |

### Deliberately not decided here

- **Whether the branch splits** (§5). Both shapes are viable; the risk profile differs.
- **The `/Charts` rival facet.** Cut from v1, not rejected — it's the surface that answers
  *"where am I losing?"* across the whole catalog, and it's worth revisiting once people have
  rivals.
- **Whether ghost rivals should eventually contribute weekly-snapshot feed rows**
  (`OfficialWeeklyHighlight` already holds board movement). A second feed vocabulary is a
  bigger question than this branch.

---

## 2. The model

### 2.1 The edge

```
Rival(Id, OwnerUserId, TargetUserId Guid?, TargetTag nvarchar(100)?, AddedAt)
      -- exactly one target non-null (D1)
RivalBlock(UserId, BlockedUserId, CreatedAt)   -- checked in both directions (D15)
RivalInviteCode(UserId, Code, CreatedAt)       -- one per user (D25)
```

Resolution produces a **`RivalSubject`** — the one abstraction every surface consumes, so the
tag/user duality lives in exactly one place:

```
RivalSubject(Guid? UserId, Name? Tag, string DisplayName, Uri Avatar,
             bool IsGhost, RivalCapabilities Capabilities)
```

`RivalCapabilities` is what makes D29 buildable: a subject declares whether it can answer for
live scores, folder compare, titles and progression, or only official standings.

### 2.2 Visibility

Add-time only (D9–D11). Once the edge exists nothing re-checks it, which is the entire
simplification: no dormancy state machine, no reconciliation job, no scheduled sweep. The
counterweight is that the reverse list must be good, because it is the only revocation (D14).

Note the existing precedent this inherits rather than invents: community-scope boards already
*don't* filter `IsPublic` (see the `ScoresForScopeAsync` switch in `ChartLeaderboardDialog`),
because membership is itself the consent grant. A rival edge is the same kind of grant.

### 2.3 What the mirror actually holds for a ghost

This is the ceiling on D26 and it is not raisable — piugame exposes no per-player score page for
an arbitrary tag (`my_best_score.php` is your own account only). The boards are all there is:

| Dimension | Bound | Source |
|---|---|---|
| Charts | **level 20+ only** | `Get20AboveSongs` → `over_ranking.php` is the board enumeration |
| Depth | **~300 per board** on P2 (paged to the next/last icon) | `OfficialSiteClient.GetOfficialChartBoards` |
| Freshness | **the last sealed weekly snapshot** | `OfficialLeaderboardPlacement` is per-snapshot |
| Also available | PUMBILITY rank + rank history, boards-in-top, chart firsts, best place | `OfficialPlayerStandingRecord` |

Coverage is therefore decent across the competitive band (the 17–23 core is mostly 20+) and
degrades to *"no board for this chart"* everywhere else — which is why the ghost head-to-head
counts in charts ("boards you're both on, 14 charts") rather than pretending to a folder.

⚠ `OfficialLeaderboardPlacement` is per snapshot. Any count over it must `GROUP BY PlayerId`
first or a player is counted once per snapshot.

### 2.4 Two tiers of highlights

The owner's framing, and the code already agrees with it:

- **Tier 1 — personal highlights.** `ScoreHighlightEntity` / `PlayerMilestoneEntity` in
  PlayerProgress: write-time flags per journal row, the Sessions page's material. Lower bar.
- **Tier 2 — significant wins.** `CommunityHighlightPolicy.Classify(e, charts, snapshot, stats)`
  — a site-wide rarity snapshot (PG holders across all players, active-player count, title
  holders) plus the player's own stats. **No community input at all.** Higher bar, feed material.

Tier 2 is already the shared thing; it is merely *stored* in a community-keyed table. Moving the
payload to a user-keyed store (D32) is what lets Rivals read it without a second policy, a second
payload, or a fan-out that would have to backfill on every add.

```
PlayerHighlight(EventId PK, UserId, MixId, OccurredAt, SessionId, Payload, SchemaVersion)
```

Community feed = membership → the audience index → payload. Rivals feed = rival user ids →
payload directly. Fan-out on read over ≤N rivals, so adding a rival surfaces their last 30 days
immediately.

### 2.5 One query for rival scores

D45 seeds rival scores across several surfaces. Exactly one thing knows how to fetch them:

```
GetRivalScoresForChartsQuery(mix, chartIds)
  → IReadOnlyDictionary<Guid, IReadOnlyList<RivalChartScore>>

RivalChartScore(RivalKey, DisplayName, Avatar, Score, Plate, IsBroken,
                Source: Site | Official, AsOf?)
```

Site rivals resolve through `IScoreReader.GetPlayerScores(mix, userIds, chartIds, ct)` —
**which already exists in exactly this shape**, so ScoreLedger needs nothing new. Ghost tags
resolve through a new OfficialMirror contract query against the latest sealed snapshot. The
merge and the `Source` stamp happen once, here.

**Perf:** at 300 rivals × N charts this must stay set-based. A 300-element `IN (...)` is past
where SQL Server is comfortable — expect a TVP on both the score read and the placement read.

---

## 3. The surfaces

Drawn in the [mock](https://claude.ai/code/artifact/44f10b77-25b7-4a4a-8e79-909980226ed9); this
section is the spec behind it.

### 3.1 `/Rivals` — roster

Recent-activity sort (D18), ghosts inline with a `Board only` tag and the "as of" date (D27).
PUMBILITY rank is the stat column because it is the one number **every** rival has, ghost or not.
Two search cards (D19–D21) and — only while you are private — the invite card (D23–D24).

### 3.2 `/Rivals` — rivals of you

Remove / Block / Unblock (D14–D15), with the symmetric rule stated on the row it applies to.
Private players appear by name (D13). No notification exists, so this list *is* the discovery
mechanism (D47); the `/Account` private toggle links here (D12).

Since it is the discovery mechanism, the row also carries **Add as rival** — the one constructive
action here, and the only filled button among the two revocations. It is an ordinary add answering
to the ordinary gate: their arrow at you is **not** a fifth basis (D9), so the button appears only
where the add would succeed — not on somebody you already rival (the row says so), and not on a
private stranger who rivalled you off your public profile. Offering it there would resolve to
"that player isn't available", which is a worse answer than no button.

### 3.3 Head-to-head — the shared player summary

`/Community/Player`'s member summary + folder compare is lifted into a shared component with
three hosts: community member, rival, and eventually the standalone Player Stats page the owner
is deferring. It takes `RivalCapabilities` (D29).

| Subject | Live scores | Folder compare | Titles / progression | Standings |
|---|---|---|---|---|
| Community member / site rival | ✅ | ✅ | ✅ | if linked |
| Ghost rival | boards only | ❌ | ❌ | ✅ |

Win/loss colouring reuses the row-glow vocabulary: ahead = the community green, behind = the
rival red. One vocabulary, two uses.

### 3.4 Sessions — Community Peers ⟷ Rivals

The `MudButtonGroup` toggle (D42), rendering only with rivals registered, persisted per page
(`RivalsPeers__PlayerSessions`, following the `Density__<Page>` pattern). Uncapped but paged
(D43). Ghosts inline with the asterisk and one footnote (D27).

### 3.5 `ChartLeaderboardDialog` — the fifth scope

Rivals joins the rail (D44). **No red glow on this board** — every row is a rival, the same
reasoning that already blanks the green on Community scope.

⚠ **Build note:** do *not* assemble these rows by filtering the dialog's `_world` array. That is
the World *community* breakdown and it excludes private players, so a private rival who gave you
their invite code would silently vanish from the board. Rivals needs its own scores-for-user-set
fetch, the way Community scope has one.

### 3.6 The highlight vocabulary

One new token (D39), one new combined state (D41), one precedence ladder (D40) — applied across
**four row shapes that share no layout**: `.weekly-lb-row`, `.chart-lb-*` (table cells),
`.dash-ch-entry` (feed card), and the new roster row. One utility set applied over each layout,
not four families each growing their own variants.

**The ladder has exactly one implementation**: `CommunityGlowReader.RowClass`. Every board calls
it, passing its own native class names for the you and clubmate states; the rival states use the
utility set, which is the whole point of that set. Field testing found the ladder had been copied
per family instead, and the copies drifted — boards written before rivals existed kept a
you/clubmate ternary that could never produce a rival row, so red lit up on some boards and not
others while every test stayed green. Two ratchets hold it now (`HighlightVocabularyTests`): every
`is-*` state must have a rule that paints, and a board that passes `CommunityUserIds` must pass
`RivalUserIds` — an unpassed Blazor parameter is silently null, which is how the segmented row
stayed unreachable in a component that had supported it all along.

### 3.7 Feeds

Rivals page = rivals only, green marks clubmates. Community page = communities only, red marks
rivals. Home widget = checkable, all three states live (D35–D36). Ghosts never appear (D30), and
the empty state names what would fill it.

---

## 4. Technical scope

### 4.1 Verticals and layers

| Vertical / layer | Change |
|---|---|
| **`ScoreTracker.Rivals` (NEW)** | The whole vertical: three tables, the edge model, resolution to `RivalSubject`, the invite-code lifecycle, block enforcement, the rival-scores query, the feed read, the purge manifest, two event consumers. |
| **PlayerProgress** | Owns tier 2 after the move: the win policy + capturer, the `PlayerHighlight` store, and one published query for a set of users' recent wins. `RivalMatcher` renamed (D48). |
| **Communities** | Loses the policy/capturer and the payload; keeps `CommunityHighlight` as the audience index (D33) and re-points its feed query at the new payload. `SignificantWin` / `WinKind` / the schema stamp re-home to `PlayerProgress.Contracts`. |
| **OfficialMirror** | Two published events (D5), and three published queries: resolve a tag to a subject, board scores for tags × charts at the latest sealed snapshot, and the snapshot-scoped tag search the picker types into (D21). |
| **Identity** | **Nothing.** `SearchForUsersQuery` already exists; Rivals filters its results to public players ∪ the caller's clubmates. Teaching Identity what a community is would put the membership graph in the wrong vertical. |
| **ScoreLedger** | **Nothing.** `IScoreReader.GetPlayerScores(mix, userIds, chartIds, ct)` is already the exact shape. |
| **HomePage** | Nothing structural — the widget's config record gains a field, the type key is untouched (D38). |
| **Data** | One migration (§4.3). |
| **Web** | The page, its components, the dialog extraction, the sessions toggle, the CSS tokens, the widget. |

No new ports. Every cross-vertical read is a published contract query — no SQL joins onto another
vertical's tables (ADR-001).

### 4.2 Classes

**`ScoreTracker.Rivals` — new assembly**, following the WeeklyChallenge template.

```
Contracts/
  Commands/    AddRivalCommand, RemoveRivalCommand, BlockRivalCommand, UnblockRivalCommand,
               RecycleRivalInviteCodeCommand, RedeemRivalInviteCodeCommand
  Queries/     GetMyRivalsQuery, GetRivalsOfMeQuery, GetMyBlockedPlayersQuery,
               GetMyRivalInviteCodeQuery, GetRivalInvitePreviewQuery,
               GetRivalScoresForChartsQuery, GetMyRivalHighlightsQuery,
               GetRivalHeadToHeadQuery, SearchRivalCandidatesQuery
  RivalSubject, RivalCapabilities, RivalRosterEntry, RivalChartScore,
  RivalScoreSource, RivalOfMeRecord, RivalHeadToHeadRecord, RivalInvitePreviewRecord
Wiring/
  RivalsRegistrationExtensions (AddRivals, AddRivalsConsumers), RivalsModelContribution
Domain/
  IRivalRepository, IRivalInviteCodeRepository, IAccountPurgeRepository,
  RivalVisibilityPolicy   (the four add-time bases, D9–D10 — pure, unit-tested hard)
  RivalInviteCode         (value type, From(...) factory)
Application/
  AddRivalHandler, RemoveRivalHandler, BlockRivalHandler, UnblockRivalHandler,
  RecycleRivalInviteCodeHandler, RedeemRivalInviteCodeHandler,
  GetMyRivalsHandler, GetRivalsOfMeHandler, GetRivalScoresForChartsHandler,
  GetMyRivalHighlightsHandler, GetRivalHeadToHeadHandler, SearchRivalCandidatesHandler,
  RivalSubjectResolver         (tag ⇄ user, the one place D1–D4 live)
  OfficialPlayerLinkSaga       (IConsumer<OfficialPlayerLinkedEvent>   — promote tag → user)
  OfficialPlayerRenameSaga     (IConsumer<OfficialPlayerRenamedEvent>  — rewrite tag)
  AccountPurgeConsumer
Infrastructure/
  Entities/  RivalEntity, RivalBlockEntity, RivalInviteCodeEntity
  EFRivalRepository, EFRivalInviteCodeRepository, EFAccountPurgeRepository
```

⚠ `EFAccountPurgeRepository.UserOwned` must name **all three** entities, and `RivalEntity` is
keyed to a user **twice** (`OwnerUserId` and `TargetUserId`) — `AccountPurgeCoverageTests` scans
for `*UserId` columns and will catch a half-covered table.

⚠ `AddRivalsConsumers` must be called from `Program.cs`'s `AddMassTransit` block — MassTransit's
assembly scan skips internal types, and `VerticalBoundaryTests` tripwires this.

⚠ `RivalsModelContribution` must be added to `VerticalModelContributions.All()`, or scaffolded
migrations silently drop all three tables.

**PlayerProgress — changed**

| Path | Change |
|---|---|
| `Contracts/SignificantWin.cs` | **moved in** from `Communities.Contracts` (record + `WinKind` + the schema stamp; the stored JSON is unaffected — this is a namespace move) |
| `Contracts/PlayerHighlightRecord.cs` | new — the payload row, name/avatar resolved at read |
| `Contracts/Queries/GetPlayerHighlightsQuery.cs` | new — recent wins for a set of user ids |
| `Contracts/Events/PlayerHighlightsStoredEvent.cs` | new — `(EventId, UserId, MixId, OccurredAt)`, published after the payload lands. This is how the audience index gets written without a cross-vertical write: Communities consumes it and writes its `(EventId, CommunityId, UserId)` rows, needing no payload of its own. Fires only when the classification produced wins, so the index never records a silent event. |
| `Domain/PlayerHighlightPolicy.cs` | **moved in** from `Communities.Domain.CommunityHighlightPolicy`, unchanged logic |
| `Domain/IPlayerHighlightRepository.cs` | new |
| `Application/PlayerHighlightCapturer.cs` | **moved in** from `Communities.Application.CommunityHighlightCapturer` |
| `Application/PlayerHighlightSaga.cs` | **moved in** from `CommunityHighlightSaga` — same `IConsumer<ScoreHighlightsCapturedEvent>`, same failure isolation |
| `Application/PlayerHighlightPurgeConsumer.cs` | **moved in** — the 30-day purge follows the payload |
| `Infrastructure/Entities/PlayerHighlightEntity.cs` | new |
| `Infrastructure/EFPlayerHighlightRepository.cs` | new |
| `Domain/Recap/RivalMatcher.cs` | **renamed** (D48) — and `RivalMatcher.Candidate`, `PickRivals`, `CompetitiveRange`, `CommunityCompetitiveRange` with it |
| `Wiring/PlayerProgressModelContribution.cs` | + `PlayerHighlight` table |
| `Wiring/PlayerProgressRegistrationExtensions.cs` | + the repository, capturer, and two consumers |
| `Infrastructure/EFAccountPurgeRepository.cs` | + `PlayerHighlightEntity` in `UserOwned` |

**Communities — changed**

| Path | Change |
|---|---|
| `Contracts/SignificantWin.cs` | **deleted** (moved to PlayerProgress) |
| `Domain/CommunityHighlightPolicy.cs`, `Application/CommunityHighlightCapturer.cs`, `Application/CommunityHighlightSaga.cs`, `Application/CommunityHighlightPurgeConsumer.cs` | **deleted** (moved) |
| `Domain/ICommunityHighlightRepository.cs` | shrinks to the audience index: write `(EventId, CommunityId, UserId)`, read `EventId`s for a membership set |
| `Infrastructure/EFCommunityHighlightRepository.cs` | reshaped; reads the payload from PlayerProgress's contract instead of its own `Payload` column |
| `Application/CommunityHighlightIndexSaga.cs` | new — `IConsumer<PlayerHighlightsStoredEvent>`, writes the audience rows only. Replaces `CommunityHighlightSaga`'s classify-and-persist role. |
| `Application/GetMyCommunityHighlightsHandler.cs` | re-pointed at the new payload query; the membership gate and own-wins toggle are unchanged |
| `Wiring/CommunitiesRegistrationExtensions.cs` | swaps the two moved consumer registrations for the index saga; drops the capturer |

⚠ **Delete the ports before the implementations** — reflection DI means an orphaned
implementation is a runtime-only failure nothing catches.

**OfficialMirror — changed**

| Path | Change |
|---|---|
| `Contracts/Events/OfficialPlayerLinkedEvent.cs` | new — published from the import path where `LinkPlayer` runs |
| `Contracts/Events/OfficialPlayerRenamedEvent.cs` | new — published when a rename proposal is accepted |
| `Contracts/Queries/ResolveOfficialPlayerQuery.cs` | new — tag → `(exists, linked UserId?, avatar, lastSeen)` |
| `Contracts/Queries/GetOfficialScoresForTagsQuery.cs` | new — board scores for (tags × chartIds) at the latest sealed snapshot, batched |
| `Contracts/Queries/SearchOfficialBoardTagsQuery.cs` | new — snapshot-scoped tag search for the picker, term + cap pushed to SQL (D21) |
| `Application/OfficialLeaderboardSaga.cs`, the rename-accept handler | + the two publishes |
| `Infrastructure/OfficialSiteClient.cs` | the `?? DefaultAvatar` fix (§6) |

**Identity — unchanged.** The picker composes Identity's existing `SearchForUsersQuery` with the
clubmate set, in Rivals. See the §4.1 note.

**Web — changed**

```
Pages/Compete/Rivals.razor                        new  (roster · rivals-of-you · feed tabs)
Pages/Compete/RivalInvite.razor                   new  (/Rivals/Invite/{code})
Components/Rivals/RivalRosterRow.razor            new
Components/Rivals/RivalsOfMeList.razor            new
Components/Rivals/RivalInviteCard.razor           new
Components/Rivals/AddRivalPanel.razor             new  (the two pickers)
Components/Rivals/RivalHighlightsFeed.razor       new
Components/Players/PlayerSummary.razor            new  (extracted from Pages/Communities/CommunityPlayer)
Components/Players/PlayerComparison.razor         new  (the capability-gated compare table)
Components/ChartLeaderboardScopes.razor           new  (extracted from ChartLeaderboardDialog)
Components/ChartLeaderboardDialog.razor           now a thin MudDialog wrapper + the Rivals scope
Components/Sessions/CommunityPeersSection.razor   + the MudButtonGroup toggle and the rivals board
Components/HomeWidgets/CommunityHighlightsWidget  label → "Highlights feed", + the rivals checkbox
Components/LeaderboardDialog.razor                + the rival/segmented row classes
Components/ChartLeaderboardSection.razor          + the rival/segmented row classes
Components/HomeWidgets/DailyStepWidget.razor      + the rival row class (outline only, D45)
Components/Challenges/WeeklyBoardGrid.razor       + the rival row class (outline only, D45)
Services/Theming/…                                — unchanged; the token is a site.css :root value
wwwroot/css/site.css                              + --daily-rival, .is-rival, .is-both, roster rows
Shared/_SiteLayout + Components/Shell/…           + /Rivals under Compete (desktop menu + More sheet)
```

Localization: every new string through `L[…]`, landing in **all nine locales in the same pass**,
inserted in alphabetical position (`ResxKeysAreStoredAlphabetically`), including `en-ZW` under
the Murloc alphabet ratchet. This is a large key set — a page, a landing page, a dialog scope, a
roster, two feeds and a widget.

### 4.3 Migration (one)

| Table | Change |
|---|---|
| `Rival` | new. `Id` PK; unique filtered `(OwnerUserId, TargetUserId)` and `(OwnerUserId, TargetTag)`; index `(TargetUserId)` for the reverse list; index `(TargetTag)` for the promote/rename consumers |
| `RivalBlock` | new. PK `(UserId, BlockedUserId)`; index `(BlockedUserId)` for the reverse check |
| `RivalInviteCode` | new. PK `UserId`; unique `(Code)` |
| `PlayerHighlight` | new. PK `EventId`; index `(UserId, MixId, OccurredAt)` — the feed's seek |
| `CommunityHighlight` | `Payload` **retained and no longer written** (tables are never dropped; a column stops being written rather than being removed) |

Backfill: copy the last 30 days of `CommunityHighlight` into `PlayerHighlight`, deduped by
`EventId`. Small, one-shot, behind an admin button following the `RebuildHighlights` precedent.

---

## 5. Sequencing

**One PR** (owner, 2026-08-02). Commit order below; the hard constraints are marked ⛓.

### Phase 0 — clear the ground

| # | Commit |
|---|---|
| 1 | `docs(rivals): the design doc` |
| 2 | `refactor(progress): rename the recap's RivalMatcher` — ⛓ **first**, while "rival" still means one thing in this codebase (D48) |

### Phase 1 — the vertical exists

| # | Commit |
|---|---|
| 3 | `feat(rivals): the vertical skeleton` — csproj, `GlobalUsings`, `AddRivals`, `AddRivalsConsumers`, `RivalsModelContribution`; solution + project refs; registered in `VerticalModelContributions.All()` and `Program.cs`. Empty but wired — this is the commit that satisfies all three registration tripwires. |
| 4 | `feat(rivals): entities and repositories` — the three entities, `ToTable` lines, `IRivalRepository` / `IRivalInviteCodeRepository` + EF implementations, `EFAccountPurgeRepository` with all three in `UserOwned` |

### Phase 2 — the tier-2 relocation

| # | Commit |
|---|---|
| 5 | `feat(progress): the PlayerHighlight store` — entity, repository, port, `PlayerHighlightRecord`, `GetPlayerHighlightsQuery`, `PlayerHighlightsStoredEvent`. Additive; nothing consumes it yet. |
| 6 | `refactor(highlights): the win policy and capture move to PlayerProgress` — ⛓ **one commit, not two.** Policy, capturer, saga and purge consumer relocate; the capturer writes `PlayerHighlight` and publishes; Communities' new `CommunityHighlightIndexSaga` writes the audience rows; `GetMyCommunityHighlightsHandler` reads the payload through the new query. Splitting this leaves two consumers classifying the same event and double-writing. |
| 7 | `feat(admin): backfill PlayerHighlight from the community index` — 30 days, deduped by `EventId`, following the `RebuildHighlights` button precedent |

### Phase 3 — the migration

| # | Commit |
|---|---|
| 8 | `feat(data): the Rivals and PlayerHighlight migration` — ⛓ **after every entity exists** (commits 4 and 5), so the PR carries exactly one scaffolded migration. Includes the `DATABASE-SCHEMA.md` rows. |

### Phase 4 — OfficialMirror

| # | Commit |
|---|---|
| 9 | `feat(mirror): publish player-linked and player-renamed events` |
| 10 | `feat(mirror): resolve a tag, and board scores for tags × charts` — plus `SearchOfficialBoardTagsQuery` for the picker (D21) |
| 11 | `fix(mirror): stop persisting the default avatar over a good one` — §6; independent of Rivals, but the roster is what makes it visible |

### Phase 5 — Rivals logic

| # | Commit |
|---|---|
| 12 | `feat(rivals): the visibility policy and invite code value type` — pure domain, unit tests |
| 13 | `feat(rivals): subject resolution` — ⛓ **after 9 and 10**: `RivalSubjectResolver` plus the link and rename consumers |
| 14 | `feat(rivals): add, remove, block, unblock` — block symmetry tested in both directions |
| 15 | `feat(rivals): the invite code lifecycle` — recycle, redeem, preview |
| 16 | `feat(identity): player search for the rival picker` |
| 17 | `feat(rivals): the roster and reverse-list reads` |
| 18 | `feat(rivals): rival scores for a set of charts` — ⛓ the D45 seed query; every surface below consumes it |
| 19 | `feat(rivals): the head-to-head read` |
| 20 | `feat(rivals): the rivals highlights feed read` — ⛓ **after 6** |

### Phase 6 — the highlight vocabulary

| # | Commit |
|---|---|
| 21 | `feat(ui): --daily-rival and the segmented row state` — ⛓ **before every surface below.** Tokens plus the utility classes for all four row shapes; no consumers yet. |

### Phase 7 — Web

| # | Commit |
|---|---|
| 22 | `refactor(web): extract ChartLeaderboardScopes from the dialog` — ⛓ **pure extraction, no behaviour change, before 23.** Adding a fifth scope first would build it into a shell the next branch dismantles. |
| 23 | `feat(web): the Rivals scope on the chart leaderboard` |
| 24 | `refactor(web): the shared player summary and comparison components` — extracted from `CommunityPlayer`, capability-gated (D29) |
| 25 | `feat(web): the Rivals page` — roster, add panel, invite card, rivals-of-you |
| 26 | `feat(web): the rival invite landing page` |
| 27 | `feat(web): the rivals feed` |
| 28 | `feat(web): Community Peers ⟷ Rivals on the sessions page` |
| 29 | `feat(web): the Highlights feed widget` — label rename, rivals checkbox, config default off (D38) |
| 30 | `feat(web): rival highlighting on the remaining boards` — `LeaderboardDialog`, `ChartLeaderboardSection`, `DailyStepWidget`, `WeeklyBoardGrid` (outline only, D45) |
| 31 | `feat(web): /Rivals under Compete` — desktop menu and mobile More sheet |

### Phase 8 — close out

| # | Commit |
|---|---|
| 32 | `feat(l10n): Rivals strings in all nine locales` — ⛓ **after the UI settles**, so the alphabetical insertion happens once. English renders correctly throughout the phase-7 commits because the key text *is* the English copy; this commit adds the `en-US` entries the ratchet needs plus the eight translations, Murloc included. |
| 33 | `test(rivals): real-DB coverage` — the three repositories, the filtered unique indexes, and the purge covering **both** directions of `Rival`. Split out because it needs Docker; unit and component tests ride with their feature commits. |
| 34 | `docs: architecture, schema and UX guidelines for Rivals` — the vertical in the solution layout and code map, the `/Rivals` row in the Pages table, the new row token in the UX guidelines |

### The three failure modes this order defends against

- **Splitting commit 6** — two capturers on one event, double-writing until the second half lands.
- **Scaffolding the migration early** — a second migration for whatever entity arrived later, and
  the first is already applied and uneditable.
- **Adding the fifth scope before extracting** — the extraction becomes a conflict with itself
  once the leaderboard contents move onto the chart details page and dialog.

---

## 6. Risks and build notes

- **`OfficialSiteClient`'s avatar asymmetry** (independent of this feature, but the roster makes
  it visible). The chart-board path passes `MirrorAvatar(...) ?? DefaultAvatar` — never null —
  while the rating-board path lets null flow. `EnsurePlayers` writes any non-null avatar, so an
  unparseable avatar on a chart board **overwrites a good mirrored avatar with the stock blue
  Phoenix character**, where the rating path would have preserved it. Invisible on a 300-row
  board; on a rival roster it's your rival's face becoming a stock character. Fix: let the null
  flow, render `DefaultAvatar` at display time.
- **Account purge and `OfficialPlayer.UserId`.** A purged account leaves the mirror's player row
  pointing at a dead Guid, so a ghost tag would resolve to nothing. `OfficialMirror` has an
  `AccountPurgeConsumer` already — verify it covers that column rather than assuming it does.
- **Avatar directories are not content-addressed.** piugame's `/data/avatar_img/` (P1) and
  `/data/avatar_img2/` (P2) serve **different pictures under identical filenames**. The mirror
  already namespaces correctly (`/avatars/` vs `/avatars/p2/`) and both the sweep and the
  personal-import path share `ConvertPiuGameAvatarToPiuScoresAvatar`, so ghost and user avatars
  are the same source. Don't "simplify" that folder split away.
- **Set-based reads or nothing.** §2.5. Three hundred rivals × N charts is the shape that will
  actually be exercised, not the exception.
- **A picker's query runs per keystroke, and gets cancelled mid-flight.** Found in field testing:
  the board-tag picker reused `GetOfficialPlayerNamesQuery(CurrentBoardsOnly: true)`, which reads
  every placement in the sealed snapshot, de-duplicates to a tag list, ships the whole thing to the
  handler, and keeps the ten that match. Two failures compound. The query is slow enough that
  MudAutocomplete cancels it when the next character arrives — and **a cancelled `SqlCommand` does
  not raise `OperationCanceledException`**; SqlClient raises "a severe error occurred on the current
  command", so a `catch (OperationCanceledException)` would miss it and the unhandled exception
  tears down the Blazor circuit. Both halves are fixed: term and cap pushed into SQL as an EXISTS
  semi-join over the player dimension (D21), and the panel filters on
  `token.IsCancellationRequested` rather than on exception type — Web cannot reference SqlClient to
  catch it by type, and a genuine failure must still surface. Any future per-keystroke surface
  inherits both traps.
- **Two normalizers is the recurring bug in this codebase.** D7 exists because
  `PiuGameApi.ImageRegex` and `OfficialSiteClient.ImageRegex` drifted apart and cost two
  separate outages with different symptoms.

---

## 7. Testing

| Suite | What lands there |
|---|---|
| `ScoreTracker.Tests/DomainTests` | `RivalVisibilityPolicy` (all four bases + the private-ghost exception), `RivalInviteCode` value type, the tier-2 policy after its move |
| `ScoreTracker.Tests/ApplicationTests` | Add/Remove/Block/Unblock handlers (block symmetry both directions), `RivalSubjectResolver` (D4 normalization), the two promote/rename consumers, `GetRivalScoresForChartsHandler` merging site + official sources |
| `ScoreTracker.Tests/ArchitectureTests` | the existing ratchets pick up the new vertical automatically — `AccountPurgeCoverageTests`, `ModelContributionRegistrationTests`, `VerticalBoundaryTests`, `UiColorTokenTests`, `LocalizationKeyTests`, `ResxKeysAreStoredAlphabetically` |
| `ScoreTracker.Tests.Components` | roster rendering (ghost tag + asterisk + footnote), the sessions toggle, the fifth dialog scope, the four highlight states, feed cross-markers, which reverse-list rows offer **Add as rival** |
| `ScoreTracker.Tests.Integration` | the three repositories against real SQL, the unique filtered indexes, the purge covering both directions of `Rival` |
| `ScoreTracker.Tests.E2E` | not a critical whole-workflow path — nothing new |


---

## 9. What shipped, and what did not

Built 2026-08-02 across 34 commits on `claude/rivals-feature-52cade`. Suites at the end:
**1,946 unit/component · 526 bUnit · 90 real-DB integration**, all green.

Three things moved during the build and the decisions above already reflect them:

- **D48 narrowed.** Only the recap's internal `RivalMatcher` renamed. `PlayerRecap` is serialized
  whole behind a `SchemaVersion` equality gate, so renaming its public `Rivals` property would
  blank every stored recap until an admin rebuild — a real cost for a cosmetic win. The
  user-facing collision gets fixed in the resx instead.
- **Identity needed no change at all** (§4.1). The picker composes its existing search.
- **`RenameProposal.Mix` is nullable rather than a detector parameter.** The detector is genuinely
  mix-agnostic and has eight call sites; threading data through a pure function so its output can
  carry it is worse than saying what is true — Mix is null until stored, the same lifecycle `Id`
  already expresses by being 0 until written.

### Outstanding

- **Localization.** Deferred to the pre-merge checks by the owner. Every new string already goes
  through `L[…]`; what remains is the `en-US` entries the ratchet needs plus the eight
  translations, inserted alphabetically, Murloc included.
- **The `/Community/Player` summary lift (D29's second host).** `RivalComparison` was built
  capability-gated and host-agnostic so it can take that page's subject, but the extraction from
  `CommunityPlayer.razor` (513 lines) has not been done. The Rivals page uses the new component;
  the community page still uses its own.
- **Roster sort is newest-arrow-first, not recent-activity** (D18). Recent activity needs each
  rival's last import, which is a read the roster does not otherwise make; the sort control is
  the shape the field test should judge before that read gets added.
- **Rival scores are `IN (…)`, not a TVP** (§2.5). Correct at any size, and the shape to revisit
  if a three-hundred-rival roster proves slow in the field.
