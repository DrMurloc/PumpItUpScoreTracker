using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SaveQualifiersCommand(Guid TournamentId, UserQualifiers Qualifiers) : IRequest
    {
    }
}
