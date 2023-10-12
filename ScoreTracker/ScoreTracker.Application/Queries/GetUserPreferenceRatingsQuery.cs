using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetUserPreferenceRatingsQuery(MixEnum Mix) : IRequest<IEnumerable<UserRatingsRecord>>
    {
    }
}
