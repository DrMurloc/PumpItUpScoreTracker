using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     The four PUMBILITY pools are <c>double</c> for the reason given on
    ///     <see cref="PlayerStatsRecord" />: rounding one below the presentation layer spends
    ///     precision that is not this layer's to spend.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record CommunityLeaderboardRecord(Name PlayerName, bool IsPublic, Uri ProfileImage, Guid UserId,
        Rating TotalRating,
        DifficultyLevel HighestLevel, int ClearCount,
        double CoOpRating,
        PhoenixScore CoOpScore, double SkillRating, PhoenixScore SkillScore, double SkillLevel,
        double SinglesRating, PhoenixScore SinglesScore, double SinglesLevel, double DoublesRating,
        PhoenixScore DoublesScore, double DoublesLevel, double CompetitiveLevel, double SinglesCompetitiveLevel,
        double DoublesCompetitiveLevel)
    {
    }
}
