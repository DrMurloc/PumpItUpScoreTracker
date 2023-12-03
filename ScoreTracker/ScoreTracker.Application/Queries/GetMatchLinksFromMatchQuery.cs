using MediatR;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetMatchLinksFromMatchQuery(Name FromMatchName) : IRequest<IEnumerable<MatchLink>>
    {
    }
}
