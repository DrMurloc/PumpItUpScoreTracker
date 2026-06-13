using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record CreateUcsChartCommand(UcsChart Chart) : IRequest
{
}
