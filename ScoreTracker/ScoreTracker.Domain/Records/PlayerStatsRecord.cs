using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     The estimated PUMBILITY ranks and their board date default to null so every existing
    ///     construction site keeps compiling — a positional record this widely built is not worth
    ///     churning twenty call sites over. Null means "not on the board" or "never estimated",
    ///     which are the same thing to a reader.
    ///     <para>
    ///         The four PUMBILITY pools are <c>double</c> rather than <see cref="Rating" />: that
    ///         value type is int-backed, and a pool that rounds here has already spent precision
    ///         the presentation layer is the only thing entitled to spend. <see cref="TotalRating" />
    ///         is not a pool — it is the lifetime sum, shown under its own label — so it stays.
    ///     </para>
    ///     <para>
    ///         <see cref="TotalPumbility" /> is a season's whole-season grind board: every seasonal
    ///         best's PUMBILITY summed, not a pool of fifty (docs/design/seasons.md §4.2, §6.2). It is
    ///         written by the season pass and is zero on every all-time row, which is why it defaults.
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PlayerStatsRecord(Guid UserId, Rating TotalRating, DifficultyLevel HighestLevel,
        int ClearCount,
        double CoOpRating,
        PhoenixScore CoOpScore, double SkillRating, PhoenixScore SkillScore, double SkillLevel,
        double SinglesRating, PhoenixScore SinglesScore, double SinglesLevel, double DoublesRating,
        PhoenixScore DoublesScore, double DoublesLevel, double CompetitiveLevel, double SinglesCompetitiveLevel,
        double DoublesCompetitiveLevel,
        int? EstimatedPumbilityRank = null,
        int? EstimatedSinglesPumbilityRank = null,
        int? EstimatedDoublesPumbilityRank = null,
        DateTimeOffset? PumbilityBoardAsOf = null,
        double TotalPumbility = 0)
    {
    }
}
