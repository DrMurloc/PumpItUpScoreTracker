using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record TitleAggregationRecord(Name Title, int Count)
    {
    }
}
