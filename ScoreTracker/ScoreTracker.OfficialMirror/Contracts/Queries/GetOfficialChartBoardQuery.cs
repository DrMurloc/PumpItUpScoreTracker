using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>The chart's full mirrored board at the latest sealed snapshot; null when the chart has no board.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialChartBoardQuery(MixEnum Mix, Guid ChartId) : IQuery<OfficialChartBoardRecord?>;
