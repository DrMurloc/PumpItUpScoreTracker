using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record CommunityLeaderboardRecord(Name PlayerName, Guid UserId, Rating TotalRating, Rating CoOpRating,
        Rating SkillRating,
        Rating SinglesRating, Rating DoublesRating)
    {
    }
}
