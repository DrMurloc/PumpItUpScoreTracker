using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;

namespace ScoreTracker.Tests.Integration.TestData;

/// <summary>
/// Seeds the minimal reference data needed to satisfy FK constraints on dependent tables.
/// Inserts via raw DbContext (no repos), keeping seed code independent of the system under test.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestDataSeeder
{
    // Mirrors ScoreTracker.Data.Persistence.MixIds.Phoenix — tests that go through
    // `MixEnum.Phoenix`-typed queries must use this exact ID.
    public static readonly Guid PhoenixMixId = Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B");

    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public TestDataSeeder(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<Guid> SeedUserAsync(string? name = null, bool isPublic = true,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.NewGuid();
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        ctx.User.Add(new UserEntity
        {
            Id = userId,
            Name = name ?? $"u_{userId:N}",
            IsPublic = isPublic,
            ProfileImage = "https://example.invalid/avatar.png",
            IsContentLocked = false,
            ClaimsInvalidatedAt = Epoch
        });
        await ctx.SaveChangesAsync(cancellationToken);
        return userId;
    }

    public async Task<Guid> SeedChartAsync(int level = 15, string type = "Single",
        CancellationToken cancellationToken = default)
    {
        return await InsertChartAsync(level, type, addToPhoenixMix: false, cancellationToken);
    }

    public async Task<Guid> SeedPhoenixChartAsync(int level = 15, string type = "Single",
        CancellationToken cancellationToken = default)
    {
        await EnsurePhoenixMixAsync(cancellationToken);
        return await InsertChartAsync(level, type, addToPhoenixMix: true, cancellationToken);
    }

    public async Task EnsurePhoenixMixAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        if (await ctx.Mix.AnyAsync(m => m.Id == PhoenixMixId, cancellationToken)) return;
        ctx.Mix.Add(new MixEntity { Id = PhoenixMixId, Name = "Phoenix" });
        await ctx.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     A MoM season + Phoenix Doubles board to hang session rows on. Raw SQL because the
    ///     MoM entities are internal to EventCompetition — this seeder's whole point is staying
    ///     independent of the code under test.
    /// </summary>
    public async Task<Guid> SeedMoMBoardAsync(CancellationToken cancellationToken = default)
    {
        var seasonId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO scores.MoMSeason (Id, Name, StartsAt, EndsAt, CreatedAt) " +
            "VALUES ({0}, {1}, {2}, {3}, {2}); " +
            "INSERT INTO scores.MoMBoard (Id, SeasonId, MixId, ChartType, ScoringConfig) " +
            "VALUES ({4}, {0}, {5}, 1, '{{}}');",
            new object[] { seasonId, $"season_{seasonId:N}"[..32], Epoch, Epoch.AddMonths(3), boardId, PhoenixMixId },
            cancellationToken);
        return boardId;
    }

    private async Task<Guid> InsertChartAsync(int level, string type, bool addToPhoenixMix,
        CancellationToken cancellationToken)
    {
        var chartId = Guid.NewGuid();
        var songId = Guid.NewGuid();
        var originalMixId = addToPhoenixMix ? PhoenixMixId : Guid.NewGuid();

        await using var ctx = await _factory.CreateDbContextAsync(cancellationToken);
        if (!addToPhoenixMix)
        {
            ctx.Mix.Add(new MixEntity { Id = originalMixId, Name = "Test" });
        }
        ctx.Song.Add(new SongEntity
        {
            Id = songId,
            Name = $"song_{songId:N}",
            ImagePath = "https://example.invalid/song.png",
            Type = "Arcade"
        });
        ctx.Chart.Add(new ChartEntity
        {
            Id = chartId,
            SongId = songId,
            OriginalMixId = originalMixId,
            Level = level,
            Type = type
        });
        if (addToPhoenixMix)
        {
            ctx.ChartMix.Add(new ChartMixEntity
            {
                Id = Guid.NewGuid(),
                ChartId = chartId,
                MixId = PhoenixMixId,
                Level = level
            });
        }
        await ctx.SaveChangesAsync(cancellationToken);
        return chartId;
    }
}
