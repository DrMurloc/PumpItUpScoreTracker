using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RecalculateStatsCommand(Guid UserId, MixEnum Mix = MixEnum.Phoenix) : IRequest
{
}
