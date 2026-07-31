using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

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
        typeof(PhoenixRecordStatsEntity),
        typeof(PhoenixRecordEntity),
        typeof(BestAttemptEntity)
    };

    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache cache)
    {
        _factory = factory;
        _cache = cache;
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await UserDataPurge.DeleteAll(_factory, UserOwned, userId, cancellationToken);
        // The purge spans mixes, so every per-(user, mix) score cache entry goes with it.
        foreach (var mix in Enum.GetValues<MixEnum>())
            _cache.Remove(EFPhoenixRecordsRepository.ScoreCache(userId, mix));
    }
}
