# Communities Overhaul

Status: **built** (commits 1–8 landed on the feature branch; localization sweep rides the same
branch). Build deviations from the original plan are listed in §9.

Makeover of the Communities feature: declutter the directory, formalize **roles &
permissions** for user communities, give **regional/World** communities their own visual
lane, add a real **invite landing + join consent**, and rebuild **community leaderboards**
to mirror the Official Leaderboards surface on piuscores-native data.

The interactive mock (6→5 screens, real Phoenix palette) is hosted in the claude.ai design
project "PIU Score Tracker — Mocks"; source `communities-overhaul-mock.html`.

---

## 1. Scope & the regional/user split

Three things share the Communities page today and shouldn't be treated identically:

- **User communities** — social groups. Owned, roles, invites, private/public. *These get
  the full overhaul.*
- **Regional communities** — one per country, auto-created, unowned (`OwnerId = Guid.Empty`),
  auto-joined by mirroring profile `Country`.
- **World** — the single global regional community, auto-joined by the profile public toggle.

Regional + World **stay on the Communities page** but get a distinct visual lane (they are
leaderboards, not social groups — no roles, no invites, no management). The role/permission,
invite, and management work below applies **only to non-regional communities**.

Current state (for reference): `Community : UserGroup` (`Domain/Models/Community.cs`);
membership is a bare `(Id, CommunityId, UserId)` row (`CommunityMembershipEntity`); the
creator is a denormalized `Community.OwningUserId` column, **not** a membership row; the only
privilege distinction enforced today is membership-vs-not; privacy is
`CommunityPrivacyType {Public, PublicWithCode, Private}`; invites (`CommunityInvite.razor`)
**silently auto-join**; leaderboards (`CommunityLeaderboard.razor`) already run on
`IPlayerStatsReader`/`IScoreReader`; regional is a `bool IsRegional`. Official Leaderboards are
a **separate vertical** (`OfficialMirror`) over scraped piugame data — only its *presentation*
(`HubRankings` ranked table, `OfficialSectionFrame` chrome) is reusable.

---

## 2. Domain

Roles are a domain concern, so `Community` becomes a **rich aggregate** (the
`TournamentSession` reference pattern), replacing today's scattered membership checks. Authorization
has exactly one home: the aggregate.

### 2.1 New value types (`ScoreTracker.SharedKernel.Enums`)

```csharp
public enum CommunityRole { Creator, Admin, Member, Banned }

[Flags]
public enum CommunityPermission
{
    None                       = 0,
    ManageInviteLinks          = 1 << 0,
    PromoteAdmins              = 1 << 1,
    ManageUsers                = 1 << 2,   // ban / unban
    ManageChannelSubscriptions = 1 << 3
}
```

`Creator` implicitly holds every permission. Permissions are only meaningful for `Admin`.
`Banned` is a terminal state whose row is retained precisely to block rejoin.

### 2.2 Membership becomes a member, not a Guid

```csharp
public sealed record CommunityMember(
    Guid UserId, CommunityRole Role, CommunityPermission Permissions,
    Guid? GrantedBy, DateTimeOffset? JoinedAt);
```

`Community` holds `IReadOnlyCollection<CommunityMember> Members`. `MemberIds` stays as a
**derived** convenience (`Members.Where(m => m.Role != Banned).Select(UserId)`) so existing
callers (`community.MemberIds.Contains(...)`) keep compiling. `OwnerId` stays as a denormalized
creator pointer, kept in sync by `TransferCreator`.

### 2.3 Aggregate methods (each takes the acting user, throws on violation)

