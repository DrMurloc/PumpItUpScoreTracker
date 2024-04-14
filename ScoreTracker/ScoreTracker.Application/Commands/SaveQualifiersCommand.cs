using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Commands
{
    public sealed record SaveQualifiersCommand(Guid TournamentId, UserQualifiers Qualifiers) : IRequest
    {
    }
}
