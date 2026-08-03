using Microsoft.EntityFrameworkCore;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Communities.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table this vertical keys to a user. AccountPurgeCoverageTests checks this
    ///     against the assembly, and UserDataPurge executes it — one list, so a table cannot
    ///     be declared without also being deleted. Membership rows carry a second user key,
    ///     GrantedByUserId, which is somebody else's; [PurgeKey] on the entity says so, and the
    ///     grants this account handed out are cleared by hand below.
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(CommunityMembershipEntity),
        typeof(CommunityHighlightEntity)
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
            // An admin this account promoted keeps their seat — the seat is theirs, not the
            // granter's — but the pointer back to a deleted account does not outlive it.
            await database.Set<CommunityMembershipEntity>()
                .Where(m => m.GrantedByUserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.GrantedByUserId, (Guid?)null),
                    cancellationToken);
        }

        await UserDataPurge.DeleteAll(_factory, UserOwned, userId, cancellationToken);
    }
}
