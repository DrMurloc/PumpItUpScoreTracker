using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Records
{
    public sealed record ChartLetterGradeDifficulty(Guid ChartId, IDictionary<ParagonLevel, double> Percentiles,
        IDictionary<ParagonLevel, double> WeightedSum)
    {
    }
}
