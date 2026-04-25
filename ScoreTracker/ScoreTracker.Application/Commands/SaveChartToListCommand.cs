using MediatR;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record SaveChartToListCommand(ChartListType ListType, Guid ChartId) : IRequest
{
}
