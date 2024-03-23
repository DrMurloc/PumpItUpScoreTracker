using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record CommunityLeaderboardRecord(Name PlayerName, Guid UserId, Rating TotalRating,
        DifficultyLevel HighestLevel, int ClearCount,
        Rating CoOpRating,
        PhoenixScore CoOpScore, Rating SkillRating, PhoenixScore SkillScore, double SkillLevel,
        Rating SinglesRating, PhoenixScore SinglesScore, double SinglesLevel, Rating DoublesRating,
        PhoenixScore DoublesScore, double DoublesLevel)
    {
    }
}
