# Discord role management — design

> **Status: BUILT.** Workshopped and built 2026-09-09, one PR, 14 commits. The player's own
> opt-out (D22–D25, §2, §3.4, §5.4) was workshopped and built 2026-09-14 in a second PR.
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
| D22 | **A player can turn a community's title roles off for themselves** with `/piu roles off`, run in the community's designated server, and back on with `/piu roles on` (owner, 2026-09-14). Keyed on the **community**, not the server: the preference survives an admin repointing the server under D14, and a community that designates the same server later is a new question rather than a covered one. One command covers every community designating the server — normally one — so the player never has to know which site community is behind the roles. |
| D24 | **The command is visible everywhere and refuses where it does not apply** (owner, 2026-09-14), like `/piu link-server` and `/piu register` before it. Hiding it per *server* is possible — a guild-scoped registration in only the servers that hand out roles — but a guild command and a global command may share a name and both show, so it would need its own top-level command, and registration would become a second thing to keep in step with designations. Hiding it per *user* (only somebody with a PIU Scores link) is not possible at all: Discord's per-user command permissions can only be edited with a Bearer token from a server admin's own OAuth grant, never with the bot's, so no bot can drive them from its own data. |

### Authority

| # | Decision |
|---|---|
| D8 | **New `CommunityPermission.ManageDiscord`**, delegable by the Creator under the existing rules. It does not subsume `ManageChannelSubscriptions`; feeds and roles are configured by different people in practice. **Delegable means a switch on `/Community/Members`** — the flag shipped absent from that page's `ManageablePermissions`, which is the only array that hands any permission out, so no creator could grant it and every admin sent to `/piu link-server` was refused (fixed 2026-09-09). A permission missing from that list is a permission nobody but the creator can ever hold. |
| D9 | **Designating a server requires Discord authority too** — `Manage Server` in the target guild, read off the interaction that links it. Site permission alone cannot point a community at a server the admin does not run. |
| D10 | **The admin needs their own Discord account linked**, because designation happens by running a command as themselves. The page checks up front and sends them to `/Account` rather than letting them discover it mid-command. |
| D21 | **The refusal says which of the three things is wrong** (2026-09-09). One sentence covered the community not existing, the invoker's Discord resolving to a *different* PIU Scores login, and an admin missing the flag — three causes fixed by three different people. The second is the commonest and reads exactly like a permission problem, so each refusal now names the account the invocation resolved to. |

### Mechanics

