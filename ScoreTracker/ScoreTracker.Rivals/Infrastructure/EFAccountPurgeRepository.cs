using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.Rivals.Infrastructure.Entities;

namespace ScoreTracker.Rivals.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table this vertical keys to a user. AccountPurgeCoverageTests checks this against
    ///     the assembly.
    ///     <para>
    ///         Unlike every other vertical this does NOT hand the list to
    ///         <see cref="UserDataPurge" />: that helper resolves ONE owning <c>*UserId</c> column
    ///         per entity and throws outright on two. Both rival tables carry two by design — an
    ///         edge and a block are each a relationship BETWEEN two accounts — so the deletes below
    ///         are hand-written and cover both ends. Erasing only the near end would leave a
    ///         deleted player sitting on somebody else's roster forever.
    ///     </para>
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(RivalEntity),
        typeof(RivalBlockEntity),
        typeof(RivalInviteCodeEntity)
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        await database.Set<RivalEntity>()
            .Where(r => r.OwnerUserId == userId || r.TargetUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await database.Set<RivalBlockEntity>()
            .Where(b => b.UserId == userId || b.BlockedUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await database.Set<RivalInviteCodeEntity>()
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
