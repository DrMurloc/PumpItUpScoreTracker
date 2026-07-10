using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetOfficialLeaderboardUsernamesQuery(MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<string>>
{
}
