using ScoreTracker.Domain.Records;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetAllWorldRankingsQuery : IQuery<IEnumerable<WorldRankingRecord>>
{
}
