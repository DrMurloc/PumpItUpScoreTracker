using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.HomePage.Domain;
using ScoreTracker.HomePage.Infrastructure.Entities;

namespace ScoreTracker.HomePage.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table this vertical keys to a user. AccountPurgeCoverageTests checks this
    ///     against the assembly, and UserDataPurge executes it.
    ///     Widget instances are keyed to the page rather than the user and their FK declares no
    ///     cascade, so they are cleared first, by hand — deleting the pages alone would strand
    ///     them. Adding the cascade would be the better fix and belongs in its own change.
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(HomePageEntity)
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using (var database = await _factory.CreateDbContextAsync(cancellationToken))
        {
            var pageIds = database.Set<HomePageEntity>().Where(p => p.UserId == userId).Select(p => p.Id);
            await database.Set<HomePageWidgetEntity>().Where(w => pageIds.Contains(w.PageId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await UserDataPurge.DeleteAll(_factory, UserOwned, userId, cancellationToken);
    }
}
