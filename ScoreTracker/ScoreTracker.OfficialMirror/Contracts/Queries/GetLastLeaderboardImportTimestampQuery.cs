using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetLastLeaderboardImportTimestampQuery : IQuery<DateTimeOffset?>
    {
    }
}
