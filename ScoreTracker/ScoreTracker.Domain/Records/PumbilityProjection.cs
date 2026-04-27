using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record PumbilityProjection(
        IReadOnlyDictionary<Guid, PhoenixScore> ExpectedScores,
        IReadOnlyDictionary<Guid, int> ProjectedGains,
        IReadOnlyDictionary<(ChartType ChartType, DifficultyLevel Level), int> InsufficientDataGains,
        IReadOnlyDictionary<Guid, TierListCategory> ChartDifficulty);
}
