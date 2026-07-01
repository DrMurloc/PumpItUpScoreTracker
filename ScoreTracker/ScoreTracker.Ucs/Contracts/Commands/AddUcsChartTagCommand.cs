using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Ucs.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record AddUcsChartTagCommand(Guid ChartId, Name Tag) : IRequest
{
}
