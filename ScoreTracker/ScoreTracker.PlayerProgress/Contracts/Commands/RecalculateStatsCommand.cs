using MediatR;

namespace ScoreTracker.PlayerProgress.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record RecalculateStatsCommand(Guid UserId) : IRequest
{
}
