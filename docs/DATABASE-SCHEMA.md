# Database Schema

One SQL Server database, one EF Core `DbContext` ([`ChartAttemptDbContext`](../ScoreTracker/ScoreTracker.Data/Persistence/ChartAttemptDbContext.cs)). Almost everything lives in the `scores` schema. Migrations live in [`ScoreTracker.Data/Migrations/`](../ScoreTracker/ScoreTracker.Data/Migrations/) and are applied by a self-contained EF bundle during the gated production deploy (locally, the Aspire AppHost auto-migrates at startup).

**Table ownership follows the verticals** (see [ARCHITECTURE.md](ARCHITECTURE.md)): a vertical owns its EF entities as `internal` classes and registers them with the shared context via an `IDbModelContribution` in its `Wiring/` namespace. Cross-vertical reads go through published ports and contracts — never SQL joins onto another vertical's tables. Tables not yet extracted to a vertical live in `ScoreTracker.Data` directly.

**Tables are never dropped — they are archived** (owner standard, 2026-07-27). Deleting a feature removes its EF entity and `ToTable` line, but the rows survive: the new migration transfers the table to the **`archive` schema** (`ALTER SCHEMA archive TRANSFER scores.<Table>`) instead of dropping it, so a revived feature starts from real data. Archived tables move to [Archived](#archived) below rather than leaving this document. `archive` is the only sanctioned destination — `back`, `bup`, `books`, and `smx` are unrelated legacy artifacts of unknown provenance, and the older `*_archived` suffix-in-`scores` convention (the UCS tables) is superseded.

## Game content (shared, read by everything)

| Table | Purpose |
|---|---|
| `scores.Mix` | Game version/mix definition — all 31 mixes (1st Dance Floor → Phoenix 2), seeded by migration with picker `SortOrder` + `IsPrimary` (the P2/Phoenix/XX trio; the rest sit behind "More Mixes") |
| `scores.Song` | Song metadata: artist, name, BPM, duration, image |
| `scores.Chart` | A playable chart: song, type (Single/Double/CoOp/HalfDouble), level, step artist, debut mix (`OriginalMixId`), explicit `PlayerCount` (legacy Routine-era co-ops carry a real difficulty in Level, so player count is no longer derived from it) |
| `scores.ChartMix` | Chart↔mix mapping with the level and note count for that mix, plus `LegacySlot` (pre-Exceed slot identity — "Crazy", "Another Nightmare" — part of chart identity in those eras; [legacy-mixes design](design/legacy-mixes.md)) |
| `scores.Country` | Country list with flag image path |

## Identity & accounts (shared; logically the Identity vertical, physical extraction pending)

| Table | Purpose |
|---|---|
| `scores.User` | User account: name, profile image, game tag, country, content-lock status |
| `scores.ExternalLogin` | Sign-in method mappings to users, many-to-one (Discord/Google/Facebook OAuth ids; namespaced PiuGame aliases `mbid:*`/`card:*`) |
| `scores.UserApiToken` | API tokens for the partner API, with usage tracking |
| `scores.UserSettings` | Per-user UI settings key/value store |
| `scores.MergeRequest` | Durable account-merge record: survivor/retired users, moved logins + retired-user snapshot (JSON, for undo), state, and the grace-window purge schedule (Identity-vertical entity) |
| `scores.AccountDeletionRequest` | A self-serve account deletion: when it was asked for, when the purge may begin, and the `IsPublic`/game-tag snapshot taken before the account was hidden, which is what cancelling restores. Deliberately not `MergeRequest` — that record demands a survivor and carries moved logins, both meaningless here. Retained past the purge as its audit trail, with the game tag nulled on completion so no personal data outlives the account ([delete-my-data.md](design/delete-my-data.md) §8) |
| `scores.UserImportCredentialKey` | Per-device wrapped data-key for a remembered PIUGAME credential (envelope encryption): `KeyId`, `UserId`, the Key-Vault-wrapped DEK, `CreatedAt`. **Holds no password** — the AES-GCM ciphertext lives in the user's browser local storage and the master key never leaves Key Vault; deleting rows revokes ([remember-my-password design](design/import-widget-remember-password.md)) |

## Score Ledger (vertical: `ScoreTracker.ScoreLedger`)

| Table | Purpose |
|---|---|
| `scores.PhoenixRecord` | Best-known Phoenix-scoring attempt per user+chart+mix: score, plate (NULL when broken — the game awards none for a failed stage), broken flag, and the `Source` of the current best (verified ⇔ `officialImport`; NULL predates capture). `Perfects/Greats/Goods/Bads/Misses` hold the producing play's judgement breakdown (all five set together; NULL = never observed). Unique on UserId+ChartId+MixId; pre-Phoenix-2 rows backfilled as Phoenix — as are all MixId columns below |
| `scores.BestAttempt` | Legacy-model best attempts (letter grade + broken + optional era-scale score) per user+chart+mix; `MixId` defaults to XX — the table's original implicit scope — and every pre-Phoenix mix records here ([legacy-mixes design](design/legacy-mixes.md)) |
| `scores.PhoenixRecordStats` | Per-score Pumbility stats per user+chart+mix, written by PlayerProgress through a Ledger port |
| `scores.ScoreEventJournal` | **Append-only** journal of observed plays. One row is one play, uniquely keyed **(UserId, MixId, ChartId, OccurredAt)** — the site's own play time — so re-importing a recently-played window collapses onto the existing row instead of duplicating it. `IsBest` says whether the play became the record when it was written; rows sourced from recently-played that beat nothing are `IsBest=0`, and are the only rows the ledger's record ignores. Rows are never updated (except to raise `IsBest`) or deleted. `SessionId` groups rows into play sessions / import runs (NULL predates capture). `Perfects/Greats/Goods/Bads/Misses` hold the play's judgement breakdown when observed (all five together; NULL otherwise); `OccurredAt` carries the official site's saved timestamp when the import had one, clock time otherwise. Seeded 2026-06 from `PhoenixRecord` (`Source='backfill'`, dated at the record's last update); every pre-2026-07-30 row is `IsBest=1`, because until then the journal was written only when the record changed. Rows *are* deleted by a player deleting their own data — the append-only rule governs the write path, not the owner of the data ([delete-my-data.md](design/delete-my-data.md) D8). See [score-truth-model.md](design/score-truth-model.md) |
| `scores.ScoreSession` | One play session or import run, keyed by the `SessionId` the batch accumulator mints. `StartedAt`/`LastActivityAt` are **wall clock** — when the scores reached us — which is what `ScoreEventJournal.OccurredAt` cannot say, being the official site's play date; that distinction is the whole reason the table exists. `AccountTag`/`CardId` record which card an official import pulled from, so "I imported the wrong card" is answerable. `ScoreCount`/`NewCount`/`UpscoreCount` are denormalized for the Undo list and count records changed, not plays observed. No FK from the journal: historical rows carry session ids with no row behind them and the journal is updated in place, so the constraint would fail on the first `IsBest` raise ([delete-my-data.md](design/delete-my-data.md) §15) |

## Home Page (vertical: `ScoreTracker.HomePage`)

| Table | Purpose |
|---|---|
| `scores.HomePage` | A user's dashboard pages (name, tab ordinal, default flag, optional page-level mix context). Capped at 8 per user in handlers |
| `scores.HomePageWidget` | Widget instances on a page: registry TypeId, optional title, auto-flow ordinal, size preset, versioned config JSON (public via export/import — D19). Capped at 8 per page in handlers |

## Player Progress (vertical: `ScoreTracker.PlayerProgress`)

| Table | Purpose |
|---|---|
| `scores.PlayerStats` | Aggregated player stats per mix (PK UserId+MixId): ratings, competitive levels, clear counts |
| `scores.PlayerHistory` | Point-in-time snapshots of player stats, per mix |
| `scores.UserTitle` | Titles earned per mix, with paragon progression |
| `scores.UserHighestTitle` | Denormalized current-highest title per mix (PK UserId+MixId) for fast reads |
| `scores.SuggestionFeedback` | User feedback on chart recommendations |
| `scores.ScoreHighlight` | Write-time noteworthy-score flags per journal row (crown, title progress, Score Quality ≥90th, folder ≥90%, competitive improver, folder debut), denormalized Level/ScoringLevel for noteworthy ordering plus per-flag caption detail (PumbilityRank, FolderDebutOrdinal, Peer{Count,BetterCount,PgCount}, SkillTitle{Name,Score,Threshold}); joined to the journal by (SessionId, ChartId). Never backfilled |
| `scores.PlayerMilestone` | Session-level milestones with timestamps: Pumbility gains, Singles/Doubles competitive gains, title completions, paragon gains, folder lamps (Kind + compact Detail payload). Never backfilled |
| `scores.PlayerFolderLevel` | One row per (UserId, MixId, ChartType, Level) — the composite PK, no surrogate id, because the folder is the identity and every write is an upsert. Holds `Size`/`Played` (the completion percent), `TierScore` (the score at the completion tier position, which the folder grade reads off) and `AverageScore` (a display number); `Level` carries the player count for co-op. Written by the highlight saga per touched folder and by the admin backfill; read by the tier-list page, the folder-levels widget and the community player profile ([folder-level-progression](design/folder-level-progression.md)) |
| `scores.PlayerSeasonRecap` | Computed season-recap payload per user+mix (PK UserId+MixId): JSON `PlayerRecap` contract + SchemaVersion + ComputedAt. Written whole by the recap saga (admin-triggered), read whole by the recap page; older-schema rows read as "not computed yet" |

## Chart Intelligence (vertical: `ScoreTracker.ChartIntelligence`)

| Table | Purpose |
|---|---|
| `scores.TierListEntry` | Tier list entries per mix (the site's most-used feature) |
| `scores.UserTierListEntry` | Materialized per-user relative tier lists, event-driven off score imports (tier-lists overhaul C1); `Freshness` weights each entry's similar-players vote by score age relative to the player's own folder (score-age workshop — default 1.0 until the Backfill User Tier Lists run re-stamps rows) |
| `scores.ChartScoreStats` | Population score variance per chart, refreshed by the daily scores tier-list rebuild (tier-lists overhaul C1) |
| `scores.FolderCohortStats` | Folder pass-count histograms per competitive-level bucket, refreshed by the daily scores tier-list rebuild — powers the "Folder Passes vs Similar Players" bar (tier-lists overhaul C16) |
| `scores.ChartScoringLevel` | Calculated scoring-difficulty level per chart+mix |
| `scores.ChartSimilarity` | Similarity-graph edges: the top-20 nearest charts per chart+mix, stored **floor-free** so the shelf can move its own bar and render near-misses without a rebuild. `SignalsJson` carries the skill/intensity breakdown plus the shared badges the shelf names each match from. Rebuilt wholesale by the nightly similarity job ([design](design/chart-similarity.md)) |
| `scores.ChartLetterDifficulty` | Letter-grade (AA–PG) difficulty percentiles per chart |
| `scores.ChartDifficultyRating` | Aggregated community difficulty ratings (count + std dev) |
| `scores.UserChartDifficultyRating` | An individual user's difficulty vote |
| `scores.ChartPreferenceRatingEntity` | Aggregated preference ratings |
| `scores.UserPreferenceRating` | An individual user's preference vote |
| `scores.CoOpRating` | Aggregated co-op difficulty ratings, per mix |
| `scores.UserCoOpRating` | An individual user's co-op difficulty vote |

## Game Content Catalog (vertical: `ScoreTracker.Catalog`)

| Table | Purpose |
|---|---|
| `scores.ChartVideo` | YouTube video links per chart with uploader metadata |
| `scores.ChartSkill` | Skill tags on charts with highlight flags — regenerated per piucenter crawl since the PiuCenter integration (hand tags archived) |
| `scores.ChartSkillArchive` | One-time snapshot of the pre-crawler hand-maintained ChartSkill rows; never read by the app |
| `scores.ChartSkillMetric` | Banked per-chart numeric step-analysis facts per external source ((ChartId, Source, MetricName) → decimal + optional grade): badge fractions, top-3 ranks, practice ranks, NPS/sustain/difficulty prediction |
| `scores.ExternalChartAlias` | Generic external-name map ((Source, ExternalKey) → nullable ChartId) with Auto/Manual/NotFound status + last-checked stamp; for piucenter the key doubles as the fetch URL, so this is also the crawl plan and negative cache |
| `scores.SongNameLanguage` | Localized song names per culture |
| `scores.SavedChart` | User bookmark lists of charts *(ownership split pending — currently shared)* |

## Randomizer (vertical: `ScoreTracker.Randomizer`)

| Table | Purpose |
|---|---|
| `scores.UserRandomSettings` | Saved randomizer presets (JSON) + mix + optional share token |
| `scores.TournamentRandomSettings` | Tournament-scoped randomizer presets (replaces the Match-subsystem storage) |
| `scores.RandomizerDraw` | The active draw per context (user or tournament); slug = stable spectator link |
| `scores.RandomizerDrawCard` | Pulled cards with per-pull identity, stable order, and protect/veto state |

## Official Game Mirror (vertical: `ScoreTracker.OfficialMirror`)

| Table | Purpose |
|---|---|
| `scores.OfficialLeaderboard` | Board dimension: one row per mirrored board (Rating or Chart), chart boards keyed to their catalog ChartId |
| `scores.OfficialPlayer` | Player dimension: one row per board-visible tag per mix, avatar + optional import-confirmed UserId link |
| `scores.OfficialLeaderboardSnapshot` | One sweep run per row — run state (stage/counts/error) while executing, snapshot anchor once `CompletedAt` seals it; unsealed rows are invisible to reads |
| `scores.OfficialLeaderboardPlacement` | One player-place-on-board per snapshot (weekly history); clustered by (Snapshot, Leaderboard, Place, Player) |
| `scores.OfficialChartPopularity` | Official play-ranking place per chart per snapshot (popularity history) |
| `scores.OfficialBoardRecord` | Record book per chart board: all-time high score (encodes every claimed grade band) |
| `scores.OfficialFolderRecord` | Record book per folder (mix + type + level): all-time high score across the folder's boards |
| `scores.OfficialWeeklyHighlight` | Editorial weekly highlights computed at import (movers, boards climbed, new #1s, grade firsts, plus the This Week hero's playerless summary rows: pulse, gainers, debuts, floor marks); rebuildable from snapshots |
| `scores.OfficialPlayerRenameProposal` | Detected likely renames awaiting admin accept/dismiss; survives merges as the audit trail |

## Weekly Challenge (vertical: `ScoreTracker.WeeklyChallenge`)

| Table | Purpose |
|---|---|
| `scores.WeeklyTournamentChart` | The active weekly chart set per mix, with expiration |
| `scores.WeeklyUserEntry` | Player entries per mix: score, plate (NULL when broken), optional photo proof, trust source (official import vs manual self-report) |
| `scores.UserWeeklyPlacing` | Historical placements from finished weeks, per mix |
| `scores.PastTourneyCharts` | Archive of previously used weekly charts per mix (avoids repeats; PK ChartId+MixId) |
| `scores.DailyStepChart` | The one live Daily Step chart per mix (0–1 rows): ChartId, ForDate, IsLimbo, ExpirationDate. Redrawn each midnight-ET rotation |
| `scores.DailyStepEntry` | Player entries on today's Daily Step chart per mix (score, plate — NULL when broken —, competitive level, source: official import vs manual widget submission); cleared at rotation |
| `scores.UserDailyStepPlacing` | Retained per-user Daily Step history, snapshotted at each rotation (ForDate, IsLimbo, Place) |

## Event Competition (vertical: `ScoreTracker.EventCompetition`)

| Table | Purpose |
|---|---|
| `scores.Tournament` | Competitive event definition: configuration, location, visibility, and the Discord channel the randomizer's Push to Discord posts into |
| `scores.UserTournamentRegistration` | Player registrations |
| `scores.UserTournamentSession` | A player's session: charts played, scores, approval state, and the mix it was played on |
| `scores.PhotoVerification` | Photo proofs attached to sessions |
| `scores.TournamentChartLevel` | Per-tournament chart level overrides |
| `scores.TournamentRole` | Per-tournament roles (organizer, judge, …) |
| `scores.TournamentRoleInvite` | Role-carrying invite link tokens (Head TO mints; optional expiry) |
| `scores.QualifiersConfiguration` | Qualifier stage setup: charts, scoring, cutoff, and the mix the qualifier runs on |
| `scores.UserQualifier` | Qualifier entries and approval status |
| `scores.UserQualifierHistory` | Timestamped snapshots of qualifier submissions |
| `scores.CoOpTeam` / `scores.CoOpPlayers` | Co-op tournament teams and their members |

## UCS (retired)

The UCS vertical and its `/UcsLeaderboards` page were deleted — the feature never took hold.
Its three tables were **renamed, not dropped**, because they hold real user submissions and
the owner may revive the feature: `scores.UcsChart_archived`,
`scores.UcsChartLeaderboardEntry_archived`, `scores.UcsChartTag_archived`. Nothing in the
model references them, and no code reads them. Their PKs and indexes keep their original
(un-suffixed) names, since `sp_rename` on a table leaves constraint names alone.

## Communities (vertical: `ScoreTracker.Communities`)

| Table | Purpose |
|---|---|
| `scores.Community` | Communities with privacy type and regional flag; `DefaultAdminPermissions` (CommunityPermission flags applied to newly promoted admins) and `DefaultLanguage` (Discord-notification fallback culture) |
| `scores.CommunityMembership` | Community membership + role overlay: `Role` (CommunityRole — Creator/Admin/Member/Banned; a Banned row is retained to block rejoin), `Permissions` (CommunityPermission flags, admins only), `GrantedByUserId`, `JoinedAt`. One row per (community, user) — the index is unique, so a plain join/leave writes a single row rather than rewriting the roster |
| `scores.CommunityInviteCode` | Invite codes, optionally expiring |
| `scores.CommunityChannel` | Discord channels wired to a community's event feed. Every registered channel receives every community notification — the old per-type opt-in columns (`SendNewScores`/`SendTitles`/`SendNewMembers`) were never honored and were dropped. `Culture` (nullable, null = English) is the language the channel's cards render in |
| `scores.CommunityHighlight` | Community big-wins feed: one summary row per (score-event × community the winner belongs to), `Payload` a JSON list of `SignificantWin`, `EventId` dedupes across shared communities. Written by the highlight saga off `ScoreHighlightsCapturedEvent`, purged weekly after 30 days ([home-page-widgets §7](design/home-page-widgets.md)) |
| `scores.DiscordFeedSubscription` | A channel's subscription to a broadcast feed, independent of any community: `ChannelId`, `FeedKind` (WeeklyCharts/DailyStep/OfficialLeaderboards), `Mix` (per-mix), `RegisteredByDiscordUserId`, `Culture` (nullable, null = English — the language its posts render in; re-registering updates it). Unique on (ChannelId, FeedKind, Mix); registered via `/piu register` ([discord-overhaul](design/discord-overhaul.md)) |

## Community Tools (vertical: `ScoreTracker.CommunityTools`)

| Table | Purpose |
|---|---|
| `scores.Tool` | A registered community tool: `Name`/`Description`/`Url` (what an approved listing shows), `Visibility` (Private/PendingApproval/Public/Rejected, stored as the enum **name** so a reordered enum cannot relabel every row), `AcceptsAllToolsShare`, `WebhookMode` (None/PlayerPing/ScorePush/PiuGameSession), `WebhookUrl`, `OutboundHeaderName`/`OutboundHeaderValue` (the header a maker authenticates us by — **plaintext by necessity**, since we send it verbatim on every delivery; the name says so, because the obvious "fix" for a name implying otherwise would break every delivery), `ApprovedAt`, `RejectionReason` |
| `scores.ToolMixSubscription` | Which mixes' imports trigger a delivery for one tool. Empty = every mix |
| `scores.ToolInviteCode` | A private tool's recruiting link, keyed by the code itself as `CommunityInviteCode` is |
| `scores.ToolShare` | A player's grant to one tool: `Source` (Direct/AllTools — what a revoke means differs), `GrantedAt`, `RevokedAt` |
| `scores.ToolBlock` | An all-tools player's "not this one". Without it the only way to refuse a single tool would be to turn off sharing entirely |
| `scores.ToolSharePreference` | One row per player: `ShareWithAllTools`. Seeded from `IsPublic` once at rollout by the `SeedToolSharePreference` migration — a one-time seed, not a rule; public and all-tools stay separate concepts and are never joined in a query |
| `scores.ToolApiKey` | Hashed tool keys (`pst_live_` prefix, SHA-256). Two live per tool, six-month default lifetime; `LastUsedAt`, `RevokedAt`. The plaintext exists only in the response that mints it |
| `scores.WebhookDelivery` | One outbound delivery: `QueuedAt`, status, attempt count, next attempt, the remote's status code and a 500-char body snippet, and `Body` — kept **only** for a pending/failed/abandoned delivery and only for 7 days, so a maker can replay. Never written at all in PIUGame-session mode, because that body carries a live piugame credential (`SessionModeNeverPersistsBody`) |
| `scores.ToolActivity` | The maker-facing console feed. Point events (delivery succeeded/rejected, key created/revoked) plus hourly rollups for the high-volume ones — at 600 requests a minute a per-call row would put tens of thousands of rows in front of someone who wants one line saying "you hit the limit 212 times this hour" |

## Archived

Tables whose feature was deleted. They carry no EF entity and no `ToTable` registration — the rows
are queryable in SQL only, kept so a revived feature starts from real data. Nothing here is
referenced by running code; a table listed here is safe to ignore unless you are reviving what
owned it.

| Table | Archived | Was |
|---|---|---|
| `archive.Match` | 2026-07-28 | Bracket match definition (JSON configuration) |
| `archive.MatchLink` | 2026-07-28 | Winner/loser routing between matches |
| `archive.RandomSettings` | 2026-07-28 | Named randomizer configurations for bracket matches — unrelated to the randomizer's own `UserRandomSettings`/`TournamentRandomSettings`, which are live |
| `archive.TournamentPlayer` | 2026-07-28 | Bracket participants with seeds |
| `archive.TournamentMachine` | 2026-07-28 | Machine assignments for brackets |
| `archive.UserOfficialLeaderboard` | 2026-07-28 | Pre-snapshot official placements, superseded by `OfficialLeaderboardPlacement` |
| `archive.UserWorldRanking` | 2026-07-28 | Calculated world rankings; the feature had no reader left |
| `archive.OfficialUserAvatar` | 2026-07-28 | Avatar cache, absorbed by `OfficialPlayer` |
| `archive.OfficialLeaderboardImportState` | 2026-07-28 | Last-import timestamp, absorbed by the snapshot seal |

The UCS tables (`scores.UcsChart_archived` and its two siblings) predate the `archive` schema and
keep their suffix-in-place form.

Not archives, despite appearances: the `back`, `bup`, `books`, and `smx` schemas are legacy
artifacts of unknown provenance (`back.Match` holds 83 rows against the live table's 490 — a stale
partial backup). Leave them alone. The `scores.Ucs*_archived` tables predate this convention.

## System tables

| Table | Purpose |
|---|---|
| `dbo.__EFMigrationsHistory` | EF Core's applied-migrations ledger |
| `HangFire.*` | Hangfire's job storage — **auto-created by Hangfire, not EF-managed**. Never add EF entities for these; recurring schedules live here and survive restarts |
