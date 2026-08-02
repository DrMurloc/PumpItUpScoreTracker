# API v2 and Community Tools

Status: **owner-workshopped 2026-08-01, not yet built.** Adds a versioned `api/v2/*` surface
addressed by player rather than by "me", a Community Tools directory where players choose which
community-built tools may read their scores, and the toolmaker side of that: registration, API
keys, webhooks, an activity console, and admin approval.

Mocks: [the twelve screens](https://claude.ai/code/artifact/bb7bd2f0-6c46-4b99-a0b0-459ae1fb9038).

Companion specs: [delete-my-data.md](delete-my-data.md) (the purge ecosystem this must join),
[score-truth-model.md](score-truth-model.md) (what a score record means),
[legacy-mixes.md](legacy-mixes.md) (why a Fiesta EX score is not a number),
[communities-overhaul.md](communities-overhaul.md) (the invite-code and consent patterns this reuses).

---

## 1. Context

Today's partner API is five controllers, one credential, and one subject.

- **The token *is* the user.** A raw `Guid` column on `User`, plaintext, no name, no expiry, no
  scope, no last-used. Regenerating kills the old one.
- **Every endpoint is "me"-scoped.** `api/phoenixScores` reads `_currentUser.User.Id`. There is no
  way to address another player at all, so a tool that wants to compare two players cannot exist
  unless both hand over their personal token — which is the credential that also records scores.
- **No webhooks.** The fat contract events (`PlayerScoresUpdatedEvent`,
  `ScoreImportCompletedEvent`) were designed as webhook bodies under ADR-001 D3 and are
  JSON-round-trip-ratcheted by `ContractEventSerializationTests`. Nothing sends them.
- **No rate limiting.** No `AddRateLimiter`, nowhere.
- **One integration exists and it is hardcoded.** `PiuTrackerClient` POSTs
  `{ "sid": <piugame session id> }` to `piutracker.app:3002`, inline in the import, blocking it for
  up to five minutes.

The ask is to make the surface REST-shaped and versioned, retire the `dev/export/*` escape hatch by
publishing what it exposes properly, and build the consent and toolmaker machinery around it.

### What the numbers say

Measured against the prod-synced database, 2026-08-01, last 90 days:

| | |
|---|---|
| Imports/day | **42** (90-day) · **58** (last 30) |
| Active players | 579 in 90 days · 448 in 30 |
| Scores per import — median | **10** |
| p90 / p99 / max | 50 / 909 / 3,651 |
| Imports exceeding 100 scores | **6.4%** |
| Capped at 100 → the delivery payload | **19.9 scores ≈ 3.2 KB** |

Three things follow and they shape the whole design. Deliveries are **small** — score push is not a
bulk feature. The 100-score chunk covers **93.6%** of imports in one call, so the `next` cursor is a
tail case, not the common path. And the consenting-player pool is **hundreds, not thousands**, so a
tool sweeping everyone it can see makes ~500 requests, not 50,000.

---

## 2. The reframe

Everything else falls out of one change: **v2 resources are addressed by subject, and authorization
is "has this player shared with this caller?"**

```
v1:  GET api/phoenixScores                     → implicitly me
v2:  GET api/v2/players/{playerId}/scores      → gated on a share
     GET api/v2/players/me/scores              → the alias a personal key uses
```

Two credential kinds, and they do not overlap:

| | Personal token | Tool key |
|---|---|---|
| Subject | one user | a tool, acting across its consenting players |
| Format | `Guid`, Basic auth | `pst_live_…`, `Authorization: Bearer` |
| Storage | plaintext (unchanged) | **hashed**, shown once |
| Expiry | none (unchanged) | 6 months default |
| Works on v1 | yes | yes |
| Works on v2 | yes, scoped to `me` | yes, scoped to its shares |
| May mutate | **yes** | **no** |

**Personal tokens are not changing in this pass** (owner, 2026-08-01). They keep working exactly as
they do, including on v2 where they resolve only `players/me`. v1 stays frozen; its wire shapes are
already pinned by `Tests.Api` and a break there is breaking-change review.

**All mutation stays on personal tokens.** `POST api/phoenixScores` and `POST
api/phoenixScores/import` are personal-only, forever as far as this doc is concerned. Tool
integrations are **GET-only**. That single decision deletes an entire scope system: there is no
`scores:write`, no OAuth-style scope strings, no per-endpoint permission matrix. A share is one
thing — *this tool may read my scores* — plus the separate PIUGame-session mode below.

---

## 3. The v2 surface

Conventions: cursor pagination (`?cursor=&limit=`, opaque cursor, `next` link in the envelope) on
anything player-scoped or unbounded; page numbers only where a collection is small and bounded.
Errors are `application/problem+json` (RFC 9457) with a stable machine `type` — never a bare string,
never exception text (`DiagnosticExposureTests`). `mix` is **required** on mix-scoped resources: a
400 listing valid values, no silent Phoenix default. `ETag`/`If-None-Match` on catalog reads.

Full shapes: [the scope artifact](https://claude.ai/code/artifact/6b1ad685-034b-42f7-876e-29a593ab8bc0).
**22 endpoints, 17 of them new capability** (24 as first designed; the tool self-view and the pull
feed were removed 2026-08-02, see below).

### Catalog — a key, no share required

| Route | Notes |
|---|---|
| `GET /api/v2/mixes` | All 30. Carries `sortOrder`, `isPrimary`, and **`scoringModel`** (§6) |
| `GET /api/v2/songs` | Gains `artist`, `durationSeconds`, `bpm{min,max}` — on the model today, never exposed |
| `GET /api/v2/charts` | `mix`, `level`, `type` filters. ETag. Real chart GUIDs. `shorthand` renamed **`difficulty`**, carrying slot-aware `DifficultyDisplay` so a pre-Exceed chart reads `"Crazy 6"` not an ambiguous `"S6"` |
| `GET /api/v2/charts/{chartId}` | `?expand=skills` for the full metric set |
| `GET /api/v2/charts/random` | Ported |
| `GET /api/v2/charts/{chartId}/similar` | §3.1 |
| `GET /api/v2/tier-lists/{listType}` | `score-difficulty` · `pass-difficulty` · `pg-difficulty` — four v1 routes collapse to one, and the published set is narrowed to the three that are difficulty judgements (owner, 2026-08-02, see §14) |
| ~~`GET /api/v2/chart-scoring-levels`~~ | **Folded onto the chart, 2026-08-02.** It keys on (chart, mix), which is what `ChartV2Dto` already is — publishing it separately mirrored our table layout rather than the domain, and made an integrator join two calls for one float. Null where unmeasured, which today is every mix but Phoenix and XX |
| `GET /api/v2/charts/{chartId}/skills` | §3.2. Was `?expand=skills`, then `chart-skills`; a sub-resource is the shape (owner, 2026-08-02) |
| `GET /api/v2/charts/skills` | §3.2 in bulk, filtered exactly like `/charts` — one sweep per mix rather than a call per chart |

### Players — share-gated

| Route | Notes |
|---|---|
| `GET /api/v2/players` | **The linchpin.** Every player who has shared with the calling tool, cursor-paginated. Without it a tool has no way to learn who consented |
| `GET /api/v2/players/{playerId}` | Identity + profile. **404, not 403**, when no share exists — a 403 confirms the player exists |
| `GET /api/v2/players/{playerId}/scores` | v1's filters plus cursor, `recordedAfter`, and **judgment counts** — `PhoenixRecord` carries `Perfects/Greats/Goods/Bads/Misses`, all nullable, so the block is `null` rather than zeroed for a CSV or hand-entered score (zeros read as a perfect game) |
| `GET /api/v2/players/{playerId}/sessions` | `ScoreSession`, landed 2026-07-31 |
| `GET /api/v2/players/{playerId}/journal` | `ScoreEventJournal` — per-attempt history with judgments. 1,072,377 rows already accumulated |

**One date, not two.** `PhoenixRecord` has exactly one — `RecordedDate`, set from
`IDateTimeOffsetAccessor.Now` at write time. It already *is* "imported at"; a second field would be
the same value renamed. What we do not have anywhere is when the play happened, and the maker docs
must say so, because `recordedAt` invites exactly that misreading.

**No `pumbilityPlus`** on any shape (owner, 2026-08-01). `pumbility` stays.

### Official leaderboards — a key, no share

Six reads off the OfficialMirror snapshot: `rankings`, `players/{gameTag}`,
`charts/{chartId}/board`, `popularity`, `what-it-takes`, `weekly-highlights`. This is piugame's own
public data, so no consent is involved.

⚠ **`UserId` is stripped at the API boundary.** `OfficialPlayerRecord` carries the piugame-tag →
site-account link internally; returning it would let any tool map a tag to our user id **with no
share**, including for private profiles. A tool that needs the join has `gameTag` on both sides,
which keeps the mapping a deliberate act rather than a freebie.

`GetOfficialRecentScoresQuery` is **not** exposed — it takes a username and password and is a
credentialed live scrape, not mirror data.

### Competition — ported from v1

`GET /api/v2/weekly-charts`, `GET /api/v2/weekly-charts/scores`.

**Tournaments are not carried forward** (owner, 2026-08-01). `api/tournaments` stays on v1 for
existing callers; no v2 equivalent is built.

### 3.1 Similar charts

`GET /api/v2/charts/{chartId}/similar?mix=&minLevel=&maxLevel=&minScoringLevel=&maxScoringLevel=&minBpm=&maxBpm=&minNps=&maxNps=`

Backed by `GetFilteredSimilarChartsQuery`. Three things the contract dictates, all of them load-bearing
([chart-similarity.md](chart-similarity.md)):

- **The list ships unfiltered, with `matchFloor: 0.55` in the envelope.** `ChartSimilarityRecord` is
  explicit that where the bar falls is a render decision — the graph stores its top 20 floor-free so
  sub-floor rows are near-misses, not absences. Publishing the constant rather than baking it in also
  means a tool moves with us if it changes, which matters: §10 pins that floor inside a 0.013-wide
  window, with THE REVOLUTION missing by 0.0004.
- **`chartsCompared` is in the response.** It is not a statistic — it turns "1 match" from a bug
  report into *"compared 30 charts by SPHAM within 2 levels, 1 match."*
- **Both sub-scores and `sharedBadges` are exposed.** §4 calls explainability the product. Badges
  carry piucenter's **raw** names (`bracket`, `twist_90`), never the display skill vocabulary — the
  contract warns that projection is lossy and must not be inherited.

Filters narrow what we compare against and then rescore; they never sieve the precalculated top-20,
which would trivially return nothing. That also makes this the out-of-window path — *"I liked this
D18, what D23s play like it"* — deliberately outside the ±1 the nightly job precalculates.

`GetLeastSimilarChartsQuery` exists (§5.1) and is **not** exposed; it becomes `?order=least` if ever
wanted.

### 3.2 Chart skills — PIU Center

`ChartSkillMetric` holds 140+ metric names over 4,411 charts as a key-value bag
(`badge_fraction:bracket = 0.3333`). The API gives it structure: `nps`, `difficultyPrediction`,
`sustainTimeSeconds`, `timeUnderTensionSeconds`, `lastSegmentIsPeak`, a `skills[]` array joining
`badge_fraction` / `top3` / `practice_rank` / `last_segment_badge` per skill, and `rarePatterns[]`.

⚠ **NPS is PIU Center's measurement, not a derivation.** It must not be computed from
`noteCount / duration` — it is a different number, and a tool that recomputes it will silently
disagree with every other tool. Observed range 0.4–17.0.

⚠ **`ChartSkillMetric` has no `MixId`.** Skills are a property of the chart, not of its expression in
a mix, so this is the one catalog endpoint with **no `mix` parameter**.

### The tool's own view — removed 2026-08-02

Both routes are gone.

`GET /api/v2/tool` returned a tool its own registration. Overkill: a maker configured that tool on
`/Developers` and can read it there. It also promised "current limits and remaining quota" and never
carried either.

`GET /api/v2/events` was justified as the pull-based alternative for a maker with no public URL —
and **it never worked for that maker**. The fan-out skips `WebhookMode.None`, so a tool without a
webhook writes no delivery rows and the feed returns an empty array forever. It only ever served
tools that already had a working webhook, where replay on the debug page covers the same ground. The
gap survived because the endpoint (C22) and the fan-out (C20) landed in different commits and
nothing exercised the pair.

A maker with no public URL polls `GET /api/v2/players/{id}/scores`, which is honest about being a
poll and works today (owner, 2026-08-02).

---

## 4. Structure

This is **two pieces of work wearing one name**, and separating them is what makes the size
tractable.

**v2 is almost entirely presentation.** Controllers, DTOs, auth and conventions in `Web`, dispatching
to queries that mostly already exist. No new domain logic.

**Community Tools is a real vertical.** Domain, storage, bus consumers, outbound HTTP. All of the
actual weight is here.

### 4.1 What v2 needs that does not exist

Of the endpoints, the great majority already have a query behind them: `GetChartsQuery`,
`GetChartQuery`, `GetRandomChartsQuery`, `GetFilteredSimilarChartsQuery`, `GetTierListQuery`,
`GetChartScoringLevelsQuery`, `GetPhoenixRecordsQuery`, `GetRecentSessionsQuery`,
`GetWeeklyBoardQuery`, `GetUserByIdQuery`, and **all six** OfficialMirror leaderboard queries.

Four are genuinely missing, and all four are **additive to an existing vertical** — no shape changes,
nothing moved:

| New query | Vertical | Why the existing one will not do |
|---|---|---|
| `GetMixesQuery` | Catalog | No mix read exists at all |
| `GetSongsQuery` | Catalog | `GetSongNamesQuery` returns names; the API needs artist, duration, BPM |
| `GetChartSkillMetricsQuery` | Catalog | `GetChartBadgeCoverageQuery` returns only the `badge_fraction:*` family — the API also publishes nps, top3, practice_rank, sustain, rare patterns |
| `GetPlayerJournalQuery` | ScoreLedger | `GetChartScoreJourneyQuery` is per-chart; the API is player-wide with a cursor |

**On paging.** v1's `PhoenixScoresController` loads every record and every chart and then filters,
sorts and pages **in memory**. That reads like a blocker for cursor pagination — it is not, at this
size: 1,036,830 records over 1,586 users is ~650 per player. In-memory paging behind a cursor-shaped
API is honest here. Shape the contract correctly now; push paging into SQL only if a player's record
count ever justifies it. Designing SQL-level keyset pagination today would be work spent on a
problem the data does not have.

### 4.2 The one hard structural problem

Ping and score-push deliveries are easy: CommunityTools consumes `PlayerScoresUpdatedEvent` and
`ScoreImportCompletedEvent` off the bus and fans out. No reference to anything.

**Session mode cannot work that way.** The piugame sid exists only inside OfficialMirror, only during
the import, and must never ride a broadcast event or reach a table (§5). So OfficialMirror has to
hand it off — but OfficialMirror must not reference CommunityTools.

The answer is the escape hatch the architecture already sanctions: **a Domain port**.

```
Domain/SecondaryPorts/ISessionDeliveryClient
    DeliverSession(Guid userId, MixEnum mix, RedactedString sid, CancellationToken ct)
```

Implemented inside `CommunityTools/Infrastructure/`, injected into `OfficialLeaderboardSaga`.
OfficialMirror says *"here is a sid for this user and mix"*; CommunityTools decides who is entitled
and delivers. Identical in shape to `IDiscordFeedReader` for OfficialMirror → Communities.

It is also the natural home for the piutracker call that currently blocks the import for up to five
minutes — that call moves behind this port and off the critical path.

**No second port is needed.** An earlier revision of this doc proposed `IToolShareReader` for the v2
authorization filter. That was wrong: `ApiTokenAuthenticationScheme` already sets the precedent of an
auth handler doing `IMediator.Send(...)`, so the v2 scheme sends a query and Web stays MediatR-only.

### 4.3 The vertical

New assembly `ScoreTracker.CommunityTools`, following the WeeklyChallenge template. Packages: the
vertical baseline plus **`Microsoft.Extensions.Http`** for outbound delivery — the same exemption
`OfficialMirror` already carries. That is a row in CLAUDE.md's package table.

**`Domain/`** — `Tool` is a **rich aggregate**, not a property bag. It is a `TournamentSession`-class
model because its invariants are dense: the listing state machine, and the rule that entering session
mode requires zero connected players (§9). Those rules live in the aggregate, not in handlers — the
same call `Community` made for its role and permission rules. Alongside it: `ToolShare`, and the
`WebhookMode` / `ToolVisibility` / `DeliveryStatus` / `FailureReason` enums.

**`Infrastructure/Entities/`** (internal):

| Entity | Key fields |
|---|---|
| `ToolEntity` | `OwnerUserId`, `Name`, `Description?`, `Url?`, `Visibility` (Private/PendingApproval/Public/Rejected), `AcceptsAllToolsShare`, `WebhookMode`, `WebhookUrl?`, `SigningSecretHash`, `OutboundHeaderName?`, `OutboundHeaderValueHash?`, `CreatedAt`, `ApprovedAt?`, `RejectionReason?` |
| `ToolMixSubscriptionEntity` | `(ToolId, MixId)` — which mixes trigger a delivery |
| `ToolInviteCodeEntity` | `Guid` PK, `ToolId`, `CreatedAt`, `RevokedAt?` — lifted from `CommunityInviteCodeEntity` |
| `ToolShareEntity` | `(ToolId, UserId)`, `Source` (Direct/AllTools), `GrantedAt`, `RevokedAt?` |
| `ToolBlockEntity` | `(ToolId, UserId)` — an all-tools player's per-tool no |
| `ToolSharePreferenceEntity` | `UserId`, `ShareWithAllTools`, `SetAt` |
| `ApiKeyEntity` | `ToolId`, `Name`, `KeyHash`, `Last4`, `ExpiresAt?`, `CreatedAt`, `LastUsedAt?`, `RevokedAt?` |
| `WebhookDeliveryEntity` | `ToolId`, `UserId`, `Mix`, `Mode`, `DeliveryId`, `Body?`, `SignedAt`, `Signature`, `Attempt`, `Status`, `RemoteStatusCode?`, `FailureReason?`, `RemoteBodySnippet?`, `LatencyMs?`, `NextAttemptAt?` |
| `ToolActivityEntity` | The console's non-delivery rows: key-use rollups, rate-limit rollups, key expiry, player connect/disconnect |

**`ShareWithAllTools` lives here, not on `User`.** An earlier revision put it on the user table on
the grounds that it is authorization data. That was backwards: it is authorization data *this vertical
owns*, and putting it in Identity makes the effective-access check (§5) a join across two verticals'
tables — exactly what the vertical rules forbid. Keeping shares, blocks and the preference together
makes that check one local join, means Identity needs no schema change at all, and lets the existing
purge consumer cover it. The Account page sends two queries instead of one; it is a Blazor page, that
is free. The rollout still seeds the preference from `IsPublic` as a one-time data migration.

**`Application/`** — the handlers, plus three consumers: `WebhookDeliverySaga` (the score-batch
consumer and fan-out), `DeliveryRetrySweepConsumer` (Hangfire-driven), and `ToolDeletionConsumer`
(the purge).

**`Infrastructure/`** — repositories, `WebhookDeliveryClient` (typed `HttpClient`), `HmacSigner`,
`ApiKeyHasher`, and the `ISessionDeliveryClient` implementation.

**`Wiring/`** — `AddCommunityTools()`, `AddCommunityToolsConsumers()`, `CommunityToolsModelContribution`
(and its line in `VerticalModelContributions.All()`, or the migration silently drops every table above).

### 4.4 Web

- `Controllers/Api/V2/` — ~8 controllers: mixes, songs, charts (+ random, similar, skills), tier
  lists, players, official, weekly charts, tool.
- `Dtos/ApiV2/` — ~20 records. **Deliberately not reusing the v1 DTOs**, which are pinned by
  `Tests.Api` goldens; sharing them would make a v2 shape change a v1 breaking change.
- `Security/` — `ToolKeyAuthenticationScheme` (Bearer), the share-authorization filter, and the
  `AddRateLimiter` policy in `Program.cs`.
- Cross-cutting helpers — problem-details middleware, cursor encode/decode, ETag.
- Pages — the six in §12, the Account changes, one nav entry.

---

## 5. Sharing

Effective read access for tool T over player P:

```
   explicit ToolShare(T, P)
OR (P.ShareWithAllTools AND T.AcceptsAllToolsShare AND NOT ToolBlock(T, P))
```

**Public and all-tools are separate concepts and must stay separate.** They are coupled in exactly
two places, both deliberate:

- **A private profile cannot enable all-tools sharing.** The toggle is *hidden*, not disabled
  (owner, 2026-08-01), so the private card carries a **Go public** link and states plainly that
  sharing is what going public unlocks — a lot of players do not know public is an option.
- **Going public turns all-tools on, every time.** Not "only if never set" — the owner's call, on
  the grounds that people do not flip privacy back and forth and remembering a prior choice is the
  more confusing behaviour.

Consequences that must ship with it:

- The existing confirm dialog (`ProfilePanel.razor:98`) needs new copy on **both** branches. Going
  public switches sharing on; going private switches it off and disconnects N tools.
- **Explicit shares survive going private.** Precedent is the site's own piutracker copy: *"PIU
  Tracker is fully public — scores you send there are public even if your profile isn't."* Private
  blocks the blanket grant, never a deliberate named one.
- **Rollout seeds `ShareWithAllTools` from `IsPublic` once**, with a one-time dialog. The public
  branch leads with the confession and links straight to the toggle.

### Invites

`ToolInviteCode` and `/CommunityTools/Invite/{code}` are the Communities pattern reused wholesale.
The landing page is the only logged-out screen in the feature, so it sells the tool and states the
ask before asking anyone to sign in.

### PIUGame session mode

**What it is:** the piugame.com `sid` minted when we sign in as the player during an import —
forwarded to the tool so it runs its own scrape. It is *their* account, not their PIU Scores
account, and while it lives it can change their card, their settings, or delete the account.

Rules, all non-negotiable:

- **Never available through all-tools sharing.** Explicit grant only, with the warning, always. A
  blanket toggle has no consent moment and this needs one.
- **Entering session mode requires zero connected players.** Moving *within* the read tier (ping ↔
  score push) is free — those carry no power the API key does not already have. Session mode is a
  different tier and every connected player must have agreed to it individually.
- **The sid is never persisted.** Not in a delivery body, not in a log. ⚠ `RedactedString` masks
  `ToString()` but its JSON converter **round-trips the real value** — it protects logs, not
  persistence. Serialising a session-mode body to a table writes a live credential in plaintext past
  a type that looks like it is protecting you.
- **Session mode is therefore fire-and-forget, inline, during the import** — exactly what
  `PiuTrackerClient` does today. No durable queue, no retry, no replay, no signature echo. The
  console shows delivered/failed and nothing behind it, and the UI says why rather than looking
  broken.

**PIU Tracker migrates in as a public tool with a hardcoded bespoke adapter** (owner: "Tusa gets
special privileges because I like him"). Its `{sid}` body shape does not constrain the generic
contract. Everyone with `SyncPiuTracker = true` is grandfathered into an explicit share — same
consent, same data, no surprise. This takes a five-minute blocking call off the import path.

Because piutracker is the only session consumer at launch, the **generic** session payload contract
can be deferred until a second maker asks. The consent flow cannot — it is how anyone grants this.

---

## 6. The delivery contract

### Modes

| Mode | Body | Durable |
|---|---|---|
| Player ping | identity only — "this player imported" | yes |
| Score push | identity + up to 100 changes + `next` | yes |
| PIUGame session | identity + the sid | **no** |

### Envelope

```json
{
  "deliveryId": "d-4f819c",
  "eventId": "…",
  "schemaVersion": 1,
  "sentAt": "2026-08-01T14:21:55Z",
  "test": false,
  "player": {
    "mix": "Phoenix",
    "scoringModel": "phoenix",
    "userId": "9f14c0e2-…",
    "username": "DrMurloc",
    "gameTag": "MURLOC#1"
  },
  "sessionId": "…",
  "changes": [ … ],
  "next": null
}
```

`gameTag` comes from the **per-mix** link (`IOfficialPlayerIdentityRepository.LinkPlayer`), not
`User.GameTag` — the latter is a single last-write-wins field that a Phoenix import will clobber
with a Phoenix 2 tag.

### Legacy mixes change the change shape

All 30 mixes are selectable. `MixEnum.UsesLegacyScoring()` splits the world: Phoenix and Phoenix 2
carry 1M-scale scores with plates; **everything else — XX and older, plus Infinity/Pro — is letter
grade + broken flag + an optional era-scale score.**

A tool reading a Fiesta EX record as a number gets zero. So the envelope's `scoringModel`
discriminates, and `changes` items carry the fields for that model with the others null:

- `scoringModel: "phoenix"` → `oldScore`, `newScore`, `plate`, `isBroken`, `isNewPass`
- `scoringModel: "legacy"` → `oldLetterGrade`, `newLetterGrade`, `oldScore?`, `newScore?`,
  `isBroken`, `isNewPass`

Nulls-with-a-discriminator rather than polymorphism: easier to consume from a dynamically typed
language, which most of these tools are.

### Endpoint verification

**Nothing is delivered to an unverified URL, in any mode** (owner, 2026-08-02: "higher trust for
people sharing their scores"). We POST `{type: url_verification, challenge}` and the endpoint must
echo the challenge; `Tool.WebhookUrlVerifiedAt` records it, `Tool.CanDeliver` gates on it, and
`SetWebhook` clears it whenever the URL changes.

The first framing of this rule was graduated — required for session mode, optional elsewhere — on
the grounds that an unauthenticated score endpoint is the maker's own risk. That was wrong, and the
owner corrected it: an *unverified* URL is not the maker's risk at all. It means **we** send **our
players'** scores to a host nobody proved they own. The header protects the maker's system; the
handshake protects the player's data, and only one of those is ours to carry.

It proves cooperation at that moment, not domain ownership. A DNS TXT record or a `.well-known`
path would prove more and cost real friction; three tools do not justify it yet.

Checked in three places rather than one — the fan-out skips, the dispatcher refuses, and the
session client refuses — because three callers reach the POST and only one of them is the fan-out.

### Auth

**A maker-supplied header**, name and value of their choosing, sent verbatim over TLS. One `if` in
their handler. Required in PIUGame-session mode, optional elsewhere — that mode hands over a live
credential, and an endpoint with no way to tell our call from anyone else's has no business
receiving one.

> **Superseded 2026-08-02.** This section previously specified HMAC-SHA256 signing alongside the
> header, justified as "sending both costs nothing." That was wrong twice over. It was not in the
> brief — the ask was *"webhooks prob need optional API keys too so that toolmakers can secure their
> own tools by verifying the call comes from me"*, which is the header. And it was not free: it cost
> a signing module, a recoverably-stored secret, a column, a debug panel, ~600 words of integration
> docs about re-serialization pitfalls, nine locales of copy, and a shipped defect — the secret was
> never surfaced anywhere, so no maker could have verified a signature even if they wanted to.
> TLS already authenticates the transport; the marginal gain was replay protection against an
> attacker already inside the maker's TLS, who could equally forge the header. Removed at D1.

`X-PIU-Delivery-Id` is the `EventId`. Retrying sends the same id; tools dedupe on it.

### Durability, retry, retention

The in-memory bus dies with the process, which is unacceptable for an outbound promise. **Write the
delivery row first, then attempt.** A Hangfire sweep picks up pending and failed rows. Five attempts
with exponential backoff over roughly an hour, then dead-lettered into the console.

Retention, and the arithmetic behind it:

| | Kept | Why |
|---|---|---|
| Metadata (every delivery + activity row) | 14 days | ~1.3 MB / 6,500 rows at current volume |
| Body — failed or pending | 7 days | replay and signature echo need the exact bytes |
| Body — last success per tool | until superseded | the signature echo sample |
| Body — other successes | not stored | nobody replays a success |
| Body — session mode | **never** | it is a live credential |

Storage at 58 imports/day × 8 subscribed tools: **208 KB** steady state. 1.3 MB if one tool is dead
for the full week. 50 MB in the physically-worst case where every tool is down and every import
maxes the 100-cap. The failures-only rule is not a cost measure at that scale — it is there to bound
how long we hold other players' scores.

**Chunking.** 100 changes per delivery, `next` cursor for the tail. A first-time import of 3,000
scores therefore stores 15 KB, not 450 KB — the tail is pulled, never pushed. There is no such thing
as a large delivery.

---

## 7. Keys and rate limits

**Keys.** `pst_live_` prefix so leak scanners catch them. Hashed at rest, shown once, `last4` for
identification. Default expiry **6 months**; options 30d/90d/6mo/1yr/never, with "never" carrying a
warning. **Two active keys per tool** so rotation has no downtime.

**No expiry emails** (owner, 2026-08-01) — outbound mail to makers is its own project and this does
not start it. The warning surface is therefore entirely in-page, which means it has to work harder
than a date in a table:

- The key list shows the expiry date on every key, with an **Expiring soon** chip inside 14 days and
  an **Expired** chip after, both carrying the count of rejected requests since.
- `/Developers` shows a banner while any key is inside 14 days, so a maker who opens the page for an
  unrelated reason still sees it.
- The console logs `KeyExpired` with the running rejected-request count.

This is honestly weaker than a nudge that reaches them: a maker whose key dies is told by their
users, not by us. That is an accepted trade, not an oversight — revisit it if and when outbound mail
to makers exists.

**Rate limits.** ASP.NET Core's built-in `AddRateLimiter`, no new package. Token bucket partitioned
by key: **600/min tool, 60/min personal**. `429` with `Retry-After` and `RateLimit-*` headers.

Noted for the record: the data says 120/min would never be noticed by an honest tool — 579 active
players means a full sweep is ~500 requests. 600 is the owner's number and it is generous, which is
the right way to be wrong.

---

## 8. The console

Every row is a **curated phrase plus the remote's own status code**. No stack traces, no framework
strings, no failed-job internals — so `DiagnosticExposureTests` is never touched and no exemption is
needed. Rule 8: each severity carries an icon and a word, never colour alone.

| Event | Renders as |
|---|---|
| `DeliverySucceeded` | ✓ Score push delivered · 100 scores · page 1 of 3 · Phoenix 2 · 240ms |
| `DeliveryTimedOut` | ✕ Webhook timed out · no response in 10s · attempt 3 of 5 · next retry 14:38 |
| `DeliveryRejected` | ✕ Webhook returned 500 · *your server said:* "Internal Server Error" |
| `DeliveryUnreachable` | ▲ Couldn't reach your server · DNS lookup for … failed |
| `RateLimited` | ▲ Rate limit hit 212 times · 13:00–14:00 · key "production" |
| `KeyUsed` | · Key "production" used 8,140 times · rolling hourly total |
| `KeyExpired` | ✕ Rejected: key expired · 412 requests rejected since |
| `PlayerConnected` / `PlayerDisconnected` | · Player connected · through "share with all tools" |

`RateLimited` and `KeyUsed` are **hourly rollups**, not per-request. One bad loop must not be able to
flood the log or the table.

### Debug tools

Three, in order of value per unit of build:

1. **Send test delivery** — real signed POST to their real endpoint, landing in the console like any
   other. Source: *my last import* (real scores, one query, zero new UI) or *N synthetic scores*. A
   chart picker using the existing `ChartSelector` is a later nicety. **Always `"test": true`, always
   a `test-` delivery-id prefix, and always the maker's own account** — a test can never carry
   another player's scores.
2. **Signature echo** — the exact raw bytes signed for the last delivery, next to the computed
   signature. HMAC mismatches are almost always re-serialisation changing whitespace before hashing,
   and this turns two days into twenty minutes.
3. **Replay** — re-send a failed delivery on demand. Nearly free given the durable table. Rows past
   the 7-day body window render **Body expired** with the button disabled, because a maker who hits
   that limit in month two will otherwise file it as a bug.

---

## 9. State machines

**Listing.** `Private → PendingApproval → Public | Rejected`. A private tool is fully functional —
invite links work, keys work, webhooks fire. Approval buys the directory listing and eligibility for
the all-tools pool, nothing else. Editing name, link or description on a public tool returns it to
`PendingApproval` while it stays listed. Rejection carries a required reason, sent to the maker.

**Webhook mode.** `None ↔ PlayerPing ↔ ScorePush` freely. `→ PiuGameSession` only when the tool has
zero connected players. `PiuGameSession →` anything is always allowed (a de-escalation).

---

## 10. Account deletion

`delete-my-data.md` shipped 2026-07-31 and this must join its ecosystem rather than sit beside it.

**Tools cascade; they do not block** (owner, 2026-08-01). This diverges from communities on purpose:
a community has members who can inherit it and a Make Creator flow to do it with, while a tool's
value is the maker's server, which is leaving anyway. Nothing to transfer to.

- The confirm state of `DeleteAccountDialog` gains a consequence panel naming each tool and its
  **player count**. "Pumbility Planner · 1,204 players lose access" lands where "3 tools" does not.
- Tools keep running until the purge date, matching §8.2's rule that the account works normally
  during the 7-day window. No de-listing, no invite-link kill, no notification to connected players
  (owner: not worth over-engineering for a rare case).
- **§8.2's second guard applies verbatim: a flagged account cannot create a tool.** Without it you
  request deletion owning nothing, create a tool on day three, and it evaporates on day seven taking
  its players with it — the identical hole the community guard closes.
- The communities blocker still wins. Tools are not mentioned in that state: the confirm step is
  unreachable, and listing a second consequence before the first is resolved just makes the wall
  taller.

---

## 11. Retiring `dev/export`

The harness exposes six reference tables (`Mix`, `Song`, `Chart`, `ChartMix`, `TierListEntry`,
`ChartScoringLevel`) plus the caller's `PhoenixRecord` rows. Every one is data a tool maker
legitimately wants, which is why (B) is aligned with (A) rather than a chore.

The shift is in kind, not in capability: `IDevDataTransfer` does `SELECT *` and replays **physical
rows**; v2 returns **domain shapes**. So `/Dev/Populate` stops being a byte-copy and becomes an
importer. Two hard requirements:

- **Column parity per table.** `ChartScoringLevel` is not published today at all; `api/tierlist` may
  not carry every column of `TierListEntry`.
- **Real chart GUIDs.** `PhoenixRecord.ChartId` FKs to `Chart.Id`, so v2 must expose the actual GUID
  and never drift to a slug. `ChartDto` already does.

Acceptance test: wipe a local database, run Populate against `api/v2/*` only, confirm the site works.
`dev/export` is deleted in the same PR that passes it.

**Where it lives (2026-08-02).** All of it in `ScoreTracker.Data/DevTooling/`: the API reads, the
wire shapes, the mapping and the SQL, every one of them internal. Domain keeps a single port with a
primitive signature — an earlier cut put a snapshot record and six row types there, eight public
types in the layer everything depends on, for a tool that runs on a laptop. Web keeps the Razor page
and nothing else.

The routes the harness calls are declared as a list and pinned by a test against the controllers'
registered routes, because they once diverged: it asked for
`api/v2/chart-analysis/chart-scoring-levels`, which never existed, and `/Dev/Populate` failed
outright for two commits.

---

## 12. UI surfaces

Routes: **`/CommunityTools`** (player directory + connections), **`/CommunityTools/Invite/{code}`**,
**`/Developers`** (maker list, tool settings, integrate checklist), **`/Developers/{tool}/Console`**,
**`/Developers/{tool}/Debug`**, and the admin review queue under `Pages/Admin/`.

**Nav: one entry, "Community Tools", in the Community menu** (owner, 2026-08-01) — the
`ShellMenu Label="Community"` group in `ShellNav.razor` and its mirrored `more-group` in
`ShellMoreSheet.razor`. That group holds only Communities and Discord today, so it has room and the
grouping is right.

`/Developers` gets **no nav entry of its own**. It is reached from `/CommunityTools`, which carries
a "Make a tool" entry point. A permanent menu item for a page serving twenty people is clutter, and
hiding it until you own a tool is a chicken-and-egg — routing through the player page solves both.
Unlike `/Communities`, neither route is wrapped in `IsGatedMix`: tool sharing is account-level, so a
player browsing Fiesta EX must not lose access to their own privacy controls.

`/CommunityTools` leads with *who can read my scores*, not the catalogue: the page's job is the
answer, and browsing is secondary (rule 1). Rows wear the board skin (`.olb-rank-card`), never cards.

**Documentation splits by what needs to be live.** Prose goes to `docs/INTEGRATING.md` on GitHub —
zero localisation cost, versioned with the code it documents, and makers already live there. What
stays on-site is the part needing *your* keys and *your* tool: a seven-step checklist with live
state, which doubles as the `/Developers` empty state.

---

## 13. Ratchets

| Ratchet | What it pins |
|---|---|
| `AccountPurgeCoverageTests` | **CommunityTools joins the vertical array.** `Tool`, `ToolShare`, `ApiKey`, `WebhookDelivery` and `ToolActivity` all carry user keys, so each is purged or explicitly exempted with a reason |
| `VerticalBoundaryTests` | only `Contracts/` and `Wiring/` public in the new assembly |
| `ModelContributionRegistrationTests` | `CommunityToolsModelContribution` in `VerticalModelContributions.All()` |
| `MessageTaxonomyTests` | new commands/queries/events land in the right folders with the right interfaces |
| `Tests.Api` | **v1 goldens must not move.** New v2 goldens are added alongside |
| `ContractEventSerializationTests` | the delivery envelope round-trips |
| `UiColorTokenTests` | no colour literals in the six new pages |
| `LocalizationKeyTests` / `ResxKeysAreStoredAlphabetically` | every new key in all nine locales, alphabetical, no case-only collisions |
| `DiagnosticExposureTests` | untouched — the console's curated vocabulary is the reason |
| New: `SessionModeNeverPersistsBody` | a session-mode delivery writes no `Body`. This is the one rule where a mistake is a leaked live credential, so it gets its own test rather than a code comment |

---

## 14. Sequencing

> **Build status, 2026-08-01: complete.** C0–C30 are built, tested and pushed. The branch is green
> at every commit.
>
> Three things landed differently from the plan below, and the reasons are worth keeping:
>
> - **C25 was mostly already standing.** The scary dialog shipped in the UI wave and the
>   transition guard is a domain rule from C8, so the commit's real content was the gap between
>   them — a maker can enter session mode the moment their last player disconnects, including
>   while someone has the ordinary connect dialog open. `ConnectToolCommand` now carries the
>   consent the player actually gave and refuses a mismatch.
> - **C26 kept PIU Tracker's wire shape.** Migrating it onto the generic envelope would have meant
>   TUSA shipping a matching change on the same day or 653 players losing their sync. The
>   divergence is one class (`PiuTrackerSessionShape`) and a well-known tool id; everything else —
>   signing, mix filtering, the activity log, the never-persist rule — is the same code every tool
>   gets. §16's "hardcoded custom shape for that one specifically" is what this is.
> - **C28 found a real gap, and the owner then narrowed the answer.** The tier-list endpoint had
>   served four of the seven stored lists; C28 exposed all seven so the dev harness could rebuild
>   what the site shows. On review (2026-08-02) the owner cut it to **three**, renamed for the
>   question each answers: `score-difficulty` (`Scores`), `pass-difficulty` (`Pass Count`, or
>   `Difficulty` before Phoenix) and `pg-difficulty` (`PG`). `Official Scores`, `Popularity` and
>   `Chabala` are visible on /TierLists but are not difficulty judgements, and publishing them
>   invites an integrator to sort by a play count and call it difficulty. Score and PG difficulty
>   answer `404` before Phoenix — those mixes had no such scoring model, which is a different fact
>   from an empty list.


**One PR** (owner, 2026-08-01), built as an ordered commit chain. Every commit compiles and every
suite passes at every commit — the chain is a reviewable narrative, not a bisect hazard.

The ordering principle: **the two tracks are independent until C11.** Commits 1–6 are the v2 API
(presentation over queries that mostly exist); commits 7–10 are the vertical (domain and storage,
no UI). They only meet when share-gating turns on. Within the chain, a commit that adds a table
always precedes the code that reads it, and every ratchet lands with the thing it guards rather
than in a cleanup pass at the end.

### Track A — v2, no tools involved

| # | Commit | Contents |
|---|---|---|
| **C1** | `feat(api): v2 conventions` | Route prefix, `problem+json` middleware, cursor encode/decode, ETag helper, required-`mix` parser, `PageDto`'s v2 successor. No endpoints — the scaffolding a reviewer needs to read C2 |
| **C2** | `feat(catalog): reads v2 needs` | `GetMixesQuery`, `GetSongsQuery`, `GetChartSkillMetricsQuery` + handlers. Vertical-internal, no Web |
| **C3** | `feat(api): v2 catalog endpoints` | Mixes, songs, charts, charts/{id}, random, tier-lists, chart-scoring-levels, chart-skills, charts/{id}/similar. Personal-token auth only. `Tests.Api` goldens |
| **C4** | `feat(api): official leaderboard endpoints` | The six OfficialMirror reads. **`UserId` stripped at the DTO boundary** — the commit that must not be got wrong (§3) |
| **C5** | `feat(ledger): player journal query` | `GetPlayerJournalQuery` + handler |
| **C6** | `feat(api): player reads on v2` | `players/me`, scores with judgments and `recordedAfter`, sessions, journal, weekly charts. Still personal-token, still "me" — subject-addressing arrives in C11 |

### Track B — the vertical, no API involved

| # | Commit | Contents |
|---|---|---|
| **C7** | `feat(tools): CommunityTools vertical skeleton` | Assembly, csproj, `AddCommunityTools()`, `CommunityToolsModelContribution`, its line in `VerticalModelContributions.All()`, the `ModelContributionRegistrationTests` pass. Zero entities — proves the wiring before anything depends on it |
| **C8** | `feat(tools): tool + share model` | The `Tool` aggregate with its state machines, `ToolShare`, the enums, all 9 entities, the migration, repositories. Domain tests for the aggregate's invariants |
| **C9** | `feat(tools): purge coverage` | `ToolDeletionConsumer`, CommunityTools joins `AccountPurgeCoverageTests`. **Lands with C8, never later** — the window where user-keyed tables exist and nothing purges them is the bug this ratchet exists to prevent |
| **C10** | `feat(tools): keys, invites, shares` | Key issue/hash/revoke, invite codes, connect/disconnect/block, the all-tools preference. Handler tests. Still no UI, still no API |

### Where they meet

| # | Commit | Contents |
|---|---|---|
| **C11** | `feat(api): tool keys and share-gated reads` | `ToolKeyAuthenticationScheme`, the share filter, `GET /players` and `/players/{id}` with **404-not-403**, `GET /tool`. The commit that turns v2 from "me" into subject-addressed |
| **C12** | `feat(api): rate limiting` | `AddRateLimiter`, partitioned by key, `RateLimit-*` headers. Separate because it is the one commit that can degrade every endpoint at once |

### Player-facing

| # | Commit | Contents |
|---|---|---|
| **C13** | `feat(tools): share preference on Account` | The toggle, hidden-when-private, the Go public link, **both branches** of the confirm dialog. Localization in all nine locales |
| **C14** | `feat(tools): community tools directory` | `/CommunityTools`, connect dialogs, disconnect, block-a-tool |
| **C15** | `feat(tools): invite landing` | `/CommunityTools/Invite/{code}`, logged-out state, private-profile copy |
| **C16** | `feat(tools): rollout announcement` | The one-time dialog, both branches, and the `IsPublic → ShareWithAllTools` seed migration. **Last of the player-facing set** — nothing should announce a page that is still half-built |

### Maker-facing

| # | Commit | Contents |
|---|---|---|
| **C17** | `feat(tools): developer console` | `/Developers`, tool CRUD, settings, mix subscription across all 30, expiry banner and chips |
| **C18** | `feat(tools): show-once keys` | The two-step create/reveal dialogs, `last4`, two-active-key rotation |
| **C19** | `docs: integration guide` | `docs/INTEGRATING.md` + the seven-step on-site checklist |

### Webhooks

| # | Commit | Contents |
|---|---|---|
| **C20** | `feat(tools): delivery pipeline` | `WebhookDeliveryEntity` writes, `HmacSigner`, the outbound header, `WebhookDeliveryClient`, the fan-out consumer. Ping + score push |
| **C21** | `feat(tools): retry and retention` | Hangfire sweep, backoff, the failures-only body rule, the prune job, `SCHEDULED-JOBS.md` rows |
| **C22** | `feat(tools): activity console` | The curated event vocabulary, hourly rollups, `GET /api/v2/events` |
| **C23** | `feat(tools): debug tools` | Test delivery (always `test: true`, always the maker's own account), signature echo, replay with the body-expired state |

### Session mode

| # | Commit | Contents |
|---|---|---|
| **C24** | `feat(tools): session delivery port` | `ISessionDeliveryClient` in Domain, the CommunityTools implementation, **`SessionModeNeverPersistsBody`**. The ratchet lands *with* the capability — a mistake here leaks a live piugame credential |
| **C25** | `feat(tools): session consent` | The scary dialog, the zero-connected-players transition guard, all-tools exclusion |
| **C26** | `refactor(import): piutracker behind the port` | The bespoke adapter moves into CommunityTools, `SyncPiuTracker` users grandfathered into explicit shares, **and the five-minute blocking call comes off the import path** |

### Closing

| # | Commit | Contents |
|---|---|---|
| **C27** | `feat(admin): tool review queue` | Approve/reject with a required reason, public listing |
| **C28** | `refactor(dev): populate from v2` | `/Dev/Populate` onto `api/v2/*`, the wipe-and-repopulate acceptance test |
| **C29** | `refactor(dev): delete dev/export` | `DevExportController`, `IDevDataTransfer`, `DevDataTransfer`. Only after C28 proves the replacement |
| **C30** | `docs: API v2 and community tools` | `API.md`, `ARCHITECTURE.md`, `CLAUDE.md`, `DATABASE-SCHEMA.md`, `UX-GUIDELINES.md`, `delete-my-data.md`, `legacy-mixes.md`, `HOW-TO-RUN.md` (§15) |

### Rules the chain obeys

- **Ratchets ship with what they guard**, never in a cleanup pass — C9 with C8, C24's with C24.
- **Delete after replace, never before**: C29 follows C28's passing acceptance test. (Ports are the
  documented exception — delete the port before the implementation — but nothing here deletes a port.)
- **A commit that adds a table precedes the code that reads it.**
- **Localization lands in the commit that adds the string**, in all nine locales, alphabetically
  positioned — never batched at the end, where the resx merge seam is at its worst.
- **v1 goldens are never touched.** If a commit makes `Tests.Api`'s v1 file move, the commit is wrong.

---

## 15. Docs updated in this PR

All done at C30 except the two the deletion forced earlier (API.md and CLAUDE.md lost their
`dev/export` paragraphs at C29, with the code).

| Doc | Change |
|---|---|
| [API.md](../API.md) | the v2 surface map; v1 marked frozen; the `dev/export` "NOT the partner surface" section is deleted at C29 |
| **`docs/INTEGRATING.md`** | new — the maker's manual |
| [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md) | a Community Tools section with all nine tables. The all-tools preference is its own table (`ToolSharePreference`) rather than a column on `User` — public and all-tools stay separable, and Identity's table stays Identity's |
| [ARCHITECTURE.md](../ARCHITECTURE.md) | CommunityTools in the solution layout and the vertical list; the Pages table gains `/CommunityTools` and `/Developers`; the published-ports paragraph gains `ISessionDeliveryClient` as the OfficialMirror → CommunityTools cycle-breaker |
| [CLAUDE.md](../../CLAUDE.md) | CommunityTools in the package-allowlist table with its `Microsoft.Extensions.Http` exemption; the new ratchets |
| [SCHEDULED-JOBS.md](../SCHEDULED-JOBS.md) | the webhook retry sweep and the retention prune |
| [UX-GUIDELINES.md](../UX-GUIDELINES.md) | the show-once reveal pattern, and the rule that a maker-facing surface may use maker vocabulary while player-facing copy never says "webhook" |
| [HOW-TO-RUN.md](../HOW-TO-RUN.md) | `/Dev/Populate` now populates from `api/v2/*` (C28) |
| [delete-my-data.md](delete-my-data.md) | new §8.3: the tools cascade and the create-a-tool guard. Both were documented and **found unbuilt** while writing it — the guard and the consequence panel were built at C30 rather than left as prose describing something that did not exist |
| [legacy-mixes.md](legacy-mixes.md) | the `scoringModel` discriminator on the wire |

---

## 16. Settled by the owner, 2026-08-01

- **Personal API keys are unchanged.** Future plans exist; they are not this pass.
- **Tool integrations are GET-only.** All mutation stays on personal tokens — which deletes the
  entire scope system.
- **Show-once applies to toolmaker keys only.**
- **Session mode is never available through all-tools sharing**, and it is the piugame sid, not a
  credential we mint.
- **Public ≠ all-tools**, kept separate, coupled only by the hidden-when-private rule and the
  turns-on-when-you-go-public rule. The latter fires **every time**, not just when unset.
- **The private card gets a Go public link** — a lot of players do not know it is an option.
- **100 scores per delivery**, `next` cursor for the tail.
- **`mix` required in v2**; toolmakers subscribe per-mix for webhooks; **all 30 mixes selectable** —
  "if someone wants legacy mix scores they should get legacy mix scores."
- **600/min tool, 60/min personal.**
- **PIU Tracker becomes a public tool with a hardcoded bespoke shape.**
- **Grandfather `SyncPiuTracker` users into an explicit share.**
- **Webhook payload identity**: mix name, PIU Scores user id, PIU Scores username, PIUGame tag.
- **Makers are auto-connected to their own tool** on creation.
- **Signature echo and replay both ship.**
- **Prose in the repo, interactive on-site.**
- **Tools cascade on account deletion** rather than blocking it; no de-listing during the grace
  window and no notification to connected players.
- **Nav: one "Community Tools" entry under the Community menu.** `/Developers` is reached from it.
- **No expiry emails.** Outbound mail to makers is a separate project; the in-page surface carries
  it alone.
- **Tournaments are not carried into v2**; **PumbilityPlus is dropped everywhere**.
- **Judgment counts ride score reads** (they are on `PhoenixRecord`), null rather than zeroed when absent.
- **One `gameTag`, not a per-mix map.** Verified against the prod-synced DB: of 246 linked users, 32
  appear in both Phoenix mixes and 5 carry different tags — every divergence 4–12 days apart, two of
  them the same name with a different discriminator. That is one AM Pass tag snapshotted at two
  scrape times, not two identities. The API returns the most recently seen, with `gameTagSeenAt`.
- **`shorthand` → `difficulty`**, carrying the slot-aware display form.
- **PIU Center skills and NPS are published**; NPS is never derived from notes ÷ duration.
- **Official leaderboards are public** (no share) but **never carry `userId`** — join on `gameTag`.
- **Chart similarity is published** with sub-scores, shared badges, `chartsCompared` and `matchFloor`.
- **No survey of existing makers** — the owner guides the shape directly.

## 17. Unverified

- **Whether a piugame sid is valid across mixes.** `GetSessionId` builds a per-mix `CookieContainer`
  against `BaseUrlFor(mix)` — separate hosts — so *ours* is per-mix by construction, but there is an
  AM Pass federation hop right after login and a browser appears to move between mixes freely. Needs
  a `[LiveSiteFact]` probe in `ExplorationTests/LiveSite/`: sign into Phoenix 2, take the sid, try an
  authenticated Phoenix page. Until it runs, the payload carries the mix and the maker docs
  disclaim it.
- **Column parity for `TierListEntry` and `ChartScoringLevel`** against what v2 would publish (§11).
