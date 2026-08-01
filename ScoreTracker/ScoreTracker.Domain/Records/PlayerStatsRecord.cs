using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     The estimated PUMBILITY ranks and their board date default to null so every existing
    ///     construction site keeps compiling — a positional record this widely built is not worth
    ///     churning twenty call sites over. Null means "not on the board" or "never estimated",
    ///     which are the same thing to a reader.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PlayerStatsRecord(Guid UserId, Rating TotalRating, DifficultyLevel HighestLevel,
        int ClearCount,
        Rating CoOpRating,
        PhoenixScore CoOpScore, Rating SkillRating, PhoenixScore SkillScore, double SkillLevel,
        Rating SinglesRating, PhoenixScore SinglesScore, double SinglesLevel, Rating DoublesRating,
        PhoenixScore DoublesScore, double DoublesLevel, double CompetitiveLevel, double SinglesCompetitiveLevel,
        double DoublesCompetitiveLevel,
        int? EstimatedPumbilityRank = null,
        int? EstimatedSinglesPumbilityRank = null,
        int? EstimatedDoublesPumbilityRank = null,
        DateTimeOffset? PumbilityBoardAsOf = null)
    {
    }
}
