using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record PumbilityProjection(
        IReadOnlyDictionary<Guid, PhoenixScore> ExpectedScores,
        IReadOnlyDictionary<Guid, int> ProjectedGains,
        IReadOnlyDictionary<(ChartType ChartType, DifficultyLevel Level), int> InsufficientDataGains,
        IReadOnlyDictionary<Guid, TierListCategory> ChartDifficulty);
}