| Method | Who may call | Invariant |
|---|---|---|
| `PromoteToAdmin(actor, target, perms)` | Creator, or Admin with `PromoteAdmins` | granted `perms` ⊆ actor's own perms (delegation rule) |
| `GrantPermissions(actor, target, perms)` | same | same subset rule; target must be Admin |
| `DemoteToMember(actor, target)` | Creator; Admin w/ `PromoteAdmins` may demote lower admins | cannot demote the Creator |
| `Ban(actor, target)` / `Unban(actor, target)` | Creator; Admin with `ManageUsers` | cannot ban the Creator; ban → `Role = Banned` (row kept) |
| `TransferCreator(actor, target)` | Creator only | **single seat**: old creator → Admin(all perms), target → Creator, `OwnerId` updated |
| `SetDefaultAdminPermissions(actor, perms)` | Creator only | — |
| `SetPrivacy(actor, privacy)` | Creator only | — |
| `SetDefaultLanguage(actor, culture)` | Creator only | — |
| `Delete` | Creator only | handled at command level |

New `Community` fields: `CommunityPermission DefaultAdminPermissions` and `string? DefaultLanguage`
(a culture code; Discord-notification fallback).

New exception: `CommunityPermissionException : ` the community/domain base (in `Domain/Exceptions/`).

Regional/World: ownerless, so every management method throws — they have no Creator to authorize.
The UI never surfaces management for `IsRegional` communities anyway.

> **Note:** roles live on `Community`, not the `UserGroup` base — no other UserGroup needs them today.

---

## 3. Application

Handlers shrink to: resolve actor (`ICurrentUserAccessor`) → load aggregate → call method →
persist. The aggregate does the authorizing.

### 3.1 New commands (`Contracts/Commands/`)

`PromoteMemberCommand`, `DemoteMemberCommand`, `BanMemberCommand`, `UnbanMemberCommand`,
`SetMemberPermissionsCommand`, `TransferCommunityOwnershipCommand`, `SetCommunityPrivacyCommand`,
`SetDefaultAdminPermissionsCommand`, `SetCommunityLanguageCommand`, `DeleteCommunityCommand`.

### 3.2 Changed handlers

- `JoinCommunityCommand` / `JoinCommunityByInviteCodeCommand` — **reject if the user has a
  `Banned` membership row** (both paths). Invite-code join additionally validates the code and
  surfaces a typed "banned" outcome to the landing page.
- New members are created with `Role = Member`, `Permissions = None`, `JoinedAt = now`.

### 3.3 New queries (`Contracts/Queries/`)

- `GetCommunityInvitePreviewQuery(code)` → community name, avatar seed, privacy, member count,
  inviter, code validity, and whether the current user is banned. Feeds the landing page.
- `GetMyCommunityRoleQuery(communityId)` → current user's `Role` + `Permissions`. The UI hides
  controls it may not use (defense in depth; the aggregate still enforces).
- `GetCommunityPlayerProfileQuery(communityId, userId, mix)` → the player summary: PUMBILITY,
  ratings, competitive level, highest clear, per-folder completion, and — **if resolvable** — the
  official placement + Top-300 count + official-profile link.
- `GetCommunityFolderComparisonQuery(communityId, meId, themId, folder, mix)` → per-chart
  you-vs-them: win/loss, score deltas (with score-age staleness), folder skill-average and
  completion for both. Logged-in only.
- `GetCommunityMembersQuery` — extended to carry each member's role + permissions.

Existing `GetCommunityLeaderboardQuery` / `GetPhoenixRecordsForCommunityQuery` are reused as-is
for Rankings and By-Chart.

### 3.4 Cross-vertical reads (ports only, never SQL joins)

