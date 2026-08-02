using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Infrastructure;

internal sealed class EFToolActivityRepository : IToolActivityRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFToolActivityRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Record(Guid toolId, ToolActivityKind kind, DateTimeOffset at, string? detail = null,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolActivityEntity>().AddAsync(new ToolActivityEntity
        {
            Id = Guid.NewGuid(), ToolId = toolId, EventType = kind.ToString(), OccurredAt = at,
            Count = 1, Detail = Truncate(detail)
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task Increment(Guid toolId, ToolActivityKind kind, DateTimeOffset at,
        string? detail = null, CancellationToken cancellationToken = default)
    {
        var window = new DateTimeOffset(at.Year, at.Month, at.Day, at.Hour, 0, 0, at.Offset);
        var type = kind.ToString();

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var updated = await database.Set<ToolActivityEntity>()
            .Where(a => a.ToolId == toolId && a.EventType == type && a.WindowStart == window)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Count, a => a.Count + 1)
                .SetProperty(a => a.OccurredAt, at), cancellationToken);

        if (updated > 0) return;

        await database.Set<ToolActivityEntity>().AddAsync(new ToolActivityEntity
        {
            Id = Guid.NewGuid(), ToolId = toolId, EventType = type, OccurredAt = at,
            WindowStart = window, Count = 1, Detail = Truncate(detail)
        }, cancellationToken);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two requests can open the same hour at once. Losing the race is not an error — the
            // other insert already created the row, so fold into it.
            await database.Set<ToolActivityEntity>()
                .Where(a => a.ToolId == toolId && a.EventType == type && a.WindowStart == window)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Count, a => a.Count + 1), cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ToolActivityRecord>> GetRecent(Guid toolId, int limit,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        var activity = await database.Set<ToolActivityEntity>()
            .Where(a => a.ToolId == toolId)
            .OrderByDescending(a => a.OccurredAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        // Deliveries are the other half of the log and live in their own table, so the console's
        // single stream is a merge rather than a join — cheaper, and it keeps the delivery row the
        // one source of truth about a delivery.
        var deliveries = await database.Set<WebhookDeliveryEntity>()
            .Where(d => d.ToolId == toolId)
            .OrderByDescending(d => d.QueuedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        return activity.Select(Map)
            .Concat(deliveries.Select(Map))
            .OrderByDescending(r => r.OccurredAt)
            .Take(limit)
            .ToArray();
    }

    public async Task Prune(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolActivityEntity>().Where(a => a.OccurredAt < before)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static string? Truncate(string? detail)
    {
        return detail is null ? null : detail[..Math.Min(detail.Length, 200)];
    }

    private static ToolActivityRecord Map(ToolActivityEntity e)
    {
        return new ToolActivityRecord(e.Id, Enum.Parse<ToolActivityKind>(e.EventType), e.OccurredAt,
            e.WindowStart, e.Count, e.Detail, null, null, null, false);
    }

    private static ToolActivityRecord Map(WebhookDeliveryEntity d)
    {
        var status = Enum.Parse<DeliveryStatus>(d.Status);
        var reason = Enum.Parse<WebhookFailureReason>(d.FailureReason ?? nameof(WebhookFailureReason.None));

        var kind = status == DeliveryStatus.Succeeded
            ? ToolActivityKind.DeliverySucceeded
            : reason switch
            {
                WebhookFailureReason.Timeout => ToolActivityKind.DeliveryTimedOut,
                WebhookFailureReason.DnsFailure or WebhookFailureReason.TlsFailure =>
                    ToolActivityKind.DeliveryUnreachable,
                _ => ToolActivityKind.DeliveryRejected
            };

        return new ToolActivityRecord(d.Id, kind, d.QueuedAt, null, 1, d.RemoteBodySnippet,
            d.RemoteStatusCode, d.DeliveryId, d.Id,
            // Replayable only while we still hold the body — which a session-mode delivery never has.
            d.Body is not null && status != DeliveryStatus.Succeeded);
    }
}
