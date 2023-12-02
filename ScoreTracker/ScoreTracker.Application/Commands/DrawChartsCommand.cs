using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record DrawChartsCommand(Name MatchName) : IRequest
    {
    }
}
