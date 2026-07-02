using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetMatchLinksFromMatchQuery
        (Guid TournamentId, Name FromMatchName) : IQuery<IEnumerable<MatchLink>>
    {
    }
}
