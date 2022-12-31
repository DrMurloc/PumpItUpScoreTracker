using MediatR;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Application.Commands;

public sealed record RemoveChartFromListCommand(ChartListType ListType, Guid ChartId) : IRequest
{
}