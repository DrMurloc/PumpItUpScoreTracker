using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.WeeklyChallenge.Domain;
using ScoreTracker.WeeklyChallenge.Infrastructure.Entities;

namespace ScoreTracker.WeeklyChallenge.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table this vertical keys to a user. AccountPurgeCoverageTests checks this
    ///     against the assembly, and UserDataPurge executes it — one list, so a table cannot
    ///     be declared without also being deleted.
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(WeeklyUserEntry),
        typeof(UserWeeklyPlacingEntity),
        typeof(DailyStepEntryEntity),
        typeof(UserDailyStepPlacingEntity)
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
