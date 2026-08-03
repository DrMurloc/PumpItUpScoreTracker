using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Infrastructure;

/// <summary>
///     A tool's webhook secrets, read only where they are needed.
///     <para>
///         Deliberately not on <c>IToolRepository</c>: a query that returns a Tool must not double as
///         a way to read what a maker authenticates by, and keeping these on their own port is what
///         makes that obvious rather than a matter of remembering.
///     </para>
///     <para>
///         The two are stored differently because they run in opposite directions — the outbound
///         header encrypted because we resend it, the verification secret hashed because we only
///         ever compare. See <see cref="WebhookSecrets" /> for why they must never be one value.
///     </para>
/// </summary>
internal sealed class EFToolSecretReader : IToolSecretReader
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;
    private readonly IToolSecretProtector _protector;

    public EFToolSecretReader(IDbContextFactory<ChartAttemptDbContext> factory,
        IToolSecretProtector protector)
    {
        _factory = factory;
        _protector = protector;
    }

    public async Task<(string? Name, string? Value)> GetOutboundHeader(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var tool = await database.Set<ToolEntity>().FirstOrDefaultAsync(t => t.Id == toolId, cancellationToken);
        if (tool is null) return (null, null);

        return (tool.OutboundHeaderName,
            await _protector.Unprotect(toolId, tool.OutboundHeaderValue, cancellationToken));
    }

    public async Task SetOutboundHeader(Guid toolId, string? name, string? value,
        CancellationToken cancellationToken = default)
    {
        var stored = value is null ? null : await _protector.Protect(toolId, value, cancellationToken);

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolEntity>().Where(t => t.Id == toolId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.OutboundHeaderName, name)
                .SetProperty(t => t.OutboundHeaderValue, stored), cancellationToken);
    }

    public async Task<string?> GetVerificationSecretHash(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ToolEntity>().Where(t => t.Id == toolId)
            .Select(t => t.WebhookVerificationSecretHash)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetVerificationSecretHash(Guid toolId, string? hash,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolEntity>().Where(t => t.Id == toolId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.WebhookVerificationSecretHash, hash),
                cancellationToken);
    }
}
