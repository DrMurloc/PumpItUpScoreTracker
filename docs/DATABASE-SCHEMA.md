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
| `scores.User` | User account: name, profile image, game tag, country, content-lock status, and `DeepScansRemaining` — the Score check's monthly full-walk balance, refilled by the `reset-deep-scans` job ([SCHEDULED-JOBS.md](SCHEDULED-JOBS.md)). A balance rather than a dated usage count, so granting someone extra is one UPDATE |
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
| `scores.ScoreEventJournal` | **Append-only** journal of observed plays. One row is one play, uniquely keyed **(UserId, MixId, ChartId, OccurredAt)** — the site's own play time — so re-importing a recently-played window collapses onto the existing row instead of duplicating it. `IsBest` says whether the play became the record when it was written; rows sourced from recently-played that beat nothing are `IsBest=0`, and are the only rows the ledger's record ignores. Rows are never updated (except to raise `IsBest`) or deleted. `SessionId` groups rows into play sessions / import runs (NULL predates capture). `Perfects/Greats/Goods/Bads/Misses` hold the play's judgement breakdown when observed (all five together; NULL otherwise); `OccurredAt` carries the official site's saved timestamp when the import had one, clock time otherwise. Seeded 2026-06 from `PhoenixRecord` (`Source='backfill'`, dated at the record's last update); every pre-2026-07-30 row is `IsBest=1`, because until then the journal was written only when the record changed. Rows *are* deleted by a player deleting their own data — the append-only rule governs the write path, not the owner of the data ([delete-my-data.md](design/delete-my-data.md) D8). `LetterGrade` is the XX-and-older axis: legacy has no plate — the letter IS the plate equivalent — and it is what most legacy records carry, since `Score` is usually absent there. It is a separate column rather than a reuse of `Plate` because a Phoenix reader parsing "SSS" as a plate throws; the row's mix decides which pair is live. Note that a legacy `Score` can exceed 1,000,000 (the largest in production is 45,282,000), so it is read as an era score rather than a `PhoenixScore`. See [score-truth-model.md](design/score-truth-model.md) and [legacy-mixes.md](design/legacy-mixes.md) |
| `scores.LimboChart` | Presence-only: the charts carrying a **limbo leaderboard** — the "how low can you clear it" board, ranked ascending on lowest passing score. PK (MixId, ChartId) and no other column; a row means the chart shows the Lowest Passing chip, its absence means it does not. **Rows are inserted by hand-run SQL** — no admin screen, nothing derives them, and the application only ever reads the table. No FK onto `Chart`, so flagging a chart that does not exist yet is inert rather than a failed INSERT. Flagging changes nothing about what the ledger keeps: the board reads passing rows already in `ScoreEventJournal` ([limbo-leaderboard.md](design/limbo-leaderboard.md)) |
| `scores.ScoreSession` | One play session or import run, keyed by the `SessionId` the batch accumulator mints. `StartedAt`/`LastActivityAt` are **wall clock** — when the scores reached us — which is what `ScoreEventJournal.OccurredAt` cannot say, being the official site's play date; that distinction is the whole reason the table exists. `AccountTag`/`CardId` record which card an official import pulled from, so "I imported the wrong card" is answerable. `ScoreCount`/`NewCount`/`UpscoreCount` are denormalized for the Undo list and count records changed, not plays observed. No FK from the journal: historical rows carry session ids with no row behind them and the journal is updated in place, so the constraint would fail on the first `IsBest` raise ([delete-my-data.md](design/delete-my-data.md) §15). `ProcessedAt` is null until everything downstream of the session's batch has run — stamped by consuming `ScoreHighlightsCapturedEvent`, which the capture chain publishes unconditionally. Null on an old-enough session is the restart-recovery signal, so every row predating the column was backfilled as processed: "unprocessed" must never be able to mean "older than the feature" ([import-restart-recovery.md](design/import-restart-recovery.md)). Indexed filtered on `ProcessedAt IS NULL`, so the index holds only in-flight and interrupted sessions |

## Home Page (vertical: `ScoreTracker.HomePage`)

| Table | Purpose |
|---|---|
| `scores.HomePage` | A user's dashboard pages (name, tab ordinal, default flag, optional page-level mix context). Capped at 8 per user in handlers |
| `scores.HomePageWidget` | Widget instances on a page: registry TypeId, optional title, auto-flow ordinal, size preset, versioned config JSON (public via export/import — D19). Capped at 8 per page in handlers |

## Player Progress (vertical: `ScoreTracker.PlayerProgress`)

| Table | Purpose |
|---|---|
| `scores.PlayerStats` | Aggregated player stats per mix (PK UserId+MixId): ratings, competitive levels, clear counts, the four PUMBILITY pools (`SkillRating`/`SinglesRating`/`DoublesRating`/`CoOpRating`) stored as **float, unrounded** — rounding one at rest costs precision only the UI may spend, and two surfaces rounding at different points disagree about the same account (2026-08-09), plus the `Estimated*PumbilityRank` trio and the `PumbilityBoardAsOf` snapshot they were ranked against — our pool placed into the last sealed official board, not a rank read back from it ([session-breakdown §2.2](design/session-breakdown.md)) |
| `scores.PlayerHistory` | Point-in-time snapshots of player stats, per mix. `SkillRating` and `CoOpRating` are **float** for the same reason as `PlayerStats` |
| `scores.UserTitle` | Titles earned per mix, with paragon progression |
| `scores.UserHighestTitle` | Denormalized current-highest title per mix (PK UserId+MixId) for fast reads |
| `scores.SuggestionFeedback` | User feedback on chart recommendations |
| `scores.ScoreHighlight` | Write-time noteworthy-score flags per journal row (crown, title progress, Score Quality ≥90th, folder ≥90%, competitive improver, folder debut), denormalized Level/ScoringLevel for noteworthy ordering plus per-flag caption detail (PumbilityRank, FolderDebutOrdinal, Peer{Count,BetterCount,PgCount}, SkillTitle{Name,Score,Threshold}, PeerPercentile, AttemptsBeforeClear, Official{Place,BoardDepth,AsOf}, CompetitiveBaseline, PumbilityGain); joined to the journal by (SessionId, ChartId). `PeerPercentile` rides **every** captured score rather than only flagged ones — the Sessions page colours each row by it. `CompetitiveBaseline` is the same-type competitive level as the batch opened, stored because it is **per-batch** and unrecoverable afterwards (a session drains as several batches; the stats row remembers only the last); the score's own competitive level stays a pure function, so the one column buys both halves of "23.6 (+0.4)". `PumbilityGain` is **float** and unrounded — it was an int until 2026-08-09, which meant a sub-point gain rounded to zero and was dropped before anything could show it. It is what the play added to the **combined** pool — Phoenix 2's Singles/Doubles pools are deliberately unmeasured, since the row reports one number. Never backfilled; dropped for a session by `ScoreSessionUndoneConsumer` |
| `scores.PlayerMilestone` | Session-level milestones with timestamps: Pumbility gains, Singles/Doubles competitive gains, title completions, paragon gains, folder lamps (Kind + compact Detail payload). Never backfilled |
| `scores.PlayerFolderLevel` | One row per (UserId, MixId, ChartType, Level) — the composite PK, no surrogate id, because the folder is the identity and every write is an upsert. Holds `Size`/`Played` (the completion percent), `TierScore` (the score at the completion tier position, which the folder grade reads off) and `AverageScore` (a display number); `Level` carries the player count for co-op. Written by the highlight saga per touched folder and by the admin backfill; read by the tier-list page, the folder-levels widget and the community player profile ([folder-level-progression](design/folder-level-progression.md)) |
| `scores.PlayerSeasonRecap` | Computed season-recap payload per user+mix (PK UserId+MixId): JSON `PlayerRecap` contract + SchemaVersion + ComputedAt. Written whole by the recap saga (admin-triggered), read whole by the recap page; older-schema rows read as "not computed yet" |
| `scores.PlayerHighlight` | The significant-wins ledger every highlights feed reads: **PK is EventId**, so a redelivered bus message or a re-run backfill collides instead of duplicating. `Payload` is a JSON list of `SignificantWin` written and read whole, stamped with SchemaVersion (stale rows are skipped, not rendered). `(UserId, MixId, OccurredAt)` is the feed seek and serves both audiences — a community's member set and a player's rival list — because neither is in the key. Written by `PlayerHighlightSaga` off `ScoreHighlightsCapturedEvent`, purged weekly after 30 days ([rivals §2.4](design/rivals.md)) |

## Rivals (vertical: `ScoreTracker.Rivals`)

| Table | Purpose |
|---|---|
| `scores.Rival` | One directed arrow per row: `OwnerUserId` picked somebody to measure against. Exactly one target is set — `TargetUserId` for a player found on piuscores, `TargetTag` for a board-only player with no account here. Two **filtered** unique indexes (`OwnerUserId`+target, each `WHERE … IS NOT NULL`), because a NULL never equals a NULL and an unfiltered unique would let the same owner store the same tag repeatedly. `(TargetUserId)` serves the reverse list, `(TargetTag)` the link/rename consumers ([rivals §2.1](design/rivals.md)) |
| `scores.RivalBlock` | Symmetric block, PK (UserId, BlockedUserId), stored once from the blocker's side; every check reads both directions, hence the `(BlockedUserId)` index. Blocking also deletes both parties' edges onto each other |
| `scores.RivalInviteCode` | One code per user (PK UserId), unique on `Code`. Only minted for private accounts — a public one has nothing to hand out. Recycling overwrites in place: the old link dies, edges already made with it survive |

## Chart Intelligence (vertical: `ScoreTracker.ChartIntelligence`)

| Table | Purpose |
|---|---|
| `scores.TierListEntry` | Tier list entries per mix (the site's most-used feature) |
| `scores.ChartScoreStats` | Population score variance per chart, refreshed by the daily scores tier-list rebuild (tier-lists overhaul C1) |
| `scores.FolderCohortStats` | Folder pass-count histograms per competitive-level bucket, refreshed by the daily scores tier-list rebuild — powers the "Folder Passes vs Similar Players" bar (tier-lists overhaul C16) |
| `scores.PumbilityTierListEntry` | The PUMBILITY tier lists: how many players in a cohort hold each of a folder's charts in their top-50 PUMBILITY pool, with the log-banded tier that count produced. One row set per folder per cohort (`CohortKey` = a P1 difficulty title level, a P2 PUMBILITY title rung, or `*` for everyone), rewritten nightly per mix ([pumbility-tier-list.md](design/pumbility-tier-list.md)) |
| `scores.PumbilityPoolComposition` | Where PUMBILITY comes from, across every full 50-chart pool on a mix: one row per (mix, band) — the band a merged Singles+Doubles pool total falls in (P2 = a `[P.B]` gem rung, P1 = one of eight total bands) — with the player count, the pool-average level, the summed level/score/plate parts of the D16 decomposition and a 16-grade histogram (`GradeCountsJson`). No `UserId`. Rewritten by the nightly PUMBILITY tier-list sweep and read by `/PumbilityCalculator/{mix}` ([pumbility-calculator.md](design/pumbility-calculator.md)) |
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
| `scores.OfficialLeaderboardPlacement` | One player-place-on-board per snapshot (weekly history); clustered by (Snapshot, Leaderboard, Place, Player). `IsSupplemented` separates the two readings of a snapshot: false = published by piugame, true = rolled up from a linked public player's ledger ([supplemented-leaderboards.md](design/supplemented-leaderboards.md)). Every official read filters it false |
| `scores.OfficialChartPopularity` | Official play-ranking place per chart per snapshot (popularity history) |
| `scores.OfficialBoardRecord` | Record book per chart board: all-time high score (encodes every claimed grade band) |
| `scores.OfficialFolderRecord` | Record book per folder (mix + type + level): all-time high score across the folder's boards |
| `scores.OfficialWeeklyHighlight` | Editorial weekly highlights computed at import (movers, boards climbed, new #1s, grade firsts, plus the This Week hero's playerless summary rows: pulse, gainers, debuts, floor marks); rebuildable from snapshots. `IsSupplemented` marks the second pass, which emits only the diff-based kinds — world firsts and new #1s stay official so the record books never need a twin |
| `scores.OfficialPlayerRenameProposal` | Every tag that left the boards, with the analyzer's verdict and evidence; conclusive ones merge unattended, the rest await admin accept/dismiss. Survives merges as the audit trail |
| `scores.ImportResult` | One row per press of Import / Import and check / Deep scan: `Kind` (Standard·Check·DeepScan), `StartedAt`, and — once something closes the run — `FinishedAt` + `Outcome` (Completed·PiuGameError·PiuScoresError, enum **names** so the table reads in SQL). Both nullable: a row with neither is a run nothing ever closed, which every deploy landing mid-import produces. Never carries exception text. Nullable `SessionId` points at the `ScoreSession` it saved into, when it got that far — deliberately separate, because a session can span hours and several runs while an import is one attempt with one ending. `ScoreCount` is stamped by the run itself at close, **not** read from `ScoreSession.ScoreCount`: that counter is written on the score batch's ~2 minute in-memory drain, so an early look or a restart inside the window leaves it at zero permanently while the journal holds the rows. `Outcome` is `nvarchar(32)` because it stores enum **names** and `CredentialRejected` is 18 characters — at the original 16, closing a rejected-credential run threw a truncation error from inside the consumer's `finally`, leaving the run open and reading to the player as "never reported back" rather than "check your password" (fixed 2026-08-10; a new member longer than the column is the same bug again). `Interrupted` joined the `Outcome` vocabulary with restart recovery: it is the startup pass's verdict on a run nothing ever closed, and `AcknowledgedAt` records that the player was shown the one-time notice about it — per-run by construction, so a second interruption raises it again. Indexed on `SessionId`, which is how the recovery pass finds the run behind an unprocessed session ([import-restart-recovery.md](design/import-restart-recovery.md)) |

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
| `scores.Tournament` | Competitive event definition: configuration, location, visibility, and the Discord channel the randomizer's Push to Discord posts into. MoM rows are legacy — copied onto the `MoM*` tables (march-of-murlocs.md §7) and no longer read |
| `scores.UserTournamentRegistration` | Player registrations |
| `scores.UserTournamentSession` | Legacy MoM session storage (JSON chart blob) — copied onto `MoMSession`/`MoMSessionChart` and no longer read |
| `scores.PhotoVerification` | Photo proofs attached to sessions (verification deleted, D5 — rows kept, never written) |
| `scores.MoMSeason` | A March of Murlocs season; filtered unique (Year, Quarter) is the anti-runaway guarantee (D2); Year/Quarter NULL for off-grid legacy seasons |
| `scores.MoMBoard` | One board of a season — (mix, chart type) with its frozen serialized scoring config; legacy boards keep their legacy tournament Guid |
| `scores.MoMChartLevel` | Season balance snapshot, delta rows only (a missing row means folder level + 0.5) |
| `scores.MoMSession` | A recorded MoM session: derived cache columns over its chart rows; PublishedAt NULL = draft; no unique (Board, User) — boards rank sessions, not players (D16) |
| `scores.MoMSessionChart` | The session's charts, normalized out of the legacy JSON blob; PlayedAt lands with timestamps (Slice 3) |
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
| `scores.CommunityHighlight` | The community **audience index** over `scores.PlayerHighlight`: one row per (score-event × community the winner belongs to), holding no wins of its own. `EventId` dedupes across shared communities and is what the payload is fetched by. It exists because World holds every account, so a World-scoped feed has to stay a seek rather than a join over that member set. Written by `CommunityHighlightIndexSaga` off `PlayerHighlightsStoredEvent`, purged weekly after 30 days. ⚠ `Payload`/`SchemaVersion` are **retained but no longer written** — rows predating the capture's move to PlayerProgress keep theirs because they are the backfill's only source; neither column is read ([rivals D33](design/rivals.md)) |
| `scores.DiscordFeedSubscription` | A channel's subscription to a broadcast feed, independent of any community: `ChannelId`, `FeedKind` (WeeklyCharts/DailyStep/OfficialLeaderboards), `Mix` (per-mix), `RegisteredByDiscordUserId`, `Culture` (nullable, null = English — the language its posts render in; re-registering updates it). Unique on (ChannelId, FeedKind, Mix); registered via `/piu register` ([discord-overhaul](design/discord-overhaul.md)) |

## Community Tools (vertical: `ScoreTracker.CommunityTools`)

| Table | Purpose |
|---|---|
| `scores.Tool` | A registered community tool: `Name`/`Description`/`Url` (what an approved listing shows), `Visibility` (Private/PendingApproval/Public/Rejected, stored as the enum **name** so a reordered enum cannot relabel every row), `AcceptsAllToolsShare`, `WebhookMode` (None/PlayerPing/ScorePush/PiuGameSession), `WebhookUrl`, `OutboundHeaderName`/`OutboundHeaderValue` (the header a maker authenticates us by — **plaintext by necessity**, since we send it verbatim on every delivery; the name says so, because the obvious "fix" for a name implying otherwise would break every delivery), `ApprovedAt`, `RejectionReason`, `WebhookUrlVerifiedAt` (when the maker last echoed our challenge back from `WebhookUrl` — null blocks every delivery, and changing the URL nulls it), `RepositoryUrl`/`RepositoryOwner`/`RepositoryCheckedAt` (the public source players are invited to read, the account it sits under parsed from the first path segment for the admin's eye only, and when the link last answered anonymously — a changed URL nulls the check exactly as it does for the webhook), `DiscordHandle` (how the maker is reached; admin-visible only), `AgreedToRulesAt`. The last five are **nullable and stay that way**: a maker building against their own scores needs none of them, and `Tool.CanBeSharedWithOthers` — not a column constraint — is what stops a tool without them reaching a second player |
| `scores.ToolMixSubscription` | Which mixes' imports trigger a delivery for one tool. Empty = every mix |
| `scores.ToolInviteCode` | A private tool's recruiting link, keyed by the code itself as `CommunityInviteCode` is. `Note` is the maker's own reminder of where they shared it — never shown to the player who follows the link |
| `scores.ToolShare` | A player's grant to one tool: `Source` (Direct/AllTools — what a revoke means differs), `GrantedAt`, `RevokedAt` |
| `scores.ToolBlock` | An all-tools player's "not this one". Without it the only way to refuse a single tool would be to turn off sharing entirely |
| `scores.ToolSharePreference` | One row per player: `ShareWithAllTools`. Seeded from `IsPublic` once at rollout by the `SeedToolSharePreference` migration — a one-time seed, not a rule; public and all-tools stay separate concepts and are never joined in a query |
| `scores.ToolApiKey` | Hashed tool keys (`piu_scores_live_` prefix, SHA-256; the older `pst_live_` prefix still validates). `scores.Tool` carries the two webhook secrets, stored oppositely: `OutboundHeaderValue` is AES-GCM encrypted under a `IKeyEnvelope`-wrapped data key because we resend it, `WebhookVerificationSecretHash` is SHA-256 because we only compare. Two live per tool, six-month default lifetime; `LastUsedAt`, `RevokedAt`. The plaintext exists only in the response that mints it |
| `scores.WebhookDelivery` | One outbound delivery: `QueuedAt`, status, attempt count, next attempt, the remote's status code and a 500-char body snippet, and `Body` — kept **only** for a pending/failed/abandoned delivery and only for 7 days, so a maker can replay. Never written at all in PIUGame-session mode, because that body carries a live piugame credential (`SessionModeNeverPersistsBody`) |
| `scores.ToolMakerBan` | A maker barred from making tools (rule 2's sanction): `UserId` as the key, `BannedAt`, `BannedByUserId`, `Notes` (the owner's own freeform scratch space, editable afterwards, seen by nobody else). Every effect is **computed from this row at read time** rather than written into their tools — shares, keys, listings, activity log and delivery history are left untouched, which is what makes a ban liftable and keeps the evidence a disputed ban is argued over. Deliberately **exempt from account purge**: purging it would mean delete-and-recreate clears a ban, the one outcome it exists to prevent |
| `scores.ToolActivity` | The maker-facing console feed. Point events (delivery succeeded/rejected, key created/revoked) plus hourly rollups for the high-volume ones — at 600 requests a minute a per-call row would put tens of thousands of rows in front of someone who wants one line saying "you hit the limit 212 times this hour" |

## Chart Comments (vertical: `ScoreTracker.ChartComments`)

| Table | Purpose |
|---|---|
| `scores.ChartComment` | One comment, reply or personal note on a chart. `Audience` is the enum **name** (Public/Private/Community) plus `CommunityId` — keyed by id rather than by community name, so a club that renames does not strand its threads. `ParentCommentId` is null on a root and never points at another reply: threads are one level deep. `Text` is **plain text**, ≤500 characters, normalized on the way in (one newline convention, no trailing spaces, no more than one blank line in a row) so the cap counts what is stored and rendered. `SourceLanguage` is filled in by the translation pivot when that lands and is deliberately **left null** until then — stamping the poster's UI culture would record a Korean speaker browsing in English as `en-US` and get their comment rewritten Korean-to-Korean. `DeletedAt`/`DeletedByUserId` are a soft delete; `DeletedByUserId` is the **moderator** rather than the author, which is the second user key on the row. ⚠ **Not in a `UserOwned` purge manifest**: a blanket delete by `UserId` would take a root out from under its replies, so ChartComments purges this table by hand — a root somebody replied to is tombstoned (`UserId` cleared to `Guid.Empty`, `Text` cleared) and everything else goes outright |
| `scores.ChartCommentRevision` | A body an edit replaced, retained so moderation can see what a comment said when it was reported. Carries **no user key** on purpose — the author is on the comment — which is exactly why a purge has to reach these rows by `CommentId`: nothing keyed on a user ever would, and they hold the text the purge exists to remove |
| `scores.ChartCommentVote` | One thumbs-up, unique per (comment, user) — the constraint is the rule, so a double-tap on a slow connection cannot count twice. Never cast on a personal note, and never on your own comment. There is no denormalised count: the Top sort groups these, which at this volume costs nothing and cannot drift |
| `scores.ChartCommentConsent` | One row per player, not per comment: `AgreedToTermsAt` + `TermsVersion` (written on the first **public or community** comment — never on a personal note, because the rules are about how you treat other people) and `ConsentedToPublicIdentityAt` (null until a private-profile player actually posts in public). A real row rather than a UiSettings key because an agreement wants a timestamp and a version, and should be auditable if a dispute lands |

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
| `archive.UserTierListEntry` | 2026-08-13 | Materialized per-user relative tier lists, read only by the similar-players source of personalized Pass. **Transferred empty** — the rows were derived from scores, and a million user-keyed rows outside the purge path would strand personal data ([pumbility-tier-list.md](design/pumbility-tier-list.md)) |

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
