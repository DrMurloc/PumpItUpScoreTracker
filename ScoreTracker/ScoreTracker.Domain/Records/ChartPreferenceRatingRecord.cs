using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

public sealed record ChartPreferenceRatingRecord(Guid ChartId, PreferenceRating Rating, int Count)
{
}