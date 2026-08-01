using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.CommunityTools.Infrastructure;

internal sealed class EFWebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFWebhookDeliveryRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<Guid> Enqueue(Guid toolId, Guid userId, MixEnum mix, WebhookMode mode,
        string deliveryId, string? body, string? signature, DateTimeOffset signedAt, bool isTest,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = new WebhookDeliveryEntity
        {
            Id = Guid.NewGuid(),
            ToolId = toolId,
            UserId = userId,
            MixId = MixIds.For(mix),
            Mode = mode.ToString(),
            DeliveryId = deliveryId,
            // Never for session mode: that body carries a live piugame.com credential.
            Body = WebhookRetention.ShouldPersistBody(mode, DeliveryStatus.Pending) ? body : null,
            Signature = signature,
            SignedAt = signedAt,
            Attempt = 0,
            Status = DeliveryStatus.Pending.ToString(),
            FailureReason = WebhookFailureReason.None.ToString(),
            IsTest = isTest
        };
        await database.Set<WebhookDeliveryEntity>().AddAsync(entity, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task RecordSuccess(Guid id, int statusCode, int latencyMs, bool keepBody,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var query = database.Set<WebhookDeliveryEntity>().Where(d => d.Id == id);

        await query.ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Status, DeliveryStatus.Succeeded.ToString())
            .SetProperty(d => d.RemoteStatusCode, statusCode)
            .SetProperty(d => d.LatencyMs, latencyMs)
            .SetProperty(d => d.NextAttemptAt, (DateTimeOffset?)null), cancellationToken);

        // A success nobody will replay does not need to exist; one per tool is kept as the
        // console's signature sample.
        if (!keepBody)
            await query.ExecuteUpdateAsync(s => s.SetProperty(d => d.Body, (string?)null),
                cancellationToken);
    }

    public async Task RecordFailure(Guid id, int attempt, WebhookFailureReason reason, int? statusCode,
        string? remoteBodySnippet, int? latencyMs, DateTimeOffset? nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        var status = nextAttemptAt is null ? DeliveryStatus.Abandoned : DeliveryStatus.Failed;
        var snippet = remoteBodySnippet is null
            ? null
            : remoteBodySnippet[..Math.Min(remoteBodySnippet.Length, 500)];

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<WebhookDeliveryEntity>()
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, status.ToString())
                .SetProperty(d => d.Attempt, attempt)
                .SetProperty(d => d.FailureReason, reason.ToString())
                .SetProperty(d => d.RemoteStatusCode, statusCode)
                .SetProperty(d => d.RemoteBodySnippet, snippet)
                .SetProperty(d => d.LatencyMs, latencyMs)
                .SetProperty(d => d.NextAttemptAt, nextAttemptAt), cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDeliveryRecord>> GetDue(DateTimeOffset now, int limit,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<WebhookDeliveryEntity>()
                .Where(d => d.NextAttemptAt != null && d.NextAttemptAt <= now)
                .OrderBy(d => d.NextAttemptAt)
                .Take(limit)
                .ToArrayAsync(cancellationToken))
            .Select(Map).ToArray();
    }

    public async Task<WebhookDeliveryRecord?> Get(Guid id, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<WebhookDeliveryEntity>()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<WebhookDeliveryRecord>> GetForTool(Guid toolId, int limit,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<WebhookDeliveryEntity>()
                .Where(d => d.ToolId == toolId)
                .OrderByDescending(d => d.SignedAt)
                .Take(limit)
                .ToArrayAsync(cancellationToken))
            .Select(Map).ToArray();
    }

    public async Task<WebhookDeliveryRecord?> GetLatestWithBody(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<WebhookDeliveryEntity>()
            .Where(d => d.ToolId == toolId && d.Body != null)
            .OrderByDescending(d => d.SignedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task Prune(DateTimeOffset bodiesBefore, DateTimeOffset rowsBefore,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<WebhookDeliveryEntity>()
            .Where(d => d.SignedAt < bodiesBefore && d.Body != null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Body, (string?)null), cancellationToken);
        await database.Set<WebhookDeliveryEntity>()
            .Where(d => d.SignedAt < rowsBefore)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static WebhookDeliveryRecord Map(WebhookDeliveryEntity e)
    {
        return new WebhookDeliveryRecord(e.Id, e.ToolId, e.UserId,
            MixIds.IsKnown(e.MixId) ? MixIds.ToEnum(e.MixId) : MixEnum.Phoenix,
            Enum.Parse<WebhookMode>(e.Mode), e.DeliveryId, e.Body, e.SignedAt, e.Signature, e.Attempt,
            Enum.Parse<DeliveryStatus>(e.Status), e.RemoteStatusCode,
            Enum.Parse<WebhookFailureReason>(e.FailureReason ?? nameof(WebhookFailureReason.None)),
            e.RemoteBodySnippet, e.LatencyMs, e.NextAttemptAt, e.IsTest);
    }
}
