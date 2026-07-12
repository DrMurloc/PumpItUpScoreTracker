using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record AddChartToDrawCommand(Guid DrawId, Guid ChartId) : IRequest<DrawDto>
    {
    }
}
