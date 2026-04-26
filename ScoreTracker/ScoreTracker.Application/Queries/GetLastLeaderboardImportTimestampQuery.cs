using MediatR;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetLastLeaderboardImportTimestampQuery : IRequest<DateTimeOffset?>
    {
    }
}
