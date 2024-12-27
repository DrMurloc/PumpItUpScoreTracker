using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record UserChartAggregate(Guid UserId, int Passed, int Total, PhoenixScore AverageScore)
    {
    }
}
