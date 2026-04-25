using MediatR;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record RemoveChartFromListCommand(ChartListType ListType, Guid ChartId) : IRequest
{
}
