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
                SinglesChartsHeld = r.SinglesHeld,
                DoublesChartsHeld = r.DoublesHeld,
                ComputedAt = computedAt
            }), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     The board, best first, carrying the held count OF THE SELECTED POOL beside its total.
    /// </summary>
    public async Task<IReadOnlyList<OfficialHardmodeRow>> GetBoard(MixEnum mix, ChartType? pool,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var rows = await (from rating in database.Set<OfficialHardmodeRatingEntity>()
                join player in database.Set<OfficialPlayerEntity>() on rating.OfficialPlayerId equals player.Id
                join account in database.User on player.UserId equals account.Id into linked
                from account in linked.DefaultIfEmpty()
                where rating.MixId == mixId
                select new
                {
                    player.Id,
                    player.Username,
                    player.AvatarUrl,
                    // A private account's link is mirrored data they never published, so it does
                    // not travel: the chip's "Linked to a site account" tick says "this tag is
                    // someone here" as plainly as a name would (D17). The census drops linked
                    // players outright, so this only catches the account that linked BETWEEN
                    // Sunday's rebuild and now — a stale row that still names its owner.
                    UserId = account != null && account.IsPublic ? player.UserId : null,
                    Hardmode = pool == ChartType.Single ? rating.Singles
                        : pool == ChartType.Double ? rating.Doubles
                        : rating.Combined,
                    Held = pool == ChartType.Single ? rating.SinglesChartsHeld
                        : pool == ChartType.Double ? rating.DoublesChartsHeld
                        : rating.ChartsHeld
                })
            .Where(r => r.Hardmode > 0)
            .OrderByDescending(r => r.Hardmode)
            .ToArrayAsync(cancellationToken);

        // The avatar rides the row rather than being fetched per player by the page: it is one
        // column of a join the board already makes. IsSupplemented stays false by construction -
        // a Hardmode rating has no supplemented reading, and the census excludes linked players,
        // so UserId is normally null here anyway - the read above is what holds when it is not.
        return rows.Select((r, i) => new OfficialHardmodeRow(i + 1,
                new OfficialPlayerRecord(r.Id, r.Username,
                    string.IsNullOrWhiteSpace(r.AvatarUrl) ? null : new Uri(r.AvatarUrl), r.UserId),
                r.Hardmode, r.Held))
            .ToArray();
    }
}
