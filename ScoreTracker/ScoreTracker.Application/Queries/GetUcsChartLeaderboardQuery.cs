using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetUcsChartLeaderboardQuery(Guid ChartId) : IQuery<IEnumerable<UcsLeaderboardEntry>>
{
}
