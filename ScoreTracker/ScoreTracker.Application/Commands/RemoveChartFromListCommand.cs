using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record RemoveChartFromListCommand(ChartListType ListType, Guid ChartId) : IRequest
{
}
