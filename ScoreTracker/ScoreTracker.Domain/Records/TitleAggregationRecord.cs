using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record TitleAggregationRecord(Name Title, int Count)
    {
    }
}
