using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record ChartPreferenceRatingRecord(Guid ChartId, Rating Rating, int Count)
    {
    }
}
