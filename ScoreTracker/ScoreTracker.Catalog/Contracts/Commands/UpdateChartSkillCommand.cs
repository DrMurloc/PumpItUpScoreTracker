using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Catalog.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record UpdateChartSkillCommand(ChartSkillsRecord Skills) : IRequest
    {
    }
}
