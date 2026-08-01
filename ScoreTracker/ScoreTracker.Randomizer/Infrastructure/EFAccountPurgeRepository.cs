using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Randomizer.Domain;
using ScoreTracker.Randomizer.Infrastructure.Entities;

namespace ScoreTracker.Randomizer.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table this vertical keys to a user. AccountPurgeCoverageTests checks this
    ///     against the assembly, and UserDataPurge executes it — one list, so a table cannot
    ///     be declared without also being deleted.
    ///     Draw cards ride along: their FK to the draw is declared Cascade, so deleting the
    ///     draw takes them. TournamentRandomSettings is a tournament's, not a player's.
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(RandomizerDrawEntity),
        typeof(UserRandomSettingsEntity)
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        return UserDataPurge.DeleteAll(_factory, UserOwned, userId, cancellationToken);
    }
}
