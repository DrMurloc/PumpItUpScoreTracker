using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

public record UserRatingsRecord(Guid ChartId, PreferenceRating Rating)
{
}