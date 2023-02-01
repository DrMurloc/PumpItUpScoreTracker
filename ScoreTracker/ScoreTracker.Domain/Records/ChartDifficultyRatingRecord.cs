using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Records;

public sealed record ChartDifficultyRatingRecord(Guid ChartId, double Difficulty, int RatingCount)
{
    public DifficultyAdjustment? MyRating { get; set; }
}