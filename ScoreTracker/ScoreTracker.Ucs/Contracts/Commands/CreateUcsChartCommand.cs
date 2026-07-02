using MediatR;

namespace ScoreTracker.Ucs.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record CreateUcsChartCommand(UcsChart Chart) : IRequest
{
}
