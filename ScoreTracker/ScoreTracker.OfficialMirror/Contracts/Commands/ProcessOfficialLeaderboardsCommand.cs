using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record ProcessOfficialLeaderboardsCommand : IRequest
    {
    }
}
