using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record SaveChartToListCommand(ChartListType ListType, Guid ChartId) : IRequest
{
}
