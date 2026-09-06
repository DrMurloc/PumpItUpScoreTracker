using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;

namespace ScoreTracker.Tests.E2E.Support;

/// <summary>
///     Seeds the catalog the snapshot fixtures assume, via raw DbContext (no repos) —
///     same approach as Tests.Integration's TestDataSeeder. The PIU pages served by
///     WireMock name real songs; the import can only map scores onto charts that exist
///     here with the same name, type, and level.
/// </summary>
public sealed class E2ESeedData
{
    // Mirrors ScoreTracker.Data.Persistence.MixIds.Phoenix — MixEnum.Phoenix-typed
    // queries resolve to this exact ID.
    public static readonly Guid PhoenixMixId = Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B");

    // Mirrors ScoreTracker.Data.Persistence.MixIds.XX.
    public static readonly Guid XXMixId = Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8");

    // Mirrors ScoreTracker.Data.Persistence.MixIds.Infinity — a pumpout-era mix, for facts
    // about charts no modern mix carries.
    public static readonly Guid InfinityMixId = Guid.Parse("363B8D21-2DDE-4CE0-A54E-2AEE2B7280A2");

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public E2ESeedData(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>TRICKL4SH 220 Double 20 — 999,231 in BestScores_SinglePage.html.</summary>
    public Guid Tricklash220Double20 { get; private set; }

    /// <summary>Bluish Rose Double 18 — the 1,000,000 in BestScores_SinglePage.html.</summary>
    public Guid BluishRoseDouble18 { get; private set; }

    /// <summary>
    ///     Every chart on the captured best-scores page (BestScores_SinglePage.html) — the
    ///     import can only map scores onto charts seeded with the same name, type, and level.
    ///     Recapturing the fixture means re-deriving this list from the new page. Call after
    ///     ResetDatabaseAsync in any test that logs in or imports through the WireMock site.
    /// </summary>
    public async Task SeedSnapshotCatalogAsync(CancellationToken cancellationToken = default)
    {
        await SeedPhoenixChartAsync("Full Moon - FULL SONG -", 20, "Single", cancellationToken);
        await SeedPhoenixChartAsync("Demon of Laplace", 20, "Double", cancellationToken);
        await SeedPhoenixChartAsync("DUEL", 18, "Double", cancellationToken);
        await SeedPhoenixChartAsync("See", 18, "Double", cancellationToken);
        Tricklash220Double20 = await SeedPhoenixChartAsync("TRICKL4SH 220", 20, "Double", cancellationToken);
        await SeedPhoenixChartAsync("Appassionata", 21, "Double", cancellationToken);
        await SeedPhoenixChartAsync("GOODBOUNCE", 18, "Double", cancellationToken);
        await SeedPhoenixChartAsync("Crimson hood", 18, "Double", cancellationToken);
        await SeedPhoenixChartAsync("Curiosity Overdrive", 20, "Single", cancellationToken);
        BluishRoseDouble18 = await SeedPhoenixChartAsync("Bluish Rose", 18, "Double", cancellationToken);
        await SeedPhoenixChartAsync("Rush-More", 23, "Double", cancellationToken);
        await SeedPhoenixChartAsync("8 6 - FULL SONG -", 23, "Double", cancellationToken);
    }

    public async Task EnsurePhoenixMixAsync(CancellationToken cancellationToken = default)
    {
        await EnsureMixAsync(PhoenixMixId, "Phoenix", cancellationToken);
    }

    private async Task EnsureMixAsync(Guid mixId, string name, CancellationToken cancellationToken)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        if (await context.Mix.AnyAsync(m => m.Id == mixId, cancellationToken)) return;
        context.Mix.Add(new MixEntity { Id = mixId, Name = name });
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>An XX-only chart — for facts that pin which mix's catalog a page renders.</summary>
    public async Task<Guid> SeedXXChartAsync(string songName, int level, string type,
        CancellationToken cancellationToken = default)
    {
        await EnsureMixAsync(XXMixId, "XX", cancellationToken);
        return await SeedChartAsync(XXMixId, songName, level, type, cancellationToken);
    }

    /// <summary>A chart in one legacy mix and nowhere else — the pumpout-era catalog's shape.</summary>
    public async Task<Guid> SeedLegacyChartAsync(Guid mixId, string mixName, string songName, int level,
        string type, CancellationToken cancellationToken = default)
    {
        await EnsureMixAsync(mixId, mixName, cancellationToken);
        return await SeedChartAsync(mixId, songName, level, type, cancellationToken);
    }

    public async Task<Guid> SeedPhoenixChartAsync(string songName, int level, string type,
        CancellationToken cancellationToken = default)
    {
        await EnsurePhoenixMixAsync(cancellationToken);
        return await SeedChartAsync(PhoenixMixId, songName, level, type, cancellationToken);
    }

    private async Task<Guid> SeedChartAsync(Guid mixId, string songName, int level, string type,
        CancellationToken cancellationToken = default)
    {
        var chartId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        context.Song.Add(new SongEntity
        {
            Id = songId,
            Name = songName,
            ImagePath = $"https://e2e-files.invalid/songs/{songId:N}.png",
            Type = "Arcade"
        });
        context.Chart.Add(new ChartEntity
        {
            Id = chartId,
            SongId = songId,
            OriginalMixId = mixId,
            Level = level,
            Type = type
        });
        context.ChartMix.Add(new ChartMixEntity
        {
            Id = Guid.NewGuid(),
            ChartId = chartId,
            MixId = mixId,
            Level = level
        });
        await context.SaveChangesAsync(cancellationToken);
        return chartId;
    }

    /// <summary>
    ///     A signed-in player who has already seen the one-time notices.
    ///     <para>
    ///         <paramref name="seenAnnouncements" /> exists because a seeded user is brand new, and a
    ///         brand-new user is exactly who a rollout announcement fires at. Those are modal, so
    ///         their scrim sits over whatever page the test just opened and swallows every click —
    ///         which surfaces as a thirty-second timeout waiting for a button that is right there.
    ///         Every test but the announcement's own wants a returning player.
    ///     </para>
    /// </summary>
    public async Task<Guid> SeedUserAsync(string name, bool isPublic = true,
        bool seenAnnouncements = true, CancellationToken cancellationToken = default)
    {
        var userId = Guid.NewGuid();
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        context.User.Add(new UserEntity
        {
            Id = userId,
            Name = name,
            IsPublic = isPublic,
            ProfileImage = "https://e2e-files.invalid/avatar.png",
            IsContentLocked = false,
            ClaimsInvalidatedAt = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
        await context.SaveChangesAsync(cancellationToken);

        if (seenAnnouncements)
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [scores].[UserSettings] ([UserId], [UiSettings]) VALUES ({userId}, {SeenAnnouncements})",
                cancellationToken);

        return userId;
    }

    /// <summary>
    ///     The UiSettings blob is one JSON object of string to string per user. Adding a notice
    ///     means adding its key here, or every E2E test starts timing out on a covered page.
    /// </summary>
    private const string SeenAnnouncements = """{"CommunityToolsAnnouncementSeen":"true"}""";

    /// <summary>
    ///     The language a player chose on /Account, which outranks their browser on every
    ///     request. Rewrites the whole blob, so it keeps the announcement key alongside.
    /// </summary>
    public async Task SeedCultureAsync(Guid userId, string culture, CancellationToken cancellationToken = default)
    {
        var settings = $$"""{"CommunityToolsAnnouncementSeen":"true","Culture":"{{culture}}"}""";
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [scores].[UserSettings] SET [UiSettings] = {settings} WHERE [UserId] = {userId}",
            cancellationToken);
    }

    /// <summary>
    ///     What choosing Automatic leaves behind: no Culture key at all. Absence is how "follow
    ///     the browser" is stored, so this is the state, not a reset.
    /// </summary>
    public async Task ClearCultureAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [scores].[UserSettings] SET [UiSettings] = {SeenAnnouncements} WHERE [UserId] = {userId}",
            cancellationToken);
    }

    /// <summary>A Phoenix best-score row (ScoreLedger-internal entity) — seeded with SQL.</summary>
    public async Task SeedPhoenixScoreAsync(Guid userId, Guid chartId, int score, bool isBroken = false,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[PhoenixRecord] ([Id], [UserId], [ChartId], [MixId], [RecordedDate], [Score], [IsBroken]) VALUES ({Guid.NewGuid()}, {userId}, {chartId}, {PhoenixMixId}, {new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)}, {score}, {isBroken})",
            cancellationToken);
    }

    /// <summary>Puts a chart on the live weekly board (WeeklyChallenge-internal table — SQL, per the house rule).</summary>
    public async Task SeedWeeklyChartAsync(Guid chartId, DateTimeOffset expiration,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[WeeklyTournamentChart] ([ChartId], [MixId], [ExpirationDate]) VALUES ({chartId}, {PhoenixMixId}, {expiration})",
            cancellationToken);
    }

    /// <summary>An entry on the live weekly board. <paramref name="source" />: 0 = Official, 1 = Manual.</summary>
    public async Task SeedWeeklyEntryAsync(Guid userId, Guid chartId, int score, string plate = "SuperbGame",
        int source = 0, double competitiveLevel = 18.0, CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[WeeklyUserEntry] ([UserId], [ChartId], [MixId], [Score], [Plate], [IsBroken], [WasWithinRange], [CompetitiveLevel], [Photo], [Source]) VALUES ({userId}, {chartId}, {PhoenixMixId}, {score}, {plate}, {false}, {true}, {competitiveLevel}, {null}, {source})",
            cancellationToken);
    }

    /// <summary>A tournament row (EventCompetition-internal table — SQL, per the house rule).</summary>
    public async Task<Guid> SeedTournamentAsync(string name, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[Tournament] ([Id], [Name], [Configuration], [Type], [Location], [IsHighlighted], [IsMoM], [IsUnlisted]) VALUES ({id}, {name}, {"{}"}, {"Match"}, {"Remote"}, {true}, {false}, {false})",
            cancellationToken);
        return id;
    }

    /// <summary>The qualifiers configuration a tournament's board reads from.</summary>
    public async Task SeedQualifiersConfigurationAsync(Guid tournamentId, IEnumerable<Guid> chartIds,
        int playCount = 2, DateTimeOffset? cutoff = null, CancellationToken cancellationToken = default)
    {
        var charts = string.Join(",", chartIds);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[QualifiersConfiguration] ([TournamentId], [MixId], [ScoringType], [Charts], [AllCharts], [NotificationChannel], [ChartPlayCount], [CutoffTime]) VALUES ({tournamentId}, {PhoenixMixId}, {"Score"}, {charts}, {false}, {0L}, {playCount}, {cutoff})",
            cancellationToken);
    }

    /// <summary>
    ///     An entry on a qualifiers board. Entries is the submission blob; passing a photo URL
    ///     marks it a manual submission, passing none marks it an official import.
    /// </summary>
    public async Task SeedQualifierEntryAsync(Guid tournamentId, string name, Guid chartId, int score,
        Guid? userId = null, string? photoUrl = null, CancellationToken cancellationToken = default)
    {
        var photo = photoUrl == null ? "null" : $"\"{photoUrl}\"";
        var source = photoUrl == null ? 1 : 0;
        var entries =
            $"[{{\"ChartId\":\"{chartId}\",\"Score\":{score},\"PhotoUrl\":{photo},\"Source\":{source}}}]";
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[UserQualifier] ([Id], [TournamentId], [Name], [Entries], [IsApproved], [UserId]) VALUES ({Guid.NewGuid()}, {tournamentId}, {name}, {entries}, {false}, {userId})",
            cancellationToken);
    }

    /// <summary>Journal, highlight, and milestone rows belong to vertical-internal entities — seeded with SQL.</summary>
    public async Task SeedJournalRowAsync(Guid userId, Guid chartId, DateTimeOffset occurredAt, int? score,
        string? plate, bool isBroken, Guid? sessionId, string source = "manual",
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[ScoreEventJournal] ([Id], [EventId], [OccurredAt], [Source], [MixId], [UserId], [ChartId], [Score], [Plate], [IsBroken], [SessionId]) VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {occurredAt}, {source}, {PhoenixMixId}, {userId}, {chartId}, {score}, {plate}, {isBroken}, {sessionId})",
            cancellationToken);
    }

    public async Task SeedHighlightAsync(Guid userId, Guid chartId, Guid? sessionId, DateTimeOffset occurredAt,
        int flags, int level, int? pumbilityRank = null, CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[ScoreHighlight] ([Id], [UserId], [MixId], [ChartId], [SessionId], [OccurredAt], [Flags], [Level], [ScoringLevel], [PumbilityRank]) VALUES ({Guid.NewGuid()}, {userId}, {PhoenixMixId}, {chartId}, {sessionId}, {occurredAt}, {flags}, {level}, {null}, {pumbilityRank})",
            cancellationToken);
    }

    public async Task SeedMilestoneAsync(Guid userId, Guid? sessionId, DateTimeOffset occurredAt, string kind,
        double? oldValue = null, double? newValue = null, string? title = null, string? detail = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[PlayerMilestone] ([Id], [UserId], [MixId], [SessionId], [OccurredAt], [Kind], [OldValue], [NewValue], [Title], [Detail]) VALUES ({Guid.NewGuid()}, {userId}, {PhoenixMixId}, {sessionId}, {occurredAt}, {kind}, {oldValue}, {newValue}, {title}, {detail})",
            cancellationToken);
    }

    /// <summary>ChartVideo belongs to the Catalog vertical (internal entity) — seeded with SQL.</summary>
    public async Task SeedChartVideoAsync(Guid chartId, string videoUrl,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[ChartVideo] ([ChartId], [VideoUrl], [ChannelName], [LastUpdated]) VALUES ({chartId}, {videoUrl}, {"e2e"}, {DateTimeOffset.UtcNow})",
            cancellationToken);
    }

    /// <summary>
    ///     TierListEntry belongs to the ChartIntelligence vertical (internal entity), so it
    ///     is seeded with SQL rather than an entity type. TierListName is one of the four
    ///     lists the /TierLists page loads ("Pass Count", "Scores", "Official Scores",
    ///     "Popularity"); category is a TierListCategory enum name.
    /// </summary>
    /// <summary>
    ///     One sealed official-leaderboard snapshot with a chart board, two players, their
    ///     placements, one PG world-first highlight, and a popularity row — the smallest
    ///     dataset that lights up every hub view. Returns the top player's username.
    /// </summary>
    public async Task<string> SeedOfficialSnapshotAsync(Guid chartId, string chartBoardName,
        DateTimeOffset sealedAt, CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var snapshot = new OfficialLeaderboardSnapshotEntity
        {
            MixId = PhoenixMixId,
            StartedAt = sealedAt.AddMinutes(-40),
            CompletedAt = sealedAt,
            Stage = "Sealed",
            BoardsExpected = 1,
            BoardsWritten = 1
        };
        context.Set<OfficialLeaderboardSnapshotEntity>().Add(snapshot);
        var board = new OfficialLeaderboardEntity
        {
            MixId = PhoenixMixId,
            LeaderboardType = "Chart",
            Name = chartBoardName,
            ChartId = chartId,
            ChartType = "Double",
            Level = 26
        };
        // The rankings read the mirrored PUMBILITY board and nothing else — a snapshot
        // without one ranks nobody by design, so the seed has to carry it.
        var pumbility = new OfficialLeaderboardEntity
        {
            MixId = PhoenixMixId,
            LeaderboardType = "Rating",
            Name = "PUMBILITY"
        };
        context.Set<OfficialLeaderboardEntity>().AddRange(board, pumbility);
        var champion = new OfficialPlayerEntity
            { MixId = PhoenixMixId, Username = "E2ECHAMP", LastSeenAt = sealedAt };
        var runnerUp = new OfficialPlayerEntity
            { MixId = PhoenixMixId, Username = "E2ERUNNER", LastSeenAt = sealedAt };
        context.Set<OfficialPlayerEntity>().AddRange(champion, runnerUp);
        await context.SaveChangesAsync(cancellationToken);

        context.Set<OfficialLeaderboardPlacementEntity>().AddRange(
            new OfficialLeaderboardPlacementEntity
            {
                SnapshotId = snapshot.Id, LeaderboardId = board.Id, PlayerId = champion.Id, Place = 1,
                Score = 1_000_000
            },
            new OfficialLeaderboardPlacementEntity
            {
                SnapshotId = snapshot.Id, LeaderboardId = board.Id, PlayerId = runnerUp.Id, Place = 2,
                Score = 995_000
            },
            new OfficialLeaderboardPlacementEntity
            {
                SnapshotId = snapshot.Id, LeaderboardId = pumbility.Id, PlayerId = champion.Id, Place = 1,
                Score = 1_040.25m
            },
            new OfficialLeaderboardPlacementEntity
            {
                SnapshotId = snapshot.Id, LeaderboardId = pumbility.Id, PlayerId = runnerUp.Id, Place = 2,
                Score = 1_012.80m
            });
        context.Set<OfficialWeeklyHighlightEntity>().Add(new OfficialWeeklyHighlightEntity
        {
            SnapshotId = snapshot.Id,
            MixId = PhoenixMixId,
            Kind = "ChartGradeFirst",
            SortOrder = 1,
            PlayerId = champion.Id,
            LeaderboardId = board.Id,
            ChartId = chartId,
            ChartType = "Double",
            Level = 26,
            GradeBand = "PG",
            Score = 1_000_000,
            PrevValue = 998_000
        });
        context.Set<OfficialChartPopularityEntity>().Add(new OfficialChartPopularityEntity
        {
            SnapshotId = snapshot.Id, ChartId = chartId, Place = 1
        });
        await context.SaveChangesAsync(cancellationToken);
        return champion.Username;
    }

    public async Task SeedTierListEntryAsync(string tierListName, Guid chartId, string category, int order,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[TierListEntry] ([Id], [TierListName], [ChartId], [Category], [Order]) VALUES ({Guid.NewGuid()}, {tierListName}, {chartId}, {category}, {order})",
            cancellationToken);
    }

    /// <summary>
    ///     A live March of Murlocs season with one Phoenix Doubles board (EventCompetition-internal
    ///     tables — SQL, per the house rule). The frozen configuration is the minimal JSON the board
    ///     reader accepts; the read surfaces print stored figures and never re-score, so no table needs
    ///     to be complete.
    /// </summary>
    public async Task<(Guid SeasonId, Guid BoardId)> SeedMoMSeasonAsync(string name, DateTimeOffset startsAt,
        DateTimeOffset endsAt, CancellationToken cancellationToken = default)
    {
        var seasonId = Guid.NewGuid();
        await using (var context = await _factory.CreateDbContextAsync(cancellationToken))
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [scores].[MoMSeason] ([Id], [Name], [StartsAt], [EndsAt], [CreatedAt]) VALUES ({seasonId}, {name}, {startsAt}, {endsAt}, {startsAt})",
                cancellationToken);
        }

        var boardId = await SeedMoMBoardAsync(seasonId, name, PhoenixMixId,
            cancellationToken: cancellationToken);
        return (seasonId, boardId);
    }

    /// <summary>
    ///     One board on an existing season — a mix's Doubles board unless told otherwise — with the
    ///     minimal frozen configuration the read side deserializes (MoM-internal table — SQL, per the
    ///     house rule). Chart type 1 is Double, 0 Single.
    /// </summary>
    /// <param name="levelRatings">
    ///     Level to rating, for a board a session can actually be recorded onto: a chart with no
    ///     rating prices to zero, and a chart that prices to zero cannot enter a session at all.
    /// </param>
    public async Task<Guid> SeedMoMBoardAsync(Guid seasonId, string seasonName, Guid mixId, byte chartType = 1,
        IReadOnlyDictionary<int, int>? levelRatings = null, CancellationToken cancellationToken = default)
    {
        var boardId = Guid.NewGuid();
        var typeName = chartType == 1 ? "Doubles" : "Singles";
        var ratings = levelRatings == null
            ? "{}"
            : "{" + string.Join(",", levelRatings.Select(r => $"\"{r.Key}\":{r.Value}")) + "}";
        var config = "{\"Id\":\"" + boardId + "\",\"Name\":\"March of Murlocs " + seasonName + " - " + typeName + "\"," +
                     "\"LevelRatings\":" + ratings + ",\"SongTypeModifiers\":{},\"ChartTypeModifiers\":{},\"LetterGradeModifiers\":{}," +
                     "\"PlateModifiers\":{},\"ChartModifiers\":{},\"CalculationType\":0,\"PgModifier\":1.6,\"MinimumScore\":0," +
                     "\"CustomFormula\":\"\",\"StageBreakModifier\":1.0,\"AdjustToTime\":true,\"ContinuousLetterGradeScale\":true," +
                     "\"MaxTime\":\"01:45:00\",\"AllowRepeats\":false}";
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[MoMBoard] ([Id], [SeasonId], [MixId], [ChartType], [ScoringConfig]) VALUES ({boardId}, {seasonId}, {mixId}, {chartType}, {config})",
            cancellationToken);
        return boardId;
    }

    /// <summary>A published session on a MoM board, with one chart row and the derived columns the board prints.</summary>
    public async Task<Guid> SeedMoMSessionAsync(Guid boardId, Guid userId, Guid chartId, int score, int points,
        DateTimeOffset publishedAt, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[MoMSession] ([Id], [BoardId], [UserId], [PublishedAt], [TotalScore], [ChartsPlayed], [RestTime], [AverageDifficulty], [AverageGrade], [LowestLevel], [HighestLevel], [CreatedAt], [UpdatedAt]) VALUES ({id}, {boardId}, {userId}, {publishedAt}, {points}, 1, {TimeSpan.FromMinutes(103).Ticks}, 24.5, 8, 24, 24, {publishedAt}, {publishedAt})",
            cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[MoMSessionChart] ([SessionId], [Ordinal], [ChartId], [Score], [Plate], [IsBroken], [SessionScore], [BonusPoints], [PlayedAt]) VALUES ({id}, 0, {chartId}, {score}, {"FairGame"}, 0, {points}, 0, NULL)",
            cancellationToken);
        return id;
    }
}
