# Database Schema

One SQL Server database, one EF Core `DbContext` ([`ChartAttemptDbContext`](../ScoreTracker/ScoreTracker.Data/Persistence/ChartAttemptDbContext.cs)). Almost everything lives in the `scores` schema. Migrations live in [`ScoreTracker.Data/Migrations/`](../ScoreTracker/ScoreTracker.Data/Migrations/) and are applied by a self-contained EF bundle during the gated production deploy (locally, the Aspire AppHost auto-migrates at startup).

**Table ownership follows the verticals** (see [ARCHITECTURE.md](ARCHITECTURE.md)): a vertical owns its EF entities as `internal` classes and registers them with the shared context via an `IDbModelContribution` in its `Wiring/` namespace. Cross-vertical reads go through published ports and contracts — never SQL joins onto another vertical's tables. Tables not yet extracted to a vertical live in `ScoreTracker.Data` directly.

## Game content (shared, read by everything)

| Table | Purpose |
|---|---|
| `scores.Mix` | Game version/mix definition (XX, Phoenix, …) |
| `scores.Song` | Song metadata: artist, name, BPM, duration, image |
| `scores.Chart` | A playable chart: song, type (Single/Double/CoOp), level, step artist |
| `scores.ChartMix` | Chart↔mix mapping with the level and note count for that mix |
| `scores.Country` | Country list with flag image path |

## Identity & accounts (shared; logically the Identity vertical, physical extraction pending)

| Table | Purpose |
|---|---|
| `scores.User` | User account: name, profile image, game tag, country, content-lock status |
| `scores.ExternalLogin` | Sign-in method mappings to users, many-to-one (Discord/Google/Facebook OAuth ids; namespaced PiuGame aliases `mbid:*`/`card:*`) |
| `scores.UserApiToken` | API tokens for the partner API, with usage tracking |
| `scores.UserSettings` | Per-user UI settings key/value store |
| `scores.MergeRequest` | Durable account-merge record: survivor/retired users, moved logins + retired-user snapshot (JSON, for undo), state, and the grace-window purge schedule (Identity-vertical entity) |

## Score Ledger (vertical: `ScoreTracker.ScoreLedger`)

| Table | Purpose |
|---|---|
| `scores.PhoenixRecord` | Best-known Phoenix-scoring attempt per user+chart+mix: score, plate, broken flag (unique on UserId+ChartId+MixId; pre-Phoenix-2 rows backfilled as Phoenix — as are all MixId columns below) |
| `scores.BestAttempt` | XX-era best attempts per user+chart |
| `scores.PhoenixRecordStats` | Per-score Pumbility stats per user+chart+mix, written by PlayerProgress through a Ledger port |
| `scores.ScoreEventJournal` | **Append-only** journal of score submissions *as received* (manual, import, CSV, …), including submissions that don't beat the stored best. Rows are never updated or deleted. Seeded 2026-06 from `PhoenixRecord` (`Source='backfill'`); the foundation of score-progression history |

## Player Progress (vertical: `ScoreTracker.PlayerProgress`)

| Table | Purpose |
|---|---|
| `scores.PlayerStats` | Aggregated player stats per mix (PK UserId+MixId): ratings, competitive levels, clear counts |
| `scores.PlayerHistory` | Point-in-time snapshots of player stats, per mix |
| `scores.UserTitle` | Titles earned per mix, with paragon progression |
| `scores.UserHighestTitle` | Denormalized current-highest title per mix (PK UserId+MixId) for fast reads |
| `scores.SuggestionFeedback` | User feedback on chart recommendations |

## Chart Intelligence (vertical: `ScoreTracker.ChartIntelligence`)

| Table | Purpose |
|---|---|
| `scores.TierListEntry` | Tier list entries per mix (the site's most-used feature) |
| `scores.ChartScoringLevel` | Calculated scoring-difficulty level per chart+mix |
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
| `scores.ChartSkill` | Skill tags on charts (drills, gimmicks, …) with highlight flags |
| `scores.SongNameLanguage` | Localized song names per culture |
| `scores.UserRandomSettings` | Saved randomizer presets (JSON) |
| `scores.SavedChart` | User bookmark lists of charts *(ownership split pending — currently shared)* |

## Official Game Mirror (vertical: `ScoreTracker.OfficialMirror`)

| Table | Purpose |
|---|---|
| `scores.UserOfficialLeaderboard` | Mirrored official leaderboard placements |
| `scores.UserWorldRanking` | Calculated world-ranking stats (singles/doubles competitive, average level) |
| `scores.OfficialUserAvatar` | Cached official avatar URLs |
| `scores.OfficialLeaderboardImportState` | Timestamp of the last official leaderboard import |

## Weekly Challenge (vertical: `ScoreTracker.WeeklyChallenge`)

| Table | Purpose |
|---|---|
| `scores.WeeklyTournamentChart` | The active weekly chart set, with expiration |
| `scores.WeeklyUserEntry` | Player entries: score, plate, verification |
| `scores.UserWeeklyPlacing` | Historical placements from finished weeks |
| `scores.PastTourneyCharts` | Archive of previously used weekly charts (avoids repeats) |

## Event Competition (vertical: `ScoreTracker.EventCompetition`)

| Table | Purpose |
|---|---|
| `scores.Tournament` | Competitive event definition: configuration, location, visibility |
| `scores.UserTournamentRegistration` | Player registrations |
| `scores.UserTournamentSession` | A player's session: charts played, scores, approval state |
| `scores.PhotoVerification` | Photo proofs attached to sessions |
| `scores.TournamentChartLevel` | Per-tournament chart level overrides |
| `scores.TournamentRole` | Per-tournament roles (organizer, judge, …) |
| `scores.QualifiersConfiguration` | Qualifier stage setup: charts, scoring, cutoff |
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
