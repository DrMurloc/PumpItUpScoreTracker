# Discord overhaul — /piu commands and leaderboard feeds

Status: **implemented (2026-07-17), PR #169** — C1–C11 landed, fast suites green, the lab-channel
canary posted every card shape (chart, random, suggest, weekly result + lineup, daily, official
digest). Mocks: claude.ai artifact `d4fa5bed-e2c6-4aa6-bcbf-a5b0cb4ceb04` (round-1, workshop-notes
toggle). Companion history: [discord-rich-score-notifications.md](discord-rich-score-notifications.md)
(the session-snapshot card — its one change here is the F7 cross-mix reclear marker below).

**Iteration (C12, owner feedback):** `/piu chart` autocomplete now offers one entry per
difficulty (`Ugly Dee S20`, value = chart id); picking one renders a **chart-details card**
mirroring the `/Charts` page — the difficulty breakdown (scoring level + pass tier via
`GetChartScoringLevelsQuery` + `GetTierListQuery("Pass Count")`), the skill fingerprint
(`GetChartSkillChipsQuery`), and **similar charts by skill** (`GetSimilarChartsQuery`, the
primary ask). Each section drops when its data is absent; a bare song name still falls back to
the difficulty-list card. Similar charts were empty until `recalculate-chart-similarity` first
ran; it is on a daily cron, so that resolved itself within a day of deploy and the graph has been
populated since.