| # | Decision |
|---|---|
| D11 | **One reconcile function, called from every angle.** `Reconcile(community, user)` computes the whole desired role set and diffs it. Nothing computes roles a second way, so nothing can disagree. |
| D18 | **Mapping a title hands the role out on the spot** (owner, 2026-09-09). It used to publish and return, which left an admin looking at a page where nothing had happened, reaching for **Check now**. Every write the page makes runs inline behind the progress dialog, and Check now says what it is actually for: somebody reporting a missing role. |
| D12 | **The mapping table is the managed set, within the community.** A mapped role handed out by hand to a *member* is taken back. Somebody in the Discord who never joined the community is never touched — a community's reach stops at its own roster (owner, 2026-09-09, confirming the built scope after a bug check flagged the wording). The reconcile walks members and grant holders, so a stranger wearing a mapped role keeps it. |
| D13 | **Unmapping a title takes the role back**, unless the admin unticks the box. Reversed after the first field test: a role the site handed out and then stopped maintaining is an orphan nobody can clear except by hand, which surprises people more than losing it does. Unticking keeps it as a manual role. |
| D14 | **Changing the designated server strips every role granted under the old one first.** Confirmed, not silent. |
| D15 | **Server Members intent ON, `AlwaysDownloadUsers` OFF.** The intent buys the join event; the member-list download is the expensive half and this feature never needs it. |
| D16 | **The sweep is nightly, and a backstop rather than the mechanism** (owner, 2026-09-09). Everything it does, the page's **Check now** does on demand — so it exists for a join the gateway missed and for anything the in-memory bus dropped, not for correctness. Hourly was paying for a roster download twenty-four times a day to almost always find nothing. |
| D17 | **Unlinking Discord publishes an event.** It was the one change nothing reported, so roles granted off the back of a sign-in outlived it until a sweep noticed. `ExternalLoginRemovedEvent` makes it immediate, which is what let the sweep drop to nightly without leaving a silent case. |
| D19 | **Linking Discord publishes one too.** The mirror of D17, and the same reason: linking is the LAST step of ordinary onboarding — the community join happens before there is a Discord account, the server join before there is a site account to find — so it was the one step that fired nothing, leaving the player on a nightly sweep they cannot trigger (Check now is admin-only). |
| D20 | **A system community may be owned, but never deleted.** World and the ninety-odd country communities are auto-joined and site-owned, and nothing guarded deletion because being ownerless left them with no Creator. Naming somebody on the row is a legitimate thing to do — it is how the official Discord gets its title roles — so the guard is explicit now (owner, 2026-09-09). Without it, World carrying every account on the site was one confirm from deletion. |
| D23 | **The opt-out is a fifth fact in the rule, not an action the command takes** (2026-09-14). `Reconcile` reads it alongside membership and the linked account, so the join event, the nightly sweep, a title earned next week, an admin's Check now and the dry run all honor it, and nothing can hand the role back by accident. The command writes the fact and then runs the same reconcile inline, the way D18 hands a role out on the spot. It also made the two existing revoke loops honest: a revoke pass used to stop at the first role Discord refused, so somebody wearing one role above the bot kept the assignable one too. Every revoke is attempted now in both `ReconcileMember` and `Revoke`, the grant row stays while anything failed, and the failure surfaces after the loop. The second loop matters most on the purge, which deletes the grant row — the last handle on the snowflake — whether or not the revoke succeeded, so a role skipped there is a role nothing can ever take back. |
| D26 | **A repointed server leaving an opt-out unliftable is accepted, not fixed** (owner, 2026-09-14, on the bug check). Lifting is only reachable by running the command in the designated server, so a player who opted out, whose admin then repoints the community elsewhere, cannot undo it from a Discord they are not in. That needs three things to line up at once and has never happened. It is also not purely a loss: the row persisting means that if they ever do join the new server, **their choice is remembered** rather than quietly reversed — which is the right direction for a privacy preference. No site-side switch is being built for it. |
| D25 | **A player who is not in the community can still turn its roles off**, and a player with nothing to take off gets the same answer — the record simply waits (2026-09-14). Admins never see who opted out: the page gains the fact as a line under "When a role is given", not a list of names. The reply names the account the invocation resolved to, for the same reason D21 does. |

---

## 2. The rule

Five facts — four at the first build, and the player's own say-so since 2026-09-14 (D22). All
true ⇒ the role is held. Any one false ⇒ it is removed.

```
holds(user, role) ⟺  ∃ mapping (community → title → role)
                  ∧  user ∈ community            and not banned
                  ∧  community has server S
                  ∧  user has a linked Discord account
                  ∧  user ∈ S
                  ∧  user holds title             on Phoenix 2
                  ∧  no higher rung of title's exclusivity group is held
                  ∧  user has not turned community's roles off    (/piu roles off)
```

### 2.1 Where each fact lives

| Fact | Source | Changes when |
|---|---|---|
| member of the community | `Community.MemberIds` / `RoleOf` | join, leave, ban, unban, community deleted |
| the mapping exists | `CommunityTitleRole` | an admin edits the table |
| a server is designated | `CommunityDiscordServer` | `/piu link-server`, change, unlink |
| Discord account linked | `IUserReader.GetExternalLogins` | link on `/Account`; unlink publishes `ExternalLoginRemovedEvent` (D17); account purge |
| in the server | `IBotClient.GetMemberRoles` returns non-null | joins, leaves, kicked, banned |
| holds the title | `ITitleRepository.GetCompletedTitles` | a score import earns one |
| has not opted out | `CommunityDiscordOptOut` | `/piu roles off` writes the row, `/piu roles on` deletes it — both run in the designated server, both reconcile inline (D22) |

