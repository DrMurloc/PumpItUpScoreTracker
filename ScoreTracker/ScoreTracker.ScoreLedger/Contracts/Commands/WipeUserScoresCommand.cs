using MediatR;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record WipeUserScoresCommand(Guid UserId, bool IncludeHistory) : IRequest
{
}
