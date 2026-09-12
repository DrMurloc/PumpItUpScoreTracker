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

    public EFOfficialHardmodeRatingRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
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

        var published = from placement in database.Set<OfficialLeaderboardPlacementEntity>()
            join board in database.Set<OfficialLeaderboardEntity>() on placement.LeaderboardId equals board.Id
            where board.MixId == mixId && board.LeaderboardType == "Rating" && board.Name == boardName
            group placement by placement.PlayerId
            into ratings
            select new { PlayerId = ratings.Key, Score = ratings.Max(r => r.Score) };

        var rows = await (from rating in database.Set<OfficialHardmodeRatingEntity>()
                join player in database.Set<OfficialPlayerEntity>() on rating.OfficialPlayerId equals player.Id
                join pub in published on player.Id equals pub.PlayerId into publications
                from publication in publications.DefaultIfEmpty()
                where rating.MixId == mixId
                select new
                {
                    player.Id,
                    player.Username,
                    Hardmode = pool == ChartType.Single ? rating.Singles
                        : pool == ChartType.Double ? rating.Doubles
                        : rating.Combined,
                    rating.ChartsHeld,
                    Pumbility = publication == null ? (decimal?)null : publication.Score
                })
            .Where(r => r.Hardmode > 0)
            .OrderByDescending(r => r.Hardmode)
            .ToArrayAsync(cancellationToken);

        return rows.Select((r, i) => new OfficialHardmodeRow(i + 1, r.Id, r.Username, r.Hardmode, r.ChartsHeld,
            r.Pumbility == null ? null : (double)r.Pumbility.Value)).ToArray();
    }
}