A pass's work list is **members with Discord linked, plus anyone we still hold a grant for** — not
the whole roster. A member with neither resolves to no account and no plan, so visiting them is a
guaranteed no-op; carrying them made the work list every account on the site for World, and a
progress bar that read "12 of 14,000" while doing nothing.

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

Four tables, all Communities-owned, registered through the existing
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

### 3.4 `CommunityDiscordOptOut`

One row per (community, player) who has turned that community's title roles off for themselves
(D22). Unique on the pair.

| Column | Notes |
|---|---|
| `Id` | key |
| `CommunityId` | |
| `UserId` | the purge key |
| `OptedOutAt` | when |

It is its own table rather than a flag on the grant row for one reason: a grant row means "this
member holds at least one role we handed out" and is deleted the moment they hold nothing — which
is exactly what turning the roles off makes true. An opt-out stored there would vanish in the same
pass that acted on it, and the next sweep would hand everything back.

The reconcile reads the community's opted-out set once per pass, next to its members and their
titles (§2.1), so the fact costs one small read whichever angle the pass came in from.

---

## 4. Triggers

Every one of these ends in the same `Reconcile`.

| Angle | Mechanism |
|---|---|
| earned a title / climbed a ladder | `PlayerTitlesChangedEvent` (PlayerProgress → Communities) |
| joined, left, banned, unbanned | in-process call from the existing management handlers |
| community deleted | existing `CommunityDeletedEvent` consumer |
| account purged | existing `AccountPurgeStartedEvent` consumer, reading the grant row |
| linked or unlinked Discord | `ExternalLoginAddedEvent` (D19) / `ExternalLoginRemovedEvent` (D17); the grant row holds the snowflake to strip |
| admin edited a mapping | bulk reconcile over the community |
| server designated or changed | bulk reconcile; the old server is stripped first (D14) |
| **joined the Discord server** | `GuildMemberAdded`, plus the sweep as backstop |
| turned the community's roles off or on | `/piu roles off` / `/piu roles on` — the command writes the row, then reconciles the player inline in every community designating the server (D22, D23) |

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

The autocomplete asks the command's own question — `ManageDiscord` on the standing
`GetUserRoles` returns — rather than a second one off the membership row's `Role` string. Two
authorities on who the creator is can disagree (the aggregate derives it from `OwningUserId`, which
is what every gate reads), and D20 made that reachable: naming an owner on a system community's row
left the list offering a community the command refused, or hiding one it would accept.

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

### 5.4 `/piu roles off` and `/piu roles on`

A `roles` group with two leaves and no options, both replying privately. Run in the server whose
roles the player means; the interaction says which server that is, and `CommunityDiscordServer`
says which communities hand out roles there — normally one, and the command covers all of them
(D22). Refusals, in order: not in a server; no community designates this one ("This server doesn't
hand out title roles"); the invoker has no PIU Scores link, which gets the same sentence
`link-server` uses.

`off` writes the row for each community, then runs `ReconcileOne` for the player in each — the
existing rule with its fifth fact false, which takes every managed role off and forgets the grant
(D23). `on` deletes the row and reconciles the same way, so an earned role is back before the reply
lands. The reply names the account the invocation resolved to and the communities it covered, and
tells the player the command that undoes it. When a role cannot come off — it sits above the bot —
the row still stands, the reply says so, and the page's health list already names the role for the
admin.

The command literal rides into each reply as a placeholder argument rather than inside the
translated sentence, so every locale prints the real command. (Murloc, whose alphabet has no `i`,
had to mangle the earlier `/piu unregister` where it sat inside a value.)

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

The opt-out (2026-09-14) stays inside the same lines: the `roles` group in `PiuCommandCatalog`, its
handler in `BotCommandSaga`, the fifth fact in `DiscordRoleSaga`, one entity, three repository
members and a migration in Communities, one line on `/Community/Discord`, and nothing new in
`Domain` or `Data`.

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
- **Hiding `/piu roles` per server, or per user.** D24 — the first costs a second command tree and a
  registration to keep in step, the second Discord does not allow a bot to do at all.
- **A site switch for the opt-out.** The command is symmetric, so a player undoes it where they did
  it. Not revisited after the bug check either — see D26.
- **A single-member path through `BuildContext`.** Settling one player loads the community's whole
  membership, the mapped titles' holders and the members' Discord links, because the context is
  shared with the whole-community pass. Considered and declined (owner, 2026-09-14): for a real
  community that is three indexed reads, and the same path already runs on **every title change** —
  which a score import triggers constantly — so a command used a handful of times a month adds no
  cost class that is not already there. Worth revisiting only if World itself ever maps a title,
  and at that point the join event and the title fan-out are the bigger callers, not the command.

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

### 9.2c The first field test: it was slow, and why

Twenty seconds to add one mapping. Two N+1s, both mine:

- `GetCompletedTitles(mix, userId)` returns **every** title row a member holds — all 272 titles'
  worth, each with a `ParagonLevel` to parse — and the reconcile called it once per member. It is
  one `GetUsersWithTitles` now, scoped to the titles the community actually maps, read once per
  pass in `BuildContext`.
- `GetMemberRoles` is one Discord REST round trip, and the reconcile made one per member. A bulk
  pass reads the server's roster once (`GetGuildMemberRoles`, an on-demand
  `DownloadUsersAsync` for that guild — deliberately not `AlwaysDownloadUsers`, which would pay for
  every guild at every connect). Settling ONE person still goes by id, which needs no intent.

