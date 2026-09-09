# Discord role management — design

> **Status: BUILT.** Workshopped and built 2026-09-09, one PR, 14 commits.
>
> Mock (owner-approved): <https://claude.ai/code/artifact/c143bfe5-cff8-4b9a-b878-665a1aac8197>

A community admin maps a Phoenix 2 title to a role in their Discord server. Anyone who holds the
title gets the role, and keeps it exactly as long as they still qualify. Nobody asks, nobody
hands anything out by hand.

The whole feature is one sentence, and the owner wrote it:

> *If a user is in a community, that community has a role configured for a title, and the user is
> in that community's configured Discord, and the user has that title, they get the role assigned
> — from any angle that those facts may change.*

Everything below is that sentence made mechanical.

---

## 1. Locked decisions

Owner calls from the 2026-09-09 workshop.

### Scope and eligibility

| # | Decision |
|---|---|
| D1 | **Phoenix 2 only.** No `Mix` column anywhere; the mix is a constant in this feature. If Phoenix 1 titles ever want roles, that is a migration, not a redesign. |
| D2 | **Both memberships are required** — in the community *and* in the linked Discord server. Being in the server alone is not enough, and neither is being in the community. |
| D3 | **A community designates one Discord server.** A channel registered for score feeds is **not** that designation — feeds and roles are separate concerns, and a server that receives a community's scores does not thereby belong to it. |
| D4 | **No auto-join in either direction.** Entering the Discord does not join the site community, and joining the community does not put anyone in a server. |
| D5 | **Any of the 272 Phoenix 2 titles can be mapped.** No curated subset — the picker searches the shipped list. |
| D6 | **Pumbility ladders are highest-only.** Reaching `[P.B] GOLD` strips `[P.B] BRONZE` and `[P.B] SILVER`. Everything else is keep-once-earned. |
| D7 | **Gem sub-levels are never offered.** The 37-rung `Phoenix2PumbilityLevel` badge ladder is a display artifact; a role maps to a gem, not to `LV.3` inside one. |

### Authority

| # | Decision |
|---|---|
| D8 | **New `CommunityPermission.ManageDiscord`**, delegable by the Creator under the existing rules. It does not subsume `ManageChannelSubscriptions`; feeds and roles are configured by different people in practice. |
| D9 | **Designating a server requires Discord authority too** — `Manage Server` in the target guild, read off the interaction that links it. Site permission alone cannot point a community at a server the admin does not run. |
| D10 | **The admin needs their own Discord account linked**, because designation happens by running a command as themselves. The page checks up front and sends them to `/Account` rather than letting them discover it mid-command. |

### Mechanics

| # | Decision |
|---|---|
| D11 | **One reconcile function, called from every angle.** `Reconcile(community, user)` computes the whole desired role set and diffs it. Nothing computes roles a second way, so nothing can disagree. |
| D12 | **The mapping table is the managed set.** A mapped role handed out by hand is taken back. This is stated on the page, because it is surprising. |
| D13 | **Unmapping a title leaves the role on people** unless the admin ticks the box. Removing a row is a config edit, not a mass revocation. |
| D14 | **Changing the designated server strips every role granted under the old one first.** Confirmed, not silent. |
| D15 | **Server Members intent ON, `AlwaysDownloadUsers` OFF.** The intent buys the join event; the member-list download is the expensive half and this feature never needs it. |
| D16 | **The sweep exists regardless of the intent.** The transport is in-memory: a dropped event would otherwise strand someone permanently. |

---

## 2. The rule

Four facts. All four true ⇒ the role is held. Any one false ⇒ it is removed.

```
holds(user, role) ⟺  ∃ mapping (community → title → role)
                  ∧  user ∈ community            and not banned
                  ∧  community has server S
                  ∧  user has a linked Discord account
                  ∧  user ∈ S
                  ∧  user holds title             on Phoenix 2
                  ∧  no higher rung of title's exclusivity group is held
```

### 2.1 Where each fact lives

| Fact | Source | Changes when |
|---|---|---|
| member of the community | `Community.MemberIds` / `RoleOf` | join, leave, ban, unban, community deleted |
| the mapping exists | `CommunityTitleRole` | an admin edits the table |
| a server is designated | `CommunityDiscordServer` | `/piu link-server`, change, unlink |
| Discord account linked | `IUserReader.GetExternalLogins` | link on `/Account`, unlink, account purge |
| in the server | `IBotClient.GetMemberRoles` returns non-null | joins, leaves, kicked, banned |
| holds the title | `ITitleRepository.GetCompletedTitles` | a score import earns one |

