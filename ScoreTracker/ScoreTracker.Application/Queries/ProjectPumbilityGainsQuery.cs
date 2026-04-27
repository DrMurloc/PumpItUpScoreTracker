using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record ProjectPumbilityGainsQuery(Guid UserId) : IRequest<PumbilityProjection>;
}
