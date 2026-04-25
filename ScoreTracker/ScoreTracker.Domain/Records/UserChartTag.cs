using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record UserChartTag(Guid ChartId, Guid UserId, Name Tag)
    {
    }
}