Only one of those — **joining the server** — changes outside the system without telling us. That is
the fact the Server Members intent exists to catch, and the sweep is the backstop for it.

### 2.2 Exclusivity is keyed on the pool, not the rail

`Title.Ladder` looks like the exclusivity primitive and is **not**. `Phoenix2TitleList`'s static
constructor rails the singles pool as four separate rails — `[S] INTERMEDIATE`, `[S] ADVANCED`,
`[S] EXPERT`, `[S] MASTER` — because thirty-one rungs on one line reads as nothing. Grouping by
`Ladder` would therefore give *per-band* exclusivity: reaching `[S] EXPERT LV.1` would leave
`[S] ADVANCED LV.10` in place.

The correct key is `Phoenix2PumbilityTitle.Pool` — Singles, Doubles, Total — which is exactly what
`TitleHelpers.LinkLadder` already groups by. `TitleExclusivity.GroupOf(title)` in
`Domain/Models/Titles/` returns that group and null for everything else, so the two cannot drift.

### 2.3 The unassignable role

Discord refuses a role ranked at or above the bot's own highest role, and refuses managed
(integration-owned) roles and `@everyone`. **It refuses silently from the player's side**: no error
reaches them, the role simply never arrives.

So assignability is computed, surfaced twice (server health list and the mapping row), and a role
that fails it is **skipped rather than retried** — a reconcile does not spend a write it knows will
be rejected.

---

## 3. The model

Three tables, all Communities-owned, registered through the existing
`CommunitiesModelContribution`.

### 3.1 `CommunityDiscordServer`

One row per community. `CommunityId` is unique — D3's "one server" is a database constraint, not a
convention.

| Column | Notes |
|---|---|
| `Id` | key |
| `CommunityId` | unique |
| `GuildId` | the Discord snowflake |
| `GuildName` | denormalized for the page; refreshed on read when the bot can see the guild |
| `DesignatedAt` | when |

There is deliberately **no `DesignatedByUserId`**. The community already learned that lesson with
`CommunityMembership.GrantedByUserId`, which nothing ever read.

### 3.2 `CommunityTitleRole`

One row per mapping. Unique on `(CommunityId, TitleName)` — one role per title — and separately on
`(CommunityId, RoleId)`, because two titles pointing at one role makes "remove it" ambiguous.

| Column | Notes |
|---|---|
| `Id` | key |
| `CommunityId` | |
| `TitleName` | the shipped Phoenix 2 title name, verbatim |
| `RoleId` | the Discord role snowflake |

### 3.3 `CommunityDiscordGrant`

One row per (community, user) we have ever granted for. **This is the table that makes removal
possible**, and it is why the feature is not stateless.

| Column | Notes |
|---|---|
| `Id` | key |
| `CommunityId` | |
| `UserId` | the purge key |
| `DiscordUserId` | the snowflake we granted against |
| `LastReconciledAt` | |

Two failures it fixes, both of which strand roles permanently otherwise:

- **Account purge.** `EFAccountPurgeRepository.UserOwned` in Identity deletes `ExternalLoginEntity`
  on the *first* purge pass, immediately after `context.Publish(AccountPurgeStartedEvent)`. An
  in-memory publish returns on dispatch, not on completion, so a consumer that tries to read the
  snowflake then is racing a delete it will usually lose — and on the daily re-fires the row is
  already gone. Reading our own row instead is ordering-independent.
- **Leaving the community.** A sweep walks *members*; someone who left is never visited, so their
  roles are never removed. The sweep walks **grants** instead, which is precisely the set of people
  who might need something taken away.

Note the row is per member, not per role: what gets removed is drawn from the community's currently
mapped roles, which is the same set the page calls managed (D12).

---

## 4. Triggers

Every one of these ends in the same `Reconcile`.

| Angle | Mechanism |
|---|---|
| earned a title / climbed a ladder | `PlayerTitlesChangedEvent` (PlayerProgress → Communities) |
| joined, left, banned, unbanned | in-process call from the existing management handlers |
| community deleted | existing `CommunityDeletedEvent` consumer |
| account purged | existing `AccountPurgeStartedEvent` consumer, reading the grant row |
| linked or unlinked Discord | picked up by the sweep; the grant row holds the snowflake to strip |
| admin edited a mapping | bulk reconcile over the community |
| server designated or changed | bulk reconcile; the old server is stripped first (D14) |
| **joined the Discord server** | `GuildMemberAdded`, plus the sweep as backstop |

### 4.1 `PlayerTitlesChangedEvent`

