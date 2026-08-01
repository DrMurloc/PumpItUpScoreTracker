using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>The activity console, the signature echo, and the delivery feed a tool can pull.</summary>
internal sealed class ToolConsoleSaga :
    IRequestHandler<GetToolActivityQuery, IReadOnlyList<ToolActivityRecord>>,
    IRequestHandler<GetToolActivitySummaryQuery, ToolActivitySummary>,
    IRequestHandler<GetToolSignatureEchoQuery, SignatureEcho?>,
    IRequestHandler<GetToolDeliveryFeedQuery, IReadOnlyList<DeliveryFeedRecord>>
{
    private readonly IToolActivityRepository _activity;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IToolRepository _tools;

    public ToolConsoleSaga(IToolActivityRepository activity, IWebhookDeliveryRepository deliveries,
        IToolRepository tools, ICurrentUserAccessor currentUser)
    {
        _activity = activity;
        _deliveries = deliveries;
        _tools = tools;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ToolActivityRecord>> Handle(GetToolActivityQuery request,
        CancellationToken cancellationToken)
    {
        if (!await Owns(request.ToolId, cancellationToken)) return Array.Empty<ToolActivityRecord>();

        return await _activity.GetRecent(request.ToolId, Math.Clamp(request.Limit, 1, 500),
            cancellationToken);
    }

    public async Task<ToolActivitySummary> Handle(GetToolActivitySummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!await Owns(request.ToolId, cancellationToken))
            return new ToolActivitySummary(0, 0, 0, 0);

        var rows = await _activity.GetRecent(request.ToolId, 500, cancellationToken);
        return new ToolActivitySummary(
            rows.Count(r => r.Kind == ToolActivityKind.DeliverySucceeded),
            rows.Count(r => r.Kind is ToolActivityKind.DeliveryTimedOut
                or ToolActivityKind.DeliveryRejected or ToolActivityKind.DeliveryUnreachable),
            rows.Where(r => r.Kind == ToolActivityKind.RateLimited).Sum(r => r.Count),
            await _tools.CountConnectedPlayers(request.ToolId, cancellationToken));
    }

    /// <summary>
    ///     The exact bytes we signed, so a maker can diff them against what their own code hashed.
    ///     Session mode has none — that body is never written down.
    /// </summary>
    public async Task<SignatureEcho?> Handle(GetToolSignatureEchoQuery request,
        CancellationToken cancellationToken)
    {
        if (!await Owns(request.ToolId, cancellationToken)) return null;

        var latest = await _deliveries.GetLatestWithBody(request.ToolId, cancellationToken);
        if (latest?.Body is null || latest.Signature is null) return null;

        return new SignatureEcho(latest.DeliveryId,
            WebhookSigning.PayloadToSign(latest.SignedAt.ToUnixTimeSeconds(), latest.Body),
            latest.Signature, latest.SignedAt);
    }

    /// <summary>
    ///     The delivery table with a route on it. Free to build because the table exists for the
    ///     console regardless, and it is the honest answer for a maker on a laptop with no public
    ///     URL — webhooks become an optimisation rather than a prerequisite.
    /// </summary>
    public async Task<IReadOnlyList<DeliveryFeedRecord>> Handle(GetToolDeliveryFeedQuery request,
        CancellationToken cancellationToken)
    {
        var deliveries = await _deliveries.GetForTool(request.ToolId,
            Math.Clamp(request.Limit, 1, 500), cancellationToken);

        var after = request.After;
        var rows = deliveries.OrderBy(d => d.SignedAt).AsEnumerable();
        if (after is not null)
        {
            var seen = deliveries.FirstOrDefault(d => d.DeliveryId == after);
            if (seen is not null) rows = rows.Where(d => d.SignedAt > seen.SignedAt);
        }

        return rows.Select(d => new DeliveryFeedRecord(d.DeliveryId, d.SignedAt, d.Mode.ToString(),
            d.UserId, d.Mix.ToString(), d.Body)).ToArray();
    }

    private async Task<bool> Owns(Guid toolId, CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(toolId, cancellationToken);
        return tool is not null && tool.OwnerUserId == _currentUser.User.Id;
    }
}