- Player summary: `IPlayerStatsReader.GetStats` (PlayerProgress), `IScoreReader.GetPhoenixScores`
  (ScoreLedger), chart/folder metadata (Catalog), and the official placement/Top-300 tiles from
  OfficialMirror. **The piuscores↔official link already exists and is created on user import**:
  `OfficialLeaderboardSaga.RunImport` (OfficialLeaderboardSaga.cs:142) — the path behind both the
  live PIUGAME import (`ImportOfficialPlayerScoresCommand`) and the background/remembered-password
  import (`ExecuteImportCommand`) — calls `IOfficialPlayerIdentityRepository.LinkPlayer` the moment
  it reads the account's game tag authoritatively. `LinkPlayer` **upserts** the `(mix, username)`
  player row (creating it if the player was never board-visible) and stamps `UserId` +
  `UserIdSource = "Import"`; account merges re-point it (`PlayerIdentitySaga`). Only CSV/XX uploads
  don't link — by nature, they never authenticate to piugame, so there's no authoritative username.
  So no username-resolution effort and no new import wiring — we add
  one thin published query keyed by **userId** (the existing `GetOfficialPlayerProfileQuery` is
  keyed by username) that returns placement + Top-300 for a linked player. "If applicable" is just
  the natural null case: `OfficialPlayerEntity.UserId == null` (never imported / unlinked) → omit
  the official tiles.
- Folder completion + compare: `IScoreReader` + the existing score-age algorithm + Catalog folder
  membership.

---

## 4. Infrastructure

### 4.1 Migration `CommunityRolesAndPermissions`

`CommunityMembership` gains:

| Column | Type | Note |
|---|---|---|
| `Role` | `nvarchar` | enum name; default `Member` |
| `Permissions` | `int` | flags; default `0` |
| `GrantedByUserId` | `uniqueidentifier?` | audit of who promoted/granted |
| `JoinedAt` | `datetime2?` | null for backfilled rows |

`Community` gains `DefaultAdminPermissions` (`int`) and `DefaultLanguage` (`nvarchar?`).

**Backfill (in the migration):**
1. Every existing membership row → `Role = 'Member'`.
2. For every **non-regional** community: ensure a `Creator` membership row exists for
   `OwningUserId` (insert if absent — owners are not members today).
3. `Community.DefaultAdminPermissions` seeded to
   `ManageInviteLinks | ManageUsers | ManageChannelSubscriptions` (not `PromoteAdmins`).
4. Regional/World rows untouched (ownerless, all `Member`).

Model contribution (`CommunitiesModelContribution`) is unchanged — same five tables. New index:
`(CommunityId, Role)` for roster/permission reads.

### 4.2 Repository

`EFCommunitiesRepository` hydrates `Members` with role/permissions and persists membership
mutations; `TransferCreator` writes both the role swap **and** `Community.OwningUserId`. The
existing `GetLeaderboard` path is untouched.

---

## 5. Presentation

### 5.1 Directory (`Communities.razor`)

Rev-2 layout: **left rail** = small World card over a thin vertical Regions card (auto-membership
leaderboards, no management); **right field** = "Your Communities" and "Explore" horizontal cards.
Grid areas on desktop; on mobile reflows to **World → Your Communities → Explore → Regions**.
Regional rows never render management affordances.

### 5.2 Community detail — tabs

`Rankings · By Chart · Members` (the old fused leaderboard page splits along these). The
multi-player head-to-head tool is **removed** (superseded by the player-page folder compare).

- **Rankings** — the Official-style ranked table: PUMBILITY primary sort, board selector
  (PUMBILITY / Total Rating / Co-Op), with Singles / Doubles / competitive-level as columns;
  rows deep-link to the player summary. **No separate Players tab** — the ranking rows are the
  index into player pages.
- **By Chart** — the existing per-chart community board (`ChartSelector` →
  `GetPhoenixRecordsForCommunityQuery`), kept as its own tab.
- **Members** — roster visible to all; promote/demote/ban/unban/edit-perms controls gated by
  `GetMyCommunityRoleQuery`; a creator-only panel for default admin permissions, visibility,
  notification language, transfer-creator, and delete.

**Shared table extraction:** lift the ranked-table + rank-badge + expandable top-50 grid out of
`HubRankings.razor` into `Components/RankedLeaderboardTable.razor` so Official and Community share
one component. (If we'd rather not touch Official, clone into Communities instead — noted as the
fallback.)

