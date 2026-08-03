using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table this vertical keys to a user. AccountPurgeCoverageTests checks this against
    ///     the assembly, and UserDataPurge executes it — one list, so a table cannot be declared
    ///     without also being deleted.
    ///     <para>
    ///         <see cref="ToolEntity" /> is not here and cannot be: purging its rows by
    ///         <c>OwnerUserId</c> would orphan the tool's keys, shares, deliveries and invite codes.
    ///         A deleted owner's tools are removed whole, below.
    ///     </para>
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(ToolShareEntity),
        typeof(ToolBlockEntity),
        typeof(ToolSharePreferenceEntity),
        typeof(WebhookDeliveryEntity)
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IToolRepository _tools;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory, IToolRepository tools)
    {
        _factory = factory;
        _tools = tools;
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        // Tools cascade rather than block deletion (docs/design/api-v2-community-tools.md §10): a
        // community has members who can inherit it, a tool has a maker's server that is leaving.
        // Each goes whole — keys, shares, deliveries, invite codes — before the row-level purge.
        foreach (var tool in await _tools.GetToolsOwnedBy(userId, cancellationToken))
            await _tools.DeleteTool(tool.Id, cancellationToken);

        await UserDataPurge.DeleteAll(_factory, UserOwned, userId, cancellationToken);
    }
}
