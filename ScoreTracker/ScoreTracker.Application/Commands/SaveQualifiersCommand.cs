using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SaveQualifiersCommand(Guid TournamentId, UserQualifiers Qualifiers) : IRequest
    {
    }
}
