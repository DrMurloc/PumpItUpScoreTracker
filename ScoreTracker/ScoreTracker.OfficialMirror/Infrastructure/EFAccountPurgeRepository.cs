using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table the mirror keys to a user and DELETES. Most of what this vertical stores is
    ///     public piugame data that outlives an account and is merely unlinked
    ///     (<see cref="UnlinkUser" />); a completeness check is the exception — it is a record of
    ///     one player's own scores measured against the site, and it goes with them.
    ///     AccountPurgeCoverageTests checks this list against the assembly and UserDataPurge
    ///     executes it, so a table cannot be declared without also being deleted.
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(ImportCheckRunEntity)
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task UnlinkUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<OfficialPlayerEntity>()
            .Where(p => p.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.UserId, (Guid?)null)
                .SetProperty(p => p.UserIdSource, "None"), cancellationToken);

        await UserDataPurge.DeleteAll(_factory, UserOwned, userId, cancellationToken);
    }
}
