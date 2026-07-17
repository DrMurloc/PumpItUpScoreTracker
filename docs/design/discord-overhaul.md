# Discord overhaul — /piu commands and leaderboard feeds

Status: **scoped (2026-07-17), owner decisions locked; ready to execute.** Mocks:
claude.ai artifact `d4fa5bed-e2c6-4aa6-bcbf-a5b0cb4ceb04` (round-1, workshop-notes toggle).
Companion history: [discord-rich-score-notifications.md](discord-rich-score-notifications.md)
(the session-snapshot card — **untouched by this work**).

The Discord bot grows from three flat commands into a `/piu` command family (in-channel
registration, chart lookup, random draws, personal suggestions) plus three opt-in broadcast
feeds (Weekly Charts, Daily Step, Official Leaderboards), all riding the Components V2 card
model that shipped with the session card. The session-snapshot pipeline, its card, and the
community score/title/UCS notifications keep working exactly as they do today.

---

## 1. Locked decisions (owner, 2026-07-17)

| # | Decision |
|---|---|
| 1 | One **`/piu`** command family; the legacy top-level commands are removed outright (hard swap, no alias period). `calculate-score` folds in as `/piu calc`, behavior unchanged. |
| 2 | The three per-type channel toggles (`SendNewScores`/`SendTitles`/`SendNewMembers`) are **purged** — columns, command params, dead option parsing. They never worked (options read but never declared, two mapped swapped) and the fan-out never consulted them. Deleting them preserves today's actual behavior (every registered channel gets every community notification). |
| 3 | Weekly feed results = **top 5 charts by participation**, one card each (top 10 rows), everything else behind a "Full results" link button. |
| 4 | The hardcoded weekly-progression channel post (`WeeklyTournamentSaga` → channel `1254418262406725773`) is **deleted, replaced by nothing**. It only fed the owner's test chamber. `UserWeeklyChartsProgressedEvent` keeps publishing (PlayerProgress capture and the site UI consume it); only the Discord send dies. |
| 5 | `/piu chart`, `/piu random`, `/piu suggest`: `mix` is **optional, default Phoenix 2**. The three feed registrations: `mix` is a **required** native choice (Phoenix \| Phoenix 2). |
| 6 | `/piu suggest` replies **ephemeral** (captions reference the invoker's scores). |
| 7 | `/piu random` gains a **`preset`** option for linked users, autocompleting their saved randomizer settings and running the full weighted config. |
| 8 | Feed board cards **glow community members** (green row tint) when the posting channel is also community-registered. |

Superseded lock: [daily-step.md](daily-step.md) **L7** ("no standalone daily Discord post")
was about not spamming community session channels; the Daily Step feed is a channel's own
opt-in and coexists with the session card's personal Daily Step line.

Riding along: **the Phoenix 2 weekly rotation bug fix** — `RecurringJobRunner.PublishUpdateWeeklyCharts`
publishes `RotateWeeklyChartsCommand()` (default Phoenix) only, so P2 weekly boards never
rotate on cron; it fans out per mix exactly like `PublishRotateDailyStep` (C1).

## 2. The command tree

One global top-level command. Discord permissions gate at the top level only, so `/piu` is
visible to everyone and the admin subcommands enforce **Manage Channels in the handler**
(the interaction payload carries the invoker's channel permissions; denial is an ephemeral
reply). This trades the old per-command `DefaultMemberPermissions` gate for handler
enforcement — equally authoritative, standard bot practice.

```
/piu register community   name:<autocomplete>  [invite-code]     ephemeral ack + public confirmation
/piu register weekly      mix:<Phoenix|Phoenix 2>                 "
/piu register daily       mix:<Phoenix|Phoenix 2>                 "
/piu register official    mix:<Phoenix|Phoenix 2>                 "
/piu unregister           feed:<autocomplete: this channel's registrations>
/piu feeds                                                        ephemeral
/piu chart                name:<autocomplete>  [mix]              public
/piu random               [count 1–10] [min-level] [max-level] [type S|D|Co-op] [mix] [preset:<autocomplete>]   public
/piu suggest              [goal] [type S|D] [mix]                 ephemeral
/piu calc                 perfects greats goods bads misses combo [calories]   public
```

Reply semantics, fixed per subcommand (ephemerality must be chosen at defer time):

- The adapter **always defers** (`ephemeral` per the subcommand's declaration), executes the
  handler, then follows up with the rendered card or text. Errors land in the same
  visibility. No more canned "Registering Channel..." + disconnected second message.
- **Registration confirmations are the permission probe**: the interaction ack is ephemeral,
  and the saga then posts the public "#channel now receives …" message through the normal
  channel-send path. If that send fails, the ephemeral ack says exactly which permission the
  bot is missing — a registration can no longer succeed into a channel the bot can't post to.
- `/piu unregister` mirrors it (ephemeral ack + public "no longer receives" send).
- Autocomplete answers come from in-memory state (catalog cache, own tables) — no path may
  do slow I/O; Discord's window is ~3 s and choices cap at 25.
- Goal choices on `/piu suggest` = the widget's bundles (`SuggestedGoals`): Title Hunt ·
  Score Push · Fill Gaps · Pumbility Push. Unlinked invokers get an ephemeral nudge to
  `/Account` → Connect Discord instead of results.

**Account resolution**: `GetUserByExternalLoginQuery(discordUserId.ToString(), "Discord")`
(Identity contracts — the stored ExternalId *is* the snowflake). When a subcommand needs a
user-scoped engine (`suggest`, `preset`), the handler resolves and calls
`ICurrentUserAccessor.SetScopedUser(user)` — the same background-impersonation seam
`RunOfficialImportConsumer` uses — so `RecommendedChartsSaga`/`GetRandomSettingsQuery`
read the invoker exactly as they read a signed-in circuit.

**Links**: saga-composed messages use `SiteBase` (`CommunitySaga`'s existing const) +
`/Chart/{id}` permalinks, which 301 to the canonical `/Charts/{mix}/{song}/{difficulty}`
pages — same pattern as the session card. No slug logic leaves Web.

**Localization stance**: bot messages remain English (matching every existing Discord
surface; channels have no locale). The one new *web* string set (the invite-link blurb,
§4 Presentation) lands in all nine locales.

## 3. The feeds

New Communities-owned subscription storage + one new saga, fed by three new contract events
that fire at the moments that already exist:

| Feed | Trigger (new event) | Published from | Cadence |
|---|---|---|---|
| Weekly Charts | `WeeklyChartsRotatedEvent(Mix)` — `WeeklyChallenge.Contracts/Events/` | `WeeklyTournamentSaga` rotation consumer, only on an actual rotation (never the daily retry no-op) | Mondays 05:00 UTC (midnight ET) |
| Daily Step | `DailyStepRotatedEvent(Mix, FinishedForDate)` — same home | `DailyStepSaga` rotation consumer | Daily 05:00 UTC |
| Official Leaderboards | `OfficialSnapshotSealedEvent(Mix, IsBaseline)` — `OfficialMirror.Contracts/Events/` | `LeaderboardSweepSaga` seal step | Sundays after the 10:30 (P1) / 16:30 (P2) UTC sweeps seal |

`DiscordFeedSaga` (Communities/Application, internal, bus consumer + request handlers)
consumes the three events, loads subscriptions for `(FeedKind, Mix)`, composes
`RichBotMessage` cards, and fans out via `IBotClient.SendRichMessages` with the existing
per-channel try/catch. Delivery posture is the same at-most-once as every Discord message
on the in-memory bus. Mix accent stripe + `[Phoenix]`/`[Phoenix 2]` textual prefix per the
session-card doctrine.

**Weekly drop** (per subscribed mix): latest finished week via `GetPastWeeklyDatesQuery` →
`GetPastWeeklyEntriesQuery(date, mix)` → group by chart, order by entry count desc, take 5 →
re-rank each board with the weekly placement policy (`ProcessIntoPlaces` — the same policy
the live board uses; past-entry reads drop Place, so re-ranking is by construction
identical). One card per chart: art header, top-10 rows (place · name · grade + plate
emojis · score), "Card N of 5 · M more charts had entries" footer. Then the lineup card:
`GetWeeklyChartsQuery(mix)` grouped into compact S/D/CO-OP lines, "Full results" + "Weekly
Charts" buttons. Names via `IUserReader`.

**Daily Step** (per subscribed mix): one combined card — yesterday's top 10 from the new
`GetDailyStepResultsQuery(mix, forDate)` (reads `UserDailyStepPlacing`, which rotation
already writes with Place; direction-correct for Limbo by construction) + today's chart
from `GetDailyStepQuery(mix)` with the Limbo banner ("lowest passing score wins") when
`IsLimbo`.

**Official digest** (per subscribed mix): skip when `IsBaseline`. Compose from
`GetWeeklyHighlightsQuery(mix)` (PUMBILITY movers — absent for Phoenix, which has no
pumbility board — boards climbed, new #1s, chart/folder grade world-firsts, all
name-resolved by the sweep) + `GetWhatItTakesQuery(mix, All|Singles|Doubles)` cutlines with
`WeekDelta` (rows only where the board is full). Long first-lists truncate to "+N more";
skipped weeks self-label from `PreviousSnapshotAt` ("vs Jun 28 (2 weeks)"), matching the hub.

**Community glow** (#8): all inside Communities — the feed saga joins the channel's
`CommunityChannel` registrations to member ids and tints matching board rows. Channels
without a community registration render plain.

All cross-vertical reads are published contracts/ports (`GetPastWeekly*`,
`GetWeeklyChartsQuery`, `GetDailyStep*`, `GetWeeklyHighlightsQuery`, `GetWhatItTakesQuery`,
`GetChartsQuery`, `IUserReader`) — no foreign SQL, no internals.

## 4. Technical scope by layer

### Domain (`ScoreTracker.Domain`)

- **`IBotClient` v2.** One new registration method replacing the three legacy ones:
  `RegisterCommands(IReadOnlyList<BotCommandDefinition>, Func<BotInteraction, Task<BotReply>>, Func<BotAutocompleteRequest, Task<IReadOnlyList<BotOptionChoice>>>)`.
- **New records** in `Domain/Records/` (`[ExcludeFromCodeCoverage]`, provider-agnostic —
  Domain never sees Discord.Net): `BotCommandDefinition`, `BotSubCommandGroup`,
  `BotSubCommand` (carries `Ephemeral`), `BotCommandOption` (kind String/Integer/Boolean,
  required, choices, autocomplete flag, min/max), `BotOptionChoice`,
  `BotInteraction(CommandPath, Options, ChannelId, GuildId?, UserId, UserDisplayName, InvokerCanManageChannel)`,
  `BotAutocompleteRequest(CommandPath, FocusedOption, PartialValue, Options, UserId, ChannelId)`,
  `BotReply(Card?, Text?, Ephemeral-override-free)`.
- **Deleted port members** (all zero production callers): `RegisterMenuSlashCommand`,
  `RegisterReactAdded`, `RegisterReactRemoved`, `SendMessageToUser`, `SendFileToUser`; the
  legacy `RegisterSlashCommand` overloads go in the final commit once nothing calls them.
- No domain-model or business-rule changes. No shared `Domain/Events/` additions (the three
  new events are vertical-owned contracts).

### Application

- **Core `ScoreTracker.Application`: no changes.** `GetRandomChartsQuery` stays where it is
  for the deprecated Match subsystem; the bot uses a new vertical-owned mirror instead
  (below), so no new code leans on the transitional reference.
- **Communities** (the Discord-presentation vertical; all composition lands here so it's
  testable in the fast suite):
  - `BotCommandSaga` (internal) — handles a new contract command carrying `BotInteraction`,
    routes by command path, composes every `/piu` reply; plus the autocomplete query
    handler. Uses `ScoreScreen` for `calc`, catalog/randomizer/progress/identity contracts
    for the rest, own repositories for community/feed autocomplete.
  - `DiscordFeedSaga` (internal, bus consumer) — §3.
  - Contracts: `PiuCommandCatalog` (the static command-tree definition),
    `HandleBotInteractionCommand : IRequest<BotReply>`,
    `GetBotAutocompleteQuery : IQuery<IReadOnlyList<BotOptionChoice>>`,
    `RegisterDiscordFeedCommand`, `UnregisterDiscordFeedCommand`,
    `GetChannelDiscordFeedsQuery`, `DiscordFeedKind` enum
    (WeeklyCharts · DailyStep · OfficialLeaderboards).
  - `AddDiscordChannelToCommunityCommand` loses its three toggle params;
    `Community.ChannelConfiguration` shrinks to `ChannelId`.
  - New sibling contract references: OfficialMirror, Randomizer, Identity
    (PlayerProgress/WeeklyChallenge/Catalog are already referenced).
- **WeeklyChallenge**: publishes `WeeklyChartsRotatedEvent` + `DailyStepRotatedEvent`; new
  `GetDailyStepResultsQuery(mix, forDate)` contract + repository read;
  **removes** the `IBotClient` send + hardcoded channel from `WeeklyTournamentSaga` (the
  saga sheds its `IBotClient` dependency; the bus event publish stays).
- **OfficialMirror**: publishes `OfficialSnapshotSealedEvent(Mix, IsBaseline)` at the seal
  step. Nothing else.
- **Randomizer**: new `DrawRandomChartsQuery(RandomSettings, Mix)` in `Contracts/Queries/`,
  handled beside the existing draw logic in `RandomizerSaga` — the vertical-owned mirror of
  the transitional `GetRandomChartsQuery`, so Communities never references core Application.
- **Identity / PlayerProgress / Catalog**: consumed via existing contracts, unchanged.

### Infrastructure (`ScoreTracker.Data` + vertical Infrastructure)

- **`DiscordBotClient`**: implements `RegisterCommands` — translates the definition tree to
  Discord.Net builders and `BulkOverwriteGlobalApplicationCommandsAsync` (which atomically
  drops the three retired top-level commands); `SlashCommandExecuted` → build
  `BotInteraction` (path, typed options → strings, invoker id/display name, ManageChannels
  bit from the interaction's channel permissions) → `DeferAsync(ephemeral per definition)`
  → handler → render via the existing `DiscordRichMessageRenderer` → `FollowupAsync`;
  `AutocompleteExecuted` → route → respond ≤25 choices. Per-interaction try/catch with an
  ephemeral error follow-up. Deletes the dead `ChannelIds` array, the dead private
  overload, and the implementations of the removed port members. The exact 3.18 builder
  spellings are confirmed at implementation time (same caveat the card design carried).
- **`Discord:RichScoreMessages`** stays what it is (the score-card kill switch); new
  surfaces don't consult it — feeds are opt-in by registration, commands by invocation.
- **EF migration** (one): `DiscordFeedSubscription` table (Communities' model
  contribution) + drop the three toggle columns from `CommunityChannel`.
- Repositories: `EFDiscordFeedSubscriptionRepository` (Communities/Infrastructure),
  `EFDailyStepRepository` gains the placing-history read, `EFWeeklyTourneyRepository`
  untouched (existing reads suffice).

### Presentation (`ScoreTracker` Web)

- **`BotHostedService`** slims to: start client → `WhenReady` →
  `RegisterCommands(PiuCommandCatalog, …)` where the handler lambda opens a DI scope and
  dispatches `HandleBotInteractionCommand` / `GetBotAutocompleteQuery`. All three inline
  command implementations move out; the file stops knowing what commands exist.
- **Communities page**: an "Add the PIU Scores bot to your server" link
  (`https://discord.com/oauth2/authorize?client_id={Discord:ClientId}&scope=bot+applications.commands&permissions=…`)
  with a one-line blurb — the first place the invite URL exists anywhere. New strings via
  `L[…]` in all nine locales.
- No new pages, routes, or controllers. **No `api/*` changes** — the wire-shape suite is
  untouched.

### Secrets & config

**None added, none changed.** The bot keeps `Discord:BotToken`; the invite link and OAuth
login keep `Discord:ClientId`/`ClientSecret`. Build-time check only: confirm `"Discord"` is
in the AppHost `forwardedSections` allowlist so a locally-secreted bot token reaches the
app for lab testing (no production impact either way).

### Removed functionality (complete list)

1. Per-type channel toggles — columns, params, parsing (never functional; behavior preserved).
2. Top-level commands `register-community-channel`, `deregister-community-channel`,
   `calculate-score` (replaced by `/piu register`/`unregister`/`calc`; bulk overwrite
   removes them from Discord at first startup).
3. The hardcoded weekly-progression Discord post + channel id (replaced by nothing).
4. Dead `IBotClient` surface: menu commands, reaction hooks, user DMs / file DMs.
5. (Final commit) the legacy `RegisterSlashCommand` overloads.

## 5. Schema (rows for DATABASE-SCHEMA.md in the same PR)

| Table | Change |
|---|---|
| `CommunityChannel` | − `SendNewScores`, − `SendTitles`, − `SendNewMembers` |
| `DiscordFeedSubscription` **(new, Communities contribution)** | `Id` PK, `ChannelId` (same ulong mapping as `CommunityChannel.ChannelId`), `FeedKind nvarchar(32)`, `MixId guid`, `RegisteredByDiscordUserId` null, `CreatedAt`; unique `(ChannelId, FeedKind, MixId)` |

## 6. Testing

- **`ScoreTracker.Tests/ApplicationTests`** (the point of the Communities composition home):
  `BotCommandSagaTests` — routing, calc math, chart card shape, random option→settings
  mapping + preset path, suggest linked/unlinked + scoped-user verify, register
  probe/denial/invite-code paths, feeds listing; `DiscordFeedSagaTests` — weekly top-5
  selection + re-rank, lineup card, daily normal/Limbo, glow membership, official digest
  section presence/absence, baseline skip, per-channel fan-out. Rotation/seal sagas gain
  publish-`Verify` facts.
- **`ScoreTracker.Tests` (Data-referencing)**: pure translator tests for definition→builder
  mapping (like the renderer tests).
- **Ratchets/tripwires**: `VerticalBoundaryTests` consumer allowlist += `DiscordFeedSaga`;
  MassTransit hook `AddCommunitiesConsumers` += same; MessageTaxonomy picks up the new
  contracts automatically.
- **Canary** (manual, lab channel from secrets): sample weekly/daily/official cards + a
  command-catalog registration smoke (bulk overwrite against the test app, REST read-back).
- **No E2E** — Discord can't be wire-stubbed (established posture). `Tests.Api` untouched.

## 7. Commit plan (one PR, sequential, each green on the fast suites)

- **C1 — Weekly rotation parity fix.** `PublishUpdateWeeklyCharts` fans out Phoenix +
  Phoenix 2 (mirroring Daily Step). SCHEDULED-JOBS.md note.
- **C2 — Bot port v2 (Domain + Data).** Command/interaction/reply records;
  `IBotClient.RegisterCommands`; `DiscordBotClient` implementation (bulk overwrite,
  defer/follow-up, ephemeral-by-definition, autocomplete routing, ManageChannels bit);
  delete dead port members + dead client code. Translator tests.
- **C3 — `/piu` spine + calc (Communities + Web).** `PiuCommandCatalog`,
  `HandleBotInteractionCommand`/`GetBotAutocompleteQuery`, `BotCommandSaga` with routing +
  `/piu calc` + the error envelope; `BotHostedService` rewires to the catalog (legacy
  commands stop being registered). Saga tests.
- **C4 — Registration v2 (Communities).** Migration (feed table + toggle-column drops);
  slimmed channel config + command; `/piu register` ×4, `/piu unregister`, `/piu feeds`;
  community + feed autocomplete; ephemeral acks + public confirmation-probe; ManageChannels
  denial. DATABASE-SCHEMA rows. Saga tests.
- **C5 — Invite link (Web).** Communities-page "Add the bot" blurb + link; `L[…]` keys ×9.
- **C6 — `/piu chart`.** Song autocomplete (catalog cache, ChartSelector matching incl.
  `S21` shorthand), reply card with permalinks. Tests.
- **C7 — `/piu random` + presets.** `DrawRandomChartsQuery` (Randomizer contract, delegating
  handler); option mapping; preset autocomplete via scoped user; draw card. Tests.
- **C8 — `/piu suggest`.** Snowflake→user resolve, unlinked nudge, goal bundles →
  `GetRecommendedChartsQuery`, ephemeral card. Tests.
- **C9 — Weekly + Daily feeds.** The two rotation events; `GetDailyStepResultsQuery`;
  `DiscordFeedSaga` weekly cards (top-5 + lineup) + daily card (Limbo banner) + community
  glow; **delete the hardcoded weekly-progression send** (saga sheds `IBotClient`).
  Consumer wiring + tripwires. Tests.
- **C10 — Official digest feed.** `OfficialSnapshotSealedEvent` at seal (baseline-flagged);
  digest composition from highlights + WhatItTakes. Tests.
- **C11 — Legacy trim + canary + docs.** Delete the legacy `RegisterSlashCommand`
  overloads; canary feed samples + registration smoke; ARCHITECTURE.md eventing note,
  daily-step.md L7 pointer, doc status flips, final tripwire sweep.

## 8. Build-time verifications (expected-fine, confirm while coding)

1. `CommunityChannel.ChannelId`'s exact ulong column mapping (reuse for the new table).
2. `UserDailyStepPlacing.ForDate` type — the `DailyStepRotatedEvent` field matches it.
3. The weekly placement policy's exact home/name for the re-rank (`ProcessIntoPlaces`).
4. The Communities listing query the name-autocomplete should reuse (or an internal repo read).
5. `"Discord"` present in AppHost `forwardedSections` (local lab testing only).
6. Discord.Net 3.18 builder/API spellings for bulk overwrite, V2 follow-ups, and
   autocomplete responses (the design doesn't depend on them).
