using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record AddUcsChartTagCommand(Guid ChartId, Name Tag) : IRequest
{
}