### 5.3 Player summary — new route `/Community/{name}/Player/{userId}`

Home-page-style: **big glowy PUMBILITY top-left** of the identity card; name, country,
competitive level, official placement pill + "N Top-300 charts →" link (when resolvable), and the
stat tiles. Below: **folder-completion vertical bars** (completed over remaining, minimal labels —
the home-page graph, lightweight). Then **Compare in folder**, driven by the shared
`FolderPicker` (`Components/FolderPicker.razor`): win/loss counts, per-chart score deltas with the
score-age "stale" badge, and folder skill-average + completion for both players. Compare renders
only when logged in.

### 5.4 Invite landing (`CommunityInvite.razor`) — replace auto-join

Preview + accept: community card (name, privacy, member count, inviter, code validity) with
**Accept / No thanks**. Not-logged-in → login then return. Banned → a "you can't rejoin" state.
For a **private-profile** user (`User.IsPublic == false`), accepting opens the **consent dialog**:
"Your scores will be visible to everyone in this community — continue?" Membership is created only
after consent. Public users skip the dialog.

### 5.5 Cross-cutting

- Every new string via `L[…]`, populated in all nine locales in the same pass (glossary per
  `docs/LOCALIZATION-<locale>.md`).
- No color literals — mix/rarity/difficulty tokens only (`UiColorTokenTests`).
- Discord notification language: `CommunitySaga` uses `Community.DefaultLanguage` as the fallback
  when a subscribed channel has no language of its own; per-reply culture still follows the
  account (see the Discord integration work).

---

## 6. Deferred / open

- **Rank-delta arrows (▲▼)** — **decided: not in v1.** Community boards store no history; deltas
  would need a community-rank snapshot table + weekly Hangfire job. Deferred to a possible follow-up;
  v1 ranked tables render without movement arrows.
- **Official placement link** — **resolved: no mapping work.** The link already exists
  (`OfficialPlayerEntity.UserId`, import-stamped); §3.4 just adds a by-userId query. Unlinked users
  omit the tiles.
- **Shared table extraction vs clone** — *still open.* Recommended: extract
  `RankedLeaderboardTable` from `HubRankings` (one component, both Official + Community); fallback:
  clone into Communities to leave Official untouched. Only outstanding pre-build decision.

---

## 7. Commit order

Each commit is independently mergeable, builds green, and lands its own tests + doc updates.

1. **Domain: roles & permissions model.** `CommunityRole` + `CommunityPermission` enums,
   `CommunityMember`, the `Community` aggregate methods + invariants + `CommunityPermissionException`.
   `DomainTests` for every invariant (delegation subset, single-seat transfer, ban-blocks,
   creator-only guards). Pure — no infra. `MemberIds` stays derived so nothing downstream breaks.
2. **Infra: schema + repository.** Migration `CommunityRolesAndPermissions` with the backfill;
   `EFCommunitiesRepository` hydrate/persist; keep `OwningUserId` in sync. Integration test
   (real SQL) for backfill + round-trip. `docs/DATABASE-SCHEMA.md` rows.
3. **Application: management commands + handlers.** The §3.1 command set + handlers; ban-check on
   both join paths; `GetMyCommunityRoleQuery`; extend `GetCommunityMembersQuery`. `ApplicationTests`
   (mock ports; authorization delegated to the aggregate, so mostly persistence + outcome checks).
4. **Presentation: Members / roles tab.** Roster + gated controls + creator settings panel
   (default perms, visibility, language, transfer, delete). `Tests.Components` (bUnit: controls
   show/hide by role). Localization pass.
5. **Invite landing + consent.** `GetCommunityInvitePreviewQuery`; preview/accept page; private-
   profile consent dialog; banned state. Retire the auto-join path. One E2E for the accept flow.
   Localization.
6. **Directory makeover.** World card + Regions rail + Your/Explore field; regional/user visual
   split; overview query shape. `Tests.Components`. Localization.
