using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records;

[ExcludeFromCodeCoverage]
public sealed record ChartDifficultyRatingRecord(Guid ChartId, double Difficulty, int RatingCount,
    double StandardDeviation)
{
    public DifficultyAdjustment? MyRating { get; set; }
}
