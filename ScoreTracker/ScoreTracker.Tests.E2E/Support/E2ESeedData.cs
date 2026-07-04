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

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public E2ESeedData(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>TRICKL4SH 220 Double 20 — 999,231 in BestScores_SinglePage.html.</summary>
    public Guid Tricklash220Double20 { get; private set; }

    /// <summary>Conflict Single 15 — 850,000 in BestScores_SinglePage.html.</summary>
    public Guid ConflictSingle15 { get; private set; }

    /// <summary>Appassionata Double 21 — appears in RecentlyPlayed.html.</summary>
    public Guid AppassionataDouble21 { get; private set; }

    /// <summary>
    ///     The charts every PiuGame snapshot page references. Call after ResetDatabaseAsync
    ///     in any test that logs in or imports through the WireMock site.
    /// </summary>
    public async Task SeedSnapshotCatalogAsync(CancellationToken cancellationToken = default)
    {
        Tricklash220Double20 = await SeedPhoenixChartAsync("TRICKL4SH 220", 20, "Double", cancellationToken);
        ConflictSingle15 = await SeedPhoenixChartAsync("Conflict", 15, "Single", cancellationToken);
        AppassionataDouble21 = await SeedPhoenixChartAsync("Appassionata", 21, "Double", cancellationToken);
    }

    public async Task EnsurePhoenixMixAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        if (await context.Mix.AnyAsync(m => m.Id == PhoenixMixId, cancellationToken)) return;
        context.Mix.Add(new MixEntity { Id = PhoenixMixId, Name = "Phoenix" });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> SeedPhoenixChartAsync(string songName, int level, string type,
        CancellationToken cancellationToken = default)
    {
        await EnsurePhoenixMixAsync(cancellationToken);
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
            OriginalMixId = PhoenixMixId,
            Level = level,
            Type = type
        });
        context.ChartMix.Add(new ChartMixEntity
        {
            Id = Guid.NewGuid(),
            ChartId = chartId,
            MixId = PhoenixMixId,
            Level = level
        });
        await context.SaveChangesAsync(cancellationToken);
        return chartId;
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
