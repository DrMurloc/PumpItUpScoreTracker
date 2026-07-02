using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public record UserRatingsRecord(Guid ChartId, PreferenceRating Rating)
{
}
