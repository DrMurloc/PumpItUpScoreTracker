using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    /// <summary>
    ///     The tier-list page's blended list for one folder and lens, computed
    ///     server-side (tier-lists overhaul C2). Lens values: "Pass", "Score",
    ///     "Popularity", "Chabala", "PG". Personalized bends the lens with the player's
    ///     skill estimates and the materialized similar-players aggregation; when false
    ///     the result is the shared community reference (and is cached per folder+lens,
    ///     not per user). UserId defaults to the current user when Personalized.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetBlendedTierListQuery(ChartType ChartType, DifficultyLevel Level, Name Lens,
        bool Personalized = false, Guid? UserId = null, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<TierListResult>
    {
    }
}