7. **Leaderboard restyle (Rankings + By Chart).** Extract `RankedLeaderboardTable` from
   `HubRankings`; wire `GetCommunityLeaderboardQuery` with the comp-level column; split the fused
   page into tabs; remove the old head-to-head. `Tests.Components`. Localization.
8. **Player summary + folder compare.** `GetCommunityPlayerProfileQuery` +
   `GetCommunityFolderComparisonQuery`; glowy hero; vertical folder-completion bars; `FolderPicker`
   compare; official cross-read (if resolvable); deep-link from Rankings. Tests at the lowest level
   that catches it. Localization.
9. **Discord language fallback.** `CommunitySaga` consumes `Community.DefaultLanguage`. Tests.
10. **Docs + cleanup.** Finalize this doc, update `docs/ARCHITECTURE.md` (if the aggregate/ port
    shape counts as structural), `docs/DATABASE-SCHEMA.md`, and delete dead code (auto-join,
    head-to-head). `docs/SCHEDULED-JOBS.md` only if the deferred delta-snapshot job lands here.

Dependencies: 1→2→3; 4 & 5 depend on 3; 6/7/8 depend on 2–3; 9 depends on 2. 4–9 can interleave
once 3 is in.

---

## 8. Docs to touch (per the "same-PR" rule)

`docs/DATABASE-SCHEMA.md` (membership/community columns), `docs/ARCHITECTURE.md` (Communities
aggregate + new ports, if structural), this file, and — only if deferred work lands —
`docs/SCHEDULED-JOBS.md`.

## 9. As built — deviations from the plan

- **The Discord language fallback landed after the main merge, not as its own commit slot.**
  Pre-merge this branch had no localization machinery in the Discord card path, so the wiring
  waited; once the Discord-overhaul merge (#169) arrived via main, `Community.DefaultLanguage`
  slotted into the fan-out's culture resolution as designed — channel language → community
  default → English (`CommunitySaga.GetCommunityChannels`), with the creator's input normalized
  through `SupportedCultures` on save.
- **No new E2E for the invite flow.** The UI granularity ladder wins over §7's original "one
  E2E": every branch (preview states, both consent paths, banned) is pinned at bUnit level and
  the preview/join logic at application level.
- **Invite-link creation is now permission-gated** (`ManageInviteLinks` or creator), replacing
  the old any-member rule — implied by the roles design, made explicit here because it's a
  behavior change for existing communities (plain members lose the Invite button; the migration's
  `DefaultAdminPermissions` seed includes the flag, so newly promoted admins keep it).
- **Ranked-table sharing landed as atoms, not a wrapper.** The two tables share the rank cell
  (`Components/RankDelta.razor`), the `olb-*` styling, and the expandable `tier-card-grid` +
  `TierListChartCard` pattern; a generic templated table wrapper would have been speculative
  generality — the tables agree on look, not shape.
- **The community rankings board is compact rows, not a table** (post-merge with the official
  field-test rounds): the leaderboard skin moved out of `OfficialSectionFrame`'s `<style>` into
  `site.css`, so the community board renders the same `.olb-rank-card` rows — rank left, player
  next, a tail carrying the playstyle chip, charts played, competitive level and PUMBILITY —
  with its own `MudPagination`, `.olb-row-me` on your row, and the sortable compare/members
  tables wearing `.olb-board-table`. The playstyle chip is one shared component
  (`Components/PlayerTypeChip.razor`) over `MixThemes.PlayerTypeHex`, whose ramp is now the
  grade-metal ladder the official rankings shipped (AA → AAA+ → S+ → SSS → SSS+, summit glows).
- **The directory's World card shows member count + your PUMBILITY, not your world rank** —
  computing a live rank means sorting the whole World membership on a directory load; deferred
  with the rank-delta snapshot work.
- **Players tab dropped entirely** (owner call): ranking rows deep-link straight to
  `/Community/Player`.
