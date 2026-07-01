using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Ucs.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record DeleteUcsChartTagCommand(Guid ChartId, Name Tag) : IRequest
{
}
