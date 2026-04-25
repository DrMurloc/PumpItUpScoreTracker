using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record WorldRankingRecord(Name Username, string Type, double AverageDifficulty,
        PhoenixScore AverageScore,
        int SinglesCount, int DoublesCount, int TotalRating, double CompetitiveLevel, double SinglesCompetitiveLevel,
        double DoublesCompetitiveLevel)
    {
    }
}
