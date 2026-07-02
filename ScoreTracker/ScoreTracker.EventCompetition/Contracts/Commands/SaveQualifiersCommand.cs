using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.EventCompetition.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record SaveQualifiersCommand(Guid TournamentId, UserQualifiers Qualifiers) : IRequest
    {
    }
}