**Iteration (F7, owner feedback):** the session-snapshot card now marks **cross-mix reclears**.
A new pass on a chart the player already cleared (non-broken) in another mix trails an escaped
`*` on its row, and the footer footnotes `* = reclears` when at least one marked row actually
rendered (reclears folded into the compressed "+N more" count carry no visible mark, so they
don't trigger the note). `CommunitySaga` computes the set from the already-injected
`IScoreReader` — the other Phoenix-family mix's non-broken passes (`GetBestScores`) plus legacy
XX (`GetBestXXAttempts`), intersected by canonical chart id — the same "passed in another mix"
semantics the tier list draws, with no new project reference and no privacy gate. Upscore-only
or first-clear batches short-circuit before any cross-mix read.

**Iteration (D1, owner feedback 2026-08-05) — the official digest is reshaped onto the This Week
hero. IMPLEMENTED.** The card as shipped rendered **39 lines across five stacked lists with no
image** (owner: "ugly AF, too much wall of text") while ignoring most of what the hero it should
mirror already computes: `WeeklyHighlightsRecord` has carried `Pulse`, `Gainers`, `Floors` and
`Debuts` since [I2](official-leaderboards-overhaul.md) and `DigestCard` reads none of them. The new
shape is **19 lines**, in this order — header (`###` + `-#` subtext carrying the mix tag, the week
pair and the hero's hype sentence, with the top world first's **song jacket as the section
thumbnail**: the header `RichBotSection` already accepts one and passes `null`) → **pulse** (board
entries · players active · debuts, new/upscored as subtext) → **biggest PUMBILITY gain** (the
hero's gainer card — value gained leads, the rank jump trails) → **biggest board climber** →
**the floors** (#100 and #1000, last week's 50× AAA level → this week's, plus value and delta) →
**world firsts** → footer + buttons.

Locked calls:

- **The PUMBILITY top 10 is cut.** Eleven of the thirty-nine lines went to standings that barely
  move — on the 2026-08-02 Phoenix 2 sweep #1 held and the biggest shuffle inside the ten was ↑8.
  A **Rankings** link button replaces it.
- **The new-#1 sample is cut entirely**, not reduced. That same sweep produced **105** of them;
  listing four was an arbitrary slice that read as the whole story, and the "dethroning" clause is
  what made those lines wrap on a phone.
- **World firsts move to the bottom and keep their `Take(6)`.** A big week there is the hype, not
  an info dump — but a content drop hands every brand-new chart a first at once, so the cap stays
  and the block sorts by level, biggest first.
- **No row gets emphasis.** Components V2 text has no per-line background, border or size, so a
  "featured" first can only be faked with a leading emoji. The jacket is the only real emphasis the
  format offers.
- **An empty firsts list drops the section *and* the thumbnail.** The jacket belongs to the top
  first; with none there is no chart for the card to be about. Late-mix weeks post the short card.
- **The card always posts.** `blocks.Count == 0` stops being the suppressor once the pulse strip is
  unconditional, and it needn't be one: board activity never stops, so there are no quiet weeks —
  only firstless ones.
- **Names lose the discriminator**, matching I6 on the hub. The digest was the last surface still
  printing raw `TAG#1234`.
- **The debut subtext dies** — "every debut a first-ever appearance on any board" is tautologous.

**The floors block targets AAA, deliberately diverging from the hero, which shows SS** — AAA is the
yardstick this audience actually plays toward, and the card is read by people who will never see the
hub. That has a mechanical consequence: `FloorMark` stores the **SS** level (`Level` /
`NewValue`) and nothing else, so the digest cannot read its levels off the row. It computes them —
`CutlineCalculator.LevelFor(PumbilityScoring(mix, false), ChartType.Single, PhoenixLetterGrade.AAA,
value)`, twice per rung, against the row's `Score` (this week) and `PrevValue` (last week). That
static is pure and lives in the same assembly, so this needs no sweep change, no new stored column
and no Rebuild press — and the hero keeps rendering SS off the untouched columns.

Two more consequences. The consumer still drops **two** of its three dispatches:
`GetOfficialRankingsQuery` existed only for the top 10, and `GetWhatItTakesQuery` goes too — its
`CutlineTierRecord` carries a current AAA level but no previous one, and its `History` is the #1000
floor alone, so it cannot answer "what did #100 take last week" at all. The `FloorMark` rows can.
Fixed in passing: the climbers template interpolates `NetPlacesGained` as a raw int, so today the
card's largest number renders `net +11990` with no separators.

**As built — one vertical, no schema.** *Domain*: nothing. *Application* (`OfficialMirror`):
`OfficialDigestFeedSaga` only — `Consume` dropped two dispatches, `DigestCard` was rewritten around
the new order, and the tag-to-name rule moved into `OfficialPlayerNames` in `OfficialMirror/
Contracts` so the card and `HubPlayerChip.NameOf` share one implementation (a helper class, not a
member on the coverage-excluded `OfficialPlayerRecord`). *Infrastructure*: nothing — every figure was
already stored, so no entity, repository or migration, and no Rebuild-highlights press.
*Presentation*: resx only, plus `HubPlayerChip.NameOf` delegating to the contract helper. The card
needed seven strings and only **six** were new: `Biggest board climber` already existed, because the
hero names the same card, and they now share the key. Eleven keys the reshape orphaned came out of
all nine locales. Murloc coined `Roglub` (gain), `Plglarg` (across) and `Ur` (on). *Tests*:
`OfficialDigestFeedSagaTests` grew from 4 to 9 — the four assertions that pinned the old shape moved,
and the new cases cover the pulse + jacket, one-name-per-category, the AAA floors ignoring the stored
SS level, the firsts closing the card ordered by level, the firstless card losing its picture, and
both dropped dispatches.

The floor levels are deliberately **not** pinned to numbers in the tests: they follow pumbility
scoring, which is not this card's business. The assertions pin the *shapes* — a floor that rose
without crossing a level shows one level, one that crossed shows an arrow. Against the real
2026-08-02 values the built code renders `#100 **Lv.24**` holding and `#1000 Lv.16 → **Lv.20**`.

**Canary: run and green** (2026-08-05, lab channel). `PiuCardCanaryTests` carries the reshaped card
in English and Korean, sampled from that same sweep so the sample has a live week's lengths and digit
counts. Discord accepted both payloads — thumbnail, five text blocks, four separators, three link
buttons — and the read-back found every card. Korean is in there on purpose: CJK glyphs are
double-width and the floor rows depend on the rank labels lining up.

Mock: claude.ai artifact `4e24d721-1ea3-4620-a380-c2ff6c05d163` (before/after, rendered from the
real 2026-08-02 Phoenix 2 sweep).

**As-built deviation (C10):** the official-leaderboards digest lives in **OfficialMirror**, not
Communities. Communities cannot reference OfficialMirror — the vertical graph would cycle
(`OfficialMirror → ScoreLedger → Communities`). So OfficialMirror hosts `OfficialDigestFeedSaga`
and reads the subscribed channels through a new published port,
`IDiscordFeedReader` (`Domain.SecondaryPorts`, implemented by Communities'
`EFDiscordFeedSubscriptionRepository`, keyed by the `DiscordFeedKinds` string constants). The
weekly/daily feeds stayed in Communities' `DiscordFeedSaga` as planned. Everything else matches
the design below.

The Discord bot grows from three flat commands into a `/piu` command family (in-channel
registration, chart lookup, random draws, personal suggestions) plus three opt-in broadcast
feeds (Weekly Charts, Daily Step, Official Leaderboards), all riding the Components V2 card
model that shipped with the session card. The session-snapshot pipeline and the community
score/title/UCS notifications keep working exactly as they do today; the card itself gains
only the F7 cross-mix reclear marker.

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

**Localization stance (superseded 2026-07-18, owner iteration — see §9)**: originally bot
messages stayed English. Now every registration carries an optional **language**, channel
notifications compose per-channel-culture, `/piu` replies follow the invoker's linked
account language, and the command tree ships Discord-native description localizations.

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

**Official digest** (per subscribed mix) — *the original C10 composition, kept as history.
**Superseded by the D1 reshape above**, which drops the rankings and cutline reads and recomposes
the card around the This Week hero*: skip when `IsBaseline`. Compose from
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

- **`BotHostedService`** slims to: start client → `RegisterCommands(PiuCommandCatalog, …)`
  (straight after Start since §10 — the adapter publishes the tree once the socket is up and
  wires the handlers into every client it builds) where the handler lambda opens a DI scope and
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

## 9. Localization iteration (owner, 2026-07-18) — the L-series

Reverses §2's original English-only stance. Two owner requirements: (a) registrations
specify a culture for channel notifications — community cards included; (b) `/piu` replies
use the invoker's linked account language, English default. Plus one locked flex:
Discord-native `description_localizations` on the command tree (client-locale-driven help
text). All nine locales (`en-US`, `es-MX`, `es-ES`, `pt-BR`, `ko-KR`, `ja-JP`, `fr-FR`,
`it-IT`, `en-ZW`); new resx keys land in every locale in the same commit as their strings.

**Culture sources, fixed per surface:**

| Surface | Culture |
|---|---|
| Feed cards (weekly / daily / official) | The subscription's stored language |
| Community cards (session snapshot, titles, UCS) | The community channel's stored language |
| `/piu` replies incl. ephemeral acks + errors | Invoker's linked account `Culture` UiSetting; English unlinked/unset |
| Public registration confirmations | The channel's just-registered language |
| Command-tree descriptions | The viewer's Discord client locale (`description_localizations`) |

**Architecture:** the resx stay Web-owned (`Resources/App.<code>.resx`, keys = English text
verbatim). Verticals compose through a new published port, **`ILocalizedTextAccessor`**
(`Domain.SecondaryPorts`, `Get(culture, key, args)`), implemented by
`Web/Accessors/ResxLocalizedTextAccessor` over `IStringLocalizerFactory` — the accessor
swaps `CurrentUICulture`/`CurrentCulture` for the lookup's duration so catalogue selection
AND numeric/date argument formatting follow the target locale, restoring both after. Null,
unknown, and unsupported cultures resolve to English; missing keys fall back to the key
itself. The nine locales live in ONE canonical `SupportedCultures` list
(`Domain/Records`, code + native name) consumed by Program.cs request-localization, the
culture endpoint, the account language picker, and the Discord language choices — it was
previously triplicated. Fan-out changes from compose-once to **group channels by culture,
compose per culture** (data reads still happen once; only rendering repeats).
Chart/song/player/title/skill names stay verbatim, matching the site.

**Storage:** `Culture nvarchar` nullable (null = English) on `CommunityChannel` and
`DiscordFeedSubscription`; re-registering updates it. `IDiscordFeedReader` returns
`(ChannelId, Culture)` records. `Community.ChannelConfiguration` and
`AddDiscordChannelToCommunityCommand` gain the culture.

**Commit plan:**

- **L1 — Culture plumbing.** `SupportedCultures` + `ILocalizedTextAccessor` +
  `ResxLocalizedTextAccessor` + DI; the three duplicated culture lists collapse onto the
  canonical one; resx-wiring facts in Tests.Components (incl. a resx-set ↔ list parity
  ratchet); this doc section.
- **L2 — Culture storage + register option.** Migration (two `Culture` columns);
  `language` option on `/piu register` ×4 (nine native-name choices); `/piu feeds` lists
  the language; localized public confirmations; DATABASE-SCHEMA rows.
- **L3 — Replies follow the invoker.** Router-entry account resolution → `Culture`
  UiSetting; every `BotCommandSaga` reply string through the accessor; keys ×9.
- **L4 — Command-tree descriptions.** `DescriptionLocalizations` on the bot definition
  records, decorated in Web from the accessor; translator maps culture codes → Discord
  locales (`es-MX`→`es-419`, `ko-KR`→`ko`, `en-ZW` skipped — Discord has no Murloc);
  keys ×9.
- **L5 — Weekly/daily feed cards.** `DiscordFeedSaga` per-culture grouping + strings ×9.
- **L6 — Official digest.** `OfficialDigestFeedSaga` per-culture + strings ×9,
  culture-aware dates.
- **L7 — Session/title/UCS cards.** `CommunitySaga` data/render split, per-culture
  community fan-out, the full card vocabulary ×9 (largest key batch).
- **L8 — Canary + docs.** Korean sample card set to the lab channel; doc/status sweep.

**As-built (all eight landed, ~160 new keys ×9):**

- **Discord.Net rejects `es-419`** (its locale validation chokes on the digit segment even
  though Discord's API accepts the locale), so es-MX carries no command-tree entry — LatAm
  Spanish clients see the English help text; their *replies and feeds* still localize via
  the account/channel culture. Revisit if a Discord.Net upgrade fixes the validator.
- **The L4 catalog test exposed a live defect**: `PiuCommandCatalog.Commands` was an eager
  static initializer running before the choice fields declared below it (static members
  initialize in textual order), so every `/piu` option had registered on Discord with NO
  choice dropdown — mix, goal, type, and language were all free-text. `Commands` is now
  computed on access and a fast-suite fact pins every choice list's entries.
- The env-gated `RealSessionShowcaseTests` pipeline registers an English passthrough
  `ILocalizedTextAccessor` (its container deliberately excludes the Web assembly that owns
  the resx).
- Sampled in Korean on the lab-channel canary: session snapshot, weekly lineup, official
  digest (`PostsTheKoreanSampleCards`).
- Number/date formatting follows the target culture everywhere a value renders inside a
  localized template (the accessor swaps `CurrentCulture` for the lookup) or via the
  culture's month-day pattern for week tags.

## 10. Gateway watchdog and log hook (incident 2026-09-01)

**What happened.** At 00:43 UTC on 2026-09-01 the bot's gateway session dropped and Discord.Net
spent until the owner's app restart at 14:38 UTC in a Connecting → Disconnecting → Disconnected
loop, about once a minute. Every attempt was a websocket handshake to the *resume* host Discord
had handed out at READY (`gateway-us-east-1a.discord.gg`), and every one came back HTTP 503. REST on
`discord.com` was healthy the whole time, so score cards and feeds kept posting while `/piu`
commands — which arrive over the gateway — silently died for fourteen hours. The restart fixed it
only because a fresh client IDENTIFYs on `gateway.discord.gg` and is handed a new resume host.

**Why Discord.Net never recovered.** READY stores `ApiClient.ResumeGatewayUrl`; the only code that
clears it (and the session id) is receiving `GatewayOpCode.InvalidSession` on an open socket
(`DiscordSocketClient.EventHandling.cs`). A 503 on the handshake never opens a socket, so that
opcode can never arrive, and `ConnectionManager` retries the same dead host with a backoff capped
at 60 s, forever — no attempt counter, no fallback to the generic gateway. 3.20.1 (latest at the
time) carries the identical logic, so an upgrade alone does not fix it.

**Why the logs said nothing.** `DiscordBotClient`'s `Log` hook forwarded only `msg.Message` at
Information. Discord.Net reports the disconnect reason as an exception-only log entry, so every one
of the ~830 failures landed in App Insights as a trace whose message was literally `[null]`. The 503
was only recoverable because the websocket handshakes happen to be tracked as HTTP dependencies
(`dependencies | where target has 'gateway'`).

**Decisions (owner, 2026-09-01).** ① Watchdog: yes, Hangfire-driven. ② Fold in the Ready
double-subscription fix (below). ③ No admin "Restart Discord Bot" button. ④ Log hook as scoped;
no gateway-intents trim. ⑤ The two dead channel registrations the same telemetry surfaced are
cleaned up by hand (script in the owner's Downloads), not by code.

### Behaviour

- **Watchdog.** Every two minutes Hangfire publishes `CheckDiscordGatewayCommand`. The consumer
  reads `IBotClient.Status`; if the socket has been out of the Connected state continuously for
  five minutes or more it logs a warning with the duration and calls `IBotClient.Restart`. A
  restart that fails (Discord down) is logged at Error and swallowed — the next tick retries and
  MassTransit never faults the message. `NotStarted` (no token: local dev, E2E) is a no-op.
- **Restart.** Builds, logs in and starts a *new* `DiscordSocketClient` first, swaps it in
  atomically, then stops and disposes the old one, under a semaphore so two ticks cannot
  double-restart. Sends racing the swap still hold the old client and land in the existing
  warning path; the client field is never null after the first start. The replacement client's
  disconnected clock starts at the restart, so a replacement that also fails to connect is
  replaced again five minutes later.
- **Handlers subscribe once per client instance.** Before this change the hosted service
  registered the command tree from `WhenReady`, and `RegisterCommands` subscribed the
  slash-command and autocomplete handlers on every Ready. Discord.Net raises Ready on every fresh
  IDENTIFY, so a mid-run re-identify (INVALID_SESSION) would have stacked a second handler and
  answered every command twice. Now the hosted service calls `RegisterCommands` straight after
  `Start`; the adapter stores the registration, subscribes the handlers when it builds a client
  (first or replacement), and on Ready only performs the bulk overwrite — once per process, retried
  on the next Ready if it failed. `WhenReady` stays for the exploration canaries; it binds to the
  current client instance and does not survive a restart.
- **Log hook.** Discord.Net severity maps onto the logger's levels (Critical → Critical,
  Error → Error, Warning → Warning, Info → Information, Verbose → Debug, Debug → Trace); the line
  reads `Discord.Net {Source}: {Message}` with the exception attached, and an exception-only entry
  uses the exception's message as its text. Discord.Net's own filter stays at Info, so the volume
  is unchanged — only the content is.

| Constant | Value | Why |
|---|---|---|
| Restart after | 5 min disconnected | every healthy resume in the logs took about a second; the Aug 31 flap recovered in 30 s |
| Check cadence | `*/2 * * * *` | worst case a dead gateway is replaced within 7 min; same shape as the two five-minute jobs |

Both are picked, not tuned — there is nothing to observe them against until the next incident.

### Layer scope

| Layer | Project | Change |
|---|---|---|
| Domain | `ScoreTracker.Domain` | `IBotClient` gains `Status` and `Restart`; `Records/BotGatewayStatus.cs` (record + `BotGatewayState` enum: NotStarted / Connected / Disconnected) |
| Infrastructure | `ScoreTracker.Data` | `DiscordBotClient` restructured around a swappable session (client + tracker); new pure helpers `GatewayStateTracker` (monotonic stopwatch, injectable timestamp for tests) and `DiscordLogMapping` |
| Vertical | `ScoreTracker.Communities` | `Contracts/Messages/CheckDiscordGatewayCommand`, `Application/DiscordGatewayWatchdogSaga`, one `AddConsumer` line. Communities already owns every Discord composition path and takes the port, so no new references |
| Presentation | `ScoreTracker` (Web) | `BotHostedService` registers after Start; `RecurringJobRunner.PublishCheckDiscordGateway`; one tuple in the Program.cs job list. No pages, no resx |
| CompositionRoot | — | untouched: the singleton registration and the adapter's constructor are unchanged |
| Tests | `ScoreTracker.Tests` | `DiscordGatewayWatchdogSagaTests`, `GatewayStateTrackerTests`, `DiscordLogMappingTests` in `ApplicationTests/` beside the other Data helper tests; a config-gated restart canary in `ExplorationTests/DiscordCanary/` |
| Docs | `docs/` | this section; the `check-discord-gateway` row in SCHEDULED-JOBS.md |

### Tests

- Saga: connected never restarts; disconnected 2 min never; disconnected 5 min restarts once;
  NotStarted never; a throwing restart is logged and swallowed.
- Tracker: NotStarted until told otherwise; Connected then Disconnected reports the elapsed
  duration; Connected again resets it.
- Log mapping: every severity maps; an exception-only entry yields the exception's message.
- Canary (`[DiscordCanaryFact]`): start the real bot, `Restart`, wait for Connected, post a line to
  the lab channel and read it back. Run with the rest of the canary suite, per the standing rule.

### Rollout

No migration, no config. After deploy: `Started bot client` in App Insights and the new job in the
Hangfire dashboard. During the next incident the signal is a Warning trace reading
`restarting the bot client`, and the disconnect reasons themselves are now real exceptions.

### Out of scope

Admin restart button, intents trim, auto-pruning dead channel registrations, a Discord.Net upgrade.

### As-built (2026-09-01)

- **Publish hooks.** The command tree is published from both `Connected` and `Ready`, not Ready
  alone: `RegisterCommands` may run before or after either fires (the hosted service calls it
  straight after `Start`, the canaries after Ready), so both hooks try, and a once-per-process
  flag behind a lock makes the extra attempts no-ops. A failed publish is logged and the next
  hook retries.
- **Downtime counts from `Start`.** The tracker enters Disconnected the moment a client is built,
  so a first client that never connects is replaced after five minutes like a dropped one, and a
  replacement that also fails to connect is replaced again five minutes after the swap.
- **A repeat Disconnected keeps the original stamp.** Discord.Net raises `Disconnected` on every
  cycle of its reconnect loop; only the first one after a Connected moves the clock.
- **Sends never see a null client.** The adapter's client accessor is a snapshot that throws
  "Client was never started" before the first `Start`; after a swap the old client is stopped
  only once the replacement is in place.
- **Canaries run serialized.** The five lab-channel classes share a non-parallel xUnit
  collection: Discord admits one IDENTIFY per five seconds per bot, and five classes logging the
  same bot in at once queued until the thirty-second Ready waits expired. Serialized, the suite
  passes in about a minute and a quarter — 12 of 13 on 2026-09-01; the thirteenth,
  `RealSessionShowcaseTests`, fails at SQL connect because its
  `DiscordTest:ExampleConnectionString` user-secret still names an earlier Aspire container's
  port (environmental, not this change).
- Constants, layer scope and the test list landed as written above.