`PlayerProgress/Contracts/Events/PlayerTitlesChangedEvent(Guid UserId, MixEnum Mix)`, published from
`TitleSaga.ProcessCharts` after `SaveTitles` and only when the completed set actually moved.

A `Guid` and an enum, deliberately: an opaque value type on a bus message serializes to `{}` and
arrives as `default` with no error at all (CLAUDE.md, and the Daily Step card that printed every
placement as a 0-point F for seven weeks). There is nothing here that can do that.

### 4.2 The sweep

Hangfire, hourly. Per community with a designated server: one read for the mapped titles' holders
(`ITitleRepository.GetUsersWithTitles`), one bulk read for the members' Discord IDs, then a member
fetch per candidate. Writes only on a diff.

It is the backstop for the two angles nothing reports — someone joining the server if the gateway
event is missed, and someone unlinking their Discord — and the self-heal for anything the in-memory
bus dropped.

---

## 5. Surfaces

### 5.1 `/Community/Discord`

Four sections, in the mock's order.

1. **Discord server** — the designated server, who set it, and a health list: bot present, bot can
   manage roles, and any mapped role ranked above the bot. Three states: designated, not set up,
   and *your own Discord isn't linked* (D10).
2. **Titles and roles** — rows grouped by exclusivity group, with a `Highest only` chip on the
   pumbility groups. Add opens a searchable picker over the shipped title list; roles the bot
   cannot assign are disabled in the role dropdown with the reason on them.
3. **When a role is given** — the four facts as the page's own explanation of itself, plus last
   sweep and a manual trigger.
4. **Next check will change** — a dry run of the pending diff, so a mis-mapped role is caught
   before it lands on forty people.

Interactive render mode; every string through `L[…]`.

### 5.2 `/piu link-server`

Run in the target server, community chosen by autocomplete. The interaction proves which server it
is (`GuildId`) and that the invoker holds `Manage Server` there (`InvokerCanManageGuild`, new); the
handler checks `ManageDiscord` on the site side. Both authorities, one step, and no need to
enumerate servers we are not permitted to list.

### 5.3 The bot invite URL

`Communities.razor` asks for `permissions=347136` — view, send, embed, history, external emojis.
Manage Roles is `268435456`, so the URL becomes **`268782592`**.

⚠ **This does not retroactively grant anything.** Discord applies an invite's permissions when the
invite is accepted, so a server that already has the bot keeps whatever it was added with.

