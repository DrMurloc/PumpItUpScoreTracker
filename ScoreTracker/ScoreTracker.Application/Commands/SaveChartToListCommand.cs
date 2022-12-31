using MediatR;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Application.Commands;

public sealed record SaveChartToListCommand(ChartListType ListType, Guid ChartId) : IRequest
{
}