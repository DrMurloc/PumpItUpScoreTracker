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
    // Hardcoded in EFChartRepository / EFPhoenixRecordsRepository (search for `MixGuids`).
    // Tests that go through `MixEnum.Phoenix`-typed queries must use this exact ID.
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