The page says so itself rather than relying on anyone reading a release note: `IBotClient` reports
the bot's own `ManageRoles` in the guild, a role reports `BotCannotManageRoles` when it is absent,
and `DiscordReinvitePointer` offers the invite link while that is true. It is driven by the real
state, not by a "seen it" flag — so it retires itself server by server as people re-add, and an
admin who never re-adds keeps being told. The ✕ dismisses it per account (UiSettings, the same shape
as the tier list's title pointer) for somebody who granted the permission by hand.

**Hierarchy and permission are different failures with different fixes**, which is why they are
separate reasons: no amount of role reordering fixes a permission the bot was never given.

---

## 6. Layers

| Layer | Work |
|---|---|
| `SharedKernel` | `CommunityPermission.ManageDiscord` |
| `Domain` | `IBotClient` role members; `IUserRepository` bulk external-login read; `BotInteraction.InvokerCanManageGuild`; `TitleExclusivity` |
| `Communities/Application` | `DiscordRoleSaga` — the reconcile, its triggers, the page's commands and queries; `/piu link-server` in `BotCommandSaga` |
| `Communities/Infrastructure` | three entities, one repository, model contribution |
| `PlayerProgress` | `PlayerTitlesChangedEvent` + its publish |
| `Data` | `DiscordBotClient` role API, gateway intent, join callback; `EFUserRepository` bulk read |
| `Web` | `/Community/Discord`; invite URL; `RecurringJobRunner` + registration |

**No new project references.** Everything Communities needs — titles, users, the bot — is already a
Domain port it can see, and the one cross-vertical event rides a reference that already exists.

---

## 7. Deliberately not done

- **Phoenix 1 titles.** D1.
- **Nickname or other Discord state.** Roles only.
- **Per-role grant rows.** The mapping table is the managed set (D12); tracking each grant
  separately buys precision this feature does not need.
- **Enumerating guild members.** The intent permits it; nothing here requires it, and
  `AlwaysDownloadUsers` stays off (D15).
- **A Discord-side audit log.** The site's own activity surfaces are elsewhere and this feature is
  not the place to invent one.

---

## 8. Post-deploy

1. **Re-run the bot invite URL** on every server that already has the bot, or grant its role Manage
   Roles by hand. Existing installs do not gain the permission on their own (§5.3).
2. The Server Members intent is already enabled on the application (owner, 2026-09-09).

---

## 9. As built

Fourteen commits, docs first and localization last. Nothing in §1–§7 changed shape during the
build; what follows is what the code learned on the way.

### 9.1 Two corrections the build made to this document

- **Exclusivity keys on the pool, not the rail.** `Title.Ladder` looked like the primitive and is
  not: `Phoenix2TitleList` rails a pool as three bands plus a capstone, so grouping on it gives
  *per-band* exclusivity and leaves `[S] ADVANCED LV.10` standing under `[S] EXPERT LV.1`.
  `TitleExclusivity.GroupOf` keys on `Phoenix2PumbilityTitle.Pool`, and a test pins the two rails
  apart so the mistake cannot be made again.
- **The feature cannot be stateless.** §3.3 was written after finding that Identity's purge drops
  `ExternalLoginEntity` on its first pass, right after an in-memory publish that returns on
  dispatch — and that a roster sweep never visits somebody who left. `CommunityDiscordGrant` is the
  answer to both.

### 9.2 Shapes that emerged

- **`IDiscordRoleService`** exists because `DiscordRoleSaga` is sealed and two callers need it in a
  specific order relative to their own work (the purge, the community delete). Those orderings are
  now asserted rather than hoped for.
- **Plan and apply are separate.** `PlanMember` decides; `ReconcileMember` writes. The page's dry
  run calls the planner, so a preview cannot promise something the real pass would not do — and the
  plan carries the roles a member *earned* but the bot cannot hand over, which is what lets the page
  name the silent failure on the row.
- **Triggers publish, two paths do not.** Join, leave, ban and unban publish
  `ReconcileDiscordRolesCommand` so nothing waits on Discord's REST API. The purge and the community
  delete call in-process, because both must run before rows they depend on are gone.

### 9.2b What the canary caught on its first real run

The bot in the owner's lab server holds **no Manage Roles at all** — it was added with the old
`347136` invite. `CanAssign` compared role *positions* only, so every role passed and the write came
back `50001 Missing Access`, which Discord reports to nobody. The page would have said "the bot can
hand out every role below" while nothing worked.

That is the whole reason a canary that writes exists, and it is the state **every** server invited
before this feature is in. Fixed in the same pass: `BotGuild.CanManageRoles`, the
`BotCannotManageRoles` reason, the page's wording, and the re-invite pointer above.

### 9.3 Ratchets that fired, and were right to

| Ratchet | What it caught |
|---|---|
| `LayerDependencyTests` | `IUserRepository` injected into a vertical. The bulk external-login read moved to `IUserReader`; the reverse lookup sends Identity's existing query rather than growing a second port member. |
| `CommunityTests` | The new `ManageDiscord` flag moving `All` 31 → 63. Adding a bit is safe where reordering is not. |
| `MessageTaxonomyTests` | Two view records living in `Queries/`, which is for `*Query` types only. |
| Command-tree localization | `/piu link-server`'s description with no translation. |
| `MurlocValuesUseOnlyTheMurlocAlphabet` | `/piu` inside a translated string. A slash command is a literal — the page prints it as markup now. |

### 9.4 The resx trap worth remembering

A resx opens with the schema comment, and that comment contains **example `<data>` elements**
(`Name1`, `Color1`, `Bitmap1`). An alphabetical insert that scans the file from the top can pick one
of those as the neighbour and splice a real entry *inside the comment*, where `GenerateResource`
never sees it and the UI silently renders the key name. Anchor the scan after the last
`</resheader>`.

### 9.5 Testing

- `ScoreTracker.Tests/ApplicationTests` — the rule (22), the triggers and their two orderings (10),
  the `/piu link-server` authorities (7).
- `ScoreTracker.Tests/DomainTests` — exclusivity, including the band-vs-pool regression.
- `ScoreTracker.Tests.Components` — the three server states, both silent failures, the preview.
- `ScoreTracker.Tests.Integration` — the generic purge probe covers `CommunityDiscordGrant` the
  moment it joined the manifest; no per-entity test code was needed.
- `ScoreTracker.ExplorationTests/DiscordCanary/DiscordRoleCanaryTests` — the only test that WRITES a
  role to real Discord. Owner-authorized against a throwaway role, gated on three extra secrets,
  restores the starting state whether or not the assertions pass. It buys the three failures no
  mocked suite can see: that the token carries Manage Roles, that the role sits below the bot, and
  that a single member can be fetched by id.
