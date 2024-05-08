using MediatR;

namespace ScoreTracker.Application.Commands
{
    public sealed record ImportOfficialPlayerScoresCommand
        (string Username, string Password, bool IncludeBroken) : IRequest
    {
    }
}
