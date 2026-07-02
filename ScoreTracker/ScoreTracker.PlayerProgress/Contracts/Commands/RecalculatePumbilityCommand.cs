using MediatR;

namespace ScoreTracker.PlayerProgress.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RecalculatePumbilityCommand(Guid UserId, IEnumerable<Guid> chartIds) : IRequest
{
}
