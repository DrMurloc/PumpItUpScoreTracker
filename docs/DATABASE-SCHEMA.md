# Database Schema

One SQL Server database, one EF Core `DbContext` ([`ChartAttemptDbContext`](../ScoreTracker/ScoreTracker.Data/Persistence/ChartAttemptDbContext.cs)). Almost everything lives in the `scores` schema. Migrations live in [`ScoreTracker.Data/Migrations/`](../ScoreTracker/ScoreTracker.Data/Migrations/) and are applied by a self-contained EF bundle during the gated production deploy (locally, the Aspire AppHost auto-migrates at startup).

**Table ownership follows the verticals** (see [ARCHITECTURE.md](ARCHITECTURE.md)): a vertical owns its EF entities as `internal` classes and registers them with the shared context via an `IDbModelContribution` in its `Wiring/` namespace. Cross-vertical reads go through published ports and contracts — never SQL joins onto another vertical's tables. Tables not yet extracted to a vertical live in `ScoreTracker.Data` directly.

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
| `scores.UserImportCredentialKey` | Per-device wrapped data-key for a remembered PIUGAME credential (envelope encryption): `KeyId`, `UserId`, the Key-Vault-wrapped DEK, `CreatedAt`. **Holds no password** — the AES-GCM ciphertext lives in the user's browser local storage and the master key never leaves Key Vault; deleting rows revokes ([remember-my-password design](design/import-widget-remember-password.md)) |

## Score Ledger (vertical: `ScoreTracker.ScoreLedger`)

| Table | Purpose |
|---|---|
| `scores.PhoenixRecord` | Best-known Phoenix-scoring attempt per user+chart+mix: score, plate, broken flag, and the `Source` of the current best (verified ⇔ `officialImport`; NULL predates capture). Unique on UserId+ChartId+MixId; pre-Phoenix-2 rows backfilled as Phoenix — as are all MixId columns below |
| `scores.BestAttempt` | Legacy-model best attempts (letter grade + broken + optional era-scale score) per user+chart+mix; `MixId` defaults to XX — the table's original implicit scope — and every pre-Phoenix mix records here ([legacy-mixes design](design/legacy-mixes.md)) |
| `scores.PhoenixRecordStats` | Per-score Pumbility stats per user+chart+mix, written by PlayerProgress through a Ledger port |
| `scores.ScoreEventJournal` | **Append-only** journal of best-attempt *changes* (progress only since 2026-07: first entries incl. broken, unbreaks, score/plate improvements, manual corrections — no-ops are never written; rows from before the guard include them). Rows are never updated or deleted. `SessionId` groups rows into play sessions / import runs (NULL predates capture). Seeded 2026-06 from `PhoenixRecord` (`Source='backfill'`, dated at the record's last update); the foundation of score-progression history |

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
| `scores.UserOfficialLeaderboard` | Mirrored official leaderboard placements, per mix |
| `scores.UserWorldRanking` | Calculated world-ranking stats per mix (singles/doubles competitive, average level) |
| `scores.OfficialUserAvatar` | Cached official avatar URLs |
| `scores.OfficialLeaderboardImportState` | Timestamp of the last official leaderboard import, one row per mix (PK MixId) |

## Weekly Challenge (vertical: `ScoreTracker.WeeklyChallenge`)

| Table | Purpose |
|---|---|
| `scores.WeeklyTournamentChart` | The active weekly chart set per mix, with expiration |
| `scores.WeeklyUserEntry` | Player entries per mix: score, plate, optional photo proof, trust source (official import vs manual self-report) |
| `scores.UserWeeklyPlacing` | Historical placements from finished weeks, per mix |
| `scores.PastTourneyCharts` | Archive of previously used weekly charts per mix (avoids repeats; PK ChartId+MixId) |
| `scores.DailyStepChart` | The one live Daily Step chart per mix (0–1 rows): ChartId, ForDate, IsLimbo, ExpirationDate. Redrawn each midnight-ET rotation |
| `scores.DailyStepEntry` | Player entries on today's Daily Step chart per mix (score, plate, competitive level, source: official import vs manual widget submission); cleared at rotation |
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

## UCS (vertical: `ScoreTracker.Ucs`)

| Table | Purpose |
|---|---|
| `scores.UcsChart` | User-created step chart metadata (PIU game id, level, type, uploader) |
| `scores.UcsChartLeaderboardEntry` | UCS leaderboard entries with score, plate, video proof |
| `scores.UcsChartTag` | User tags on UCS charts for discovery |

## Communities (vertical: `ScoreTracker.Communities`)

| Table | Purpose |
|---|---|
| `scores.Community` | Communities with privacy type and regional flag |
| `scores.CommunityMembership` | Community membership |
| `scores.CommunityInviteCode` | Invite codes, optionally expiring |
| `scores.CommunityChannel` | Discord channels wired to a community's event feed |
| `scores.CommunityHighlight` | Community big-wins feed: one summary row per (score-event × community the winner belongs to), `Payload` a JSON list of `SignificantWin`, `EventId` dedupes across shared communities. Written by the highlight saga off `ScoreHighlightsCapturedEvent`, purged weekly after 30 days ([home-page-widgets §7](design/home-page-widgets.md)) |

## Match subsystem (shared; deprecated, deletion gated on an owner announcement)

| Table | Purpose |
|---|---|
| `scores.Match` | Bracket match definition (JSON configuration) |
| `scores.MatchLink` | Winner/loser routing between matches |
| `scores.RandomSettings` | Named randomizer configurations for matches |
| `scores.TournamentPlayer` | Bracket participants with seeds |
| `scores.TournamentMachine` | Machine assignments for brackets |

## System tables

| Table | Purpose |
|---|---|
| `dbo.__EFMigrationsHistory` | EF Core's applied-migrations ledger |
| `HangFire.*` | Hangfire's job storage — **auto-created by Hangfire, not EF-managed**. Never add EF entities for these; recurring schedules live here and survive restarts |
