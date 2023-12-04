using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record FinishCardDrawCommand(Name MatchName) : IRequest
    {
    }
}
