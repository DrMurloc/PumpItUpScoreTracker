using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record ChartLetterGradeDifficulty(Guid ChartId, IDictionary<ParagonLevel, double> Percentiles,
        IDictionary<ParagonLevel, double> WeightedSum)
    {
    }
}
