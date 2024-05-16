using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries;

public sealed record GetAllMatchesQuery(Guid TournamentId) : IRequest<IEnumerable<MatchView>>
{
}