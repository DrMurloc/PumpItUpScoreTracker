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
        double DoublesCompetitiveLevel,
        // Non-null on XX and older, and then it is the ONLY meaningful half of this record:
        // every field above is derived from Phoenix scoring, which those mixes do not have.
        LegacyScoreTotals? Legacy = null)
    {
        /// <summary>
        ///     A row for a mix with no Phoenix scoring. The fields above are filled with the
        ///     smallest values their types permit and are never read — Legacy being non-null is
        ///     what tells a reader which half of this record is real. They are placeholders
        ///     rather than zeros throughout because DifficultyLevel refuses to be zero.
        /// </summary>
        public static CommunityLeaderboardRecord ForLegacy(Name playerName, bool isPublic, Uri profileImage,
            Guid userId, LegacyScoreTotals totals)
        {
            return new CommunityLeaderboardRecord(playerName, isPublic, profileImage, userId,
                Rating.Min, DifficultyLevel.Min, totals.Recorded,
                0, PhoenixScore.Min, 0, PhoenixScore.Min, 0,
                0, PhoenixScore.Min, 0, 0,
                PhoenixScore.Min, 0, 0, 0, 0,
                totals);
        }
    }
}
