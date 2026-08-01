using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Infrastructure;

/// <summary>
///     A tool's outbound secrets, read only where they are needed.
///     <para>
///         Deliberately not on <c>IToolRepository</c>: a query that returns a Tool must not double as
///         a way to read its signing secret, and keeping the read on its own port is what makes that
///         obvious rather than a matter of remembering.
///     </para>
///     <para>
///         The signing secret is stored recoverably because we have to re-sign every retry with it —
///         unlike an API key, which we only ever compare against. It is minted on first use so a tool
///         created before webhooks existed still gets one.
///     </para>
/// </summary>
internal sealed class EFToolSecretReader : IToolSecretReader
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFToolSecretReader(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<string> GetSigningSecret(Guid toolId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var tool = await database.Set<ToolEntity>().FirstOrDefaultAsync(t => t.Id == toolId, cancellationToken);
        if (tool is null) return string.Empty;

        if (!string.IsNullOrWhiteSpace(tool.SigningSecretHash)) return tool.SigningSecretHash;

        tool.SigningSecretHash = WebhookSigning.MintSecret();
        await database.SaveChangesAsync(cancellationToken);
        return tool.SigningSecretHash;
    }

    public async Task<(string? Name, string? Value)> GetOutboundHeader(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var tool = await database.Set<ToolEntity>().FirstOrDefaultAsync(t => t.Id == toolId, cancellationToken);
        return (tool?.OutboundHeaderName, tool?.OutboundHeaderValueHash);
    }

    public async Task SetSigningSecret(Guid toolId, string secret, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolEntity>().Where(t => t.Id == toolId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SigningSecretHash, secret), cancellationToken);
    }

    public async Task SetOutboundHeader(Guid toolId, string? name, string? value,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolEntity>().Where(t => t.Id == toolId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.OutboundHeaderName, name)
                .SetProperty(t => t.OutboundHeaderValueHash, value), cancellationToken);
    }
}
