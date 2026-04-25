using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record ChartPreferenceRatingRecord(Guid ChartId, PreferenceRating Rating, int Count)
{
}
