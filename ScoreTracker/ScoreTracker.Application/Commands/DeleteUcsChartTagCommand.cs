using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record DeleteUcsChartTagCommand(Guid ChartId, Name Tag) : IRequest
{
}
