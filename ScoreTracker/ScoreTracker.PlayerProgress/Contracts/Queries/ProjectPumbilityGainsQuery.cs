using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record ProjectPumbilityGainsQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<PumbilityProjection>;
}
