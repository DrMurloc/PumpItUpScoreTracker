using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;

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

    public async Task<Guid> SeedUserAsync(string name, bool isPublic = true,
        CancellationToken cancellationToken = default)
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
        return userId;
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
    public async Task SeedTierListEntryAsync(string tierListName, Guid chartId, string category, int order,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [scores].[TierListEntry] ([Id], [TierListName], [ChartId], [Category], [Order]) VALUES ({Guid.NewGuid()}, {tierListName}, {chartId}, {category}, {order})",
            cancellationToken);
    }
}
