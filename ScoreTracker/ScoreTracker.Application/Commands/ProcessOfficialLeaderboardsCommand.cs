using MediatR;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record ProcessOfficialLeaderboardsCommand : IRequest
    {
    }
}
