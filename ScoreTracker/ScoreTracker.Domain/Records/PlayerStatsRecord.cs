using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record PlayerStatsRecord(Rating TotalRating, Rating CoOpRating, Rating SkillRating,
        Rating SinglesRating, Rating DoublesRating)
    {
    }
}
