using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RecalculatePumbilityCommand(Guid UserId, IEnumerable<Guid> chartIds,
    MixEnum Mix = MixEnum.Phoenix) : IRequest
{
}