`ASweepReadsTitlesAndTheServerRosterOnceEach` fails if either goes back to being per-member.

### 9.2d Progress, and why it is deterministic

A pass writes to Discord one member at a time, so on a big server it is a minute of watching
nothing. `ReconcileCommunity` takes an `IProgress<DiscordRoleProgress>` and reports its **whole work
list before the first write**, so the bar starts at a real denominator rather than growing one — a
report that only ever grew its own total would be a spinner with numbers on it. The page draws it in
a blocking dialog with a Stop button; stopping is safe, because reconciling is idempotent and the
next pass resumes from wherever it stopped.

The callback rides a MediatR request, never a bus message, so it stays in-process.

The dry run stopped blocking the page as well: it walks the whole server, which makes it the slowest
read here, so the page paints first and fills it in.

Every write the page makes goes through the same dialog — adding a mapping, removing one, changing
the server — because they all walk members one at a time and all need the same way out. `RevokeRole`
counts only the people who actually hold the role rather than everyone the community ever granted
for; a bar whose denominator is the wrong set finishes instantly and says nothing.

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

### 9.6 What the opt-out's bug check found (2026-09-14)

Four fixes and one ratchet, all on the same PR.

- **Every Discord-role read threw.** The shared `Servers(...)` helper returned
  `IQueryable<CommunityDiscordServerRecord>` and its two callers filtered the **projection** — and
  EF cannot translate a predicate back through a positional record's constructor, so `GetServer`
  and `GetServersByGuild` failed at query-compile time, unconditionally. That is the whole feature,
  plus `/Community/Discord`, `/piu link-server`, every reconcile and community deletion. It is the
  same trap `EFHardmodeRatingRepository` shipped one day earlier (PR #338), and it survived 4,734
  green tests for the same reason: **the repository had no integration test.**
  `EFDiscordRoleRepositoryTests` is that missing ratchet — seven facts that execute the reads
  rather than mocking them, which is the only kind of test this class of bug cannot pass.
- **An unrecognized `roles` leaf turned roles back on.** `path[1] == "off"` meant everything else
  fell into the branch that *deletes* the opt-out. A privacy preference fails closed: the leaf is
  matched explicitly and an unknown one is refused.
- **`/piu roles on` said "already on" over a failed pass.** The realistic caller is somebody whose
  role is missing and who was never opted out, so nothing is lifted — and they were handed a
  reassurance for a reconcile that had just thrown. The failure is reported first now.
- **Recording the opt-out was check-then-insert against a unique index**, and the write sat outside
  the loop's try, so with two communities on one server a throw on the first left the second never
  written and said nothing. The insert treats its own unique violation as success — pressing once
  and having the client retry is not an error — and the write is inside the same try the reconcile
  has.

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
