using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries
{
    /// <summary>
    ///     The Personalized Breakdown for one folder: what went into the personalized
    ///     blend and what each source said per chart. Only the lenses that personalize
    ///     are valid ("Pass", "Score"). UserId defaults to the current user. Cached
    ///     with the same lifetime as the blend itself.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetPersonalizedTierListBreakdownQuery(ChartType ChartType, DifficultyLevel Level,
        Name Lens, Guid? UserId = null, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<PersonalizedTierListBreakdown>
    {
    }
}
