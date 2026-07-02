using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record ProjectPumbilityGainsQuery(Guid UserId) : IQuery<PumbilityProjection>;
}
