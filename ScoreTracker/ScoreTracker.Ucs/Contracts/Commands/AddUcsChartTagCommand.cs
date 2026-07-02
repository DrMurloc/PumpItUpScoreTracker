using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Ucs.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record AddUcsChartTagCommand(Guid ChartId, Name Tag) : IRequest
{
}
