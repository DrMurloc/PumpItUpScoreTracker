using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record ResolveMatchCommand(Name MatchName) : IRequest
    {
    }
}
