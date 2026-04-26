using MediatR;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record WipeUserScoresCommand(Guid UserId, bool IncludeHistory) : IRequest
{
}
