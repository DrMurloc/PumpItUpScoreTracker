using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>What a maker sees on their tool's console: the event log and its rollup summary.</summary>
internal sealed class ToolConsoleSaga :
    IRequestHandler<GetToolActivityQuery, IReadOnlyList<ToolActivityRecord>>,
    IRequestHandler<GetToolActivitySummaryQuery, ToolActivitySummary>
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

    private async Task<bool> Owns(Guid toolId, CancellationToken cancellationToken)
    {
        var tool = await _tools.GetTool(toolId, cancellationToken);
        return tool is not null && tool.OwnerUserId == _currentUser.User.Id;
    }
}
