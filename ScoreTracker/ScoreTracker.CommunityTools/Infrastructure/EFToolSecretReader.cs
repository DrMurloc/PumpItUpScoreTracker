using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Infrastructure;

/// <summary>
///     A tool's outbound header, read only where it is needed.
///     <para>
///         Deliberately not on <c>IToolRepository</c>: a query that returns a Tool must not double as
///         a way to read the secret a maker authenticates us by, and keeping the read on its own port
///         is what makes that obvious rather than a matter of remembering.
///     </para>
///     <para>
///         Stored recoverably, because we send it verbatim on every delivery — unlike an API key,
///         which we only ever compare against.
///     </para>
/// </summary>
internal sealed class EFToolSecretReader : IToolSecretReader
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFToolSecretReader(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<(string? Name, string? Value)> GetOutboundHeader(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var tool = await database.Set<ToolEntity>().FirstOrDefaultAsync(t => t.Id == toolId, cancellationToken);
        return (tool?.OutboundHeaderName, tool?.OutboundHeaderValue);
    }

    public async Task SetOutboundHeader(Guid toolId, string? name, string? value,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolEntity>().Where(t => t.Id == toolId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.OutboundHeaderName, name)
                .SetProperty(t => t.OutboundHeaderValue, value), cancellationToken);
    }
}
