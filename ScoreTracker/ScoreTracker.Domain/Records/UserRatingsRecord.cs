using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public record UserRatingsRecord(Guid ChartId, PreferenceRating Rating)
{
}
