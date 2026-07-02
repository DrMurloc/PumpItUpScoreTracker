using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetMatchQuery(Guid TournamentId, Name MatchName) : IQuery<MatchView>
    {
    }
}
