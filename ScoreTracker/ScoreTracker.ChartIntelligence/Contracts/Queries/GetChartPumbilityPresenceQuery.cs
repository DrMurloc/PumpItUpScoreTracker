using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     One chart's PUMBILITY presence, title by title (docs/design/chart-presence-graph.md): what the
///     last census counted, and — for a viewer standing on the ladder — their own title and the
///     chart's place in their top 50 today. Null before the first census, and for a chart neither it
///     nor any other chart of its folder is held on any title.
/// </summary>
/// <param name="ViewerId">The signed-in player to place on the graph, or null for nobody.</param>
[ExcludeFromCodeCoverage]
public sealed record GetChartPumbilityPresenceQuery(Guid ChartId, MixEnum Mix, Guid? ViewerId)
    : IQuery<ChartPumbilityPresenceRecord?>;
