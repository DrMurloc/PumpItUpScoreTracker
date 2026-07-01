using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetAllWorldRankingsQuery : IQuery<IEnumerable<WorldRankingRecord>>
{
}
