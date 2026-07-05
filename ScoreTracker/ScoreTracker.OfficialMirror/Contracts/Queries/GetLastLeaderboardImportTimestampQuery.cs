using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetLastLeaderboardImportTimestampQuery(MixEnum Mix = MixEnum.Phoenix)
        : IQuery<DateTimeOffset?>
    {
    }
}
