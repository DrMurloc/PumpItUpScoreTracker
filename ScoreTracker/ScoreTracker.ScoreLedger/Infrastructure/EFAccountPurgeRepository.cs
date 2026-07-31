using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table the Ledger keys to a user. AccountPurgeCoverageTests checks this against
    ///     the assembly, and UserDataPurge executes it — one list, so a new table cannot be
    ///     declared without also being deleted.
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(ScoreEventJournalEntity),
        typeof(PhoenixRecordStatsEntity)
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
