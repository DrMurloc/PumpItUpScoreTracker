using MediatR;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     What a listed tool gets from being listed.
///     <para>
///         Clicks are the only feedback a listing-only tool ever receives — it has no players to
///         count — and they are the one directory number that differs between tools. Impressions
///         were considered and left out: with no paging and no ranking every listed tool is rendered
///         on every load, so a view count measures how busy the directory was rather than how
///         interesting one tool is, and a number that reads the same for everyone invites makers to
///         compare it and conclude nothing.
///     </para>
/// </summary>
internal sealed class ToolMetricsSaga : IRequestHandler<RecordToolClickCommand>
{
    private readonly IToolActivityRepository _activity;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IToolRepository _tools;

    public ToolMetricsSaga(IToolActivityRepository activity, IToolRepository tools,
        IDateTimeOffsetAccessor dateTime)
    {
        _activity = activity;
        _tools = tools;
        _dateTime = dateTime;
    }

    public async Task Handle(RecordToolClickCommand request, CancellationToken cancellationToken)
    {
        // Only listed tools. A click can only have come from the directory, and counting one against
        // a private tool would mean an unlisted tool accruing directory traffic.
        var tool = await _tools.GetTool(request.ToolId, cancellationToken);
        if (tool is null || tool.Visibility != ToolVisibility.Public) return;

        // Incremented into the hour's tally rather than logged. A row per click would bury the
        // maker's own activity feed under traffic they did not generate.
        await _activity.Increment(request.ToolId, ToolActivityKind.DirectoryClicked, _dateTime.Now,
            cancellationToken: cancellationToken);
    }
}
