using ScoreTracker.CommunityTools.Contracts;

namespace ScoreTracker.CommunityTools.Domain;

internal interface IToolActivityRepository
{
    /// <summary>A point event — something that happened once, at a moment.</summary>
    Task Record(Guid toolId, ToolActivityKind kind, DateTimeOffset at, string? detail = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds one to the hour's tally, creating the row if this is the first.
    ///     <para>
    ///         Rate limiting and key use are counted rather than logged: at 600 requests a minute a
    ///         per-call row would put tens of thousands of rows a day in front of a maker who wants
    ///         one line saying "you hit the limit 212 times this hour".
    ///     </para>
    /// </summary>
    Task Increment(Guid toolId, ToolActivityKind kind, DateTimeOffset at, string? detail = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ToolActivityRecord>> GetRecent(Guid toolId, int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Every roll-up of one kind, summed over the tool's whole life. The recent-activity read
    ///     cannot answer this — it takes the last few hundred rows, which is a window, not a total.
    /// </summary>
    Task<int> SumAllTime(Guid toolId, ToolActivityKind kind, CancellationToken cancellationToken = default);

    Task Prune(DateTimeOffset before, CancellationToken cancellationToken = default);
}
