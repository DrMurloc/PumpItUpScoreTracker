using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetAllWorldRankingsQuery(MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<WorldRankingRecord>>
{
}
