using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetAllMatchesQuery(Guid TournamentId) : IRequest<IEnumerable<MatchView>>
{
}
