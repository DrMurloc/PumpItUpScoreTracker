using MediatR;

namespace ScoreTracker.Application.Commands
{
    public sealed record ProcessOfficialLeaderboardsCommand : IRequest
    {
    }
}
