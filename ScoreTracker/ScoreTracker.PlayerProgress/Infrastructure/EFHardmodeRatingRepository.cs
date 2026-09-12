using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFHardmodeRatingRepository : IHardmodeRatingRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFHardmodeRatingRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Save(MixEnum mix, IReadOnlyCollection<HardmodeRatingRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var byUser = rows.ToDictionary(r => r.UserId);
        var existing = await database.Set<PlayerStatsEntity>()
            .Where(e => e.MixId == mixId && byUser.Keys.Contains(e.UserId))
            .ToArrayAsync(cancellationToken);
        foreach (var entity in existing)
        {
            var row = byUser[entity.UserId];
            entity.HardmodeRating = row.Combined;
            entity.HardmodeSinglesRating = row.Singles;
            entity.HardmodeDoublesRating = row.Doubles;
            entity.HardmodeChartsHeld = row.Held;
            entity.HardmodeSinglesChartsHeld = row.SinglesHeld;
            entity.HardmodeDoublesChartsHeld = row.DoublesHeld;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     Zeroes the mix. A bulk update rather than a load-and-write: this touches every row on
    ///     the mix and the alternative is materialising the whole stats table to set four columns.
    /// </summary>
    public async Task Clear(MixEnum mix, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        await database.Set<PlayerStatsEntity>()
            .Where(e => e.MixId == mixId && (e.HardmodeRating > 0 || e.HardmodeChartsHeld > 0
                                             || e.HardmodeSinglesChartsHeld > 0
                                             || e.HardmodeDoublesChartsHeld > 0))
            .ExecuteUpdateAsync(e => e
                .SetProperty(x => x.HardmodeRating, 0d)
                .SetProperty(x => x.HardmodeSinglesRating, 0d)
                .SetProperty(x => x.HardmodeDoublesRating, 0d)
                .SetProperty(x => x.HardmodeChartsHeld, 0)
                .SetProperty(x => x.HardmodeSinglesChartsHeld, 0)
                .SetProperty(x => x.HardmodeDoublesChartsHeld, 0), cancellationToken);
    }

    /// <summary>
    ///     The board, best first, carrying the held count OF THE SELECTED POOL beside its total.
    ///     Only accounts the census found something for appear: a zero is not a standing.
    /// </summary>
    public async Task<IReadOnlyList<HardmodeBoardRow>> GetBoard(MixEnum mix, ChartType? pool,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var rows = await database.Set<PlayerStatsEntity>()
            .Where(e => e.MixId == mixId)
            .Select(e => new
            {
                e.UserId,
                Hardmode = pool == ChartType.Single ? e.HardmodeSinglesRating
                    : pool == ChartType.Double ? e.HardmodeDoublesRating
                    : e.HardmodeRating,
                Held = pool == ChartType.Single ? e.HardmodeSinglesChartsHeld
                    : pool == ChartType.Double ? e.HardmodeDoublesChartsHeld
                    : e.HardmodeChartsHeld
            })
            .Where(e => e.Hardmode > 0)
            .OrderByDescending(e => e.Hardmode)
            .ToArrayAsync(cancellationToken);
        return rows.Select((r, i) => new HardmodeBoardRow(i + 1, r.UserId, r.Hardmode, r.Held)).ToArray();
    }

    public async Task<HardmodeRatingRow?> Get(MixEnum mix, Guid userId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return await database.Set<PlayerStatsEntity>()
            .Where(e => e.MixId == mixId && e.UserId == userId)
            .Select(e => new HardmodeRatingRow(e.UserId, e.HardmodeRating, e.HardmodeSinglesRating,
                e.HardmodeDoublesRating, e.HardmodeChartsHeld, e.HardmodeSinglesChartsHeld,
                e.HardmodeDoublesChartsHeld))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
