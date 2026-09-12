using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFOfficialHardmodeRatingRepository : IOfficialHardmodeRatingRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IOfficialSnapshotRepository _snapshots;

    public EFOfficialHardmodeRatingRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        IOfficialSnapshotRepository snapshots)
    {
        _factory = factory;
        _snapshots = snapshots;
    }

    public async Task Replace(MixEnum mix, IReadOnlyCollection<OfficialHardmodeRating> rows,
        DateTimeOffset computedAt, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var existing = await database.Set<OfficialHardmodeRatingEntity>().Where(e => e.MixId == mixId)
            .ToArrayAsync(cancellationToken);
        database.Set<OfficialHardmodeRatingEntity>().RemoveRange(existing);
        await database.Set<OfficialHardmodeRatingEntity>().AddRangeAsync(rows.Select(r =>
            new OfficialHardmodeRatingEntity
            {
                OfficialPlayerId = r.OfficialPlayerId,
                MixId = mixId,
                Combined = r.Combined,
                Singles = r.Singles,
                Doubles = r.Doubles,
                ChartsHeld = r.Held,
                ComputedAt = computedAt
            }), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     The board, best first, with the player's published pool beside their Hardmode total
    ///     where the mirror holds one. The PUMBILITY board is a rating board keyed by name, so
    ///     the join is to the latest placement on it — null where no rating board carried them,
    ///     which is honest rather than zero.
    /// </summary>
    public async Task<IReadOnlyList<OfficialHardmodeRow>> GetBoard(MixEnum mix, ChartType? pool,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var boardName = pool switch
        {
            ChartType.Single => "PUMBILITY Singles",
            ChartType.Double => "PUMBILITY Doubles",
            _ => "PUMBILITY"
        };

        // Through the snapshot repository, which forces a placement scope: piugame's own board
        // is what this column quotes, and a supplemented row is ours (supplemented-leaderboards.md §7).
        var published = await _snapshots.GetRatingBoardScores(mix, boardName, PlacementScope.OfficialOnly,
            cancellationToken);

        var rows = await (from rating in database.Set<OfficialHardmodeRatingEntity>()
                join player in database.Set<OfficialPlayerEntity>() on rating.OfficialPlayerId equals player.Id
                where rating.MixId == mixId
                select new
                {
                    player.Id,
                    player.Username,
                    Hardmode = pool == ChartType.Single ? rating.Singles
                        : pool == ChartType.Double ? rating.Doubles
                        : rating.Combined,
                    rating.ChartsHeld
                })
            .Where(r => r.Hardmode > 0)
            .OrderByDescending(r => r.Hardmode)
            .ToArrayAsync(cancellationToken);

        return rows.Select((r, i) => new OfficialHardmodeRow(i + 1, r.Id, r.Username, r.Hardmode, r.ChartsHeld,
            published.TryGetValue(r.Id, out var pumbility) ? (double)pumbility : null)).ToArray();
    }
}
