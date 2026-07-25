using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     You-vs-them best attempts across one level×type folder. "Me" is the current user;
///     both players must share the community. One row per chart in the folder.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityFolderComparisonQuery(
    Name CommunityName, Guid UserId, ChartType ChartType, int Level, MixEnum Mix)
    : IQuery<IEnumerable<CommunityChartComparisonRecord>>;
