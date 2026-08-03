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

    Task Prune(DateTimeOffset before, CancellationToken cancellationToken = default);
}
