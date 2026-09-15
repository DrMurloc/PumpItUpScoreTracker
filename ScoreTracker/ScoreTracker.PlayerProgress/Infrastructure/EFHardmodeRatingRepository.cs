using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFHardmodeRatingRepository : IHardmodeRatingRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly ILogger<EFHardmodeRatingRepository> _logger;

    public EFHardmodeRatingRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        ILogger<EFHardmodeRatingRepository> logger)
    {
        _factory = factory;
        _logger = logger;
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
        if (existing.Length != rows.Count)
            _logger.LogWarning(
                "Hardmode ratings for {Mix}: {Missing} of {Total} accounts have no PlayerStats row " +
                "and were not written",
                mix, rows.Count - existing.Length, rows.Count);

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
            // Every column this resets is named here. Today combined >= either type, so the two
            // per-type ratings could not be non-zero alone - but that is an invariant of the one
            // writer, not of the schema, and a predicate that does not name what it clears leaves
            // a stale rating on the board forever the day that stops holding.
            .Where(e => e.MixId == mixId && (e.HardmodeRating > 0 || e.HardmodeChartsHeld > 0
                                             || e.HardmodeSinglesRating > 0
                                             || e.HardmodeDoublesRating > 0
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
    ///     <para>
    ///         Built for the viewer (D17): a private account is on its own board and nobody
    ///         else's, so the filter runs in SQL and a stranger's private row is never
    ///         materialised here at all. Places number over what came back, which is why they are
    ///         viewer-shaped. The board no longer says how many it left out (D25).
    ///     </para>
    /// </summary>
    public async Task<HardmodeBoardRecord> GetBoard(MixEnum mix, ChartType? pool, Guid? viewerId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Guid.Empty stands in for an anonymous viewer rather than comparing against a nullable:
        // SQL's three-valued logic makes `stats.UserId = NULL` unknown rather than false, so the
        // "or it is your own row" arm would never fire. Public rows still come back on the other
        // arm, which is why this reads as correct until a private viewer loses their own row.
        var viewer = viewerId ?? Guid.Empty;

        var rows = await VisibleTo(database, mix, viewer)
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

        return new HardmodeBoardRecord(
            rows.Select((r, i) => new HardmodeBoardRow(i + 1, r.UserId, r.Hardmode, r.Held)).ToArray());
    }

    /// <summary>
    ///     The stats rows one viewer's board is allowed to draw on: public accounts, plus their
    ///     own (D17). Every read this page makes starts here, which is what keeps the standing
    ///     strip's "#N of M" honest against the rows printed under it — a field counted over a
    ///     different population would disagree with the list and look like a rounding quirk.
    ///     <para>
    ///         The join and the filter are one expression so a private row is excluded in SQL
    ///         rather than after materialising, and the pool's own column is left to the caller:
    ///         a projection this returned could not then be filtered, since EF cannot translate a
    ///         predicate over a constructor-projected type.
    ///     </para>
    /// </summary>
    private static IQueryable<PlayerStatsEntity> VisibleTo(ChartAttemptDbContext database, MixEnum mix,
        Guid viewer)
    {
        var mixId = MixIds.For(mix);
        return from stats in database.Set<PlayerStatsEntity>()
            join user in database.User on stats.UserId equals user.Id
            where stats.MixId == mixId && (user.IsPublic || stats.UserId == viewer)
            select stats;
    }

    /// <summary>
    ///     Two aggregates rather than a materialised board: how many stand above you, and how
    ///     many stand at all. Ties share a place, which the board's own index-based numbering
    ///     does not — on a float pool total that is a theoretical difference, and the competition
    ///     ranking is the more defensible of the two anyway.
    /// </summary>
    public async Task<(int Place, int Field)?> GetStanding(MixEnum mix, ChartType? pool, Guid userId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // The population this player's own board shows them, not every rated account: a place
        // measured against people they cannot see would print a field the rows contradict (D17).
        // Their own row is in it whether or not they are public — that is the exception the rule
        // is built around.
        var rated = VisibleTo(database, mix, userId)
            .Select(e => new
            {
                e.UserId,
                Hardmode = pool == ChartType.Single ? e.HardmodeSinglesRating
                    : pool == ChartType.Double ? e.HardmodeDoublesRating
                    : e.HardmodeRating
            })
            .Where(e => e.Hardmode > 0);

        var mine = await rated.Where(e => e.UserId == userId).Select(e => (double?)e.Hardmode)
            .FirstOrDefaultAsync(cancellationToken);
        if (mine == null) return null;

        var above = await rated.CountAsync(e => e.Hardmode > mine, cancellationToken);
        var field = await rated.CountAsync(cancellationToken);
        return (above + 1, field);
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
