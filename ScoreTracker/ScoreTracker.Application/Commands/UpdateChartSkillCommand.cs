using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Commands
{
    public sealed record UpdateChartSkillCommand(ChartSkillsRecord Skills) : IRequest
    {
    }
}
