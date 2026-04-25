using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record UpdateChartSkillCommand(ChartSkillsRecord Skills) : IRequest
    {
    }
}
